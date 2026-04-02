using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace GoogleAdk.Core;

/// <summary>
/// A wrapper over DotNetEnv to facilitate easy loading and retrieval of environment variables.
/// </summary>
public static class AdkEnv
{
    /// <summary>
    /// Loads environment variables from a .env file.
    /// If no path is provided, it attempts to find the .env file in the directory containing the .csproj file,
    /// traversing upwards from the current directory. If that fails, it falls back to the default traversal behavior.
    /// </summary>
    /// <param name="path">Optional path to the .env file.</param>
    public static void Load(string? path = null)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            DotNetEnv.Env.Load(path);
            return;
        }

        // Traverse up to find a directory containing a .csproj file
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDir != null && !currentDir.GetFiles("*.csproj").Any())
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir != null)
        {
            var envPath = Path.Combine(currentDir.FullName, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
                return;
            }
        }

        // Fallback to DotNetEnv's TraversePath if .csproj isn't found or .env isn't next to it
        DotNetEnv.Env.TraversePath().Load();
    }

    /// <summary>
    /// Gets the value of an environment variable as a string.
    /// </summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="defaultValue">The fallback value if the environment variable is not found.</param>
    /// <returns>The value of the environment variable, or the default value if not found.</returns>
    public static string? GetEnv(string key, string? defaultValue = null)
    {
        return DotNetEnv.Env.GetString(key, defaultValue);
    }

    /// <summary>
    /// Sets the value of an environment variable.
    /// </summary>
    /// <param name="key">The name of the environment variable.</param>
    /// <param name="value">The value to set.</param>
    public static void SetEnv(string key, string? value)
    {
        System.Environment.SetEnvironmentVariable(key, value);
    }
}
