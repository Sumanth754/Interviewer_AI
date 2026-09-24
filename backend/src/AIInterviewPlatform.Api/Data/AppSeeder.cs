using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;

namespace AIInterviewPlatform.Api.Data;

public interface IAppSeeder
{
    Task SeedAsync();
}

public sealed class AppSeeder : IAppSeeder
{
    private readonly IAppStore _store;
    private readonly ILogger<AppSeeder> _log;

    public AppSeeder(IAppStore store, ILogger<AppSeeder> log)
    {
        _store = store;
        _log = log;
    }

    public async Task SeedAsync()
    {
        if (await _store.CountBanksAsync() > 0)
            return;

        var banks = SeedData.Build();
        foreach (var bank in banks)
        {
            bank.Questions.ForEach(qn =>
            {
                qn.BankId = bank.Id;
                qn.BankName = bank.Name;
            });
            await _store.CreateBankAsync(bank);
        }

        _log.LogInformation("Seeded {Count} question banks ({Questions} questions).",
            banks.Count, banks.Sum(b => b.Questions.Count));
    }
}