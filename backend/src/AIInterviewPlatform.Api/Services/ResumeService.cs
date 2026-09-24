using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;

namespace AIInterviewPlatform.Api.Services;

/// <summary>
/// Extracts skills from a pasted resume and returns a personalized question set
/// targeted at the candidate's strongest technologies — mirroring ACHNET's own
/// resume-driven interview concept.
/// </summary>
public interface IResumeService
{
    Task<ResumeQuestionsResponse> GenerateAsync(string resumeText);
}

public sealed class ResumeService : IResumeService
{
    private readonly IAppStore _store;
    private static readonly Dictionary<string, (string Tag, string Keyword)> KeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = (Tags.Oop, "C#"),
        [".net"] = (Tags.Web, ".NET"),
        ["dotnet"] = (Tags.Web, ".NET"),
        ["react"] = (Tags.Web, "React"),
        ["typescript"] = (Tags.Web, "TypeScript"),
        ["javascript"] = (Tags.Web, "JavaScript"),
        ["html"] = (Tags.Web, "HTML/CSS"),
        ["css"] = (Tags.Web, "HTML/CSS"),
        ["node"] = (Tags.Web, "Node.js"),
        ["express"] = (Tags.Web, "Node.js"),
        ["mongo"] = (Tags.Db, "MongoDB"),
        ["nosql"] = (Tags.Db, "NoSQL"),
        ["redis"] = (Tags.Db, "Redis"),
        ["sql"] = (Tags.Db, "SQL"),
        ["python"] = (Tags.Ml, "Python"),
        ["machine learning"] = (Tags.Ml, "Machine Learning"),
        ["ml"] = (Tags.Ml, "Machine Learning"),
        ["llm"] = (Tags.Ml, "LLM"),
        ["yolo"] = (Tags.Ml, "YOLO"),
        ["opencv"] = (Tags.Ml, "OpenCV"),
        ["tensorflow"] = (Tags.Ml, "TensorFlow"),
        ["pytorch"] = (Tags.Ml, "PyTorch"),
        ["java"] = (Tags.Dsa, "Java"),
        ["oop"] = (Tags.Oop, "OOP"),
        ["object oriented"] = (Tags.Oop, "OOP"),
        ["algorithm"] = (Tags.Dsa, "Algorithms"),
        ["data structure"] = (Tags.Dsa, "Data Structures"),
        ["dsa"] = (Tags.Dsa, "DSA"),
    };

    public ResumeService(IAppStore store) => _store = store;

    public async Task<ResumeQuestionsResponse> GenerateAsync(string resumeText)
    {
        var text = (resumeText ?? "").ToLowerInvariant();
        var detected = new List<string>();
        var tags = new List<string>();

        foreach (var kv in KeywordMap)
        {
            if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (!detected.Contains(kv.Value.Keyword)) detected.Add(kv.Value.Keyword);
                if (!tags.Contains(kv.Value.Tag)) tags.Add(kv.Value.Tag);
            }
        }

        // Always include at least the fundamentals.
        if (tags.Count == 0)
        {
            tags.Add(Tags.Logic);
            detected.Add("General logic");
        }

        // Gather candidate questions from banks matching detected tags.
        var banks = await _store.ListBankSummariesAsync();
        var pool = new List<Question>();
        foreach (var summary in banks)
        {
            var bank = await _store.GetBankAsync(summary.Id);
            if (bank is null) continue;
            pool.AddRange(bank.Questions.Where(q => tags.Contains(q.Tag)));
        }

        // Fall back to all questions if none matched (e.g., brand-new user).
        if (pool.Count == 0)
        {
            foreach (var summary in banks)
            {
                var bank = await _store.GetBankAsync(summary.Id);
                if (bank is null) continue;
                pool.AddRange(bank.Questions);
            }
        }

        var picked = pool
            .GroupBy(q => q.Tag)
            .OrderBy(g => tags.IndexOf(g.Key) < 0 ? 99 : tags.IndexOf(g.Key))
            .SelectMany(g => g.Take(2))
            .OrderBy(q => q.Difficulty == "Easy" ? 0 : q.Difficulty == "Medium" ? 1 : 2)
            .Take(10)
            .ToList();

        return new ResumeQuestionsResponse
        {
            DetectedSkills = detected,
            Questions = picked.Select(q => new QuestionDto
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
            }).ToList()
        };
    }
}