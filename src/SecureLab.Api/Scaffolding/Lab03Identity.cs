using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (
    RegistrationInput input,
    UserManager<ApplicationUser> users) =>
        {
            if (!IsValidRegistration(input))
            {
                return Results.BadRequest(new
                {
                    message = "Invalid registration data"
                });
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = input.UserName!.Trim(),
                Email = input.Email!.Trim(),
                DisplayName = input.DisplayName!.Trim()
            };

            var result = await users.CreateAsync(user, input.Password!);

            if (!result.Succeeded)
            {
                bool duplicate = result.Errors.Any(error =>
                    error.Code == "DuplicateUserName" ||
                    error.Code == "DuplicateEmail");

                if (duplicate)
                {
                    return Results.Conflict(new
                    {
                        message = "Registration conflict"
                    });
                }

                return Results.BadRequest(new
                {
                    message = "Registration failed"
                });
            }

            var role = await users.AddToRoleAsync(
                user, DbSeeder.ReporterRole);

            if (!role.Succeeded)
            {
                throw new InvalidOperationException(
                    "Default role assignment failed.");
            }

            var response = new RegistrationResponse(
                user.Id,
                user.UserName!,
                user.DisplayName
            );

            return Results.Created("/api/me", response);
        });
        app.MapPost("/api/auth/login", async (LoginInput input, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrEmpty(input.Password))
                return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(input.UserName, input.Password, false, false);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });
        app.MapPost("/api/auth/logout", async (
    SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        // Навмисно хибна навчальна довіра: ЛР 03 замінює заявлений id на verified principal.
        app.MapGet("/api/me", async (
    HttpContext context,
    UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(context.User);

            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                user.Id,
                user.UserName,
                user.DisplayName
            });
        }).RequireAuthorization();
        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }

    private static bool IsValidRegistration(RegistrationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.UserName) ||
            string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.DisplayName) ||
            string.IsNullOrEmpty(input.Password))
            return false;

        string userName = input.UserName.Trim();
        string email = input.Email.Trim();
        string displayName = input.DisplayName.Trim();

        if (!Regex.IsMatch(userName, @"^[a-zA-Z0-9_-]{3,32}$"))
            return false;

        if (email.Length > 254 ||
            !new EmailAddressAttribute().IsValid(email))
            return false;

        if (displayName.Length > 100)
            return false;

        return true;
    }
}

public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);
public sealed record RegistrationResponse(
    Guid Id,
    string UserName,
    string DisplayName);