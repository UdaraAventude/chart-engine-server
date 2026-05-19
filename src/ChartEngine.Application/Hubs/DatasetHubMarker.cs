namespace ChartEngine.Application.Hubs;

/// <summary>
/// Marker hub class for dependency injection purposes.
/// The actual DatasetHub lives in the API layer.
/// This allows Infrastructure to use IHubContext<DatasetHubMarker>
/// without needing to reference the API project.
/// </summary>
public sealed class DatasetHubMarker
{
}
