namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnPlayerHandFinishedAsync"/>.</summary>
/// <param name="HandIndex">Zero-based index of the hand.</param>
/// <param name="Total">Final total of the hand.</param>
/// <param name="IsSoft">Whether the hand is soft.</param>
/// <param name="IsBlackjack">Whether the hand is a Blackjack.</param>
/// <param name="IsBust">Whether the hand is bust.</param>
public sealed record PlayerHandFinishedNotification(int HandIndex, int Total, bool IsSoft, bool IsBlackjack, bool IsBust);
