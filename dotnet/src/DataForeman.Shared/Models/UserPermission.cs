namespace DataForeman.Shared.Models;

public class UserPermission
{
    public Guid UserId { get; set; }
    public required string Feature { get; set; }
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
}
