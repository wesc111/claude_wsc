using System.Text.Json;

namespace Blackjack.Gui.Settings;

/// <summary>Loads and saves <see cref="GameSettings"/> as a JSON file (spec E9).</summary>
/// <param name="filePath">The settings file, e.g. <see cref="DefaultPath"/>.</param>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// <c>settings.json</c> in the folder <c>Blackjack</c> of the local application data:
    /// <c>~/Library/Application Support</c> on macOS, <c>~/.local/share</c> on Linux, <c>%LOCALAPPDATA%</c> on Windows.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackjack", "settings.json");

    /// <summary>The settings file.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The saved settings; the defaults if the file is missing or unreadable, and per value if it is not offered.</summary>
    public GameSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return GameSettings.Default;
            }

            using var stream = File.OpenRead(FilePath);
            return (JsonSerializer.Deserialize<GameSettings>(stream, Json) ?? GameSettings.Default).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return GameSettings.Default;
        }
    }

    /// <summary>
    /// Saves the settings (creating the folder if missing). The file is written to a temporary file first and then
    /// replaced, so it is never left half written.
    /// </summary>
    /// <returns><c>false</c> if the file could not be written; the game goes on without saving.</returns>
    public bool Save(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Json));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
