using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;
using AIInterviewPlatform.Api.Scoring;
using AIInterviewPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIInterviewPlatform.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly ITokenService _tokens;

    public ReportsController(IReportService reports, ITokenService tokens)
    {
        _reports = reports;
        _tokens = tokens;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard()
        => Ok(await _reports.GetDashboardAsync(UserId));

    private string UserId => _tokens.GetUserId(User) ?? throw new AppException("Not authenticated.", 401);
}

[ApiController]
[Route("api/resume")]
[Authorize]
public class ResumeController : ControllerBase
{
    private readonly IResumeService _resume;

    public ResumeController(IResumeService resume) => _resume = resume;

    [HttpPost("questions")]
    public async Task<ActionResult<ResumeQuestionsResponse>> Generate(ResumeQuestionsRequest request)
        => Ok(await _resume.GenerateAsync(request.ResumeText));
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAppStore _store;
    private readonly AIInterviewPlatform.Api.Infrastructure.ICacheService _cache;

    public AdminController(IAppStore store, AIInterviewPlatform.Api.Infrastructure.ICacheService cache)
    {
        _store = store;
        _cache = cache;
    }

    [HttpPost("banks")]
    public async Task<ActionResult<Contracts.BankSummary>> CreateBank(CreateBankRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Bank name is required.");

        var bank = new QuestionBank
        {
            Id = IdGen.New(),
            Name = request.Name.Trim(),
            Description = request.Description
        };
        await _store.CreateBankAsync(bank);
        await _cache.RemoveAsync("banks:list");
        return Ok(new Contracts.BankSummary { Id = bank.Id, Name = bank.Name, Description = bank.Description });
    }

    [HttpPost("questions")]
    public async Task<ActionResult> AddQuestion(AddQuestionRequest request)
    {
        var bank = await _store.GetBankAsync(request.BankId)
            ?? throw new AppException("Bank not found.", 404);

        var question = new Question
        {
            Id = IdGen.New(),
            BankId = bank.Id,
            BankName = bank.Name,
            Text = request.Text,
            Tag = request.Tag,
            Difficulty = request.Difficulty,
            Type = (QuestionType)request.Type,
            ExpectedKeywords = request.ExpectedKeywords,
            ModelPoints = request.ModelPoints,
            Options = request.Options.Select(o => new McqOption { Text = o.Text, Code = o.Code }).ToList(),
            CorrectIndex = request.CorrectIndex,
            TimeLimitSeconds = request.TimeLimitSeconds,
            Hint = request.Hint
        };

        bank.Questions.Add(question);
        await _store.UpdateBankAsync(bank);
        await _cache.RemoveAsync($"bank:{bank.Id}");
        await _cache.RemoveAsync("banks:list");
        return Ok(new { question.Id });
    }
}

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IAppStore _store;
    private readonly AIInterviewPlatform.Api.Infrastructure.ICacheService _cache;
    private readonly IScoreService _scorer;

    public HealthController(IAppStore store, AIInterviewPlatform.Api.Infrastructure.ICacheService cache, IScoreService scorer)
    {
        _store = store;
        _cache = cache;
        _scorer = scorer;
    }

    [HttpGet]
    public ActionResult<HealthDto> Get()
        => Ok(new HealthDto
        {
            Status = "Ok",
            Database = _store.Kind,
            Cache = _cache.Kind,
            AiProvider = _scorer.Provider,
            Version = "1.0.0"
        });
}