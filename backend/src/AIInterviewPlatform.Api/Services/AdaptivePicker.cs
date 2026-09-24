using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Services;

/// <summary>
/// Selects assessment questions. In adaptive mode, prioritizes the candidate's
/// weakest tags first and adjusts difficulty to each tag's current skill level —
/// exactly how a good interviewer drills into your weak spots.
/// </summary>
public sealed class AdaptiveDifficultyPicker
{
    public List<Question> PickBalanced(List<Question> pool, int count)
    {
        var byDifficulty = pool.GroupBy(q => q.Difficulty).ToDictionary(g => g.Key, g => g.ToList());
        var easy = byDifficulty.GetValueOrDefault("Easy", new());
        var medium = byDifficulty.GetValueOrDefault("Medium", new());
        var hard = byDifficulty.GetValueOrDefault("Hard", new());

        // Deterministic mix: ~hardest-third each difficulty rounded to count.
        var ratio = count >= 6 ? 0.33 : 0.4;
        int hardPick = Math.Min(hard.Count, Math.Max(1, (int)Math.Round(count * ratio * 0.5)));
        int easyPick = Math.Min(easy.Count, Math.Max(2, (int)Math.Round(count * ratio)));
        int mediumPick = Math.Min(medium.Count, Math.Max(1, count - easyPick - hardPick));

        var picks = new List<Question>();
        picks.AddRange(Rotate(easy, easyPick));
        picks.AddRange(Rotate(medium, Math.Max(0, mediumPick)));
        picks.AddRange(Rotate(hard, Math.Max(0, hardPick)));

        if (picks.Count < count && pool.Count >= count)
        {
            var taken = picks.Select(p => p.Id).ToHashSet();
            foreach (var q in pool)
            {
                if (picks.Count >= count) break;
                if (taken.Contains(q.Id)) continue;
                picks.Add(q);
            }
        }

        return picks.Take(count).ToList();
    }

    public List<Question> PickAdaptive(List<Question> pool, int count, Dictionary<string, double> skillByTag)
    {
        // Order tags weakest-first; within a tag, difficulty by skill.
        var grouped = pool
            .GroupBy(q => q.Tag)
            .OrderBy(g => skillByTag.GetValueOrDefault(g.Key, 50.0)) // weakest first
            .ToList();

        var picks = new List<Question>();
        var used = new HashSet<string>();

        int rounds = Math.Max(1, (count + grouped.Count - 1) / grouped.Count);
        for (int round = 0; round < rounds && picks.Count < count; round++)
        {
            foreach (var group in grouped)
            {
                if (picks.Count >= count) break;
                var skill = skillByTag.GetValueOrDefault(group.Key, 50.0);
                var targetDifficulty = skill < 40 ? "Easy" : skill < 70 ? "Medium" : "Hard";

                var q = group
                    .Where(x => !used.Contains(x.Id))
                    .OrderBy(x => x.Difficulty == targetDifficulty ? 0
                        : x.Difficulty == "Medium" ? 1
                        : x.Difficulty == "Easy" ? 2 : 3)
                    .ThenBy(x => Guid.NewGuid().ToString())
                    .FirstOrDefault();

                if (q is null) continue;
                used.Add(q.Id);
                picks.Add(q);
            }
        }

        // Any remainder if pool was thin.
        foreach (var q in pool)
        {
            if (picks.Count >= count) break;
            if (used.Contains(q.Id)) continue;
            used.Add(q.Id);
            picks.Add(q);
        }

        return picks.Take(count).ToList();
    }

    private static List<Question> Rotate(List<Question> source, int n)
    {
        var list = source.ToList();
        if (list.Count <= n) return list;

        // Rotate the starting index so repeated attempts vary the questions.
        list = list.Skip(list.Count % 7).Concat(list.Take(list.Count % 7)).ToList();
        return list.Take(n).ToList();
    }
}