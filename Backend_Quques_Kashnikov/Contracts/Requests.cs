using System.ComponentModel.DataAnnotations;

namespace Backend_Quques_Kashnikov.Contracts;

public record LoginRequest([Required, StringLength(30)] string PassNumber,
    [Required, StringLength(128)] string Password);
public record UserRequest([Required] string PassNumber, [Required] string FirstName,
    [Required] string LastName, string? GroupName, [Required] string Role, string? Password);
public record QueueRequest([Required] string Subject, [Required] string Title,
    [Required] string Teacher, [Required] string GroupName, [Required] string Room,
    string? Note, DateTimeOffset OpensAt, DateTimeOffset? ClosesAt, DateTimeOffset StartsAt, int? Capacity);
public record EntryRequest([Range(1, int.MaxValue)] int QueueId,
    [Range(1, int.MaxValue)] int UserId, string? Note);
public record NoteRequest(string? Note);
public record StateRequest([Required] string State);
