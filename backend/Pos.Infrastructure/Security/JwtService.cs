using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pos.Application.Ports;

namespace Pos.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionKey = "Jwt";
    public string SecurityKey { get; set; } = "UnSecretoSuperSeguroParaElProyectoIntegradorPDS2026!";
    public string ValidIssuer { get; set; } = "PosApi";
    public string ValidAudience { get; set; } = "PosWebClient";
    public int ExpirationMinutes { get; set; } = 120;
}

public class JwtService : IJwtProvider
{
    private readonly JwtOptions _options;

    public JwtService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    private SigningCredentials GetSigningCredentials()
    {
        var key = Encoding.UTF8.GetBytes(_options.SecurityKey);
        var secret = new SymmetricSecurityKey(key);
        return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
    }

    public string GenerateToken(string username, string fullName, string role, int? posSessionId, string? posConfigName, int odooUserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("sub", username),
            new(ClaimTypes.Role, role),
            new("FullName", fullName),
            new("name", fullName),
            new("pos_config_name", !string.IsNullOrWhiteSpace(posConfigName) ? posConfigName : "Sin Caja Asignada"),
            new("odoo_user_id", odooUserId.ToString())
        };

        if (posSessionId.HasValue && posSessionId.Value > 0)
        {
            claims.Add(new Claim("pos_session_id", posSessionId.Value.ToString()));
        }
        else
        {
            claims.Add(new Claim("pos_session_id", ""));
        }

        if (username.Contains('@'))
        {
            claims.Add(new Claim("email", username));
        }

        var signingCredentials = GetSigningCredentials();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.ValidIssuer,
            Audience = _options.ValidAudience,
            Expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(tokenDescriptor);
    }
}
