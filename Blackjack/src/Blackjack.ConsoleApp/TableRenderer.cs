namespace Blackjack.ConsoleApp;

/// <summary>Shows the table on the console, driven entirely by engine notifications.</summary>
internal sealed class TableRenderer(TextWriter output, CardFormatter formatter) : IGameObserver
{
    private readonly List<Card?> _dealerCards = [];
    private readonly List<List<Card>> _playerHands = [];
    private readonly List<HandSettledNotification> _settled = [];
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
        _playerHands.Clear();
        _settled.Clear();
        _cardsDealt = 0;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        _cardsDealt++;
        var index = notification.Recipient.HandIndex;
        if (notification.Recipient.IsDealer)
        {
            _dealerCards.Add(notification.Card);
        }
        else if (notification.Card is { } card)
        {
            HandCards(index).Add(card);
        }

        // Show the table once the initial deal is complete, then every new card.
        if (_cardsDealt == 4)
        {
            WriteDealer();
            WritePlayer(0);
        }
        else if (_cardsDealt > 4)
        {
            if (notification.Recipient.IsDealer)
            {
                WriteDealer();
            }
            else
            {
                WritePlayer(index);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHandSplitAsync(HandSplitNotification notification, CancellationToken ct)
    {
        var hand = HandCards(notification.HandIndex);
        hand.RemoveAt(hand.Count - 1);
        HandCards(notification.NewHandIndex).Add(notification.MovedCard);
        output.WriteLine($"You split into Hand {notification.HandIndex + 1} and Hand {notification.NewHandIndex + 1}.");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHandDoubledAsync(HandDoubledNotification notification, CancellationToken ct)
    {
        var hand = _playerHands.Count > 1 ? $"Hand {notification.HandIndex + 1} doubled" : "You double";
        output.WriteLine($"{hand}: bet {Money.Format(notification.NewBet)}, one more card.");
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
    public ValueTask OnInsuranceSettledAsync(InsuranceSettledNotification notification, CancellationToken ct)
    {
        output.WriteLine(notification.Net > 0
            ? $"Insurance wins {Money.FormatSigned(notification.Net)}."
            : $"Insurance lost {Money.FormatSigned(notification.Net)}.");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHandSettledAsync(HandSettledNotification notification, CancellationToken ct)
    {
        _settled.Add(notification);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct)
    {
        var bankroll = result.Bankroll is { } b ? Money.Format(b) : "unlimited";
        if (_settled.Count == 1 && result.InsuranceBet == 0)
        {
            output.WriteLine($"Result:  {Describe(_settled[0].Outcome)}  {Money.FormatSigned(result.Net)}   Bankroll: {bankroll}");
        }
        else
        {
            foreach (var hand in _settled)
            {
                output.WriteLine($"{Label(hand.HandIndex)}{Describe(hand.Outcome)}  {Money.FormatSigned(hand.Net)}");
            }

            if (result.InsuranceBet > 0)
            {
                output.WriteLine($"Insurance {(result.InsuranceNet > 0 ? "wins" : "lost")}  {Money.FormatSigned(result.InsuranceNet)}");
            }

            output.WriteLine($"Result:  {Money.FormatSigned(result.Net)}   Bankroll: {bankroll}");
        }

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

    private List<Card> HandCards(int index)
    {
        while (_playerHands.Count <= index)
        {
            _playerHands.Add([]);
        }

        return _playerHands[index];
    }

    // "You:" while there is one hand, "Hand 1:" etc. after a split; always 9 characters wide.
    private string Label(int index) => _playerHands.Count > 1 ? $"Hand {index + 1}:".PadRight(9) : "You:     ";

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

    private void WritePlayer(int index)
    {
        var cards = HandCards(index);
        // A split hand is never a Blackjack, so A + 10 shows as 21.
        var hand = new Hand(cards) { IsFromSplit = _playerHands.Count > 1 };
        output.WriteLine($"{Label(index)}{formatter.Format(cards.Select(c => (Card?)c))}   ({DescribeTotal(hand)})");
    }
}
