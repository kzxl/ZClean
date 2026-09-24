namespace ZClean.Core.Contracts;

/// <summary>
/// Probes whether a file is actively opened or locked by running processes.
/// </summary>
public interface IFileLockDetector
{
    /// <summary>
    /// Attempts to open the file with exclusive write access to determine if it is locked.
    /// </summary>
    bool IsFileLocked(string filePath);
}
