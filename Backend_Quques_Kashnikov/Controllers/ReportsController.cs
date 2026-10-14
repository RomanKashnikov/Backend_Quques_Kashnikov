using Backend_Quques_Kashnikov.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Controllers;

[ApiController, Route("api/reports"), Authorize]
public class ReportsController(AppDbContext db) : ControllerBase
{
    // Очередь + записи + пользователи, с вычислением текущего места.
    [HttpGet("queues/{id:int}/participants")]
    public async Task<IActionResult> Participants(int id)
    {
        var queue = await db.Queues.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id);
        if (queue == null) return NotFound();
        var rows = await (from e in db.Entries.AsNoTracking()
                          join u in db.Users on e.AppUserId equals u.Id
                          where e.StudyQueueId == id orderby e.Sequence
                          select new { entryId = e.Id, userId = u.Id, u.FirstName, u.LastName, u.GroupName, e.JoinedAtUtc, e.Note }).ToListAsync();
        return Ok(new { queue.Id, queue.Subject, queue.Title, queue.Teacher,
            status = queue.GetStatus(DateTime.UtcNow), participants = rows.Select((r, i) => new { position = i + 1, participant = r }) });
    }
    // GroupJoin включает очереди, в которых пока нет записей.
    [HttpGet("queue-load")]
    public async Task<IActionResult> QueueLoad() => Ok(await db.Queues.AsNoTracking()
        .GroupJoin(db.Entries, q => q.Id, e => e.StudyQueueId, (q, entries) => new
        {
            q.Id, q.Subject, q.Title, q.GroupName, q.Capacity,
            participantCount = entries.Count(),
            availablePlaces = q.Capacity.HasValue ? q.Capacity.Value - entries.Count() : (int?)null
        }).ToListAsync());
    [HttpGet("users/{id:int}/schedule")]
    public async Task<IActionResult> UserSchedule(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        var schedule = await (from e in db.Entries.AsNoTracking()
                              join q in db.Queues on e.StudyQueueId equals q.Id
                              where e.AppUserId == id orderby q.StartsAtUtc
                              select new { queueId = q.Id, q.Title, q.Subject, q.Room, q.Teacher, q.StartsAtUtc, q.Lifecycle }).ToListAsync();
        return Ok(new { user.Id, user.FirstName, user.LastName, schedule });
    }
    [HttpGet("subjects")]
    public async Task<IActionResult> Subjects()
    {
        var rows = await db.Queues.AsNoTracking().Select(q => new
        { q.Subject, participantCount = db.Entries.Count(e => e.StudyQueueId == q.Id) }).ToListAsync();
        return Ok(rows.GroupBy(q => q.Subject).Select(g => new
        { subject = g.Key, queueCount = g.Count(), registrationCount = g.Sum(q => q.participantCount) }));
    }
}
