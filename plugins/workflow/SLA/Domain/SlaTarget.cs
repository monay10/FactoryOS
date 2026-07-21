namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// A reference to the work an SLA tracks: what kind it is, the definition key it came from, and the instance id
/// when there is one. The SLA engine holds only this reference — it never reaches into the workflow, human task,
/// approval or forms engines to read or change the work itself.
/// </summary>
/// <param name="Kind">The kind of work tracked.</param>
/// <param name="Key">The work's definition key (a workflow activity node id, task/approval/form key).</param>
/// <param name="Id">The work's instance id, when it has one.</param>
public sealed record SlaTarget(SlaTargetKind Kind, string Key, Guid? Id = null)
{
    /// <summary>Creates a reference to a workflow activity node.</summary>
    /// <param name="activityNodeId">The activity node id.</param>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <returns>The target reference.</returns>
    public static SlaTarget ForWorkflowActivity(string activityNodeId, Guid workflowInstanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        return new SlaTarget(SlaTargetKind.WorkflowActivity, activityNodeId, workflowInstanceId);
    }

    /// <summary>Creates a reference to a human task.</summary>
    /// <param name="definitionKey">The task definition key.</param>
    /// <param name="taskId">The task id.</param>
    /// <returns>The target reference.</returns>
    public static SlaTarget ForHumanTask(string definitionKey, Guid taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        return new SlaTarget(SlaTargetKind.HumanTask, definitionKey, taskId);
    }

    /// <summary>Creates a reference to an approval.</summary>
    /// <param name="definitionKey">The approval definition key.</param>
    /// <param name="approvalId">The approval id.</param>
    /// <returns>The target reference.</returns>
    public static SlaTarget ForApproval(string definitionKey, Guid approvalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        return new SlaTarget(SlaTargetKind.Approval, definitionKey, approvalId);
    }

    /// <summary>Creates a reference to a form submission.</summary>
    /// <param name="formKey">The form key.</param>
    /// <param name="formInstanceId">The form instance id.</param>
    /// <returns>The target reference.</returns>
    public static SlaTarget ForFormSubmission(string formKey, Guid formInstanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formKey);
        return new SlaTarget(SlaTargetKind.FormSubmission, formKey, formInstanceId);
    }
}
