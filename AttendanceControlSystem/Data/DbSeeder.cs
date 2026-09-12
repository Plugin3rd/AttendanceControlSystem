
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AttendanceDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.Users.AnyAsync())
        {
            return;
        }

        var admin = new User
        {
            Username = "admin",
            PasswordHash = new PasswordHasher<User>().HashPassword(null, "Admin123!"),
            Role = UserRole.Admin
        };

        var operatorUser = new User
        {
            Username = "operator",
            PasswordHash = new PasswordHasher<User>().HashPassword(null, "Operator123!"),
            Role = UserRole.Operator
        };

        dbContext.Users.AddRange(admin, operatorUser);

        await dbContext.SaveChangesAsync();
    }
}
