using System.ComponentModel.DataAnnotations;

namespace AIInterviewPlatform.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    // Matches the service-layer check so both paths agree.
    [Required, StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public sealed class StartSessionRequest
{
    [Required]
    public string BankId { get; set; } = "";

    // Ranges mirror SessionService's own guards.
    [Range(3, 15)]
    public int QuestionCount { get; set; } = 5;

    [Range(3, 60)]
    public int DurationMinutes { get; set; } = 10;

    public bool Adaptive { get; set; } = true;
}

public sealed class SubmitAnswerRequest
{
    [MaxLength(20000)]
    public string? Answer { get; set; }

    public int? SelectedOptionIndex { get; set; }

    [RegularExpression("^(typed|voice)$")]
    public string Mode { get; set; } = "typed";

    [Range(0, 86400)]
    public int TimeTakenSeconds { get; set; }

    public bool FocusLost { get; set; }
}

public sealed class ResumeQuestionsRequest
{
    [Required, MaxLength(50000)]
    public string ResumeText { get; set; } = "";
}

public sealed class CreateBankRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [MaxLength(1000)]
    public string Description { get; set; } = "";
}

public sealed class AddQuestionRequest
{
    [Required]
    public string BankId { get; set; } = "";

    [Required, StringLength(4000, MinimumLength = 5)]
    public string Text { get; set; } = "";

    [StringLength(40)]
    public string Tag { get; set; } = "Logic";

    [StringLength(40)]
    public string Difficulty { get; set; } = "Medium";

    [Range(0, 2)]
    public int Type { get; set; } = 0;

    [MaxLength(40)]
    public List<string> ExpectedKeywords { get; set; } = new();

    [MaxLength(40)]
    public List<string> ModelPoints { get; set; } = new();

    [MaxLength(10)]
    public List<McqOptionContract> Options { get; set; } = new();

    public int CorrectIndex { get; set; } = -1;

    [Range(10, 3600)]
    public int TimeLimitSeconds { get; set; } = 180;

    [MaxLength(2000)]
    public string? Hint { get; set; }
}

public sealed class McqOptionContract
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string Text { get; set; } = "";

    [MaxLength(200)]
    public string? Code { get; set; }
}
