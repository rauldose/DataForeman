namespace DataForeman.Shared.DTOs;

public record FlowDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? OwnerUserId,
    Guid? FolderId,
    bool Deployed,
    bool Shared,
    bool TestMode,
    string ExecutionMode,
    int ScanRateMs,
    bool LogsEnabled,
    int LogsRetentionDays,
    string? Definition,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateFlowRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    bool? Shared,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? LogsEnabled,
    int? LogsRetentionDays,
    string? Definition
);

public record UpdateFlowRequest(
    string? Name,
    string? Description,
    Guid? FolderId,
    bool? Shared,
    bool? Deployed,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? LogsEnabled,
    int? LogsRetentionDays,
    string? Definition
);
