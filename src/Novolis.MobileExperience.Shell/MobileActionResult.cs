namespace Novolis.MobileExperience.Shell;

/// <summary>Outcome of a user-initiated platform action.</summary>
public sealed record MobileActionResult
{
    /// <summary>Whether the platform accepted the action.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Whether the user must complete a system prompt or settings action.</summary>
    public bool RequiresUserAction { get; init; }

    /// <summary>Short, user-facing explanation of the outcome.</summary>
    public required string Message { get; init; }

    /// <summary>Creates a successful action result.</summary>
    public static MobileActionResult Success(
        string message,
        bool requiresUserAction = false) =>
        new()
        {
            Succeeded = true,
            RequiresUserAction = requiresUserAction,
            Message = message,
        };

    /// <summary>Creates a failed action result.</summary>
    public static MobileActionResult Failure(string message) =>
        new()
        {
            Succeeded = false,
            Message = message,
        };
}
