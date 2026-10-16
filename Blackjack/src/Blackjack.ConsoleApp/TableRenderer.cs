namespace Blackjack.ConsoleApp;

/// <summary>Shows the table on the console, driven entirely by engine notifications.</summary>
internal sealed class TableRenderer(TextWriter output, CardFormatter formatter) : IGameObserver
{
    private readonly List<Card?> _dealerCards = [];
    private readonly List<Card> _playerCards = [];
    private readonly List<HandOutcome> _outcomes = [];
    private int _cardsDealt;

    /// <inheritdoc />
    public ValueTask OnShoeShuffledAsync(CancellationToken ct)
    {
        output.WriteLine("The shoe is shuffled.");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnRoundStartedAsync(RoundStartedNotification notification, CancellationToken ct)
    {
        _dealerCards.Clear();
        _playerCards.Clear();
        _outcomes.Clear();
        _cardsDealt = 0;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        _cardsDealt++;
        if (notification.Recipient.IsDealer)
        {
            _dealerCards.Add(notification.Card);
        }
        else if (notification.Card is { } card)
        {
            _playerCards.Add(card);
        }

        // Show the table once the initial deal is complete, then every new card.
        if (_cardsDealt == 4)
        {
            WriteDealer();
            WritePlayer();
        }
        else if (_cardsDealt > 4)
        {
            if (notification.Recipient.IsDealer)
            {
                WriteDealer();
            }
            else
            {
                WritePlayer();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct)
    {
        var hidden = _dealerCards.FindIndex(c => c is null);
        if (hidden >= 0)
        {
            _dealerCards[hidden] = notification.Card;
        }

        WriteDealer();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHandSettledAsync(HandSettledNotification notification, CancellationToken ct)
    {
        _outcomes.Add(notification.Outcome);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct)
    {
        var outcome = _outcomes.Count == 1 ? Describe(_outcomes[0]) : "Round over";
        var bankroll = result.Bankroll is { } b ? Money.Format(b) : "unlimited";
        output.WriteLine($"Result:  {outcome}  {Money.FormatSigned(result.Net)}   Bankroll: {bankroll}");
        output.WriteLine();
        return ValueTask.CompletedTask;
    }

    /// <summary>The text shown for an outcome.</summary>
    public static string Describe(HandOutcome outcome) => outcome switch
    {
        HandOutcome.BlackjackWin => "Blackjack! You win",
        HandOutcome.Win => "You win",
        HandOutcome.DealerBust => "Dealer busts – you win",
        HandOutcome.Push => "Push",
        HandOutcome.Loss => "You lose",
        HandOutcome.PlayerBust => "Bust – you lose",
        HandOutcome.DealerBlackjack => "Dealer has Blackjack – you lose",
        _ => outcome.ToString(),
    };

    /// <summary>The total text of a hand, e.g. <c>soft 17</c>, <c>22 bust</c> or <c>Blackjack</c>.</summary>
    public static string DescribeTotal(Hand hand)
    {
        if (hand.IsBlackjack)
        {
            return "Blackjack";
        }

        if (hand.IsBust)
        {
            return $"{hand.Total} bust";
        }

        return hand.IsSoft ? $"soft {hand.Total}" : hand.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void WriteDealer()
    {
        var line = "Dealer:  " + formatter.Format(_dealerCards);
        // The dealer total is only shown once no card is hidden.
        if (_dealerCards.TrueForAll(c => c is not null))
        {
            line += "   (" + DescribeTotal(new Hand(_dealerCards.Select(c => c!.Value))) + ")";
        }

        output.WriteLine(line);
    }

    private void WritePlayer()
    {
        var cards = formatter.Format(_playerCards.Select(c => (Card?)c));
        output.WriteLine($"You:     {cards}   ({DescribeTotal(new Hand(_playerCards))})");
    }
}
