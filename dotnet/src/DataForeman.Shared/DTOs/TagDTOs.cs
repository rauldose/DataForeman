namespace DataForeman.Shared.DTOs;

public record TagMetadataDto(
    int TagId,
    Guid ConnectionId,
    string DriverType,
    string TagPath,
    string? TagName,
    bool IsSubscribed,
    int PollGroupId,
    string? DataType,
    int? UnitId,
    string? Description,
    string? Metadata,
    bool OnChangeEnabled,
    float OnChangeDeadband,
    string OnChangeDeadbandType,
    int OnChangeHeartbeatMs,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateTagRequest(
    Guid ConnectionId,
    string DriverType,
    string TagPath,
    string? TagName,
    bool IsSubscribed,
    int? PollGroupId,
    string? DataType,
    int? UnitId,
    string? Description,
    bool? OnChangeEnabled,
    float? OnChangeDeadband,
    string? OnChangeDeadbandType,
    int? OnChangeHeartbeatMs
);

public record UpdateTagRequest(
    string? TagName,
    bool? IsSubscribed,
    int? PollGroupId,
    int? UnitId,
    string? Description,
    bool? OnChangeEnabled,
    float? OnChangeDeadband,
    string? OnChangeDeadbandType,
    int? OnChangeHeartbeatMs
);
