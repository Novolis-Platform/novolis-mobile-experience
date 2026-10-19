using System.Net;
using System.Text;
using Novolis.IO.GitHub;
using Novolis.Registry.Primitives.Updates;
using Novolis.Registry.Updates;
using Novolis.MobileExperience.Provisioning;

namespace Novolis.MobileExperience.Unit;

public sealed class ReleaseAssetSourceTests
{
    [Test]
    public async Task Source_selects_the_newest_pinned_asset_without_a_manifest()
    {
        using var client = new HttpClient(
            new StaticResponseHandler(
                """
                [
                  {
                    "draft": false,
                    "prerelease": false,
                    "tag_name": "v2026.1.0",
                    "html_url": "https://github.com/Novolis-Platform/merglyph/releases/tag/v2026.1.0",
                    "published_at": "2026-09-01T00:00:00Z",
                    "assets": [
                      {
                        "name": "Merglyph-android.apk",
                        "browser_download_url": "https://github.com/Novolis-Platform/merglyph/releases/download/v2026.1.0/Merglyph-android.apk",
                        "size": 100,
                        "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        "content_type": "application/vnd.android.package-archive"
                      }
                    ]
                  },
                  {
                    "draft": false,
                    "prerelease": false,
                    "tag_name": "v2026.1.1",
                    "html_url": "https://github.com/Novolis-Platform/merglyph/releases/tag/v2026.1.1",
                    "published_at": "2026-09-18T00:00:00Z",
                    "assets": [
                      {
                        "name": "Merglyph-android.apk",
                        "browser_download_url": "https://github.com/Novolis-Platform/merglyph/releases/download/v2026.1.1/Merglyph-android.apk",
                        "size": 200,
                        "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        "content_type": "application/vnd.android.package-archive"
                      }
                    ]
                  }
                ]
                """));
        var source = new GitHubReleaseAssetUpdateSource(
            client,
            new GitHubRepository
            {
                Owner = "Novolis-Platform",
                Name = "merglyph",
            },
            "Merglyph-android.apk",
            "Novolis-Mobile-Experience.Tests");

        var result = await source.CheckAsync(
            new UpdateRequest
            {
                AppId = "Novolis.Merglyph",
                Repository = new Uri(
                    "https://github.com/Novolis-Platform/merglyph"),
                CurrentVersion = "2026.1.0",
                Platform = UpdatePlatform.Android,
                ArtifactKind = UpdateArtifactKind.AndroidApk,
            },
            new UpdateSourceCheckpoint());

        await Assert.That(result.Candidate).IsNotNull();
        await Assert.That(result.Candidate!.Version).IsEqualTo("2026.1.1");
        await Assert.That(result.Candidate.Artifact.Length).IsEqualTo(200);
        await Assert.That(result.Candidate.Artifact.Sha256)
            .IsEqualTo("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    }

    private sealed class StaticResponseHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        content,
                        Encoding.UTF8,
                        "application/json"),
                });
    }
}
