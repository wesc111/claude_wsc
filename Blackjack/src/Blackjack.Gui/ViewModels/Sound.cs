namespace Blackjack.Gui.ViewModels;

/// <summary>The events of the table that make a sound (spec E8).</summary>
public enum Sound
{
    /// <summary>A card is pulled out of the shoe.</summary>
    CardDeal,

    /// <summary>The hole card turns over.</summary>
    CardTurn,

    /// <summary>The shoe is shuffled.</summary>
    Shuffle,

    /// <summary>A chip is put into the bet area.</summary>
    ChipPlace,

    /// <summary>A chip is taken back, or the bet area is cleared.</summary>
    ChipTake,

    /// <summary>The round is won.</summary>
    Win,

    /// <summary>The round is won with a player Blackjack.</summary>
    Blackjack,

    /// <summary>The round is lost.</summary>
    Lose,
}
