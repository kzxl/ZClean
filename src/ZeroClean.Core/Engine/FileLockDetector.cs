using ZeroClean.Core.Contracts;

namespace ZeroClean.Core.Engine;

/// <summary>
/// Probes for active write locks and sharing violations on candidate files.
/// </summary>
public class FileLockDetector : IFileLockDetector
{
    public bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return false;
        }
        catch (IOException)
        {
            // File is locked or in use by another process
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only, access denied, or system protection
            return true;
        }
        catch
        {
            return true;
        }
    }
}
