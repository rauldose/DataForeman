namespace DataForeman.Auth;

/// <summary>
/// Service for password hashing and verification.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Hash a password.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verify a password against a hash.
    /// </summary>
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// BCrypt-based password service.
/// </summary>
public class PasswordService : IPasswordService
{
    /// <inheritdoc />
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
