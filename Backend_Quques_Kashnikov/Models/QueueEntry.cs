using System.ComponentModel.DataAnnotations;

namespace Backend_Quques_Kashnikov.Models;

public class QueueEntry
{
    public int Id { get; set; }
    public int StudyQueueId { get; private set; }
    public int AppUserId { get; private set; }
    public long Sequence { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public string? Note { get; private set; }

    private QueueEntry() { }

    public QueueEntry(int queueId, int userId, long sequence, DateTime nowUtc, string? note)
    {
        if (queueId <= 0 || userId <= 0 || sequence <= 0) throw new ValidationException("Некорректная запись в очередь.");
        StudyQueueId = queueId;
        AppUserId = userId;
        Sequence = sequence;
        JoinedAtUtc = nowUtc;
        UpdateNote(note);
    }

    public void UpdateNote(string? note)
    {
        if (note?.Length > 1000) throw new ValidationException("Примечание участника: максимум 1000 символов.");
        Note = note?.Trim();
    }
}
