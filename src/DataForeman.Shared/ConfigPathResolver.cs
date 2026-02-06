namespace DataForeman.Shared;

/// <summary>
/// Resolves the shared configuration directory used by both App and Engine.
/// Search order:
/// 1. DATAFOREMAN_CONFIG_DIR environment variable (absolute path)
/// 2. If the configured path is absolute and exists, use it
/// 3. Walk up from the current working directory looking for the solution root,
///    then use {solutionRoot}/config
/// 4. Walk up from the entry assembly location doing the same
/// 5. Fall back to {CWD}/config
/// </summary>
public static class ConfigPathResolver
{
    private const string EnvVarName = "DATAFOREMAN_CONFIG_DIR";
    private static readonly string[] SolutionFileNames = { "DataForeman.slnx", "DataForeman.sln" };
    private const string ConfigDirName = "config";
    // A known file that proves the config directory has real config (not an empty auto-created one)
    private const string MarkerFileName = "connections.json";

    /// <summary>
    /// Resolves the configuration directory path. Both the App and Engine
    /// should call this with their appsettings "ConfigDirectory" value.
    /// </summary>
    public static string Resolve(string? configuredPath)
    {
        // 1. Environment variable takes highest priority (for deployment scenarios)
        var envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            return Path.GetFullPath(envPath);

        // 2. If the configured path is absolute and contains config files, use it
        if (!string.IsNullOrWhiteSpace(configuredPath) && Path.IsPathRooted(configuredPath))
        {
            if (Directory.Exists(configuredPath) && HasConfigFiles(configuredPath))
                return configuredPath;
        }

        // 3. Search upward from CWD for the solution root
        var fromCwd = FindConfigDirFromAncestor(Directory.GetCurrentDirectory());
        if (fromCwd != null)
            return fromCwd;

        // 4. Search upward from the entry assembly location
        var entryAsm = System.Reflection.Assembly.GetEntryAssembly();
        if (entryAsm?.Location is { Length: > 0 } asmLocation)
        {
            var asmDir = Path.GetDirectoryName(asmLocation);
            if (asmDir != null)
            {
                var fromAsm = FindConfigDirFromAncestor(asmDir);
                if (fromAsm != null)
                    return fromAsm;
            }
        }

        // 5. Fall back: use the configured relative path resolved against CWD
        var fallback = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.Combine(Directory.GetCurrentDirectory(), ConfigDirName);

        return fallback;
    }

    /// <summary>
    /// Walks up from <paramref name="startDir"/> looking for either:
    /// - A directory containing DataForeman.sln (solution root), then returns {root}/config
    /// - A config/ sibling directory that contains known config files
    /// </summary>
    private static string? FindConfigDirFromAncestor(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            // Check if this directory contains a solution file
            var hasSln = SolutionFileNames.Any(name => File.Exists(Path.Combine(dir.FullName, name)));
            if (hasSln)
            {
                var configDir = Path.Combine(dir.FullName, ConfigDirName);
                if (Directory.Exists(configDir) && HasConfigFiles(configDir))
                    return configDir;
            }

            // Also check if there's a config/ subdirectory with real config files
            var candidateConfig = Path.Combine(dir.FullName, ConfigDirName);
            if (Directory.Exists(candidateConfig) && HasConfigFiles(candidateConfig))
                return candidateConfig;

            dir = dir.Parent;
        }

        return null;
    }

    private static bool HasConfigFiles(string dirPath)
    {
        return File.Exists(Path.Combine(dirPath, MarkerFileName));
    }
}
