namespace Blackjack.Gui.ViewModels;

/// <summary>
/// Animates cards on the table; implemented by the main window. The table view model awaits each animation,
/// so the engine deals the next card only when the previous one has arrived.
/// </summary>
public interface ICardAnimator
{
    /// <summary>
    /// Lets a card fly from the shoe to its place. The card is already in its hand (hidden while
    /// <see cref="CardViewModel.IsDealing"/> is set) and face down when <see cref="CardViewModel.Card"/> is <c>null</c>.
    /// </summary>
    Task DealAsync(CardViewModel card, CancellationToken ct);

    /// <summary>Turns a face-down card over in place; <paramref name="turn"/> shows its face and is called halfway.</summary>
    Task TurnOverAsync(CardViewModel card, Action turn, CancellationToken ct);
}
