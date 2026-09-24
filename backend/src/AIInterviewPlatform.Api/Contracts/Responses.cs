using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Contracts;

public sealed class AuthResponse
{
    public string Token { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "Candidate";
}

public sealed class BankSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int QuestionCount { get; set; }
    public List<string> Tags { get; set; } = new();
}

public sealed class QuestionView
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Type { get; set; } = "Subjective";
    public int TimeLimitSeconds { get; set; } = 180;
    public string? Hint { get; set; }
    public List<McqOption> Options { get; set; } = new();
    public List<string> ModelPoints { get; set; } = new();
}

public sealed class BankDetail
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<QuestionView> Questions { get; set; } = new();
}

public sealed class QuestionDto
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public int Type { get; set; }
    public string TypeName { get; set; } = "";
    public List<McqOption> Options { get; set; } = new();
    public int TimeLimitSeconds { get; set; }
    public string? Hint { get; set; }
}

public sealed class McqResultDto
{
    public int SelectedIndex { get; set; }
    public int CorrectIndex { get; set; }
}

public sealed class SessionQuestionView
{
    public int Index { get; set; }
    public QuestionDto Question { get; set; } = new();
    public string Mode { get; set; } = "typed";
    public int? Score { get; set; }
    public string? Answer { get; set; }
    public string? Feedback { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public int TimeTakenSeconds { get; set; }
    public bool FocusLost { get; set; }
    public bool IsAnswered { get; set; }
    public McqResultDto? McqResult { get; set; }
}

public sealed class SessionView
{
    public string Id { get; set; } = "";
    public string BankName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalSeconds { get; set; }
    public bool Adaptive { get; set; }
    public int FocusLossCount { get; set; }
    public int? OverallScore { get; set; }
    public double Percentile { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "InProgress";
    public List<SessionQuestionView> Questions { get; set; } = new();
}

public sealed class TagStat
{
    public string Tag { get; set; } = "";
    public double Average { get; set; }
    public int Attempts { get; set; }
    public string Level { get; set; } = "Need practice";
}

public sealed class RecentSessionDto
{
    public string Id { get; set; } = "";
    public string BankName { get; set; } = "";
    public DateTime CompletedAt { get; set; }
    public int Score { get; set; }
    public double Percentile { get; set; }
}

public sealed class DashboardDto
{
    public int SessionsCompleted { get; set; }
    public int QuestionsAnswered { get; set; }
    public double OverallAverage { get; set; }
    public double Percentile { get; set; }
    public int CurrentStreakDays { get; set; }
    public List<TagStat> Radars { get; set; } = new();
    public List<RecentSessionDto> RecentSessions { get; set; } = new();
}

public sealed class HealthDto
{
    public string Status { get; set; } = "Ok";
    public string Database { get; set; } = "InMemory";
    public string Cache { get; set; } = "Memory";
    public string AiProvider { get; set; } = "Rubric";
    public string Version { get; set; } = "1.0.0";
}

public sealed class ResumeQuestionsResponse
{
    public List<string> DetectedSkills { get; set; } = new();
    public List<QuestionDto> Questions { get; set; } = new();
    public int Count => Questions.Count;
}

public sealed class ApiError
{
    public string Error { get; set; } = "";
    public string? Detail { get; set; }
}