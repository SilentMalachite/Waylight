using System.Security.Claims;
using LocalLlmAssistant.Data;
using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalLlmAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, ILogger<AuthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public record AuthRequest(string Username, string Password);

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AuthRequest request)
    {
        var validationError = ValidateCredentials(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            return BadRequest(new { error = validationError });
        }

        var normalized = request.Username.Trim().ToLowerInvariant();
        var exists = await _db.UserAccounts.AnyAsync(u => u.UserName == normalized);
        if (exists)
        {
            return Conflict(new { error = "Username already exists" });
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        var account = new UserAccount
        {
            UserName = normalized,
            PasswordHash = hash,
            PasswordSalt = salt
        };

        _db.UserAccounts.Add(account);
        await _db.SaveChangesAsync();

        await SignInAsync(account.UserName);
        _logger.LogInformation("User registered: {UserName}", account.UserName);

        return Ok(new { username = account.UserName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        var validationError = ValidateCredentials(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            return BadRequest(new { error = validationError });
        }

        var normalized = request.Username.Trim().ToLowerInvariant();
        var account = await _db.UserAccounts.FirstOrDefaultAsync(u => u.UserName == normalized);
        if (account == null)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        var valid = PasswordHasher.Verify(request.Password, account.PasswordHash, account.PasswordSalt);
        if (!valid)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        await SignInAsync(account.UserName);
        _logger.LogInformation("User logged in: {UserName}", account.UserName);

        return Ok(new { username = account.UserName });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { status = "signed_out" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        return Ok(new { username });
    }

    private string? ValidateCredentials(AuthRequest request)
    {
        if (request is null) return "Invalid payload";
        if (string.IsNullOrWhiteSpace(request.Username)) return "Username is required";
        if (string.IsNullOrWhiteSpace(request.Password)) return "Password is required";
        if (request.Username.Length < 3 || request.Username.Length > 32) return "Username must be 3-32 characters";
        if (request.Password.Length < 8 || request.Password.Length > 128) return "Password must be 8-128 characters";
        return null;
    }

    private async Task SignInAsync(string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}

