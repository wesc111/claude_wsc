using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Blackjack.Gui.ViewModels;

/// <summary>A hand on the table (dealer or player) with its cards, total, bet and result.</summary>
public sealed class HandViewModel : ObservableObject
{
    private string _title;
    private string _totalText = "";
    private string _betText = "";
    private string _resultText = "";
    private bool _isActive;

    /// <summary>Creates an empty hand.</summary>
    public HandViewModel(string title)
    {
        _title = title;
    }

    /// <summary>The cards from left to right.</summary>
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    /// <summary>The title, e.g. <c>Dealer</c>, <c>You</c> or <c>Hand 2</c>.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Whether the hand comes from a split (A + 10 is then 21, not Blackjack).</summary>
    public bool IsFromSplit { get; set; }

    /// <summary>The total, e.g. <c>soft 17</c>; empty while a card is face down.</summary>
    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    /// <summary>The bet text, e.g. <c>Bet 25</c>; empty for the dealer.</summary>
    public string BetText
    {
        get => _betText;
        set => SetProperty(ref _betText, value);
    }

    /// <summary>The result, e.g. <c>You win +25</c>; empty until the hand is settled.</summary>
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    /// <summary>Whether the player is currently deciding on this hand.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>Recomputes <see cref="TotalText"/> from the face-up cards.</summary>
    public void UpdateTotal()
    {
        if (Cards.Count == 0 || Cards.Any(c => !c.IsFaceUp))
        {
            TotalText = "";
            return;
        }

        TotalText = DisplayText.Total(new Hand(Cards.Select(c => c.Card!.Value)) { IsFromSplit = IsFromSplit });
    }
}
