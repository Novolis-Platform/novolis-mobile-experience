using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Unit;

public sealed class CapabilityModelTests
{
    [Test]
    public async Task EmptyHostDoesNotClaimPlatformCapabilities()
    {
        var services = EmptyMobileExperienceServices.Instance;

        await Assert.That(services.Browser.Status.IsAvailable).IsFalse();
        await Assert.That(services.Browser.Status.StoresHistory).IsFalse();
        await Assert.That(services.Files.Status.IsAvailable).IsFalse();
        await Assert.That(services.Files.Status.KeepsRecentHistory).IsFalse();

        var bluetooth = await services.Bluetooth.GetSnapshotAsync();
        await Assert.That(bluetooth.Availability)
            .IsEqualTo(BluetoothAvailability.Unavailable);
        await Assert.That(bluetooth.Devices).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ActionResultsExposeUserInteractionBoundary()
    {
        var result = MobileActionResult.Success(
            "Android picker opened.",
            requiresUserAction: true);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.RequiresUserAction).IsTrue();
        await Assert.That(result.Message).IsEqualTo("Android picker opened.");
    }

    [Test]
    public async Task BluetoothUnavailableSnapshotIsEmptyAndExplicit()
    {
        var snapshot = BluetoothSnapshot.Unavailable("No adapter.");

        await Assert.That(snapshot.Availability)
            .IsEqualTo(BluetoothAvailability.Unavailable);
        await Assert.That(snapshot.IsEnabled).IsFalse();
        await Assert.That(snapshot.IsDiscovering).IsFalse();
        await Assert.That(snapshot.Detail).IsEqualTo("No adapter.");
    }

    [Test]
    public async Task BrowserAddressAcceptsAHostWithoutARepeatedScheme()
    {
        var accepted = PrivacyBrowserAddress.TryNormalize(
            "example.com/account",
            out var normalized);

        await Assert.That(accepted).IsTrue();
        await Assert.That(normalized)
            .IsEqualTo("https://example.com/account");
    }

    [Test]
    public async Task BrowserAddressUpgradesHttpAndRejectsEmptyValues()
    {
        var httpAccepted = PrivacyBrowserAddress.TryNormalize(
            "http://example.com",
            out var upgraded);
        var emptyAccepted = PrivacyBrowserAddress.TryNormalize(
            "https://",
            out _);

        await Assert.That(httpAccepted).IsTrue();
        await Assert.That(upgraded).IsEqualTo("https://example.com/");
        await Assert.That(emptyAccepted).IsFalse();
    }
}
