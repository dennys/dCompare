namespace dCompare.FileOperation.Models;

/// <summary>
/// Represents the progress of a file operation.
/// </summary>
/// <param name="Percentage">The completion percentage (0-100).</param>
/// <param name="CurrentItem">The path of the file or directory currently being processed.</param>
public record OperationProgress(int Percentage, string CurrentItem);