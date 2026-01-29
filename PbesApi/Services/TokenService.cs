using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PbesApi.Models;

namespace PbesApi.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Officer officer)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secret = jwtSection["Secret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT secret is not configured. Set Jwt:Secret in appsettings.json.");
        }

        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, officer.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, officer.Id.ToString()),
            new Claim(ClaimTypes.Role, officer.Role)
        };

        if (!string.IsNullOrWhiteSpace(officer.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, officer.Email));
        }

        if (!string.IsNullOrWhiteSpace(officer.ServiceNumber))
        {
            claims.Add(new Claim("serviceNumber", officer.ServiceNumber));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
