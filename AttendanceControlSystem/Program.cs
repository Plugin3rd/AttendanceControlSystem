using System.Text;
using System.Text.Json.Serialization;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// ایجاد پوشهٔ اطلاعات برنامه و دیتابیس
var appDataPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "App_Data");

Directory.CreateDirectory(appDataPath);

// دریافت رشتهٔ اتصال دیتابیس
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AttendanceDbContext>(
    options =>
    {
        options.UseSqlite(connectionString);
    });

// ثبت سرویس‌های برنامه
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<BackupService>();

// دریافت تنظیمات JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtKey));

// تنظیم احراز هویت JWT
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

// تنظیم مجوزها
builder.Services.AddAuthorization();

// افزودن کنترلرها و پشتیبانی از Enum به‌صورت رشته‌ای
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

var app = builder.Build();

// ایجاد دیتابیس و ثبت کاربران اولیه
await DbSeeder.SeedAsync(app.Services);

// تنظیمات محیط اجرا
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();