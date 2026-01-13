using System.IO;

namespace Hft.Core;

/// <summary>
/// Abstraction for file system operations.
/// Enables testability and atomic write operations.
/// GRANDMASTER: Use for all file I/O to ensure safe, testable operations.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>True if the directory exists or was created successfully.</returns>
    bool EnsureDirectoryExists(string path);

    /// <summary>
    /// Opens a file for atomic write operations.
    /// Writes to a temporary file and renames on close to prevent corruption.
    /// </summary>
    /// <param name="path">The target file path.</param>
    /// <returns>A stream for writing.</returns>
    Stream OpenWriteAtomic(string path);

    /// <summary>
    /// Opens a file for reading with appropriate sharing mode.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>A stream for reading.</returns>
    Stream OpenRead(string path);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>True if the file exists.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>True if the directory exists.</returns>
    bool DirectoryExists(string path);
}
