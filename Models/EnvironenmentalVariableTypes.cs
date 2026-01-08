namespace enviro.Models;

/// <summary>
/// Represents the scope of an environmental variable.
/// </summary>
public enum EnvironmentalVariableType
{
    /// <summary>
    /// User-level environmental variable.
    /// </summary>
    User,
    
    /// <summary>
    /// Machine-level (system-wide) environmental variable.
    /// </summary>
    Machine,
    
    /// <summary>
    /// All environmental variables (both User and Machine).
    /// </summary>
    All
}