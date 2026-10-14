using System.ComponentModel.DataAnnotations;
using Backend_Quques_Kashnikov.Contracts;
using Backend_Quques_Kashnikov.Data;
using Backend_Quques_Kashnikov.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Controllers;

[ApiController, Route("api/users"), Authorize]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? group) => Ok(await db.Users.AsNoTracking()
        .Where(u => group == null || u.GroupName == group).OrderBy(u => u.LastName).ThenBy(u => u.Id).ToListAsync());
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        return user == null ? NotFound() : Ok(user);
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(UserRequest request)
    {
        var user = new AppUser();
        user.Update(request.PassNumber, request.FirstName, request.LastName, request.GroupName, request.Role);
        user.SetPassword(request.Password ?? throw new ValidationException("При создании требуется пароль."));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UserRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "Admin" && request.Role != "Admin" && await db.Users.CountAsync(u => u.Role == "Admin") == 1)
            return Conflict(new { message = "Нельзя лишить прав последнего администратора." });
        if (user.GroupName != request.GroupName?.Trim() && await db.Entries.AnyAsync(e => e.AppUserId == id))
            return Conflict(new { message = "Перед изменением группы удалите записи пользователя из очередей." });
        user.Update(request.PassNumber, request.FirstName, request.LastName, request.GroupName, request.Role);
        if (request.Password != null) user.SetPassword(request.Password);
        await db.SaveChangesAsync();
        return Ok(user);
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "Admin" && await db.Users.CountAsync(u => u.Role == "Admin") == 1)
            return Conflict(new { message = "Нельзя удалить последнего администратора." });
        if (await db.Entries.AnyAsync(e => e.AppUserId == id))
            return Conflict(new { message = "Сначала удалите записи пользователя из очередей." });
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
