using CommunityToolkit.Mvvm.ComponentModel;

namespace Blackjack.Gui.ViewModels;

/// <summary>One card on the table; a face-down card has no value and shows the card back.</summary>
public sealed class CardViewModel : ObservableObject
{
    /// <summary>The image name of the card back.</summary>
    public const string BackImage = "card_back";

    private Card? _card;

    /// <summary>Creates a card; <c>null</c> means face down.</summary>
    public CardViewModel(Card? card)
    {
        _card = card;
    }

    /// <summary>The card, or <c>null</c> while it is face down.</summary>
    public Card? Card
    {
        get => _card;
        set
        {
            if (SetProperty(ref _card, value))
            {
                OnPropertyChanged(nameof(IsFaceUp));
                OnPropertyChanged(nameof(ImageName));
            }
        }
    }

    /// <summary>Whether the card is face up.</summary>
    public bool IsFaceUp => _card is not null;

    /// <summary>The image file name without extension, e.g. <c>ace_of_spades</c>, <c>10_of_hearts</c> or <see cref="BackImage"/>.</summary>
    public string ImageName => _card is { } c ? ImageNameOf(c) : BackImage;

    /// <summary>The image file name of a card in the Byron Knoll set.</summary>
    public static string ImageNameOf(Card card)
    {
        var rank = card.Rank switch
        {
            Rank.Ace => "ace",
            Rank.King => "king",
            Rank.Queen => "queen",
            Rank.Jack => "jack",
            var r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return $"{rank}_of_{card.Suit.ToString().ToLowerInvariant()}";
    }
}
