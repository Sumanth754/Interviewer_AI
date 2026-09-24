using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Scoring;

/// <summary>
/// Routes scoring to Gemini when configured &amp; available, otherwise falls back to the
/// offline rubric scorer so the platform always works — even with zero external setup.
/// </summary>
public sealed class CompositeScoreService : IScoreService
{
    private readonly GeminiScoreService? _gemini;
    private readonly RubricScoreService _rubric = new();

    public CompositeScoreService(GeminiScoreService? gemini = null)
        => _gemini = gemini;

    public string Provider => _gemini is not null ? "Gemini (fallback Rubric)" : "Rubric";

    public async Task<ScoreResult> EvaluateTextAsync(Question question, string? answer, string mode, CancellationToken ct = default)
    {
        if (_gemini is not null)
        {
            try
            {
                return await _gemini.EvaluateTextAsync(question, answer, mode, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                var fallback = await _rubric.EvaluateTextAsync(question, answer, mode, ct);
                fallback.Feedback = $"[Gemini unavailable — rubric grades] {fallback.Feedback}";
                return fallback;
            }
        }

        return await _rubric.EvaluateTextAsync(question, answer, mode, ct);
    }
}