namespace DataForeman.Core.Entities;

public class Dashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsShared { get; set; }
    public bool IsDeleted { get; set; }
    public string Layout { get; set; } = "{}"; // JSON string for layout configuration
    public string Options { get; set; } = "{}"; // JSON string for additional options
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
    public virtual DashboardFolder? Folder { get; set; }
}

public class DashboardFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentFolderId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
    public virtual DashboardFolder? ParentFolder { get; set; }
    public virtual ICollection<DashboardFolder> ChildFolders { get; set; } = new List<DashboardFolder>();
    public virtual ICollection<Dashboard> Dashboards { get; set; } = new List<Dashboard>();
}
