using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Domain;
using AIInterviewPlatform.Api.Infrastructure;

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

    public AuthService(IAppStore store, ITokenService tokens)
    {
        _store = store;
        _tokens = tokens;
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

        // Bootstrap: the very first account ever registered becomes an Admin.
        var isFirstUser = await _store.CountUsersAsync() == 0;

        var user = new User
        {
            Id = IdGen.New(),
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = isFirstUser ? UserRole.Admin : UserRole.Candidate
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