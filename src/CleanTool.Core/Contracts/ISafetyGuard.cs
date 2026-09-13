using CleanTool.Core.Models;

namespace CleanTool.Core.Contracts;

/// <summary>
/// Defensive barrier preventing critical system files, root directories,
/// and currently active user-protected assets from deletion.
/// </summary>
public interface ISafetyGuard
{
    /// <summary>
    /// Checks whether a path falls inside system-critical boundaries (e.g. System32, Program Files).
    /// </summary>
    bool IsProtectedPath(string path);

    /// <summary>
    /// Determines whether a file meets age, exclusion, and whitelist policies.
    /// </summary>
    bool IsEligibleForDeletion(string filePath, CleanOptions options);

    /// <summary>
    /// Validates an entire directory target before recursive descent.
    /// </summary>
    bool IsSafeTargetDirectory(string directoryPath);
}
