using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Data;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;
using AIInterviewPlatform.Api.Scoring;
using AIInterviewPlatform.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIInterviewPlatform.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Then_Verify_Succeeds()
    {
        var hash = PasswordHasher.Hash("s3cretPass_2026");
        Assert.True(PasswordHasher.Verify("s3cretPass_2026", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = PasswordHasher.Hash("correct-horse");
        Assert.False(PasswordHasher.Verify("battery-staple", hash));
    }

    [Fact]
    public void Verify_Malformed_ReturnsFalse()
    {
        Assert.False(PasswordHasher.Verify("x", "not-a-valid-hash"));
    }

    [Fact]
    public void Hashes_Are_Salted_And_Different()
    {
        var a = PasswordHasher.Hash("same");
        var b = PasswordHasher.Hash("same");
        Assert.NotEqual(a, b);
    }
}

public class TokenServiceTests
{
    private static TokenService Create() => new(Options.Create(new JwtOptions
    {
        Key = "Test-Signing-Key-That-Is-Long-Enough-For-HMAC-256!!",
        Issuer = "Tests",
        Audience = "TestsAud",
        ExpiryDays = 1
    }));

    [Fact]
    public void CreateToken_CanRoundTrip_SubClaim()
    {
        var svc = Create();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = svc.CreateToken(new User { Id = "u42", Email = "x@y.com", FullName = "R", Role = UserRole.Candidate });

        var json = handler.ReadJwtToken(token);
        Assert.Equal("u42", json.Subject);

        var emailClaim = json.Claims.First(c =>
            c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email
            || c.Type == System.Security.Claims.ClaimTypes.Email);
        Assert.Equal("x@y.com", emailClaim.Value);

        var roleClaim = json.Claims.First(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role
            || c.Type.EndsWith("role", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Candidate", roleClaim.Value);
    }
}

public class RubricScoreServiceTests
{
    private static Question Question(string tag, params string[] keywords) => new()
    {
        Id = "q1",
        Text = "Explain how a hash table works.",
        Tag = tag,
        Difficulty = "Medium",
        ExpectedKeywords = keywords.ToList(),
        ModelPoints = new List<string> { "hash function", "collisions", "buckets" }
    };

    private readonly RubricScoreService _svc = new();

    [Fact]
    public async Task EmptyAnswer_ScoresZero()
    {
        var r = await _svc.EvaluateTextAsync(Question(Tags.Dsa, "hash"), "   ", "typed");
        Assert.Equal(0, r.Score);
    }

    [Fact]
    public async Task FullAnswer_CoversKeywords_ScoresHigh()
    {
        var r = await _svc.EvaluateTextAsync(Question(Tags.Dsa, "hash", "hash", "collision"),
            "A hash function maps keys to buckets. Collisions are resolved by chaining, making lookup fast on average.",
            "typed");
        Assert.InRange(r.Score, 60, 100);
        Assert.Contains(r.Strengths, s => s.Contains("hash"));
    }

    [Fact]
    public async Task ShortAnswer_ScoresBelowDepthThreshold()
    {
        var r = await _svc.EvaluateTextAsync(Question(Tags.Dsa, "hash"), "hash table stores things.", "typed");
        Assert.InRange(r.Score, 1, 59);
    }
}

public class AdaptivePickerTests
{
    private static List<Question> Pool(int perTag = 3)
    {
        var qs = new List<Question>();
        foreach (var tag in new[] { Tags.Dsa, Tags.Oop, Tags.Logic })
        {
            foreach (var d in new[] { "Easy", "Medium", "Hard" })
            {
                for (var i = 0; i < perTag; i++)
                {
                    qs.Add(new Question { Id = $"{tag}-{d}-{i}", Text = $"{tag} {d} {i}", Tag = tag, Difficulty = d });
                }
            }
        }
        return qs;
    }

    [Fact]
    public void Balanced_Picks_RequestedCount_WithDifficultyMix()
    {
        var picker = new AdaptiveDifficultyPicker();
        var picks = picker.PickBalanced(Pool(), 5);
        Assert.Equal(5, picks.Count);
        Assert.True(picks.Select(p => p.Difficulty).Distinct().Count() >= 2);
    }

    [Fact]
    public void Adaptive_Prioritizes_WeakestTag()
    {
        var picker = new AdaptiveDifficultyPicker();
        var skill = new Dictionary<string, double>
        {
            [Tags.Dsa] = 25,
            [Tags.Oop] = 80,
            [Tags.Logic] = 70
        };
        var picks = picker.PickAdaptive(Pool(), 6, skill);
        // Weakest tag (DSA at 25) must appear first and more often than a top tag at 80.
        Assert.Equal(Tags.Dsa, picks[0].Tag);
        Assert.True(picks.Count(p => p.Tag == Tags.Dsa) >= picks.Count(p => p.Tag == Tags.Oop));
    }

    [Fact]
    public void Adaptive_WeakTag_GetsEasierQuestions()
    {
        var picker = new AdaptiveDifficultyPicker();
        var skill = new Dictionary<string, double> { [Tags.Dsa] = 25, [Tags.Oop] = 80, [Tags.Logic] = 70 };
        var picks = picker.PickAdaptive(Pool(), 9, skill);
        var dsaPicks = picks.Where(p => p.Tag == Tags.Dsa).ToList();
        Assert.All(dsaPicks.Take(3), p => Assert.Equal("Easy", p.Difficulty));
    }
}

public class ReportServiceTests
{
    private static ReportService Create() => new(
        new InMemoryAppStore(),
        new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public void EstimatePercentile_IsMonotonicWithinBounds()
    {
        var svc = Create();

        var p10 = svc.EstimatePercentile(10);
        var p40 = svc.EstimatePercentile(40);
        var p56 = svc.EstimatePercentile(56);
        var p90 = svc.EstimatePercentile(90);

        Assert.True(p10 > 0 && p10 < 20);
        Assert.True(p40 > p10);
        Assert.True(p56 > 40 && p56 < 60, $"Expected ~50 for mean score, got {p56}");
        Assert.True(p90 > p56 && p90 < 99.9);
    }
}

public class SeedDataTests
{
    [Fact]
    public void Seed_Builds_TwoBanks_WithMcqAndSubjective()
    {
        var banks = SeedData.Build();
        Assert.Equal(2, banks.Count);
        Assert.All(banks, b => Assert.NotEmpty(b.Questions));
        Assert.Contains(banks.SelectMany(b => b.Questions), q => q.Type == QuestionType.Mcq);
        Assert.Contains(banks.SelectMany(b => b.Questions), q => q.Type == QuestionType.Voice);
    }

    [Fact]
    public void Seed_Mcqs_HaveValidCorrectIndex()
    {
        var banks = SeedData.Build();
        foreach (var q in banks.SelectMany(b => b.Questions).Where(q => q.Type == QuestionType.Mcq))
        {
            Assert.InRange(q.CorrectIndex, 0, q.Options.Count - 1);
        }
    }
}