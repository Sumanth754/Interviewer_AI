using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;
using AIInterviewPlatform.Api.Scoring;

namespace AIInterviewPlatform.Api.Services;

public interface ISessionService
{
    Task<SessionView> StartAsync(string userId, StartSessionRequest request);
    Task<SessionView> RepeatAsync(string userId, string sessionId);
    Task<SessionQuestionView> SubmitAnswerAsync(string userId, string sessionId, int index, SubmitAnswerRequest request);
    Task<SessionView> FinishAsync(string userId, string sessionId, bool timedOut = false);
    Task<SessionView> GetAsync(string userId, string sessionId);
    Task<List<SessionView>> ListAsync(string userId);
}

public sealed class SessionService : ISessionService
{
    private readonly IAppStore _store;
    private readonly IQuestionService _questions;
    private readonly IScoreService _scorer;
    private readonly IReportService _reports;
    private readonly ICacheService _cache;

    public SessionService(IAppStore store, IQuestionService questions, IScoreService scorer, IReportService reports, ICacheService cache)
    {
        _store = store;
        _questions = questions;
        _scorer = scorer;
        _reports = reports;
        _cache = cache;
    }

    public async Task<SessionView> StartAsync(string userId, StartSessionRequest request)
    {
        if (request.QuestionCount is < 3 or > 15)
            throw new AppException("Question count must be between 3 and 15.", 400);
        if (request.DurationMinutes is < 3 or > 60)
            throw new AppException("Duration must be between 3 and 60 minutes.", 400);

        var bank = await _store.GetBankAsync(request.BankId)
            ?? throw new AppException("Question bank not found.", 404);

        var questions = await _questions.GetQuestionsForAssessmentAsync(
            request.BankId, request.QuestionCount, userId, request.Adaptive);

        var session = new AssessmentSession
        {
            Id = IdGen.New(),
            UserId = userId,
            BankId = bank.Id,
            BankName = bank.Name,
            TotalSeconds = request.DurationMinutes * 60,
            Adaptive = request.Adaptive,
            Questions = questions.Select(q => new SessionQuestion { Question = q }).ToList(),
            Status = "InProgress"
        };

        await _store.CreateSessionAsync(session);
        return ToView(session);
    }

    public async Task<SessionView> RepeatAsync(string userId, string sessionId)
    {
        var prev = await _store.GetSessionAsync(sessionId)
            ?? throw new AppException("Session not found.", 404);
        if (prev.UserId != userId) throw new AppException("Forbidden.", 403);

        return await StartAsync(userId, new StartSessionRequest
        {
            BankId = prev.BankId,
            QuestionCount = prev.Questions.Count,
            DurationMinutes = Math.Max(3, prev.TotalSeconds / 60),
            Adaptive = prev.Adaptive
        });
    }

    public async Task<SessionQuestionView> SubmitAnswerAsync(string userId, string sessionId, int index, SubmitAnswerRequest request)
    {
        var session = await _store.GetSessionAsync(sessionId)
            ?? throw new AppException("Session not found.", 404);
        if (session.UserId != userId) throw new AppException("Forbidden.", 403);
        if (session.Status != "InProgress") throw new AppException("Session already completed.", 409);
        if (index < 0 || index >= session.Questions.Count)
            throw new AppException("Invalid question index.", 400);

        var item = session.Questions[index];
        if (item.IsAnswered)
            throw new AppException("This question was already answered.", 409);

        item.Mode = request.Mode;
        item.TimeTakenSeconds = Math.Clamp(request.TimeTakenSeconds, 0, int.MaxValue);
        item.FocusLost = request.FocusLost;
        if (request.FocusLost) session.FocusLossCount++;

        if (item.Question.Type == QuestionType.Mcq)
        {
            var selected = request.SelectedOptionIndex ?? -1;
            item.SelectedOptionIndex = selected;
            bool correct = selected == item.Question.CorrectIndex;
            item.Score = correct ? 100 : 0;
            item.Answer = selected >= 0 && selected < item.Question.Options.Count
                ? item.Question.Options[selected].Text : "";
            item.Feedback = correct
                ? "Correct! You identified the right option."
                : $"Not quite. The correct option is: {item.Question.Options.ElementAtOrDefault(item.Question.CorrectIndex)?.Text}";
            item.Strengths = correct && item.Question.Options.Count > 0
                ? new List<string> { "Pickled the correct concept." } : new List<string>();
            item.Improvements = correct
                ? new List<string> { "Explore the reasoning behind the other options too." }
                : new List<string> { "Review why the correct option is right before moving on." };
        }
        else
        {
            var result = await _scorer.EvaluateTextAsync(item.Question, request.Answer, request.Mode);
            item.Answer = (request.Answer ?? "").Trim();
            item.Score = result.Score;
            item.Feedback = result.Feedback;
            item.Strengths = result.Strengths;
            item.Improvements = result.Improvements;
        }

        await _store.UpdateSessionAsync(session);

        if (session.Questions.All(q => q.IsAnswered))
            await FinishAsync(userId, sessionId, false);

        return ToQuestionView(item.Question, item, index);
    }

    public async Task<SessionView> FinishAsync(string userId, string sessionId, bool timedOut = false)
    {
        var session = await _store.GetSessionAsync(sessionId)
            ?? throw new AppException("Session not found.", 404);
        if (session.UserId != userId) throw new AppException("Forbidden.", 403);
        if (session.Status == "Complete")
            return ToView(session);

        session.TimedOut = timedOut || session.Status == "InProgress" && session.Questions.All(q => q.IsAnswered) == false && HasExpired(session);
        session.Status = "Complete";
        session.CompletedAt = DateTime.UtcNow;

        var answered = session.Questions.Where(q => q.IsAnswered).ToList();
        var unanswered = session.Questions.Count - answered.Count;
        int total = answered.Sum(q => q.Score ?? 0);
        double avg = answered.Count == 0 ? 0 : (double)total / answered.Count;

        // Focus-loss penalty: up to -5 points for each focus switch (cheating guardrail).
        double final = avg;
        if (session.FocusLossCount > 0)
            final = Math.Max(0, final - Math.Min(10, session.FocusLossCount * 5));

        int overall = (int)Math.Round(final);
        session.OverallScore = overall;
        session.Percentile = _reports.EstimatePercentile(overall);
        session.Summary = BuildSummary(overall, answered.Count, unanswered, session.FocusLossCount);

        await _store.UpdateSessionAsync(session);
        await _cache.RemoveAsync($"dashboard:{userId}");
        return ToView(session);
    }

    public async Task<SessionView> GetAsync(string userId, string sessionId)
    {
        var session = await _store.GetSessionAsync(sessionId)
            ?? throw new AppException("Session not found.", 404);
        if (session.UserId != userId) throw new AppException("Forbidden.", 403);
        return ToView(session);
    }

    public async Task<List<SessionView>> ListAsync(string userId)
    {
        var sessions = await _store.ListUserSessionsAsync(userId, 50);
        return sessions.Select(ToView).ToList();
    }

    // ---------- helpers ----------

    private static bool HasExpired(AssessmentSession session)
        => DateTime.UtcNow - session.StartedAt > TimeSpan.FromSeconds(session.TotalSeconds + 60);

    private static string BuildSummary(int overall, int answered, int unanswered, int focusLoss)
    {
        var parts = new List<string>
        {
            $"You scored {overall}/100 across {answered} answered questions.",
            overall >= 80 ? "Excellent — you are close to interview-ready."
                : overall >= 60 ? "Good foundation — sharpen your weak tags to push higher."
                : "Keep practising — focus on the tags with the lowest scores."
        };
        if (unanswered > 0)
            parts.Add($"{unanswered} question(s) were left unanswered — time management is a factor.");
        if (focusLoss > 0)
            parts.Add("Note: tab switches were detected. Exams may flag repeated focus loss as a cheating signal.");
        return string.Join(" ", parts);
    }

    private static SessionView ToView(AssessmentSession s) => new()
    {
        Id = s.Id,
        BankName = s.BankName,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt,
        TotalSeconds = s.TotalSeconds,
        Adaptive = s.Adaptive,
        FocusLossCount = s.FocusLossCount,
        OverallScore = s.OverallScore,
        Percentile = s.Percentile,
        Summary = s.Summary,
        Status = s.Status,
        Questions = s.Questions.Select((q, i) => ToQuestionView(q.Question, q, i)).ToList()
    };

    private static SessionQuestionView ToQuestionView(Question q, SessionQuestion item, int index) => new()
    {
        Index = index,
        Question = new QuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            Tag = q.Tag,
            Difficulty = q.Difficulty,
            Type = (int)q.Type,
            TypeName = q.Type.ToString(),
            Options = q.Options,
            TimeLimitSeconds = q.TimeLimitSeconds,
            Hint = q.Hint
        },
        Mode = item.Mode,
        Score = item.Score,
        Answer = item.Answer,
        Feedback = item.Feedback,
        Strengths = item.Strengths,
        Improvements = item.Improvements,
        TimeTakenSeconds = item.TimeTakenSeconds,
        FocusLost = item.FocusLost,
        IsAnswered = item.IsAnswered,
        McqResult = q.Type == QuestionType.Mcq && item.IsAnswered
            ? new McqResultDto { SelectedIndex = item.SelectedOptionIndex ?? -1, CorrectIndex = q.CorrectIndex }
            : null
    };
}