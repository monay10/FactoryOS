namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>The lifecycle status of a <see cref="FormDefinition"/> (the design-time artefact).</summary>
public enum FormStatus
{
    /// <summary>The definition is being edited and cannot yet be opened.</summary>
    Draft = 0,

    /// <summary>The definition is published and instances may be opened from it.</summary>
    Published = 1,

    /// <summary>The definition is retired; existing instances run out but no new ones open.</summary>
    Archived = 2,
}

/// <summary>The state of a single <see cref="FormInstance"/> (one filling of a form).</summary>
public enum FormInstanceState
{
    /// <summary>Opened and awaiting input; nothing has been saved yet.</summary>
    Open = 0,

    /// <summary>Values have been saved but not yet submitted.</summary>
    Draft = 1,

    /// <summary>Submitted with values that passed validation.</summary>
    Submitted = 2,

    /// <summary>Approved after submission.</summary>
    Approved = 3,

    /// <summary>Rejected after submission.</summary>
    Rejected = 4,

    /// <summary>Cancelled before completion.</summary>
    Cancelled = 5,
}

/// <summary>The kind of an input or presentation field on a form.</summary>
public enum FieldType
{
    /// <summary>A single line of text.</summary>
    Text = 0,

    /// <summary>A multi-line block of text.</summary>
    Textarea = 1,

    /// <summary>An integer number.</summary>
    Number = 2,

    /// <summary>A decimal number.</summary>
    Decimal = 3,

    /// <summary>A monetary amount.</summary>
    Currency = 4,

    /// <summary>An e-mail address.</summary>
    Email = 5,

    /// <summary>A telephone number.</summary>
    Phone = 6,

    /// <summary>A calendar date.</summary>
    Date = 7,

    /// <summary>A date and time.</summary>
    DateTime = 8,

    /// <summary>A time of day.</summary>
    Time = 9,

    /// <summary>A single boolean toggle.</summary>
    Checkbox = 10,

    /// <summary>A single choice from a small set shown inline.</summary>
    Radio = 11,

    /// <summary>A single choice from a drop-down list.</summary>
    Dropdown = 12,

    /// <summary>Several choices from a list.</summary>
    MultiSelect = 13,

    /// <summary>A single choice resolved from an external reference source.</summary>
    Lookup = 14,

    /// <summary>A single free-text choice completed against suggestions.</summary>
    Autocomplete = 15,

    /// <summary>An uploaded file reference.</summary>
    File = 16,

    /// <summary>An uploaded image reference.</summary>
    Image = 17,

    /// <summary>A captured signature reference.</summary>
    Signature = 18,

    /// <summary>A static caption; carries no value.</summary>
    Label = 19,

    /// <summary>A visual divider; carries no value.</summary>
    Separator = 20,

    /// <summary>A value carried but never shown.</summary>
    Hidden = 21,
}

/// <summary>How a form's contents are arranged for rendering.</summary>
public enum FormLayoutKind
{
    /// <summary>Fields flow top to bottom in a single column.</summary>
    Stack = 0,

    /// <summary>Fields are placed on a fixed-column grid.</summary>
    Grid = 1,
}

/// <summary>The behavioural effect a <see cref="FieldRule"/> applies when its condition holds.</summary>
public enum FieldRuleKind
{
    /// <summary>The field becomes mandatory.</summary>
    Required = 0,

    /// <summary>The field cannot be edited.</summary>
    ReadOnly = 1,

    /// <summary>The field is shown.</summary>
    Visible = 2,

    /// <summary>The field is removed from view (and skipped by validation).</summary>
    Hidden = 3,

    /// <summary>The field is enabled for input.</summary>
    Enabled = 4,

    /// <summary>The field is disabled for input.</summary>
    Disabled = 5,

    /// <summary>The field's value is computed from an expression.</summary>
    Calculated = 6,
}

/// <summary>The subject a <see cref="FormAssignment"/> or <see cref="FormPermission"/> targets.</summary>
public enum FormPrincipalKind
{
    /// <summary>A specific user.</summary>
    User = 0,

    /// <summary>A role.</summary>
    Role = 1,

    /// <summary>A group.</summary>
    Group = 2,

    /// <summary>A subject resolved at runtime from an expression.</summary>
    Dynamic = 3,
}

/// <summary>The access a <see cref="FormPermission"/> grants.</summary>
public enum FormAccess
{
    /// <summary>May view the form and its values.</summary>
    View = 0,

    /// <summary>May edit values and save drafts.</summary>
    Edit = 1,

    /// <summary>May submit the form.</summary>
    Submit = 2,

    /// <summary>May approve or reject a submission.</summary>
    Approve = 3,
}
