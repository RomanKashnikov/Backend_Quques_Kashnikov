using System.Security.Claims;
using Backend_Quques_Kashnikov.Contracts;
using Backend_Quques_Kashnikov.Data;
using Backend_Quques_Kashnikov.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var number = request.PassNumber.Trim();
        var user = await db.Users.SingleOrDefaultAsync(u => u.PassNumber == number);
        if (user == null || !user.CheckPassword(request.Password))
            return Unauthorized(new { message = "Неверный номер пропуска или пароль." });
        return Ok(tokens.Issue(user));
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me() => Ok(await db.Users.AsNoTracking()
        .SingleAsync(u => u.Id == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
}
