using System.Text.RegularExpressions;
using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Scoring;

/// <summary>
/// Deterministic offline scorer that works with zero external dependencies.
/// Grades answers on keyword coverage, answer depth, structure, and code presence.
/// Used as the default provider and as the fallback when Gemini is unavailable.
/// </summary>
public sealed class RubricScoreService : IScoreService
{
    public string Provider => "Rubric";

    public Task<ScoreResult> EvaluateTextAsync(Question question, string? answer, string mode, CancellationToken ct = default)
    {
        var text = (answer ?? "").Trim();
        var result = new ScoreResult();

        if (text.Length == 0)
        {
            result.Score = 0;
            result.Feedback = "No answer was provided for this question.";
            result.Improvements.Add("Attempt an answer — partial marks start from a single clear point.");
            return Task.FromResult(result);
        }

        // 1. Keyword coverage (up to 40)
        var lower = text.ToLowerInvariant();
        var expected = question.ExpectedKeywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim().ToLowerInvariant())
            .ToList();
        int hits = expected.Count(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
        int keywordScore;
        if (expected.Count == 0)
        {
            keywordScore = text.Length >= 40 ? 25 : 15;
        }
        else
        {
            keywordScore = (int)Math.Round(40.0 * hits / expected.Count);
        }
        result.Strengths.AddRange(expected.Where(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase))
            .Select(k => $"Covered key concept: \"{k}\"")
            .Take(4));
        var missing = expected.Where(k => !lower.Contains(k, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();

        // 2. Answer depth (up to 25)
        int charTarget = Math.Max(50, question.Text.Length * 3);
        double depthRatio = Math.Min(1.0, (double)text.Length / charTarget);
        int depthScore = (int)Math.Round(25 * depthRatio);

        // 3. Structure (up to 15) — bullets / numbered points / sections
        bool structured = Regex.IsMatch(text, @"(^|\n)\s*[-•*]\s") || Regex.IsMatch(text, @"(^|\n)\s*\d+\.\s");
        int structureScore = structured ? 15 : 0;

        // 4. Code presence bonus for technical tags (up to 10)
        bool technical = question.Tag is Tags.Dsa or Tags.Web or Tags.Db;
        bool hasCode = technical && (Regex.IsMatch(text, @"[{}();]") || text.Contains('`') || Regex.IsMatch(text, @"\b(int|string|var|let|const|class|public|private|def)\b"));
        int codeScore = hasCode ? 10 : 0;

        // 5. Voice-mode communication proxy (up to 10): question length + clarity hint
        int commScore = 0;
        if (mode == "voice")
        {
            var sentences = Regex.Split(text, @"[.!?]+").Where(s => s.Trim().Length > 0).Count();
            commScore = sentences >= 2 ? 10 : sentences >= 1 ? 5 : 0;
        }

        var total = Math.Min(100, keywordScore + depthScore + structureScore + codeScore + commScore);
        // Slight smoothing to mirror rubric curves and avoid 0 for genuine attempts
        if (total == 0 && text.Length > 0) total = 5;

        result.Score = total;
        result.Provider = Provider;

        var parts = new List<string>
        {
            keywordScore >= 24 ? "Strong coverage of the expected concepts." : "Included some expected concepts but can go deeper.",
            depthScore >= 18 ? "Good depth and explanation." : "Answer could explain the 'why' in more detail.",
            structureScore == 15 ? "Clear structure with bulleted/numbered points." : "Structuring your answer in bullet points improves readability."
        };
        if (hasCode) parts.Add("Backed the reasoning with code/notation.");
        if (missing.Count > 0)
            parts.Add($"Mention keywords like: {string.Join(", ", missing)} to score higher.");
        if (commScore == 10) parts.Add("Spoke clearly with multiple points — great for interview delivery.");

        result.Feedback = string.Join(" ", parts);
        result.Improvements.AddRange(new[]
        {
            missing.Count > 0 ? $"Add missing concepts: {string.Join(", ", missing)}" : "Extend the answer with a concrete example.",
            "Link each point back to how it is applied in real software development."
        });
        result.Improvements = result.Improvements.Distinct().Take(3).ToList();

        return Task.FromResult(result);
    }
}