namespace DataForeman.Shared.DTOs;

public record DashboardDto(
    Guid Id,
    Guid UserId,
    Guid? FolderId,
    string Name,
    string? Description,
    bool IsShared,
    string? Layout,
    string? Options,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateDashboardRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    bool? IsShared,
    string? Layout,
    string? Options
);

public record UpdateDashboardRequest(
    string? Name,
    string? Description,
    Guid? FolderId,
    bool? IsShared,
    string? Layout,
    string? Options
);

public record DashboardFolderDto(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    Guid? ParentFolderId,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateDashboardFolderRequest(
    string Name,
    string? Description,
    Guid? ParentFolderId,
    int? SortOrder
);
