namespace Blackjack.Core;

/// <summary>An action a player can take on a hand.</summary>
public enum PlayerAction
{
    /// <summary>Take another card.</summary>
    Hit,

    /// <summary>Take no more cards.</summary>
    Stand,

    /// <summary>Double the bet, take exactly one more card, then stand.</summary>
    Double,

    /// <summary>Split a pair into two hands, each with the original bet.</summary>
    Split,
}
