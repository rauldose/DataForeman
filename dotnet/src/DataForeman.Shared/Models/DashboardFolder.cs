namespace DataForeman.Shared.Models;

public class DashboardFolder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentFolderId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
    public DashboardFolder? ParentFolder { get; set; }
    public ICollection<DashboardFolder>? ChildFolders { get; set; }
    public ICollection<Dashboard>? Dashboards { get; set; }
}
