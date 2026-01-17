namespace DataForeman.Shared.DTOs;

public record ChartConfigDto(
    Guid Id,
    Guid? UserId,
    Guid? FolderId,
    string Name,
    string? Description,
    string ChartType,
    bool IsSystemChart,
    bool IsShared,
    string TimeMode,
    long? TimeDuration,
    long TimeOffset,
    bool LiveEnabled,
    bool ShowTimeBadge,
    DateTime? TimeFrom,
    DateTime? TimeTo,
    string? Options,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateChartRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    string? ChartType,
    bool? IsShared,
    string? TimeMode,
    long? TimeDuration,
    long? TimeOffset,
    bool? LiveEnabled,
    bool? ShowTimeBadge,
    DateTime? TimeFrom,
    DateTime? TimeTo,
    string? Options
);

public record UpdateChartRequest(
    string? Name,
    string? Description,
    Guid? FolderId,
    string? ChartType,
    bool? IsShared,
    string? TimeMode,
    long? TimeDuration,
    long? TimeOffset,
    bool? LiveEnabled,
    bool? ShowTimeBadge,
    DateTime? TimeFrom,
    DateTime? TimeTo,
    string? Options
);
