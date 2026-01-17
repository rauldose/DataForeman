namespace DataForeman.Shared.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<UserRole>? UserRoles { get; set; }
    public ICollection<UserPermission>? UserPermissions { get; set; }
    public ICollection<Session>? Sessions { get; set; }
    public ICollection<AuthIdentity>? AuthIdentities { get; set; }
}
