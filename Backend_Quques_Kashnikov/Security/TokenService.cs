using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Backend_Quques_Kashnikov.Models;

namespace Backend_Quques_Kashnikov.Security;

public class TokenService(byte[] key)
{
    public const string Issuer = "EduQueue.Api";
    public const string Audience = "EduQueue.Client";
    public SymmetricSecurityKey SigningKey { get; } = new(key);
    public object Issue(AppUser user)
    {
        var expires = DateTime.UtcNow.AddHours(1);
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.PassNumber),
            new Claim(ClaimTypes.Role, user.Role), new Claim("stamp", user.SecurityStamp)
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims, DateTime.UtcNow, expires,
            new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresAtUtc = expires, user.Id, user.Role };
    }
}
