using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Scoring;

public sealed class ScoreResult
{
    public int Score { get; set; }
    public string Feedback { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string Provider { get; set; } = "Rubric";
}

public interface IScoreService
{
    string Provider { get; }
    Task<ScoreResult> EvaluateTextAsync(Question question, string? answer, string mode, CancellationToken ct = default);
}