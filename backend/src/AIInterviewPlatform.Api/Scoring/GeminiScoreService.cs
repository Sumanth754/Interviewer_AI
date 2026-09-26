using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Scoring;

/// <summary>
/// Uses Google Gemini to grade answers with natural-language feedback.
/// Returns null/fails gracefully so callers can fall back to the rubric scorer.
/// </summary>
public sealed class GeminiScoreService : IScoreService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;

    public GeminiScoreService(string apiKey, string model, string endpoint, int timeoutSeconds)
    {
        _apiKey = apiKey;
        _model = model;
        _endpoint = endpoint.TrimEnd('/');
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _http = new HttpClient { Timeout = _timeout };
    }

    public string Provider => "Gemini";

    private const string PromptTemplate =
        """
        You are a strict but fair coding-interview evaluator.
        Grade the candidate's answer to the interview question below.

        QUESTION (tag: {{TAG}}):
        {{QUESTION}}

        KEY CONCEPTS THE ANSWER SHOULD COVER:
        {{KEYWORDS}}

        MODEL ANSWER POINTS:
        {{MODELPOINTS}}

        CANDIDATE'S ANSWER:
        {{ANSWER}}

        Respond ONLY with JSON matching this schema:
        {
          "score": <integer 0-100>,
          "feedback": "<2 sentence overall feedback>",
          "strengths": ["<up to 3>"],
          "improvements": ["<up to 3>"]
        }
        """;

    public async Task<ScoreResult> EvaluateTextAsync(Question question, string? answer, string mode, CancellationToken ct = default)
    {
        var answerText = (answer ?? "").Trim();

        var prompt = PromptTemplate
            .Replace("{{QUESTION}}", question.Text)
            .Replace("{{TAG}}", $"{question.Tag} ({question.Difficulty})")
            .Replace("{{KEYWORDS}}", string.Join(", ", question.ExpectedKeywords))
            .Replace("{{MODELPOINTS}}", string.Join("\n", question.ModelPoints.Select(p => "- " + p)))
            .Replace("{{ANSWER}}", answerText);

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                // Grading is a short, deterministic task, so turn off the 2.5-series
                // dynamic thinking. It otherwise spends the request's latency budget
                // reasoning and can exceed the configured timeout.
                thinkingConfig = new { thinkingBudget = 0 }
            }
        };

        var apiUrl = $"{_endpoint}/models/{_model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        using var response = await _http.PostAsJsonAsync(apiUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
        var text = body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned an empty response.");

        // Gemini returns JSON inside the text part — extract and parse.
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < jsonStart)
            throw new InvalidOperationException("Gemini did not return valid JSON.");

        var parsed = JsonSerializer.Deserialize<GeminiScore>(text[jsonStart..(jsonEnd + 1)],
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new ScoreResult
        {
            Score = Math.Clamp(parsed?.Score ?? 0, 0, 100),
            Feedback = parsed?.Feedback ?? "Answer graded.",
            Strengths = parsed?.Strengths ?? new List<string>(),
            Improvements = parsed?.Improvements ?? new List<string>(),
            Provider = Provider
        };
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")] public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class GeminiScore
    {
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("feedback")] public string? Feedback { get; set; }
        [JsonPropertyName("strengths")] public List<string>? Strengths { get; set; }
        [JsonPropertyName("improvements")] public List<string>? Improvements { get; set; }
    }
}