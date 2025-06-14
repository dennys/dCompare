namespace dCompare.FileOperation.Models;

/// <summary>
/// Represents the result of a file operation batch.
/// </summary>
/// <param name="SuccessCount">The number of successfully processed items.</param>
/// <param name="FailCount">The number of failed items.</param>
/// <param name="FailedItems">A list of items that failed to process.</param>
/// <param name="ExecutionTime">The total time spent on the operation.</param>
public record OperationResult(int SuccessCount, int FailCount, List<string> FailedItems, TimeSpan ExecutionTime);