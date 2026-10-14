using Backend_Quques_Kashnikov.Contracts;
using Backend_Quques_Kashnikov.Data;
using Backend_Quques_Kashnikov.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Controllers;

[ApiController, Route("api/entries"), Authorize]
public class EntriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? queueId) => Ok(await db.Entries.AsNoTracking()
        .Where(e => queueId == null || e.StudyQueueId == queueId).OrderBy(e => e.StudyQueueId).ThenBy(e => e.Sequence).ToListAsync());
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var entry = await db.Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);
        return entry == null ? NotFound() : Ok(entry);
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(EntryRequest request)
    {
        var queue = await db.Queues.FindAsync(request.QueueId);
        var user = await db.Users.FindAsync(request.UserId);
        if (queue == null || user == null) return NotFound(new { message = "Очередь или пользователь не найдены." });
        if (user.GroupName != queue.GroupName) return Conflict(new { message = "Группа пользователя не соответствует очереди." });
        if (await db.Entries.AnyAsync(e => e.StudyQueueId == queue.Id && e.AppUserId == user.Id))
            return Conflict(new { message = "Пользователь уже записан в эту очередь." });
        var now = DateTime.UtcNow;
        queue.EnsureCanJoin(now, await db.Entries.CountAsync(e => e.StudyQueueId == queue.Id));
        var sequence = (await db.Entries.Where(e => e.StudyQueueId == queue.Id).MaxAsync(e => (long?)e.Sequence) ?? 0) + 1;
        var entry = new QueueEntry(queue.Id, user.Id, sequence, now, request.Note);
        db.Entries.Add(entry);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, NoteRequest request)
    {
        var entry = await db.Entries.FindAsync(id);
        if (entry == null) return NotFound();
        entry.UpdateNote(request.Note);
        await db.SaveChangesAsync();
        return Ok(entry);
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await db.Entries.FindAsync(id);
        if (entry == null) return NotFound();
        db.Entries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
