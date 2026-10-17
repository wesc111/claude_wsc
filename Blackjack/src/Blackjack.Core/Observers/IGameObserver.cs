namespace Blackjack.Core;

/// <summary>
/// Receives notifications about the course of a round. The engine awaits each call before continuing,
/// so an observer can, for example, finish an animation before the next card is dealt.
/// All methods have an empty default implementation; implement only those you need.
/// Calls happen on the thread (synchronization context) that runs <see cref="BlackjackGame.PlayRoundAsync"/>.
/// </summary>
public interface IGameObserver
{
    /// <summary>The shoe has been reshuffled before a round.</summary>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnShoeShuffledAsync(CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>A valid bet has been placed and the round starts.</summary>
    /// <param name="notification">Bet and bankroll.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnRoundStartedAsync(RoundStartedNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>A card has been dealt. A face-down card is reported without its value.</summary>
    /// <param name="notification">Recipient, card and face-up flag.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>The dealer's hole card has been turned over.</summary>
    /// <param name="notification">The hole card.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>A pair has been split: the second card moved to a new hand appended at the end.</summary>
    /// <param name="notification">Indices of the split and the new hand, and the moved card.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnHandSplitAsync(HandSplitNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>A hand has been doubled; exactly one more card follows.</summary>
    /// <param name="notification">Hand index and new bet.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnHandDoubledAsync(HandDoubledNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>The player's turn for one hand has ended.</summary>
    /// <param name="notification">The final state of the hand.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnPlayerHandFinishedAsync(PlayerHandFinishedNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>One player hand has been settled.</summary>
    /// <param name="notification">Outcome and net amount.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnHandSettledAsync(HandSettledNotification notification, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>The round is complete.</summary>
    /// <param name="result">The result of the round.</param>
    /// <param name="ct">Cancels the round.</param>
    ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct) => ValueTask.CompletedTask;
}
