using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PbesApi.Data;
using PbesApi.Models;
using PbesApi.Services;

namespace PbesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<Officer> _passwordHasher;

    public AuthController(
        ApplicationDbContext dbContext,
        ITokenService tokenService,
        IPasswordHasher<Officer> passwordHasher)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceNumberOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Service number/email and password are required.");
        }

        var lookup = request.ServiceNumberOrEmail.Trim().ToLowerInvariant();

        var officer = await _dbContext.Officers
            .FirstOrDefaultAsync(o =>
                o.ServiceNumber.ToLower() == lookup ||
                o.Email.ToLower() == lookup);

        if (officer is null)
        {
            return Unauthorized();
        }

        var verification = _passwordHasher.VerifyHashedPassword(officer, officer.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var token = _tokenService.GenerateToken(officer);
        return Ok(new LoginResponse(token, officer.Id, officer.Role));
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceNumberOrEmail))
        {
            return BadRequest("Service number/email is required.");
        }

        // TODO: Implement actual password reset flow (email/SMS + token).
        return Ok(new { message = "Password reset request accepted." });
    }
}

public record LoginRequest(string ServiceNumberOrEmail, string Password);

public record LoginResponse(string Token, Guid OfficerId, string Role);

public record ForgotPasswordRequest(string ServiceNumberOrEmail);
