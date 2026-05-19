namespace ChartEngine.Application.Constants;

public static class DatasetHubGroups
{
    /// <summary>
    /// Generate the SignalR group name for a specific dataset.
    /// Used by both API (to manage groups) and Infrastructure (to send events).
    /// </summary>
    public static string GetGroupName(Guid datasetId) => $"dataset-{datasetId:N}";
    public static string GetGroupName(string datasetId) => $"dataset-{datasetId}";
}
