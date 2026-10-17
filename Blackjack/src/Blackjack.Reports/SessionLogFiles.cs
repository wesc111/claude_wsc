using System.Globalization;
using System.Text;

namespace Blackjack.Reports;

/// <summary>Saves a <see cref="SessionLog"/> as a text report and two CSV files (SPEC 8a, E2).</summary>
public static class SessionLogFiles
{
    /// <summary>
    /// The default directory: <c>~/Documents/Blackjack/log</c> when running from a macOS app bundle
    /// (started from the Finder, the working directory is <c>/</c>), otherwise <c>log</c> below the working directory.
    /// </summary>
    public static string DefaultDirectory => ResolveDirectory(
        AppContext.BaseDirectory,
        Environment.CurrentDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

    /// <summary>The log directory for the given program, working and documents directories (see <see cref="DefaultDirectory"/>).</summary>
    public static string ResolveDirectory(string baseDirectory, string currentDirectory, string documentsDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        var inAppBundle = baseDirectory.Replace('\\', '/').Contains(".app/Contents/MacOS", StringComparison.Ordinal);
        return inAppBundle
            ? Path.Combine(documentsDirectory, "Blackjack", "log")
            : Path.Combine(currentDirectory, "log");
    }

    /// <summary>
    /// Writes <c>blackjack_&lt;start&gt;.txt</c>, <c>…_statistics.csv</c> and <c>…_rounds.csv</c> into
    /// <paramref name="directory"/> (created if missing).
    /// </summary>
    /// <returns>The path of the text report.</returns>
    /// <exception cref="IOException">A file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The directory is not writable.</exception>
    public static string Save(SessionLog log, string directory)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        Directory.CreateDirectory(directory);

        var endedAt = log.Now;
        var baseName = UniqueBaseName(directory, "blackjack_" + log.StartedAt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
        var textPath = Path.Combine(directory, baseName + ".txt");

        // The CSV files get a BOM so that spreadsheet programs show the card symbols correctly.
        var csvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        using (var text = new StreamWriter(textPath, append: false, new UTF8Encoding(false)))
        {
            SessionReport.WriteText(text, log, endedAt);
        }

        using (var statistics = new StreamWriter(Path.Combine(directory, baseName + "_statistics.csv"), append: false, csvEncoding))
        {
            SessionReport.WriteStatisticsCsv(statistics, log, endedAt);
        }

        using (var rounds = new StreamWriter(Path.Combine(directory, baseName + "_rounds.csv"), append: false, csvEncoding))
        {
            SessionReport.WriteRoundsCsv(rounds, log);
        }

        return textPath;
    }

    /// <summary>The path relative to the current working directory if it lies below it, e.g. <c>log/blackjack_….txt</c>.</summary>
    public static string DisplayPath(string path)
    {
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
    }

    // Two sessions started within the same second get _2, _3, … appended.
    private static string UniqueBaseName(string directory, string baseName)
    {
        var name = baseName;
        for (var i = 2; File.Exists(Path.Combine(directory, name + ".txt")); i++)
        {
            name = $"{baseName}_{i}";
        }

        return name;
    }
}
