namespace DataForeman.Shared.Models;

public class PollGroup
{
    public int GroupId { get; set; }
    public required string Name { get; set; }
    public int PollRateMs { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<TagMetadata>? Tags { get; set; }
}
