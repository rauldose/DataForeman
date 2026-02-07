using System.Net;

namespace DataForeman.Shared.Models;

public class Session
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public Guid Jti { get; set; }
    public required string RefreshHash { get; set; }
    public string? UserAgent { get; set; }
    public IPAddress? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByJti { get; set; }
    public DateTime? LastActivityAt { get; set; }
    
    public User? User { get; set; }
}
