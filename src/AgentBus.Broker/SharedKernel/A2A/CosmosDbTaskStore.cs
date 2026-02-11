using A2A;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace AgentBus.Broker.SharedKernel.A2A;

/// <summary>
/// Cosmos DB implementation of A2A ITaskStore for task persistence.
/// </summary>
public sealed class CosmosDbTaskStore : ITaskStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbTaskStore> _logger;

    public CosmosDbTaskStore(CosmosClient cosmosClient, IConfiguration configuration, ILogger<CosmosDbTaskStore> logger)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "agentbus";
        var containerName = configuration["CosmosDb:TaskContainerName"] ?? "tasks";
        
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<TaskDocument>(
                taskId,
                new PartitionKey("tasks"),
                cancellationToken: cancellationToken);

            return response.Resource.Task;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var document = new TaskDocument
        {
            Id = task.Id,
            PartitionKey = "tasks",
            Task = task,
            UpdatedAt = DateTime.UtcNow
        };

        await _container.UpsertItemAsync(
            document,
            new PartitionKey(document.PartitionKey),
            cancellationToken: cancellationToken);

        _logger.LogDebug("Saved task {TaskId} to Cosmos DB", task.Id);
    }

    public async Task<bool> DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<TaskDocument>(
                taskId,
                new PartitionKey("tasks"),
                cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted task {TaskId} from Cosmos DB", taskId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task SetTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        return SaveTaskAsync(task, cancellationToken);
    }

    public async Task<AgentTaskStatus> UpdateStatusAsync(string taskId, TaskState state, AgentMessage? message, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"Task {taskId} not found");
        }

        task.Status = new AgentTaskStatus
        {
            State = state,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow
        };

        await SaveTaskAsync(task, cancellationToken);
        return task.Status;
    }

    public Task<TaskPushNotificationConfig?> GetPushNotificationAsync(string clientId, string taskId, CancellationToken cancellationToken = default)
    {
        // Not implemented for broker - push notifications not supported
        return Task.FromResult<TaskPushNotificationConfig?>(null);
    }

    public Task SetPushNotificationConfigAsync(TaskPushNotificationConfig config, CancellationToken cancellationToken = default)
    {
        // Not implemented for broker - push notifications not supported
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TaskPushNotificationConfig>> GetPushNotificationsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        // Not implemented for broker - push notifications not supported
        return Task.FromResult(Enumerable.Empty<TaskPushNotificationConfig>());
    }

    private sealed class TaskDocument
    {
        public string Id { get; set; } = string.Empty;
        public string PartitionKey { get; set; } = "tasks";
        public AgentTask Task { get; set; } = null!;
        public DateTime UpdatedAt { get; set; }
    }
}
