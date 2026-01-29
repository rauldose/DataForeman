using System.Security.Cryptography;
using System.Text;

namespace DataForeman.Drivers;

/// <summary>
/// Utility class for generating deterministic tag IDs from tag paths.
/// Uses SHA256 to ensure consistent, collision-resistant IDs.
/// </summary>
public static class TagIdGenerator
{
    /// <summary>
    /// Generate a deterministic integer tag ID from a tag path.
    /// Uses SHA256 to create a consistent hash that won't vary across runs.
    /// </summary>
    /// <param name="tagPath">The tag path/address.</param>
    /// <param name="connectionId">Optional connection ID for namespace separation.</param>
    /// <returns>A deterministic integer ID for the tag.</returns>
    public static int GenerateTagId(string tagPath, Guid? connectionId = null)
    {
        var input = connectionId.HasValue 
            ? $"{connectionId.Value}:{tagPath}" 
            : tagPath;
        
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        
        // Use first 4 bytes to create an integer
        // Take absolute value to ensure positive number
        return Math.Abs(BitConverter.ToInt32(hashBytes, 0));
    }
    
    /// <summary>
    /// Generate a unique string key for a tag (useful for dictionary lookups).
    /// </summary>
    /// <param name="tagPath">The tag path/address.</param>
    /// <param name="connectionId">Connection ID.</param>
    /// <returns>A unique string key.</returns>
    public static string GenerateTagKey(string tagPath, Guid connectionId)
    {
        return $"{connectionId}:{tagPath}";
    }
}
