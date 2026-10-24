namespace enviro.Models;

/// <summary>
/// Represents the modification state of an environmental variable.
/// </summary>
public enum EnvironmentalVariableState
{
    /// <summary>
    /// The variable has not been modified.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The variable has been newly added.
    /// </summary>
    Added,

    /// <summary>
    /// The variable has been modified from its original value.
    /// </summary>
    Modified,

    /// <summary>
    /// The variable has been marked for deletion.
    /// </summary>
    Deleted
}
