using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}

public interface IUserPermissionRepository
{
    Task<IEnumerable<UserPermission>> GetByUserIdAsync(Guid userId);
    Task<UserPermission?> GetAsync(Guid userId, string feature);
    Task<bool> CanAsync(Guid userId, string feature, string operation);
    Task SetPermissionsAsync(Guid userId, IEnumerable<UserPermission> permissions);
    Task DeleteAsync(Guid userId, string feature);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<RefreshToken> CreateAsync(RefreshToken token);
    Task RevokeAsync(string token);
    Task RevokeAllForUserAsync(Guid userId);
    Task CleanupExpiredAsync();
}
