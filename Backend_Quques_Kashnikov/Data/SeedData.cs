using Backend_Quques_Kashnikov.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;
        var admin = new AppUser();
        admin.Update("100001", "Администратор", "Системы", null, "Admin");
        admin.SetPassword("Admin123!");
        var user = new AppUser();
        user.Update("200001", "Иван", "Иванов", "ИВТ-21", "User");
        user.SetPassword("User123!");
        db.Users.AddRange(admin, user);
        var queue = new StudyQueue();
        var now = DateTimeOffset.UtcNow;
        queue.Update("Программирование", "Защита лабораторной № 1", "Петров Пётр Петрович", "ИВТ-21", "305",
            "Подготовить отчёт и коллекцию Postman.", now.AddHours(-1), null, now.AddDays(1), 20, 0, now.UtcDateTime);
        db.Queues.Add(queue);
        await db.SaveChangesAsync();
    }
}
