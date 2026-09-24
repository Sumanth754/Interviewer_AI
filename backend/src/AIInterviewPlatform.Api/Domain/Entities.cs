namespace AIInterviewPlatform.Api.Domain;

public enum UserRole
{
    Candidate = 0,
    Admin = 1
}

public enum QuestionType
{
    Subjective = 0,
    Mcq = 1,
    Voice = 2
}

public sealed class User
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Candidate;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class McqOption
{
    public string Text { get; set; } = "";
    public string? Code { get; set; }
}

public sealed class Question
{
    public string Id { get; set; } = "";
    public string BankId { get; set; } = "";
    public string BankName { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tag { get; set; } = "Logic";
    public string Difficulty { get; set; } = "Medium";
    public QuestionType Type { get; set; } = QuestionType.Subjective;
    public List<string> ExpectedKeywords { get; set; } = new();
    public List<string> ModelPoints { get; set; } = new();
    public List<McqOption> Options { get; set; } = new();
    public int CorrectIndex { get; set; } = -1;
    public int TimeLimitSeconds { get; set; } = 180;
    public string? Hint { get; set; }
}

public sealed class QuestionBank
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Question> Questions { get; set; } = new();
}

public sealed class SessionQuestion
{
    public Question Question { get; set; } = new();
    public string? Answer { get; set; }
    public int? SelectedOptionIndex { get; set; }
    public string Mode { get; set; } = "typed";
    public int? Score { get; set; }
    public string? Feedback { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public int TimeTakenSeconds { get; set; }
    public bool FocusLost { get; set; }
    public bool IsAnswered => Score.HasValue;
}

public sealed class AssessmentSession
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string BankId { get; set; } = "";
    public string BankName { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int TotalSeconds { get; set; } = 600;
    public bool Adaptive { get; set; }
    public bool TimedOut { get; set; }
    public int FocusLossCount { get; set; }
    public List<SessionQuestion> Questions { get; set; } = new();
    public int? OverallScore { get; set; }
    public double Percentile { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "InProgress";
}

public static class IdGen
{
    public static string New() => MongoDB.Bson.ObjectId.GenerateNewId().ToString();
}

public static class Tags
{
    public const string Dsa = "DSA";
    public const string Oop = "OOP";
    public const string Logic = "Logic";
    public const string Db = "DB";
    public const string Web = "Web";
    public const string Ml = "AI/ML";
    public const string Comm = "Communication";

    public static readonly string[] All = { Dsa, Oop, Logic, Db, Web, Ml, Comm };
}