using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Platform;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Sounds;

/// <summary>
/// Plays the sounds with the macOS command <c>afplay</c> (spec E8): one process per sound, not awaited, so sounds may
/// overlap. <c>afplay</c> needs files, so the embedded WAV files are extracted into a temporary directory on first use.
/// After an error (e.g. <c>afplay</c> missing) the player stays silent.
/// </summary>
public sealed class AfplaySoundPlayer : ISoundPlayer
{
    private string? _directory;
    private bool _failed;

    /// <inheritdoc />
    public void Play(Sound sound)
    {
        if (_failed || !SoundFiles.Variants.TryGetValue(sound, out var variants))
        {
            return;
        }

        try
        {
            var file = Path.Combine(_directory ??= Extract(), variants[Random.Shared.Next(variants.Count)] + ".wav");
            using var process = Process.Start(new ProcessStartInfo("afplay", [file]) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Exception e) when (e is Win32Exception or IOException or UnauthorizedAccessException)
        {
            _failed = true;
        }
    }

    // One directory per version, so an update brings its own files.
    private static string Extract()
    {
        var version = typeof(AfplaySoundPlayer).Assembly.GetName().Version;
        var directory = Path.Combine(Path.GetTempPath(), $"blackjack-sounds-{version}");
        Directory.CreateDirectory(directory);
        foreach (var name in SoundFiles.Variants.Values.SelectMany(v => v))
        {
            var file = Path.Combine(directory, name + ".wav");
            if (!File.Exists(file))
            {
                using var source = AssetLoader.Open(new Uri($"avares://BlackjackGui/Assets/Sounds/{name}.wav"));
                using var target = File.Create(file);
                source.CopyTo(target);
            }
        }

        return directory;
    }
}
