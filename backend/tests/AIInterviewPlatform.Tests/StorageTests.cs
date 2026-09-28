using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Infrastructure;
using MongoDB.Driver;
using Xunit;

namespace AIInterviewPlatform.Tests;

public class MongoConnectionInfoTests
{
    [Fact]
    public void Host_ReturnsClusterHost()
    {
        var host = MongoConnectionInfo.Host(
            "mongodb+srv://user:hunter2@cluster0.example.mongodb.net/?retryWrites=true&w=majority");

        Assert.Equal("cluster0.example.mongodb.net", host);
    }

    [Fact]
    public void Host_NeverReturnsThePassword()
    {
        const string secret = "s3cret-Prod-Password";
        var host = MongoConnectionInfo.Host(
            $"mongodb+srv://appuser:{secret}@cluster.example.mongodb.net/?retryWrites=true");

        Assert.DoesNotContain(secret, host);
        Assert.DoesNotContain("appuser", host);
    }

    [Fact]
    public void Host_KeepsAnExplicitPort()
    {
        Assert.Equal("127.0.0.1:27017", MongoConnectionInfo.Host("mongodb://u:p@127.0.0.1:27017"));
    }

    [Fact]
    public void Host_UsesTheLastAtSignSoPercentEncodedPasswordsAreSafe()
    {
        // A password may legally contain '@' percent-encoded, and Mongo URIs
        // percent-encode it, so the credential section ends at the final '@'.
        var host = MongoConnectionInfo.Host(
            "mongodb+srv://u:p%40ss@cluster.example.mongodb.net/db");

        Assert.Equal("cluster.example.mongodb.net", host);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Host_MissingValue_ReportsNone(string? value)
        => Assert.Equal("(none)", MongoConnectionInfo.Host(value));

    [Fact]
    public void Host_BareSecret_IsNotEchoed()
    {
        // A bare password was once pasted into the connection-string setting, so
        // a non-URI must never be reflected back. Deliberately synthetic below.
        const string bare = "BareSecret-Not-A-Real-Credential";
        var host = MongoConnectionInfo.Host(bare);

        Assert.Equal("(not a MongoDB URI)", host);
        Assert.DoesNotContain(bare, host);
    }

    [Fact]
    public void Redact_StripsInlineCredentials()
    {
        var redacted = MongoConnectionInfo.Redact(
            "MongoAuthenticationException: failed for mongodb+srv://appuser:TopSecret123@cluster.example.mongodb.net");

        Assert.DoesNotContain("TopSecret123", redacted);
        Assert.Contains("cluster.example.mongodb.net", redacted);
    }

    [Fact]
    public void Redact_LeavesCredentialFreeTextUnchanged()
    {
        const string message = "timed out while connecting to cluster.example.mongodb.net";
        Assert.Equal(message, MongoConnectionInfo.Redact(message));
    }

    [Fact]
    public void Redact_HandlesEmptyInput() => Assert.Equal(string.Empty, MongoConnectionInfo.Redact(null));
}

public class MongoConnectionFailureTests
{
    [Fact]
    public async Task ConnectAsync_WithMalformedUri_ThrowsWithoutLeakingCredentials()
    {
        const string secret = "LeakedPassword-9f3a";
        var malformed = $"mongodb+srv://appuser:{secret}@not a valid host";

        var ex = await Assert.ThrowsAsync<MongoUnavailableException>(
            () => MongoAppStore.ConnectAsync(malformed, "ai_interview_platform", maxAttempts: 1));

        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain("appuser", ex.Message);
    }

    [Fact]
    public async Task ConnectAsync_ReportsTheHostEvenWhenItCannotConnect()
    {
        var ex = await Assert.ThrowsAsync<MongoUnavailableException>(
            () => MongoAppStore.ConnectAsync(
                "mongodb://u:p@cluster.example.mongodb.net:27017",
                "ai_interview_platform",
                maxAttempts: 1,
                initialDelay: TimeSpan.Zero));

        Assert.Contains("cluster.example.mongodb.net", ex.Message);
        Assert.DoesNotContain(":p@", ex.Message);
    }
}

public class RequestValidationTests
{
    private static List<string> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? string.Empty).ToList();
    }

    [Fact]
    public void Register_RejectsShortPassword()
        => Assert.NotEmpty(Validate(new RegisterRequest
        {
            FullName = "Candidate",
            Email = "candidate@example.com",
            Password = "short"
        }));

    [Fact]
    public void Register_RejectsMalformedEmail()
        => Assert.NotEmpty(Validate(new RegisterRequest
        {
            FullName = "Candidate",
            Email = "not-an-email",
            Password = "longenough"
        }));

    [Fact]
    public void Register_AcceptsValidInput()
        => Assert.Empty(Validate(new RegisterRequest
        {
            FullName = "Candidate",
            Email = "candidate@example.com",
            Password = "longenough"
        }));

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    public void StartSession_RejectsQuestionCountsOutsideThreeToFifteen(int count)
        => Assert.NotEmpty(Validate(new StartSessionRequest
        {
            BankId = "bank-1",
            QuestionCount = count
        }));

    [Fact]
    public void StartSession_AcceptsInRangeValues()
        => Assert.Empty(Validate(new StartSessionRequest
        {
            BankId = "bank-1",
            QuestionCount = 5,
            DurationMinutes = 10
        }));

    [Theory]
    [InlineData("typed")]
    [InlineData("voice")]
    public void SubmitAnswer_AcceptsTheSupportedModes(string mode)
        => Assert.Empty(Validate(new SubmitAnswerRequest { Mode = mode }));

    [Fact]
    public void SubmitAnswer_RejectsAnUnknownMode()
        => Assert.NotEmpty(Validate(new SubmitAnswerRequest { Mode = "telepathy" }));

    [Fact]
    public void SubmitAnswer_RejectsAnOversizedAnswer()
        => Assert.NotEmpty(Validate(new SubmitAnswerRequest
        {
            Mode = "typed",
            Answer = new string('x', 20001)
        }));

    [Fact]
    public void ResumeQuestions_RequiresText()
        => Assert.NotEmpty(Validate(new ResumeQuestionsRequest { ResumeText = "" }));
}

public class MongoIndexSpecTests
{
    /// <summary>
    /// Reads back the fields and directions the driver will actually index. The
    /// driver keeps its renderer internal, so the definitions are unwrapped via
    /// reflection; this is what a createIndexes command would carry.
    /// </summary>
    private static List<(string Field, int Direction)> IndexedFields(object keys)
    {
        var t = keys.GetType();

        // CombinedIndexKeysDefinition wraps a list of single-key definitions.
        if (t.GetField("_keys", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(keys)
            is System.Collections.IEnumerable combined)
        {
            var all = new List<(string, int)>();
            foreach (var item in combined)
            {
                if (item != null) all.AddRange(IndexedFields(item));
            }
            return all;
        }

        var fieldDef = t.GetField("_field", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(keys)!;
        var dir = (SortDirection)t.GetField("_direction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(keys)!;

        Expression? lambda = null;
        foreach (var f in fieldDef.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (f.GetValue(fieldDef) is Expression e) { lambda = e; break; }
        }

        var body = Assert.IsAssignableFrom<LambdaExpression>(lambda!).Body;
        if (body is UnaryExpression convert) body = convert.Operand;

        var member = Assert.IsAssignableFrom<MemberExpression>(body);
        return new List<(string, int)> { (member.Member.Name, dir == SortDirection.Descending ? -1 : 1) };
    }

    /// <summary>
    /// Regression guard. The first deployment of these indexes declared
    /// <c>unique</c> on <c>_id</c>, which MongoDB rejects outright, so
    /// createIndexes failed and the service refused to start. No spec may index
    /// <c>_id</c>, and uniqueness may never be combined with it.
    /// </summary>
    [Fact]
    public void NoStartupIndex_TargetsId()
    {
        var models = new (string Label, CreateIndexOptions Options, object Keys)[]
        {
            ("users.email", MongoIndexSpecs.UsersEmail().Options, MongoIndexSpecs.UsersEmail().Keys),
            ("sessions.user+startedAt",
                MongoIndexSpecs.SessionsUserStartedAt().Options,
                MongoIndexSpecs.SessionsUserStartedAt().Keys),
        };

        foreach (var (label, options, keys) in models)
        {
            foreach (var (field, _) in IndexedFields(keys))
            {
                Assert.NotEqual("_id", field);
                Assert.NotEqual("Id", field);

                // The exact condition the deploy hit: unique is illegal on an _id spec.
                if (options.Unique == true)
                    Assert.NotEqual("Id", field);
            }
        }
    }

    [Fact]
    public void UsersEmail_IsUniqueOnEmailForLoginLookups()
    {
        var model = MongoIndexSpecs.UsersEmail();
        Assert.Equal("users_email", model.Options.Name);
        Assert.True(model.Options.Unique);
        Assert.Equal(new List<(string, int)> { ("Email", 1) }, IndexedFields(model.Keys));
    }

    [Fact]
    public void Sessions_AreIndexedByUserThenNewestFirst()
    {
        var model = MongoIndexSpecs.SessionsUserStartedAt();
        Assert.Equal("sessions_user_started", model.Options.Name);
        Assert.NotEqual(true, model.Options.Unique);
        Assert.Equal(
            new List<(string, int)> { ("UserId", 1), ("StartedAt", -1) },
            IndexedFields(model.Keys));
    }
}
