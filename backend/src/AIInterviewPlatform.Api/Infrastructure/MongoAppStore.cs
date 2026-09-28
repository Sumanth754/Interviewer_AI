using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AIInterviewPlatform.Api.Infrastructure;

/// <summary>
/// Thrown when MongoDB is required but could not be reached. Callers decide
/// whether that is fatal; Program.cs fails closed when Database:Mode = Mongo.
/// </summary>
public sealed class MongoUnavailableException : Exception
{
    public MongoUnavailableException(string message) : base(message) { }
}

/// <summary>
/// MongoDB-backed store. Questions are embedded inside the QuestionBank document.
/// </summary>
public sealed class MongoAppStore : IAppStore
{
    private readonly IMongoDatabase _db;
    private IMongoCollection<User> _users => _db.GetCollection<User>("users");
    private IMongoCollection<QuestionBank> _banks => _db.GetCollection<QuestionBank>("banks");
    private IMongoCollection<AssessmentSession> _sessions => _db.GetCollection<AssessmentSession>("sessions");

    public MongoAppStore(string connectionString, string databaseName)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        // A cold container can take several seconds to resolve SRV, open TCP and
        // complete the TLS handshake. A 3s window lost that race and silently
        // demoted the whole deployment to the in-memory store.
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(20);
        settings.ConnectTimeout = TimeSpan.FromSeconds(20);
        var client = new MongoClient(settings);
        _db = client.GetDatabase(databaseName);
    }

    public string Kind => "MongoDB";

    /// <summary>
    /// Why the most recent ping failed, or null after a success. Redacted so it is
    /// safe to write to logs.
    /// </summary>
    public string? LastPingError { get; private set; }

    /// <summary>
    /// Pings with bounded retries and exponential backoff, so a container that is
    /// merely slow to reach Atlas gets a fair chance before we give up on it.
    /// The window is deliberately bounded (see <c>Database:Mongo:MaxConnectAttempts</c>)
    /// so a wrong connection string fails a deploy visibly in a couple of minutes
    /// rather than hanging it.
    /// </summary>
    public static async Task<MongoAppStore> ConnectAsync(
        string connectionString,
        string databaseName,
        int maxAttempts = 6,
        TimeSpan? initialDelay = null,
        CancellationToken ct = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        MongoAppStore store;

        // A malformed URI throws from the driver before any connection attempt.
        // Surface it in the same safe shape as a failed ping.
        try
        {
            store = new MongoAppStore(connectionString, databaseName);
        }
        catch (Exception ex)
        {
            throw new MongoUnavailableException(
                $"MongoDB could not be configured (host={MongoConnectionInfo.Host(connectionString)}): {Describe(ex)}");
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await store.PingAsync(ct))
                return store;

            if (attempt < maxAttempts)
            {
                Console.WriteLine(
                    $"[startup] MongoDB ping attempt {attempt}/{maxAttempts} failed " +
                    $"({store.LastPingError}); retrying in {delay.TotalSeconds:0.##}s");
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 5));
            }
        }

        throw new MongoUnavailableException(
            $"MongoDB was unreachable after {maxAttempts} attempts " +
            $"(host={MongoConnectionInfo.Host(connectionString)}): {store.LastPingError}");
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1), cancellationToken: ct);
            LastPingError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastPingError = Describe(ex);
            return false;
        }
    }

    private static string Describe(Exception ex)
    {
        // Matched by type name so the wording does not depend on which exception
        // types a given driver version happens to expose.
        var name = ex.GetType().Name;
        var detail = name switch
        {
            "MongoAuthenticationException" =>
                "authentication failed (check the username and password in the connection string)",
            "MongoConfigurationException" =>
                "the connection string is not a valid MongoDB URI",
            "MongoConnectionTimeoutException" or "MongoClientTimeoutException" or "TimeoutException" =>
                "timed out while connecting (check the host and the Atlas IP access list)",
            "MongoClientException" =>
                "no server was reachable (check the host, DNS and the Atlas IP access list)",
            _ => ex.Message
        };
        return MongoConnectionInfo.Redact($"{name}: {detail}");
    }

    /// <summary>
    /// Creates the indexes the read paths rely on. Idempotent: re-running is a
    /// no-op, and pre-existing duplicate documents are reported rather than
    /// crashing startup.
    ///
    /// Note: no index is created on <c>_id</c>. MongoDB indexes that field
    /// automatically and enforces uniqueness itself, and it rejects a
    /// <c>unique</c> option on an <c>_id</c> spec outright, so asking for one
    /// fails the whole startup command. The specs live in
    /// <see cref="MongoIndexSpecs"/> where they are unit-tested.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await EnsureAsync("users.email",
            () => _users.Indexes.CreateOneAsync(MongoIndexSpecs.UsersEmail()), ct);

        await EnsureAsync("sessions.user+startedAt",
            () => _sessions.Indexes.CreateOneAsync(MongoIndexSpecs.SessionsUserStartedAt()), ct);
    }

    private static async Task EnsureAsync(string label, Func<Task> create, CancellationToken ct)
    {
        try
        {
            await create();
            Console.WriteLine($"[startup] index '{label}' ensured");
        }
        catch (MongoCommandException ex) when (ex.Code is 85 or 86)
        {
            Console.WriteLine($"[startup] index '{label}' already exists with different options; left as-is");
        }
        catch (MongoCommandException ex) when (ex.Code is 11000 or 11001)
        {
            Console.WriteLine(
                $"[startup] index '{label}' NOT created: existing documents contain duplicates. " +
                "Deduplicate them, then redeploy.");
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email)
        => await _users.Find(u => u.Email.ToLower() == email.ToLower()).FirstOrDefaultAsync();

    public async Task<User?> GetUserByIdAsync(string id)
        => await _users.Find(u => u.Id == id).FirstOrDefaultAsync();

    public async Task CreateUserAsync(User user)
        => await _users.InsertOneAsync(user);

    public async Task<List<BankSummary>> ListBankSummariesAsync()
    {
        var banks = await _banks.Find(_ => true).ToListAsync();
        return banks.Select(b => new BankSummary
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            QuestionCount = b.Questions.Count,
            Tags = b.Questions.Select(q => q.Tag).Distinct().ToList()
        }).OrderBy(b => b.Name).ToList();
    }

    public async Task<QuestionBank?> GetBankAsync(string id)
        => await _banks.Find(b => b.Id == id).FirstOrDefaultAsync();

    public async Task CreateBankAsync(QuestionBank bank)
        => await _banks.InsertOneAsync(bank);

    public async Task UpdateBankAsync(QuestionBank bank)
        => await _banks.ReplaceOneAsync(b => b.Id == bank.Id, bank);

    public Task<long> CountBanksAsync()
        => _banks.CountDocumentsAsync(_ => true);

    public Task<long> CountUsersAsync()
        => _users.CountDocumentsAsync(_ => true);

    public async Task CreateSessionAsync(AssessmentSession session)
        => await _sessions.InsertOneAsync(session);

    public async Task<AssessmentSession?> GetSessionAsync(string id)
        => await _sessions.Find(s => s.Id == id).FirstOrDefaultAsync();

    public async Task UpdateSessionAsync(AssessmentSession session)
        => await _sessions.ReplaceOneAsync(s => s.Id == session.Id, session);

    public async Task<List<AssessmentSession>> ListUserSessionsAsync(string userId, int limit)
        => await _sessions.Find(s => s.UserId == userId)
            .SortByDescending(s => s.StartedAt)
            .Limit(limit)
            .ToListAsync();
}
