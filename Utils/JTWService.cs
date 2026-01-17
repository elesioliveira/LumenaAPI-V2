using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public class JwtService
{
    private readonly IConfiguration _config;
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessMinutes;

    public JwtService(IConfiguration config)
    {
        _config = config;
        _key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        _issuer = _config["Jwt:Issuer"]!;
        _audience = _config["Jwt:Audience"]!;
        _accessMinutes = int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "1440");
    }

    public string GenerateToken(int userId, int empresaId)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("userId", userId.ToString()),
            new Claim("empresaId", empresaId.ToString())
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
