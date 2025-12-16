using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Security.Domain.Models;

namespace Security.Application.Services;

public class TokenService
{
    private readonly IConfiguration _config;
    public TokenService(IConfiguration config) { _config = config; }

    public string CreateAccessToken(User user, IEnumerable<string> roles, IDictionary<string, string>? extraClaims = null)
    {
        var jwtSection = _config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;
        var secret = jwtSection["Secret"]!;
        var minutes = int.Parse(jwtSection["AccessTokenMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        if (extraClaims != null)
        {
            foreach (var kv in extraClaims)
            {
                claims.Add(new Claim(kv.Key, kv.Value));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}