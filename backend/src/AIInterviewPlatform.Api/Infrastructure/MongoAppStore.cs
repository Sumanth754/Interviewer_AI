using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using MongoDB.Driver;

namespace AIInterviewPlatform.Api.Infrastructure;

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
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        settings.ConnectTimeout = TimeSpan.FromSeconds(3);
        var client = new MongoClient(settings);
        _db = client.GetDatabase(databaseName);
    }

    public string Kind => "MongoDB";

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: ct);
            return true;
        }
        catch
        {
            return false;
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