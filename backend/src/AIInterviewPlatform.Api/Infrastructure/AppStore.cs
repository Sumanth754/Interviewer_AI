using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;

namespace AIInterviewPlatform.Api.Infrastructure;

public interface IAppStore
{
    string Kind { get; }
    Task<bool> PingAsync(CancellationToken ct = default);

    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(string id);
    Task CreateUserAsync(User user);

    Task<List<BankSummary>> ListBankSummariesAsync();
    Task<QuestionBank?> GetBankAsync(string id);
    Task CreateBankAsync(QuestionBank bank);
    Task UpdateBankAsync(QuestionBank bank);
    Task<long> CountBanksAsync();
    Task<long> CountUsersAsync();

    Task CreateSessionAsync(AssessmentSession session);
    Task<AssessmentSession?> GetSessionAsync(string id);
    Task UpdateSessionAsync(AssessmentSession session);
    Task<List<AssessmentSession>> ListUserSessionsAsync(string userId, int limit);
}

public sealed class InMemoryAppStore : IAppStore
{
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, QuestionBank> _banks = new();
    private readonly Dictionary<string, AssessmentSession> _sessions = new();
    private readonly object _sync = new();

    public string Kind => "InMemory";

    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<User?> GetUserByEmailAsync(string email)
    {
        lock (_sync)
            return Task.FromResult(_users.Values.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> GetUserByIdAsync(string id)
    {
        lock (_sync)
            return Task.FromResult(_users.TryGetValue(id, out var u) ? u : null);
    }

    public Task CreateUserAsync(User user)
    {
        lock (_sync) _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<List<BankSummary>> ListBankSummariesAsync()
    {
        lock (_sync)
        {
            var result = _banks.Values
                .Select(b => new BankSummary
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    QuestionCount = b.Questions.Count,
                    Tags = b.Questions.Select(q => q.Tag).Distinct().ToList()
                })
                .OrderBy(b => b.Name)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<QuestionBank?> GetBankAsync(string id)
    {
        lock (_sync)
            return Task.FromResult(_banks.TryGetValue(id, out var b) ? b : null);
    }

    public Task CreateBankAsync(QuestionBank bank)
    {
        lock (_sync) _banks[bank.Id] = bank;
        return Task.CompletedTask;
    }

    public Task UpdateBankAsync(QuestionBank bank)
    {
        lock (_sync) _banks[bank.Id] = bank;
        return Task.CompletedTask;
    }

    public Task<long> CountBanksAsync()
    {
        lock (_sync) return Task.FromResult((long)_banks.Count);
    }

    public Task<long> CountUsersAsync()
    {
        lock (_sync) return Task.FromResult((long)_users.Count);
    }

    public Task CreateSessionAsync(AssessmentSession session)
    {
        lock (_sync) _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<AssessmentSession?> GetSessionAsync(string id)
    {
        lock (_sync)
            return Task.FromResult(_sessions.TryGetValue(id, out var s) ? s : null);
    }

    public Task UpdateSessionAsync(AssessmentSession session)
    {
        lock (_sync) _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<List<AssessmentSession>> ListUserSessionsAsync(string userId, int limit)
    {
        lock (_sync)
        {
            return Task.FromResult(_sessions.Values
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartedAt)
                .Take(limit)
                .ToList());
        }
    }
}