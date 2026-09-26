using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace AIInterviewPlatform.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}

public sealed class AuthService : IAuthService
{
    private readonly IAppStore _store;
    private readonly ITokenService _tokens;
    private readonly BootstrapOptions _bootstrap;

    public AuthService(IAppStore store, ITokenService tokens, IOptions<BootstrapOptions> bootstrap)
    {
        _store = store;
        _tokens = tokens;
        _bootstrap = bootstrap.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email))
            throw new AppException("Full name and email are required.");

        if (request.Password.Length < 6)
            throw new AppException("Password must be at least 6 characters.");

        if (await _store.GetUserByEmailAsync(email) is not null)
            throw new AppException("An account with this email already exists.");

        // Bootstrap: the very first account on an empty store becomes an Admin.
        // Bootstrap:AdminEmail pins that promotion to one address, so on a public
        // deployment nobody else can claim Admin by registering first.
        var isFirstUser = await _store.CountUsersAsync() == 0;
        var pinnedAdmin = _bootstrap.AdminEmail.Trim().ToLowerInvariant();
        var becomesAdmin = isFirstUser && (pinnedAdmin.Length == 0 || pinnedAdmin == email);

        var user = new User
        {
            Id = IdGen.New(),
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = becomesAdmin ? UserRole.Admin : UserRole.Candidate
        };

        await _store.CreateUserAsync(user);
        return new AuthResponse
        {
            Token = _tokens.CreateToken(user),
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _store.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            throw new AppException("Invalid email or password.", 401);

        return new AuthResponse
        {
            Token = _tokens.CreateToken(user),
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };
    }
}

public sealed class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message)
        => StatusCode = statusCode;
}