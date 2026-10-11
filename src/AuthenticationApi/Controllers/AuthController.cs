using System.Security.Claims;
using System.Text.RegularExpressions;
using AuthenticationApi.Data;
using AuthenticationApi.Models;
using AuthenticationApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthDbContext db, TokenService tokens) : ControllerBase
{
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email) ||
            string.IsNullOrEmpty(request.Password) ||
            request.Password.Length is < 12 or > 128)
            return BadRequest("Use a valid email and a password of 12 to 128 characters.");

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("An account with this email already exists.");

        var user = new AppUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new
        {
            user.Id,
            user.Email,
            user.CreatedAtUtc
        });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var (rawToken, record) = tokens.CreateRefreshToken(user);
        db.RefreshTokens.Add(record);
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            accessToken = tokens.CreateAccessToken(user),
            refreshToken = rawToken,
            tokenType = "Bearer",
            expiresInSeconds = 1800,
            refreshTokenExpiresAtUtc = record.ExpiresAtUtc
        });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 1024)
            return Unauthorized();

        var hash = TokenService.HashRefreshToken(request.RefreshToken);

        // Serialize rotation so a refresh token is usable only once.
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var oldToken = await db.RefreshTokens.Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (oldToken is null || oldToken.RevokedAtUtc is not null ||
            oldToken.ExpiresAtUtc <= DateTime.UtcNow)
            return Unauthorized();

        oldToken.RevokedAtUtc = DateTime.UtcNow;
        var (rawToken, record) = tokens.CreateRefreshToken(oldToken.User);
        db.RefreshTokens.Add(record);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Ok(new
        {
            accessToken = tokens.CreateAccessToken(oldToken.User),
            refreshToken = rawToken,
            tokenType = "Bearer",
            expiresInSeconds = 1800,
            refreshTokenExpiresAtUtc = record.ExpiresAtUtc
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 1024)
            return NoContent();

        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var record = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (record is not null && record.RevokedAtUtc is null)
        {
            record.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var idValue = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var id)) return Unauthorized();

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Email, u.CreatedAtUtc })
            .SingleOrDefaultAsync(ct);

        return user is null ? NotFound() : Ok(user);
    }

    private static bool IsValidEmail(string email) =>
        email.Length <= 320 && Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record RefreshRequest(string RefreshToken);
}
