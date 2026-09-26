using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AIInterviewPlatform.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AIInterviewPlatform.Api.Auth;

public interface ITokenService
{
    string CreateToken(User user);
    string? GetUserId(ClaimsPrincipal principal);
}

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
        => _options = options.Value;

    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_options.ExpiryDays),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? GetUserId(ClaimsPrincipal principal)
        => principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "AIInterviewPlatform";
    public string Audience { get; set; } = "AIInterviewPlatformUsers";
    public int ExpiryDays { get; set; } = 7;
}

public sealed class BootstrapOptions
{
    /// <summary>
    /// When set, only this email address is promoted to Admin on the very first
    /// registration. Every other account becomes a Candidate even on an empty
    /// database, which stops a stranger from claiming Admin on a public
    /// deployment that uses in-memory storage. Leave empty to keep the
    /// zero-config "first user is Admin" behaviour for local development.
    /// </summary>
    public string AdminEmail { get; set; } = "";
}