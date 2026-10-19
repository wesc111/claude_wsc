using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Sounds;

/// <summary>The sound files of each event (spec E8); a player picks one of the variants at random.</summary>
public static class SoundFiles
{
    /// <summary>The file names (without extension) in <c>Assets/Sounds</c> per sound.</summary>
    public static IReadOnlyDictionary<Sound, IReadOnlyList<string>> Variants { get; } = new Dictionary<Sound, IReadOnlyList<string>>
    {
        [Sound.CardDeal] = ["card-slide-1", "card-slide-2", "card-slide-3", "card-slide-4"],
        [Sound.CardTurn] = ["card-place-2", "card-place-4"],
        [Sound.Shuffle] = ["card-shuffle"],
        [Sound.ChipPlace] = ["chip-lay-1", "chip-lay-2", "chip-lay-3"],
        [Sound.ChipTake] = ["chips-handle-3", "chips-handle-4"],
        [Sound.Win] = ["chips-stack-1", "chips-stack-3"],
        [Sound.Blackjack] = ["chips-handle-5"],
        [Sound.Lose] = ["card-shove-1", "card-shove-2"],
    };
}
