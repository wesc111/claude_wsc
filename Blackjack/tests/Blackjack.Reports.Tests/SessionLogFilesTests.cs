using System.Text;

namespace Blackjack.Reports.Tests;

/// <summary>SPEC 8a, E2 – writing the three files.</summary>
public sealed class SessionLogFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blackjack-tests-" + Guid.NewGuid().ToString("N"), "log");

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_directory)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WritesThreeFilesIntoANewDirectory()
    {
        var log = await Sessions.TwoRoundsAsync();

        var path = SessionLogFiles.Save(log, _directory);

        Assert.Equal(Path.Combine(_directory, "blackjack_2026-10-17_15-30-00.txt"), path);
        Assert.Equal(
            ["blackjack_2026-10-17_15-30-00.txt", "blackjack_2026-10-17_15-30-00_rounds.csv", "blackjack_2026-10-17_15-30-00_statistics.csv"],
            Directory.GetFiles(_directory).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        var expected = new StringWriter();
        SessionReport.WriteText(expected, log, Sessions.End);
        Assert.Equal(expected.ToString(), await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CsvFilesHaveABomAndTheTextFileNot()
    {
        var path = SessionLogFiles.Save(await Sessions.TwoRoundsAsync(), _directory);
        byte[] bom = [0xEF, 0xBB, 0xBF];

        var text = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var rounds = await File.ReadAllBytesAsync(path.Replace(".txt", "_rounds.csv", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var statistics = await File.ReadAllBytesAsync(path.Replace(".txt", "_statistics.csv", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        Assert.NotEqual(bom, text.Take(3));
        Assert.Equal(bom, rounds.Take(3));
        Assert.Equal(bom, statistics.Take(3));
        Assert.Contains("10♠ 7♠", Encoding.UTF8.GetString(rounds), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondSessionInTheSameSecondGetsASuffix()
    {
        var log = await Sessions.TwoRoundsAsync();

        var first = SessionLogFiles.Save(log, _directory);
        var second = SessionLogFiles.Save(log, _directory);

        Assert.NotEqual(first, second);
        Assert.EndsWith("blackjack_2026-10-17_15-30-00_2.txt", second, StringComparison.Ordinal);
        Assert.True(File.Exists(second.Replace(".txt", "_rounds.csv", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UnwritableDirectoryThrowsIOException()
    {
        // A file where the directory should be.
        Directory.CreateDirectory(Path.GetDirectoryName(_directory)!);
        await File.WriteAllTextAsync(_directory, "not a directory", TestContext.Current.CancellationToken);

        Assert.ThrowsAny<IOException>(() => SessionLogFiles.Save(new SessionLog(Rules.Default, 1000m), _directory));
    }

    [Fact]
    public void DisplayPathIsRelativeBelowTheWorkingDirectory()
    {
        var inside = Path.Combine(Environment.CurrentDirectory, "log", "a.txt");

        Assert.Equal(Path.Combine("log", "a.txt"), SessionLogFiles.DisplayPath(inside));
        Assert.Equal("/elsewhere/a.txt", SessionLogFiles.DisplayPath("/elsewhere/a.txt"));
    }

    [Fact]
    public void DefaultDirectoryIsLogBelowTheWorkingDirectory() =>
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "log"), SessionLogFiles.DefaultDirectory);

    [Fact]
    public void AppBundleWritesIntoTheDocumentsFolder() =>
        Assert.Equal(
            Path.Combine("/Users/me/Documents", "Blackjack", "log"),
            SessionLogFiles.ResolveDirectory("/Applications/Blackjack.app/Contents/MacOS/", "/", "/Users/me/Documents"));

    [Fact]
    public void OutsideAnAppBundleTheWorkingDirectoryIsUsed() =>
        Assert.Equal(
            Path.Combine("/Users/me/Developer/Blackjack", "log"),
            SessionLogFiles.ResolveDirectory("/Users/me/Developer/Blackjack/src/Blackjack.Gui/bin/Release/net10.0/", "/Users/me/Developer/Blackjack", "/Users/me/Documents"));
}
