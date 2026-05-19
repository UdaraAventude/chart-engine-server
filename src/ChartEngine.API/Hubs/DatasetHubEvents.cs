namespace ChartEngine.API.Hubs;

public static class DatasetHubEvents
{
    /// <summary>
    /// Sent periodically during processing to show progress.
    /// Payload: { datasetId, percent, message }
    /// </summary>
    public const string Progress = "DatasetProgress";

    /// <summary>
    /// Sent when processing completes successfully.
    /// Payload: { datasetId, totalRows }
    /// </summary>
    public const string Ready = "DatasetReady";

    /// <summary>
    /// Sent when processing fails.
    /// Payload: { datasetId, error }
    /// </summary>
    public const string Failed = "DatasetFailed";
}
