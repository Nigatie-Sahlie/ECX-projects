using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DaycareApi;

public class TokenService(IConfiguration cfg)
{
    public string Create(User u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Role, u.Role),
            new(ClaimTypes.Name, u.FullName),
        };
        if (u.CenterId is Guid c) claims.Add(new Claim("cid", c.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(cfg["Jwt:Hours"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class PrincipalExt
{
    public static Guid Uid(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Guid? Cid(this ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue("cid"), out var g) ? g : null;
    public static string RoleName(this ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.Role)!;
}
