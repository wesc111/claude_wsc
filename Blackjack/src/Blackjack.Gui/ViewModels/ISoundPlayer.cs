namespace Blackjack.Gui.ViewModels;

/// <summary>Plays the sounds of the table; implemented by the GUI, silent where no player is available.</summary>
public interface ISoundPlayer
{
    /// <summary>Starts the sound of an event without waiting for it to end.</summary>
    void Play(Sound sound);
}
