using Backend_Quques_Kashnikov.Contracts;
using Backend_Quques_Kashnikov.Data;
using Backend_Quques_Kashnikov.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Controllers;

[ApiController, Route("api/queues"), Authorize]
public class QueuesController(AppDbContext db) : ControllerBase
{
    private static object View(StudyQueue queue) => new { queue, status = queue.GetStatus(DateTime.UtcNow) };
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? group, [FromQuery] string? subject)
    {
        var queues = await db.Queues.AsNoTracking().Where(q => (group == null || q.GroupName == group)
            && (subject == null || q.Subject.Contains(subject))).OrderBy(q => q.StartsAtUtc).ToListAsync();
        return Ok(queues.Select(View));
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var queue = await db.Queues.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id);
        return queue == null ? NotFound() : Ok(View(queue));
    }
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(QueueRequest request)
    {
        var queue = new StudyQueue();
        Apply(queue, request, 0);
        db.Queues.Add(queue);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = queue.Id }, View(queue));
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, QueueRequest request)
    {
        var queue = await db.Queues.FindAsync(id);
        if (queue == null) return NotFound();
        var count = await db.Entries.CountAsync(e => e.StudyQueueId == id);
        if (count > 0 && queue.GroupName != request.GroupName.Trim())
            return Conflict(new { message = "Нельзя менять группу, пока в очереди есть участники." });
        Apply(queue, request, count);
        await db.SaveChangesAsync();
        return Ok(View(queue));
    }
    [HttpPatch("{id:int}/state"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeState(int id, StateRequest request)
    {
        var queue = await db.Queues.FindAsync(id);
        if (queue == null) return NotFound();
        queue.Finish(request.State);
        await db.SaveChangesAsync();
        return Ok(View(queue));
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var queue = await db.Queues.FindAsync(id);
        if (queue == null) return NotFound();
        db.Queues.Remove(queue); // Записи участников удаляются каскадно.
        await db.SaveChangesAsync();
        return NoContent();
    }
    private static void Apply(StudyQueue q, QueueRequest r, int count) => q.Update(r.Subject, r.Title,
        r.Teacher, r.GroupName, r.Room, r.Note, r.OpensAt, r.ClosesAt, r.StartsAt, r.Capacity, count, DateTime.UtcNow);
}
