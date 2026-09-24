using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;

namespace AIInterviewPlatform.Api.Services;

public interface IQuestionService
{
    Task<List<BankSummary>> ListBanksAsync();
    Task<BankDetail> GetBankAsync(string id);
    Task<List<Question>> GetQuestionsForAssessmentAsync(string bankId, int count, string userId, bool adaptive);
}

public sealed class QuestionService : IQuestionService
{
    private readonly IAppStore _store;
    private readonly ICacheService _cache;
    private readonly IReportService _reports;
    private readonly AdaptiveDifficultyPicker _picker;
    private const string BanksKey = "banks:list";

    public QuestionService(IAppStore store, ICacheService cache, IReportService reports, AdaptiveDifficultyPicker picker)
    {
        _store = store;
        _cache = cache;
        _reports = reports;
        _picker = picker;
    }

    public async Task<List<BankSummary>> ListBanksAsync()
    {
        var cached = await _cache.GetAsync<List<BankSummary>>(BanksKey);
        if (cached is not null) return cached;

        var banks = await _store.ListBankSummariesAsync();
        await _cache.SetAsync(BanksKey, banks, TimeSpan.FromMinutes(5));
        return banks;
    }

    public async Task<BankDetail> GetBankAsync(string id)
    {
        var cacheKey = $"bank:{id}";
        var cached = await _cache.GetAsync<BankDetail>(cacheKey);
        if (cached is not null) return cached;

        var bank = await _store.GetBankAsync(id);
        if (bank is null) throw new AppException("Question bank not found.", 404);

        var detail = new BankDetail
        {
            Id = bank.Id,
            Name = bank.Name,
            Description = bank.Description,
            Questions = bank.Questions.Select(q => new QuestionView
            {
                Id = q.Id,
                Text = q.Text,
                Tag = q.Tag,
                Difficulty = q.Difficulty,
                Type = q.Type.ToString(),
                TimeLimitSeconds = q.TimeLimitSeconds,
                Hint = q.Hint,
                Options = q.Options,
                ModelPoints = q.ModelPoints
            }).ToList()
        };

        await _cache.SetAsync(cacheKey, detail, TimeSpan.FromMinutes(10));
        return detail;
    }

    /// <summary>
    /// Runs the adaptive (or balanced) picker to select questions for a session.
    /// </summary>
    public async Task<List<Question>> GetQuestionsForAssessmentAsync(string bankId, int count, string userId, bool adaptive)
    {
        var bank = await _store.GetBankAsync(bankId);
        if (bank is null || bank.Questions.Count == 0)
            throw new AppException("Question bank not found or empty.", 404);

        var skill = adaptive ? await _reports.GetSkillLevelsAsync(userId) : new Dictionary<string, double>();

        return adaptive
            ? _picker.PickAdaptive(bank.Questions, count, skill)
            : _picker.PickBalanced(bank.Questions, count);
    }
}