namespace Novolis.MobileExperience.Shell;

/// <summary>Host-reported update state for a discovered Android application.</summary>
public enum MobileUpdateState
{
    /// <summary>No current release information is available.</summary>
    Unknown,

    /// <summary>The application is not installed.</summary>
    NotInstalled,

    /// <summary>The installed version matches the last trusted check.</summary>
    Current,

    /// <summary>A newer trusted version is available.</summary>
    Available,

    /// <summary>An update is queued by the host.</summary>
    Queued,

    /// <summary>An update is downloading.</summary>
    Downloading,

    /// <summary>An update artifact is being verified.</summary>
    Verifying,

    /// <summary>An update is being installed.</summary>
    Installing,

    /// <summary>An update was installed and verified.</summary>
    Installed,

    /// <summary>The update workflow failed.</summary>
    Failed,

    /// <summary>The available artifact cannot run on this device.</summary>
    Incompatible,
}
