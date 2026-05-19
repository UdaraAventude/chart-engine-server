namespace ChartEngine.Application.Hubs;

using ChartEngine.Application.Constants;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// SignalR hub for real-time dataset processing updates.
/// Clients connect here and join groups to listen for dataset-specific events.
/// </summary>
public sealed class DatasetHub : Hub
{
    /// <summary>
    /// Client calls this to subscribe to updates for a specific dataset.
    /// Joins the SignalR group so server can target messages at all watchers of this dataset.
    /// </summary>
    public async Task JoinDatasetGroup(string datasetId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, DatasetHubGroups.GetGroupName(datasetId));
    }

    /// <summary>
    /// Client calls this to stop listening for a dataset's updates.
    /// </summary>
    public async Task LeaveDatasetGroup(string datasetId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DatasetHubGroups.GetGroupName(datasetId));
    }
}
