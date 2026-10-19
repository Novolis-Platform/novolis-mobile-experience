using System.Diagnostics;
using System.Text;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>Resolves an explicitly requested host-side GitHub credential.</summary>
internal static class GitHubAuthentication
{
    /// <summary>
    /// Reads a token from the process environment or the current user's
    /// authenticated GitHub CLI session without displaying it.
    /// </summary>
    public static async Task<string?> TryGetTokenAsync(
        CancellationToken cancellationToken)
    {
        foreach (var variable in new[] { "GH_TOKEN", "GITHUB_TOKEN" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        var startInfo = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("auth");
        startInfo.ArgumentList.Add("token");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return null;
        }
        catch
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            _ = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var token = await output.ConfigureAwait(false);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(token)
                ? token.Trim()
                : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryStop(process);
            return null;
        }
        catch
        {
            TryStop(process);
            return null;
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The CLI may have exited at the timeout boundary.
        }
    }
}
