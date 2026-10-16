namespace Blackjack.Core;

/// <summary>The settled outcome of one player hand.</summary>
public enum HandOutcome
{
    /// <summary>Player Blackjack, dealer no Blackjack; paid at <see cref="Rules.BlackjackPayout"/>.</summary>
    BlackjackWin,

    /// <summary>Player total higher than dealer total; paid 1:1.</summary>
    Win,

    /// <summary>Dealer busts, player does not; paid 1:1.</summary>
    DealerBust,

    /// <summary>Equal totals or both Blackjack; bet returned.</summary>
    Push,

    /// <summary>Player total lower than dealer total; bet lost.</summary>
    Loss,

    /// <summary>Player total over 21; bet lost.</summary>
    PlayerBust,

    /// <summary>Dealer Blackjack, player no Blackjack; bet lost.</summary>
    DealerBlackjack,
}
