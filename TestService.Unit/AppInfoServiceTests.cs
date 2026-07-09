using Microsoft.Extensions.Hosting;
using TestService.Api.Services;

namespace TestService.Unit;

[TestFixture]
public class AppInfoServiceTests
{
    private static readonly DateTime StartUtc = new(2026, 7, 9, 8, 0, 0, DateTimeKind.Utc);

    private static IHostEnvironment BuildEnvironment(string environmentName)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return env;
    }

    private static AppInfoService BuildSut(
        string environmentName = "Testing",
        string? informationalVersion = "1.0.0",
        string? commitMetadata = null,
        string? buildDateMetadata = null,
        DateTime? nowUtc = null,
        Func<string, string?>? getEnvVar = null)
    {
        var now = nowUtc ?? StartUtc.AddMinutes(90);
        return new AppInfoService(
            BuildEnvironment(environmentName),
            informationalVersion,
            commitMetadata,
            buildDateMetadata,
            StartUtc,
            () => now,
            getEnvVar ?? (_ => null));
    }

    // ── Identity / stable fields ────────────────────────────────────────────────

    [Test]
    public void GetInfo_PopulatesStableIdentityFields()
    {
        var sut = BuildSut(environmentName: "Production", informationalVersion: "1.2.3");

        var info = sut.GetInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.Name, Is.EqualTo("Test Service API"), "Name should be the application name.");
            Assert.That(info.ApiVersion, Is.EqualTo("v1"), "API version should match the Swagger doc version.");
            Assert.That(info.Environment, Is.EqualTo("Production"), "Environment should come from IHostEnvironment.");
            Assert.That(info.Version, Is.EqualTo("1.2.3"), "Version should come from the informational version.");
            Assert.That(info.Runtime, Does.Contain(".NET"), "Runtime should report the .NET framework description.");
        });
    }

    [Test]
    public void GetInfo_StripsBuildMetadataSuffixFromVersion()
    {
        var sut = BuildSut(informationalVersion: "1.2.3+85de620e316404f3e931b3cc16b807ddc88e90dc");

        var info = sut.GetInfo();

        Assert.That(info.Version, Is.EqualTo("1.2.3"), "The '+<sha>' suffix should be stripped from the version.");
    }

    // ── Uptime / server time ────────────────────────────────────────────────────

    [Test]
    public void GetInfo_ComputesUptimeFromStartAndNow()
    {
        // now is 90 minutes after start.
        var sut = BuildSut();

        var info = sut.GetInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.UptimeSeconds, Is.EqualTo(5400), "90 minutes should be 5400 seconds.");
            Assert.That(info.Uptime, Is.EqualTo("1h 30m"), "Uptime should be formatted as hours and minutes.");
        });
    }

    [Test]
    public void GetInfo_FormatsServerTimeAsIsoUtc()
    {
        var now = new DateTime(2026, 7, 9, 10, 22, 5, DateTimeKind.Utc);
        var sut = BuildSut(nowUtc: now);

        var info = sut.GetInfo();

        Assert.That(info.ServerTimeUtc, Is.EqualTo("2026-07-09T10:22:05Z"));
    }

    [Test]
    public void GetInfo_ClampsNegativeUptimeToZero()
    {
        // now is before the recorded start time (clock skew / injected past).
        var sut = BuildSut(nowUtc: StartUtc.AddMinutes(-5));

        var info = sut.GetInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.UptimeSeconds, Is.EqualTo(0), "Negative uptime should clamp to zero.");
            Assert.That(info.Uptime, Is.EqualTo("0s"));
        });
    }

    // ── Commit resolution (precedence: env → metadata → informational version → dev) ─

    [Test]
    public void GetInfo_ReturnsDevCommitAndNullBuildDate_WhenNothingConfigured()
    {
        var sut = BuildSut(informationalVersion: "1.0.0", commitMetadata: null, buildDateMetadata: null, getEnvVar: _ => null);

        var info = sut.GetInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.Commit, Is.EqualTo("dev"), "Commit should fall back to 'dev' when unknown.");
            Assert.That(info.BuildDateUtc, Is.Null, "Build date should be null when unknown.");
        });
    }

    [Test]
    public void GetInfo_ShortensCommitFromEnvironmentVariable()
    {
        var sut = BuildSut(getEnvVar: name => name == "GIT_COMMIT" ? "abcdef1234567890" : null);

        var info = sut.GetInfo();

        Assert.That(info.Commit, Is.EqualTo("abcdef1"), "Commit should be shortened to 7 chars.");
    }

    [Test]
    public void GetInfo_PrefersRailwayCommit_WhenGitCommitAbsent()
    {
        var sut = BuildSut(getEnvVar: name => name == "RAILWAY_GIT_COMMIT_SHA" ? "0123456789" : null);

        var info = sut.GetInfo();

        Assert.That(info.Commit, Is.EqualTo("0123456"), "Should read the Railway-provided commit env var.");
    }

    [Test]
    public void GetInfo_UsesCommitMetadata_WhenNoEnvVarSet()
    {
        var sut = BuildSut(commitMetadata: "fedcba98765", getEnvVar: _ => null);

        var info = sut.GetInfo();

        Assert.That(info.Commit, Is.EqualTo("fedcba9"), "Should read the explicit CommitHash assembly metadata.");
    }

    [Test]
    public void GetInfo_FallsBackToInformationalVersionCommit_WhenNoEnvOrMetadata()
    {
        var sut = BuildSut(
            informationalVersion: "1.0.0+85de620e316404f3e931b3cc16b807ddc88e90dc",
            commitMetadata: null,
            getEnvVar: _ => null);

        var info = sut.GetInfo();

        Assert.That(info.Commit, Is.EqualTo("85de620"),
            "Should fall back to the git commit the SDK embeds in the informational version.");
    }

    [Test]
    public void GetInfo_ReadsBuildDateFromEnvironmentVariable()
    {
        const string buildDate = "2026-07-09T10:00:00Z";
        var sut = BuildSut(buildDateMetadata: "2020-01-01T00:00:00Z", getEnvVar: name => name == "BUILD_DATE" ? buildDate : null);

        var info = sut.GetInfo();

        Assert.That(info.BuildDateUtc, Is.EqualTo(buildDate), "Env var should take precedence over embedded build date.");
    }

    [Test]
    public void GetInfo_ReadsBuildDateFromMetadata_WhenNoEnvVar()
    {
        const string buildDate = "2026-07-01T08:00:00Z";
        var sut = BuildSut(buildDateMetadata: buildDate, getEnvVar: _ => null);

        var info = sut.GetInfo();

        Assert.That(info.BuildDateUtc, Is.EqualTo(buildDate));
    }

    [Test]
    public void GetInfo_TrimsWhitespaceFromEnvValues()
    {
        // Tooling that injects env vars from files often leaves a trailing newline;
        // the ISO-8601 build-date contract must not surface it verbatim.
        var sut = BuildSut(getEnvVar: name => name switch
        {
            "GIT_COMMIT" => "  abcdef1234567\n",
            "BUILD_DATE" => "2026-07-09T10:00:00Z\n",
            _ => null
        });

        var info = sut.GetInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.Commit, Is.EqualTo("abcdef1"), "Commit should be trimmed before shortening.");
            Assert.That(info.BuildDateUtc, Is.EqualTo("2026-07-09T10:00:00Z"), "Build date should be trimmed.");
        });
    }

    // ── Pure helpers ────────────────────────────────────────────────────────────

    [Test]
    public void Shorten_ReturnsShortShaUnchanged()
    {
        Assert.That(AppInfoService.Shorten("abc12"), Is.EqualTo("abc12"));
    }

    [Test]
    public void ExtractBuildMetadata_ReturnsNull_WhenNoSuffix()
    {
        Assert.That(AppInfoService.ExtractBuildMetadata("1.0.0"), Is.Null);
        Assert.That(AppInfoService.ExtractBuildMetadata(null), Is.Null);
    }

    [Test]
    public void FormatUptime_FormatsAcrossRanges()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AppInfoService.FormatUptime(TimeSpan.FromSeconds(45)), Is.EqualTo("45s"));
            Assert.That(AppInfoService.FormatUptime(TimeSpan.FromMinutes(2.5)), Is.EqualTo("2m 30s"));
            Assert.That(AppInfoService.FormatUptime(new TimeSpan(3, 4, 5, 6)), Is.EqualTo("3d 4h 5m"));
        });
    }
}
