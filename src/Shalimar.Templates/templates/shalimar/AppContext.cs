namespace ShalimarApp;

/// <summary>
/// Application context - injected into shell as __SHALIMAR_CONTEXT__.
/// Edit to add properties. Generator produces TypeScript.
/// </summary>
public record AppContextModel
{
    public required string Environment { get; init; }
}
