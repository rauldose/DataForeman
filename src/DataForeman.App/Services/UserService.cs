using System.Text.Json;
using System.Text.Json.Serialization;
using BCrypt.Net;

namespace DataForeman.App.Services;

/// <summary>
/// JSON file-based user service with permission management.
/// Stores users and permissions in config/users.json.
/// </summary>
public class UserService
{
    private readonly string _usersFilePath;
    private readonly ILogger<UserService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private UsersFile _usersFile = new();
    private readonly object _lock = new();

    public UserService(IConfiguration configuration, ILogger<UserService> logger)
    {
        var configDir = DataForeman.Shared.ConfigPathResolver.Resolve(
            configuration.GetValue<string>("ConfigDirectory"));
        _usersFilePath = Path.Combine(configDir, "users.json");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        LoadUsers();
        EnsureDefaultAdmin();
    }

    public IReadOnlyList<AppUser> Users
    {
        get { lock (_lock) return _usersFile.Users.AsReadOnly(); }
    }

    public AppUser? GetUserById(string id)
    {
        lock (_lock)
            return _usersFile.Users.FirstOrDefault(u => u.Id == id);
    }

    public AppUser? GetUserByEmail(string email)
    {
        lock (_lock)
            return _usersFile.Users.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public AppUser? ValidateCredentials(string email, string password)
    {
        var user = GetUserByEmail(email);
        if (user == null || !user.IsActive) return null;

        try
        {
            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password for {Email}", email);
        }
        return null;
    }

    public AppUser? CreateUser(string email, string password, string? displayName)
    {
        lock (_lock)
        {
            if (_usersFile.Users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return null;

            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return null;

            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                DisplayName = displayName ?? email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Permissions = GetDefaultPermissions()
            };

            _usersFile.Users.Add(user);
            SaveUsers();
            return user;
        }
    }

    public bool UpdateUser(string id, string? displayName, bool? isActive)
    {
        lock (_lock)
        {
            var user = _usersFile.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return false;

            if (displayName != null) user.DisplayName = displayName;
            if (isActive.HasValue) user.IsActive = isActive.Value;
            user.UpdatedAt = DateTime.UtcNow;

            SaveUsers();
            return true;
        }
    }

    public bool ChangePassword(string id, string currentPassword, string newPassword)
    {
        lock (_lock)
        {
            var user = _usersFile.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return false;

            if (newPassword.Length < 8) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            SaveUsers();
            return true;
        }
    }

    public bool DeleteUser(string id)
    {
        lock (_lock)
        {
            var user = _usersFile.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return false;

            _usersFile.Users.Remove(user);
            SaveUsers();
            return true;
        }
    }

    public List<FeaturePermission> GetUserPermissions(string userId)
    {
        lock (_lock)
        {
            var user = _usersFile.Users.FirstOrDefault(u => u.Id == userId);
            return user?.Permissions ?? GetDefaultPermissions();
        }
    }

    public bool UpdateUserPermissions(string userId, List<FeaturePermission> permissions)
    {
        lock (_lock)
        {
            var user = _usersFile.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.Permissions = permissions;
            user.UpdatedAt = DateTime.UtcNow;
            SaveUsers();
            return true;
        }
    }

    private void LoadUsers()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = File.ReadAllText(_usersFilePath);
                _usersFile = JsonSerializer.Deserialize<UsersFile>(json, _jsonOptions) ?? new UsersFile();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users from {Path}", _usersFilePath);
            _usersFile = new UsersFile();
        }
    }

    private void SaveUsers()
    {
        try
        {
            var dir = Path.GetDirectoryName(_usersFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_usersFile, _jsonOptions);
            File.WriteAllText(_usersFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save users to {Path}", _usersFilePath);
        }
    }

    private void EnsureDefaultAdmin()
    {
        lock (_lock)
        {
            if (_usersFile.Users.Count == 0)
            {
                _logger.LogInformation("Creating default admin user (admin@dataforeman.local)");
                var admin = new AppUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = "admin@dataforeman.local",
                    DisplayName = "Administrator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123!"),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Permissions = GetFullPermissions()
                };
                _usersFile.Users.Add(admin);
                SaveUsers();
            }
        }
    }

    private static List<FeaturePermission> GetDefaultPermissions() => new()
    {
        new() { Feature = "dashboards", CanRead = true },
        new() { Feature = "flows", CanRead = true },
        new() { Feature = "charts", CanRead = true },
        new() { Feature = "connectivity", CanRead = true },
        new() { Feature = "state_machines", CanRead = true },
        new() { Feature = "trends", CanRead = true },
        new() { Feature = "diagnostics", CanRead = true },
        new() { Feature = "users", CanRead = false },
    };

    private static List<FeaturePermission> GetFullPermissions() => new()
    {
        new() { Feature = "dashboards", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "flows", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "charts", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "connectivity", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "state_machines", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "trends", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "diagnostics", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
        new() { Feature = "users", CanCreate = true, CanRead = true, CanUpdate = true, CanDelete = true },
    };
}

/// <summary>
/// Application user model.
/// </summary>
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<FeaturePermission> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Feature-level permission (CRUD).
/// </summary>
public class FeaturePermission
{
    public string Feature { get; set; } = string.Empty;
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}

/// <summary>
/// Root structure for users.json file.
/// </summary>
public class UsersFile
{
    public List<AppUser> Users { get; set; } = new();
}
