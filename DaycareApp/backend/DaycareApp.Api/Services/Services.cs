using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DaycareApp.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace DaycareApp.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public string Create(User u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub", u.Id.ToString()),
                new Claim("role", u.Role.ToString()),
                new Claim("dc", u.DaycareId.ToString()),
                new Claim("name", u.Name)
            ],
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// "Today" follows the daycare's local calendar, not UTC.
public class Clock(IConfiguration cfg)
{
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(cfg["App:TimeZone"] ?? "UTC");
    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz));
}
