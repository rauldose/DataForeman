using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IFlowRepository
{
    Task<Flow?> GetByIdAsync(Guid id);
    Task<Flow?> GetVisibleAsync(Guid id, Guid userId);
    Task<IEnumerable<Flow>> GetByUserIdAsync(Guid userId, string scope = "all", int limit = 50, int offset = 0);
    Task<IEnumerable<Flow>> GetDeployedFlowsAsync();
    Task<Flow> CreateAsync(Flow flow);
    Task UpdateAsync(Flow flow);
    Task DeleteAsync(Guid id);
    Task<bool> IsOwnerAsync(Guid flowId, Guid userId);
    Task SetDeployedAsync(Guid flowId, bool deployed);
    Task SetTestModeAsync(Guid flowId, bool testMode, bool disableWrites = false, bool autoExit = false, int autoExitMinutes = 5);
}

public interface IFlowFolderRepository
{
    Task<FlowFolder?> GetByIdAsync(Guid id);
    Task<IEnumerable<FlowFolder>> GetByUserIdAsync(Guid userId);
    Task<FlowFolder> CreateAsync(FlowFolder folder);
    Task UpdateAsync(FlowFolder folder);
    Task DeleteAsync(Guid id);
}

public interface IFlowExecutionRepository
{
    Task<FlowExecution?> GetByIdAsync(Guid id);
    Task<IEnumerable<FlowExecution>> GetByFlowIdAsync(Guid flowId, int limit = 50);
    Task<FlowExecution> CreateAsync(FlowExecution execution);
    Task UpdateAsync(FlowExecution execution);
    Task CompleteAsync(Guid id, string status, string? nodeOutputs = null, string? errorLog = null, int? executionTimeMs = null);
}

public interface IFlowExecutionLogRepository
{
    Task<IEnumerable<FlowExecutionLog>> GetByExecutionIdAsync(Guid executionId, int limit = 100);
    Task<IEnumerable<FlowExecutionLog>> GetByFlowIdAsync(Guid flowId, int limit = 100);
    Task CreateAsync(FlowExecutionLog log);
    Task CreateManyAsync(IEnumerable<FlowExecutionLog> logs);
    Task CleanupOldLogsAsync(Guid flowId, int retentionDays);
}

public interface IFlowSessionRepository
{
    Task<FlowSession?> GetByIdAsync(Guid id);
    Task<FlowSession?> GetActiveSessionAsync(Guid flowId);
    Task<IEnumerable<FlowSession>> GetByFlowIdAsync(Guid flowId, int limit = 50);
    Task<FlowSession> CreateAsync(FlowSession session);
    Task UpdateAsync(FlowSession session);
    Task StopAsync(Guid id, string? errorMessage = null);
}
