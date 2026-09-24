using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;

namespace AIInterviewPlatform.Api.Services;

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync(string userId);
    Task<Dictionary<string, double>> GetSkillLevelsAsync(string userId);
    double EstimatePercentile(double score);
}

public sealed class ReportService : IReportService
{
    private readonly IAppStore _store;
    private readonly ICacheService _cache;

    // Baseline distribution for estimated campus candidates.
    private const double BaselineMean = 56.0;
    private const double BaselineStd = 16.5;

    public ReportService(IAppStore store, ICacheService cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<DashboardDto> GetDashboardAsync(string userId)
    {
        var cacheKey = $"dashboard:{userId}";
        var cached = await _cache.GetAsync<DashboardDto>(cacheKey);
        if (cached is not null) return cached;

        var sessions = await _store.ListUserSessionsAsync(userId, 200);
        var completed = sessions.Where(s => s.Status == "Complete").ToList();

        var tagStats = new Dictionary<string, (double Sum, int Count)>();
        int questionsAnswered = 0;

        foreach (var s in completed)
        {
            foreach (var q in s.Questions)
            {
                if (!q.IsAnswered) continue;
                questionsAnswered++;
                var tag = q.Question.Tag;
                if (!tagStats.ContainsKey(tag)) tagStats[tag] = (0, 0);
                tagStats[tag] = (tagStats[tag].Sum + q.Score!.Value, tagStats[tag].Count + 1);
            }
        }

        var radars = tagStats
            .Select(kv =>
            {
                double avg = kv.Value.Sum / kv.Value.Count;
                return new TagStat
                {
                    Tag = kv.Key,
                    Average = Math.Round(avg),
                    Attempts = kv.Value.Count,
                    Level = avg >= 80 ? "Strong" : avg >= 60 ? "Good" : avg >= 40 ? "Needs practice" : "Weak"
                };
            })
            .OrderByDescending(t => t.Attempts)
            .ToList();

        double overall = radars.Count == 0 ? 0 : radars.Average(r => r.Average);

        var recent = completed
            .OrderByDescending(s => s.CompletedAt)
            .Take(6)
            .Select(s => new RecentSessionDto
            {
                Id = s.Id,
                BankName = s.BankName,
                CompletedAt = s.CompletedAt!.Value,
                Score = s.OverallScore ?? 0,
                Percentile = s.Percentile
            })
            .ToList();

        var dto = new DashboardDto
        {
            SessionsCompleted = completed.Count,
            QuestionsAnswered = questionsAnswered,
            OverallAverage = Math.Round(overall, 1),
            Percentile = EstimatePercentile(overall),
            CurrentStreakDays = ComputeStreak(completed.Select(c => c.CompletedAt!.Value).ToList()),
            Radars = radars.Count == 0 ? BuildEmptyRadars() : radars,
            RecentSessions = recent
        };

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(2));
        return dto;
    }

    public async Task<Dictionary<string, double>> GetSkillLevelsAsync(string userId)
    {
        var sessions = await _store.ListUserSessionsAsync(userId, 200);
        var completed = sessions.Where(s => s.Status == "Complete").ToList();
        var sums = new Dictionary<string, (double Sum, int Count)>();

        foreach (var s in completed)
        foreach (var q in s.Questions)
        {
            if (!q.IsAnswered) continue;
            var tag = q.Question.Tag;
            if (!sums.ContainsKey(tag)) sums[tag] = (0, 0);
            sums[tag] = (sums[tag].Sum + q.Score!.Value, sums[tag].Count + 1);
        }

        return sums.ToDictionary(kv => kv.Key, kv => kv.Value.Sum / Math.Max(1, kv.Value.Count));
    }

    public double EstimatePercentile(double score)
    {
        // Normal CDF approximation (Abramowitz & Stegun) with baseline distribution.
        double z = (score - BaselineMean) / BaselineStd;
        double p = 0.5 * (1 + Erf(z / Math.Sqrt(2)));
        return Math.Clamp(Math.Round(p * 100, 1), 1, 99.9);
    }

    private static double Erf(double x)
    {
        // Abramowitz–Stegun 7.1.26
        double sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);
        double t = 1 / (1 + 0.3275911 * x);
        double y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }

    private static int ComputeStreak(List<DateTime> daysUtc)
    {
        if (daysUtc.Count == 0) return 0;
        var dates = daysUtc.Select(d => DateOnly.FromDateTime(d)).Distinct().OrderByDescending(d => d).ToList();
        int streak = 1;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int offset = dates[0] == today ? 0 : 1; // allow yesterday to still count
        for (int i = offset; i < dates.Count; i++)
        {
            if (dates[i - offset] == dates[i].AddDays(1))
            {
                streak++;
            }
            else
            {
                break;
            }
        }
        return streak;
    }

    private static List<TagStat> BuildEmptyRadars()
        => Tags.All.Select(t => new TagStat { Tag = t, Average = 0, Attempts = 0, Level = "Not attempted" }).ToList();
}