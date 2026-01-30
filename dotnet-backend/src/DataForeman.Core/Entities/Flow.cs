namespace DataForeman.Core.Entities;

public class Flow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? FolderId { get; set; }
    public bool Deployed { get; set; }
    public bool Shared { get; set; }
    public bool TestMode { get; set; }
    public bool TestDisableWrites { get; set; }
    public bool TestAutoExit { get; set; }
    public int TestAutoExitMinutes { get; set; } = 5;
    public string ExecutionMode { get; set; } = "continuous"; // continuous | manual
    public int ScanRateMs { get; set; } = 1000;
    public bool LiveValuesUseScanRate { get; set; }
    public bool LogsEnabled { get; set; }
    public int LogsRetentionDays { get; set; } = 30;
    public bool SaveUsageData { get; set; } = true;
    public string ExposedParameters { get; set; } = "[]"; // JSON array
    public Guid? ResourceChartId { get; set; }
    public string Definition { get; set; } = "{}"; // JSON for flow definition (nodes, edges)
    public string StaticData { get; set; } = "{}"; // JSON for static data
    public bool IsTemplate { get; set; } = false; // Mark flow as reusable template
    public Guid? TemplateFlowId { get; set; } // Reference to template if instantiated from one
    public string TemplateInputs { get; set; } = "[]"; // JSON array of input parameter names
    public string TemplateOutputs { get; set; } = "[]"; // JSON array of output parameter names
    public string? DeployedDefinition { get; set; } // JSON snapshot of definition when deployed
    public DateTime? DeployedAt { get; set; } // Timestamp when last deployed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? Owner { get; set; }
    public virtual FlowFolder? Folder { get; set; }
    public virtual ChartConfig? ResourceChart { get; set; }
    public virtual Flow? TemplateFlow { get; set; } // Reference to the template flow
    public virtual ICollection<FlowExecution> Executions { get; set; } = new List<FlowExecution>();
    public virtual ICollection<FlowSession> Sessions { get; set; } = new List<FlowSession>();
    public virtual ICollection<Flow> InstantiatedFlows { get; set; } = new List<Flow>(); // Flows created from this template
}

public class FlowFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentFolderId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
    public virtual FlowFolder? ParentFolder { get; set; }
    public virtual ICollection<FlowFolder> ChildFolders { get; set; } = new List<FlowFolder>();
    public virtual ICollection<Flow> Flows { get; set; } = new List<Flow>();
}

public class FlowExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FlowId { get; set; }
    public string? TriggerNodeId { get; set; }
    public string RuntimeParameters { get; set; } = "{}"; // JSON
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running"; // running | completed | failed
    public string NodeOutputs { get; set; } = "{}"; // JSON
    public string ErrorLog { get; set; } = "[]"; // JSON array
    public int? ExecutionTimeMs { get; set; }

    // Navigation
    public virtual Flow? Flow { get; set; }
    public virtual ICollection<FlowExecutionLog> Logs { get; set; } = new List<FlowExecutionLog>();
}

public class FlowExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ExecutionId { get; set; }
    public Guid FlowId { get; set; }
    public string? NodeId { get; set; }
    public string LogLevel { get; set; } = "info"; // debug | info | warn | error
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual FlowExecution? Execution { get; set; }
    public virtual Flow? Flow { get; set; }
}

public class FlowSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FlowId { get; set; }
    public string Status { get; set; } = "active"; // active | stopped | error | stalled
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StoppedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Config { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Flow? Flow { get; set; }
}
