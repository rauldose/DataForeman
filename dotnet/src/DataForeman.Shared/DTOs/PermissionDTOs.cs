namespace DataForeman.Shared.DTOs;

public record PermissionDto(string Feature, bool CanCreate, bool CanRead, bool CanUpdate, bool CanDelete);

public record UpdatePermissionsRequest(List<PermissionDto> Permissions);
