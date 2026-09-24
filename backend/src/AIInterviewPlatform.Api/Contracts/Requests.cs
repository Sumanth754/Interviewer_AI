namespace AIInterviewPlatform.Api.Contracts;

public sealed class RegisterRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class StartSessionRequest
{
    public string BankId { get; set; } = "";
    public int QuestionCount { get; set; } = 5;
    public int DurationMinutes { get; set; } = 10;
    public bool Adaptive { get; set; } = true;
}

public sealed class SubmitAnswerRequest
{
    public string? Answer { get; set; }
    public int? SelectedOptionIndex { get; set; }
    public string Mode { get; set; } = "typed";
    public int TimeTakenSeconds { get; set; }
    public bool FocusLost { get; set; }
}

public sealed class ResumeQuestionsRequest
{
    public string ResumeText { get; set; } = "";
}

public sealed class CreateBankRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class AddQuestionRequest
{
    public string BankId { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tag { get; set; } = "Logic";
    public string Difficulty { get; set; } = "Medium";
    public int Type { get; set; } = 0;
    public List<string> ExpectedKeywords { get; set; } = new();
    public List<string> ModelPoints { get; set; } = new();
    public List<McqOptionContract> Options { get; set; } = new();
    public int CorrectIndex { get; set; } = -1;
    public int TimeLimitSeconds { get; set; } = 180;
    public string? Hint { get; set; }
}

public sealed class McqOptionContract
{
    public string Text { get; set; } = "";
    public string? Code { get; set; }
}