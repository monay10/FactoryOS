namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>The lifecycle status of a workflow instance.</summary>
public enum WorkflowStatus
{
    /// <summary>The instance has been created but not yet started.</summary>
    NotStarted = 0,

    /// <summary>The instance is executing or waiting on activities, timers or signals.</summary>
    Running = 1,

    /// <summary>The instance ran to an end node and finished successfully.</summary>
    Completed = 2,

    /// <summary>The instance was cancelled before completing.</summary>
    Cancelled = 3,

    /// <summary>The instance faulted and stopped.</summary>
    Failed = 4,
}

/// <summary>The execution state of a single node token within an instance.</summary>
public enum WorkflowState
{
    /// <summary>The node has not been reached.</summary>
    Pending = 0,

    /// <summary>The node is executing.</summary>
    Active = 1,

    /// <summary>The node is waiting on an external activity, timer or signal.</summary>
    Waiting = 2,

    /// <summary>The node completed and passed control on.</summary>
    Completed = 3,

    /// <summary>The node faulted.</summary>
    Faulted = 4,
}

/// <summary>The kind of a workflow node, determining how the executor drives it.</summary>
public enum WorkflowNodeKind
{
    /// <summary>The single entry point of a definition.</summary>
    Start = 0,

    /// <summary>A terminal node; the instance completes when no tokens remain.</summary>
    End = 1,

    /// <summary>A human/task activity that waits for external completion.</summary>
    Activity = 2,

    /// <summary>An exclusive gateway that follows the first outgoing transition whose condition holds.</summary>
    Decision = 3,

    /// <summary>An AND-split that activates every outgoing transition.</summary>
    Parallel = 4,

    /// <summary>An AND-join that waits for every incoming branch before continuing.</summary>
    Merge = 5,

    /// <summary>A node that waits until a due time.</summary>
    Timer = 6,

    /// <summary>A node that waits until an external signal.</summary>
    Wait = 7,

    /// <summary>A node that applies variable assignments from expressions.</summary>
    Script = 8,

    /// <summary>A node that invokes a registered in-process service.</summary>
    Service = 9,
}
