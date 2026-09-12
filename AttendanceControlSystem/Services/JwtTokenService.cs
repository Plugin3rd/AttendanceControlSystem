using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceControlSystem.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(object user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var username = ReadProperty(
            user,
            "Username",
            "UserName",
            "Name");

        var userId = ReadProperty(
            user,
            "Id",
            "UserId");

        var role = ReadProperty(
            user,
            "Role",
            "UserRole");

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                "نام کاربری برای تولید توکن پیدا نشد.");
        }

        return GenerateTokenWithExpiry(username, userId, role).Token;
    }

    public string CreateToken(object user)
    {
        return GenerateToken(user);
    }

    public (string Token, DateTime Expires) CreateTokenWithExpiry(object user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var username = ReadProperty(
            user,
            "Username",
            "UserName",
            "Name");

        var userId = ReadProperty(
            user,
            "Id",
            "UserId");

        var role = ReadProperty(
            user,
            "Role",
            "UserRole");

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                "نام کاربری برای تولید توکن پیدا نشد.");
        }

        return GenerateTokenWithExpiry(username, userId, role);
    }

    public (string Token, DateTime Expires) GenerateTokenWithExpiry(
        string username,
        string? userId = null,
        string? role = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "نام کاربری نمی‌تواند خالی باشد.",
                nameof(username));
        }

        var secretKey = GetSetting(
            "Jwt:Key",
            "Jwt:Secret",
            "JwtSettings:Key",
            "JwtSettings:SecretKey",
            "JwtSettings:Secret");

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "کلید JWT در appsettings.json پیدا نشد.");
        }

        var issuer = GetSetting(
            "Jwt:Issuer",
            "JwtSettings:Issuer");

        var audience = GetSetting(
            "Jwt:Audience",
            "JwtSettings:Audience");

        var expiresText = GetSetting(
            "Jwt:ExpireMinutes",
            "JwtSettings:ExpireMinutes");

        var expireMinutes = 120;

        if (int.TryParse(
                expiresText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var configuredMinutes)
            && configuredMinutes > 0)
        {
            expireMinutes = configuredMinutes;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier,
                userId));

            claims.Add(new Claim(
                JwtRegisteredClaimNames.Sub,
                userId));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(
                ClaimTypes.Role,
                role));
        }

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(expireMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler()
                .WriteToken(token),
            expires);
    }

    public (string Token, DateTime Expires) CreateToken(
        string username,
        string? userId = null,
        string? role = null)
    {
        return GenerateTokenWithExpiry(username, userId, role);
    }

    private string? GetSetting(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadProperty(
        object source,
        params string[] propertyNames)
    {
        var type = source.GetType();

        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.IgnoreCase);

            if (property is null || !property.CanRead)
            {
                continue;
            }

            var value = property.GetValue(source);

            if (value is not null)
            {
                return Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);
            }
        }

        return null;
    }
}
