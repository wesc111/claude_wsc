namespace Blackjack.Core;

/// <summary>Classification helpers for <see cref="HandOutcome"/>.</summary>
public static class HandOutcomeExtensions
{
    /// <summary>True for <see cref="HandOutcome.BlackjackWin"/>, <see cref="HandOutcome.Win"/> and <see cref="HandOutcome.DealerBust"/>.</summary>
    /// <param name="outcome">The outcome.</param>
    public static bool IsWin(this HandOutcome outcome) =>
        outcome is HandOutcome.BlackjackWin or HandOutcome.Win or HandOutcome.DealerBust;

    /// <summary>True for <see cref="HandOutcome.Loss"/>, <see cref="HandOutcome.PlayerBust"/> and <see cref="HandOutcome.DealerBlackjack"/>.</summary>
    /// <param name="outcome">The outcome.</param>
    public static bool IsLoss(this HandOutcome outcome) =>
        outcome is HandOutcome.Loss or HandOutcome.PlayerBust or HandOutcome.DealerBlackjack;
}
