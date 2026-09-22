using System.Security.Claims;
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AttendanceControlSystem.Services;

public sealed class CookiePrincipalFactory
{
    public const string SecurityStampClaimType = "SecurityStamp";
    public const string UserIdClaimType = "UserId";

    public ClaimsPrincipal Create(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(SecurityStampClaimType, user.SecurityStamp),
            new Claim(UserIdClaimType, user.Id.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
