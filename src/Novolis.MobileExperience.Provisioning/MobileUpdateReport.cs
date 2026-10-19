namespace Novolis.MobileExperience.Provisioning;

/// <summary>Evidence projection for one host-side update check or apply run.</summary>
public sealed record MobileUpdateReport
{
    /// <summary>UTC time at which the run started.</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>UTC time at which the run completed.</summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Whether the host used the direct GitHub source.</summary>
    public bool UsedDirectGithub { get; init; }

    /// <summary>Whether the run completed without application-level failures.</summary>
    public bool Ok { get; init; }

    /// <summary>Report for each selected catalog application.</summary>
    public IReadOnlyList<MobileUpdateApplicationReport> Applications { get; init; } = [];

    /// <summary>Warnings that did not prevent the report from being written.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
