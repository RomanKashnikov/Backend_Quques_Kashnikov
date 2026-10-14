using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Backend_Quques_Kashnikov.Models;

public class AppUser
{
    public int Id { get; set; }
    public string PassNumber { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string? GroupName { get; private set; }
    public string Role { get; private set; } = "User";
    [JsonIgnore] public string PasswordHash { get; private set; } = "";
    [JsonIgnore] public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString();

    public void Update(string passNumber, string firstName, string lastName, string? groupName, string role)
    {
        if (role is not ("Admin" or "User")) throw new ValidationException("Роль: Admin или User.");
        PassNumber = Required(passNumber, 30);
        FirstName = Required(firstName, 80);
        LastName = Required(lastName, 80);
        GroupName = string.IsNullOrWhiteSpace(groupName) ? null : Required(groupName, 40);
        Role = role;
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public void SetPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
            throw new ValidationException("Пароль должен содержать от 8 до 128 символов.");
        PasswordHash = new PasswordHasher<AppUser>().HashPassword(this, password);
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public bool CheckPassword(string password) =>
        new PasswordHasher<AppUser>().VerifyHashedPassword(this, PasswordHash, password) != PasswordVerificationResult.Failed;

    public static string Required(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ValidationException($"Значение обязательно и не должно превышать {max} символов.");
        return value.Trim();
    }
}
