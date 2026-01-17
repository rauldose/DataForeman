namespace DataForeman.Shared.DTOs;

public record ConnectionDto(
    Guid Id,
    string Name,
    string Type,
    bool Enabled,
    string? ConfigData,
    bool IsSystemConnection,
    int? MaxTagsPerGroup,
    int? MaxConcurrentConnections,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateConnectionRequest(
    string Name,
    string Type,
    bool Enabled,
    string? ConfigData,
    int? MaxTagsPerGroup,
    int? MaxConcurrentConnections
);

public record UpdateConnectionRequest(
    string? Name,
    string? Type,
    bool? Enabled,
    string? ConfigData,
    int? MaxTagsPerGroup,
    int? MaxConcurrentConnections
);
