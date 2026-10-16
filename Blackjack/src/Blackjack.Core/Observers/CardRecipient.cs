namespace Blackjack.Core;

/// <summary>Who receives a card: the dealer or a player hand.</summary>
public readonly record struct CardRecipient
{
    private CardRecipient(bool isDealer, int handIndex)
    {
        IsDealer = isDealer;
        HandIndex = handIndex;
    }

    /// <summary>The dealer.</summary>
    public static CardRecipient Dealer { get; } = new(true, -1);

    /// <summary>True if the card goes to the dealer.</summary>
    public bool IsDealer { get; }

    /// <summary>Index of the player hand, or -1 for the dealer.</summary>
    public int HandIndex { get; }

    /// <summary>The player hand with index <paramref name="handIndex"/>.</summary>
    /// <param name="handIndex">Zero-based hand index.</param>
    public static CardRecipient Player(int handIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handIndex);
        return new CardRecipient(false, handIndex);
    }

    /// <inheritdoc />
    public override string ToString() => IsDealer ? "Dealer" : $"Player hand {HandIndex}";
}
