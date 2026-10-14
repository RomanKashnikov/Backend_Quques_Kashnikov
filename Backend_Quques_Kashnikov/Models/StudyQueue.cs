using System.ComponentModel.DataAnnotations;

namespace Backend_Quques_Kashnikov.Models;

public class StudyQueue
{
    public int Id { get; set; }
    public string Subject { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Teacher { get; private set; } = "";
    public string GroupName { get; private set; } = "";
    public string Room { get; private set; } = "";
    public string? Note { get; private set; }
    public DateTime OpensAtUtc { get; private set; }
    public DateTime? ClosesAtUtc { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public int? Capacity { get; private set; }
    public string Lifecycle { get; private set; } = "Active";

    public void Update(string subject, string title, string teacher, string groupName, string room,
        string? note, DateTimeOffset opensAt, DateTimeOffset? closesAt, DateTimeOffset startsAt,
        int? capacity, int participantCount, DateTime nowUtc)
    {
        if (Lifecycle != "Active") throw new InvalidOperationException("Завершённую или отменённую очередь нельзя редактировать.");
        if (Id != 0 && nowUtc >= OpensAtUtc && opensAt.UtcDateTime != OpensAtUtc)
            throw new InvalidOperationException("Время открытия уже открывшейся очереди менять нельзя.");
        if (opensAt == default || startsAt == default || startsAt < opensAt || closesAt <= opensAt || closesAt > startsAt)
            throw new ValidationException("Проверьте даты: открытие < закрытие <= проведение; открытие <= проведение.");
        if (capacity is <= 0 || capacity < participantCount)
            throw new ValidationException("Лимит должен быть положительным и не меньше числа участников.");
        Subject = AppUser.Required(subject, 150);
        Title = AppUser.Required(title, 200);
        Teacher = AppUser.Required(teacher, 160);
        GroupName = AppUser.Required(groupName, 40);
        Room = AppUser.Required(room, 60);
        if (note?.Length > 2000) throw new ValidationException("Примечание: максимум 2000 символов.");
        Note = note?.Trim();
        OpensAtUtc = opensAt.UtcDateTime;
        ClosesAtUtc = closesAt?.UtcDateTime;
        StartsAtUtc = startsAt.UtcDateTime;
        Capacity = capacity;
    }

    public string GetStatus(DateTime nowUtc) => Lifecycle != "Active" ? Lifecycle :
        nowUtc < OpensAtUtc ? "Scheduled" :
        ClosesAtUtc.HasValue && nowUtc >= ClosesAtUtc.Value ? "Closed" : "Open";

    public void EnsureCanJoin(DateTime nowUtc, int count)
    {
        if (GetStatus(nowUtc) != "Open") throw new InvalidOperationException("Запись в очередь сейчас недоступна.");
        if (Capacity.HasValue && count >= Capacity.Value) throw new InvalidOperationException("Достигнут лимит участников.");
    }

    public void Finish(string state)
    {
        if (state is not ("Completed" or "Cancelled")) throw new ValidationException("Состояние: Completed или Cancelled.");
        if (Lifecycle != "Active") throw new InvalidOperationException("Очередь уже завершена или отменена.");
        Lifecycle = state;
    }
}
