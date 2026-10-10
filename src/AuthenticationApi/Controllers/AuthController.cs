using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using AuthenticationApi.Data;
using AuthenticationApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace AuthenticationApi.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email) ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length < 12)
            return BadRequest("Enter a valid email and a password with at least 12 characters.");
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("An account with this email already exists.");
        var user = new AppUser { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new { user.Id, user.Email, user.CreatedAtUtc });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { message = "Invalid email or password." });
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });
        var keyText = configuration["Jwt:Key"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(keyText) || Encoding.UTF8.GetByteCount(keyText) < 32 ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            return Problem("JWT configuration is missing or invalid. Configure Jwt:Key, Jwt:Issuer and Jwt:Audience.");
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: credentials);
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresInSeconds = 1800 });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var idValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var id)) return Unauthorized();
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Email, u.CreatedAtUtc }).SingleOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(user);
    }

    private static bool IsValidEmail(string email) => email.Length <= 320 &&
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
}