namespace DataForeman.Shared.Models;

public class Dashboard
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsShared { get; set; }
    public bool IsDeleted { get; set; }
    public string Layout { get; set; } = "{}";
    public string Options { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
    public DashboardFolder? Folder { get; set; }
}
