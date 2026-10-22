using System.Collections.Frozen;

namespace Blackjack.Core;

/// <summary>
/// The Blackjack engine: plays rounds between one player and the dealer, settles them and keeps the bankroll.
/// It reports the course of each round to its <see cref="IGameObserver"/>s and never produces output itself.
/// </summary>
/// <remarks>
/// Awaits deliberately keep the caller's synchronization context (no <c>ConfigureAwait(false)</c>),
/// so a GUI that starts a round on the UI thread receives its notifications there.
/// </remarks>
public sealed class BlackjackGame
{
    // One cached set per combination of Hit, Stand, Double and Split (bit 0 = Hit … bit 3 = Split).
    private static readonly IReadOnlySet<PlayerAction>[] ActionSets =
    [
        .. Enumerable.Range(0, 16).Select(mask => (IReadOnlySet<PlayerAction>)new[]
            {
                PlayerAction.Hit, PlayerAction.Stand, PlayerAction.Double, PlayerAction.Split,
            }
            .Where((_, bit) => (mask & (1 << bit)) != 0)
            .ToFrozenSet()),
    ];

    private readonly Rules _rules;
    private readonly ICardSource _cards;
    private readonly DealerStrategy _dealerStrategy;
    private readonly IGameObserver[] _observers;
    private int _roundRunning;

    /// <summary>Creates a game.</summary>
    /// <param name="rules">The table rules.</param>
    /// <param name="cards">The card source, usually a <see cref="Shoe"/>.</param>
    /// <param name="startingBankroll">The player's starting bankroll; <c>null</c> = unlimited (simulation mode).</param>
    /// <param name="observers">Observers notified in this order; may be <c>null</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Rules.MaxBet"/> is less than <see cref="Rules.MinBet"/>.</exception>
    /// <exception cref="NotSupportedException"><see cref="Rules.DealerPeeks"/> is <c>false</c>.</exception>
    public BlackjackGame(Rules rules, ICardSource cards, decimal? startingBankroll,
                         IEnumerable<IGameObserver>? observers = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(cards);
        if (rules.MaxBet < rules.MinBet)
        {
            throw new ArgumentOutOfRangeException(nameof(rules), rules.MaxBet, "MaxBet must not be less than MinBet.");
        }

        if (!rules.DealerPeeks)
        {
            throw new NotSupportedException("No-peek (ENHC) rules are not supported.");
        }

        _rules = rules;
        _cards = cards;
        _dealerStrategy = new DealerStrategy(rules);
        _observers = observers?.ToArray() ?? [];
        if (Array.Exists(_observers, o => o is null))
        {
            throw new ArgumentException("Observers must not contain null.", nameof(observers));
        }

        Bankroll = startingBankroll;
    }

    /// <summary>The current bankroll, or <c>null</c> if unlimited.</summary>
    public decimal? Bankroll { get; private set; }

    /// <summary>Plays one round.</summary>
    /// <param name="player">Makes the player's decisions.</param>
    /// <param name="ct">Aborts the round; the bankroll is then restored.</param>
    /// <returns>The result of the round, or <c>null</c> if the player left the table.</returns>
    /// <exception cref="InvalidOperationException">
    /// A round is already running, the bankroll is below <see cref="Rules.MinBet"/>,
    /// the strategy returned an action that is not allowed, or the card source is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The bet is outside the limits or exceeds the bankroll.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async Task<RoundResult?> PlayRoundAsync(IPlayerStrategy player, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Interlocked.Exchange(ref _roundRunning, 1) == 1)
        {
            throw new InvalidOperationException("A round is already running.");
        }

        var bankrollBefore = Bankroll;
        try
        {
            return await PlayRoundCoreAsync(player, ct);
        }
        catch
        {
            // Any abort (cancellation, illegal action, observer error) leaves the bankroll as it was.
            Bankroll = bankrollBefore;
            throw;
        }
        finally
        {
            Volatile.Write(ref _roundRunning, 0);
        }
    }

    private async Task<RoundResult?> PlayRoundCoreAsync(IPlayerStrategy player, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Bankroll is { } available && available < _rules.MinBet)
        {
            throw new InvalidOperationException($"The bankroll ({available}) is below the minimum bet ({_rules.MinBet}).");
        }

        if (_cards.NeedsReshuffle)
        {
            _cards.Shuffle();
            await NotifyAsync(static (o, _, c) => o.OnShoeShuffledAsync(c), 0, ct);
        }

        var requestedBet = await player.GetBetAsync(new BetContext(Bankroll, _rules), ct);
        ct.ThrowIfCancellationRequested();
        if (requestedBet is not { } bet)
        {
            return null;
        }

        ValidateBet(bet);
        await NotifyAsync(static (o, n, c) => o.OnRoundStartedAsync(n, c), new RoundStartedNotification(bet, Bankroll), ct);

        // Splits append new hands at the end, so the index of a hand never changes.
        var hands = new List<PlayerHand> { new(new Hand(), bet) };
        var dealer = new Hand();

        await DealToPlayerAsync(hands, 0, ct);
        var upcard = await DealToDealerAsync(dealer, ct);
        await DealToPlayerAsync(hands, 0, ct);
        var holeCard = _cards.Draw();
        dealer.Add(holeCard);
        await NotifyAsync(static (o, n, c) => o.OnCardDealtAsync(n, c), new CardDealtNotification(CardRecipient.Dealer, null, false), ct);

        var insurance = await OfferInsuranceAsync(player, hands[0].Hand, upcard, bet, ct);

        // Peek: a dealer Blackjack ends the round before any decision, so only the original bet is at stake.
        var peekable = upcard.IsAce || upcard.IsTenValued;
        if (peekable && dealer.IsBlackjack)
        {
            await RevealHoleCardAsync(holeCard, ct);
            await SettleInsuranceAsync(insurance, dealer, ct);
            await FinishHandsAsync(hands, ct);
        }
        else if (hands[0].Hand.IsBlackjack)
        {
            await SettleInsuranceAsync(insurance, dealer, ct);
            await FinishHandsAsync(hands, ct);
            await RevealHoleCardAsync(holeCard, ct);
        }
        else
        {
            await SettleInsuranceAsync(insurance, dealer, ct);
            for (var i = 0; i < hands.Count; i++)
            {
                await PlayHandAsync(player, hands, i, upcard, insurance, ct);
                await FinishHandAsync(hands, i, ct);
            }

            await RevealHoleCardAsync(holeCard, ct);
            if (hands.Exists(h => !h.Hand.IsBust))
            {
                while (_dealerStrategy.ShouldHit(dealer))
                {
                    await DealToDealerAsync(dealer, ct);
                }
            }
        }

        return await SettleAsync(hands, bet, insurance, dealer, ct);
    }

    // Insurance (SPEC E12): offered on a dealer Ace if the bankroll covers the bet plus half of it; returns the insurance bet or 0.
    private async Task<decimal> OfferInsuranceAsync(IPlayerStrategy player, Hand hand, Card upcard, decimal bet, CancellationToken ct)
    {
        var amount = bet / 2;
        if (!upcard.IsAce || (Bankroll is { } available && bet + amount > available))
        {
            return 0m;
        }

        var context = new InsuranceContext(hand.Cards.ToArray(), hand.Total, hand.IsBlackjack, upcard, amount);
        var taken = await player.DecideInsuranceAsync(context, ct);
        ct.ThrowIfCancellationRequested();
        return taken ? amount : 0m;
    }

    private Task SettleInsuranceAsync(decimal insurance, Hand dealer, CancellationToken ct) =>
        insurance == 0m
            ? Task.CompletedTask
            : NotifyAsync(static (o, n, c) => o.OnInsuranceSettledAsync(n, c),
                          new InsuranceSettledNotification(insurance, InsuranceNet(insurance, dealer)), ct);

    private static decimal InsuranceNet(decimal insurance, Hand dealer) => dealer.IsBlackjack ? 2 * insurance : -insurance;

    private void ValidateBet(decimal bet)
    {
        var context = new BetContext(Bankroll, _rules);
        if (!context.IsValidBet(bet))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bet), bet, $"The bet must be between {_rules.MinBet} and {context.MaxAllowedBet}.");
        }
    }

    private async Task PlayHandAsync(IPlayerStrategy player, List<PlayerHand> hands, int index, Card upcard, decimal insurance, CancellationToken ct)
    {
        // A hand created by a split receives its second card when its turn starts.
        if (hands[index].Hand.Cards.Count == 1)
        {
            await DealToPlayerAsync(hands, index, ct);
        }

        while (!hands[index].Hand.IsBust && hands[index].Hand.Total < 21)
        {
            var current = hands[index];
            var allowed = AllowedActions(hands, index, insurance);

            // Split Aces without a possible re-split: the turn ends after their one card.
            if (!allowed.Contains(PlayerAction.Hit) && !allowed.Contains(PlayerAction.Split))
            {
                return;
            }

            var hand = current.Hand;
            var context = new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, upcard, allowed, index, hands.Count);
            var action = await player.DecideAsync(context, ct);
            ct.ThrowIfCancellationRequested();
            if (!allowed.Contains(action))
            {
                throw new InvalidOperationException($"The strategy returned the action '{action}', which is not allowed.");
            }

            switch (action)
            {
                case PlayerAction.Stand:
                    return;
                case PlayerAction.Hit:
                    await DealToPlayerAsync(hands, index, ct);
                    break;
                case PlayerAction.Double:
                    current.Bet *= 2;
                    current.IsDoubled = true;
                    await NotifyAsync(static (o, n, c) => o.OnHandDoubledAsync(n, c), new HandDoubledNotification(index, current.Bet), ct);
                    await DealToPlayerAsync(hands, index, ct);
                    return;
                case PlayerAction.Split:
                    await SplitAsync(hands, index, ct);
                    break;
            }
        }
    }

    private async Task SplitAsync(List<PlayerHand> hands, int index, CancellationToken ct)
    {
        var current = hands[index];
        var first = current.Hand.Cards[0];
        var moved = current.Hand.Cards[1];
        var aces = first.IsAce;

        hands[index] = new PlayerHand(new Hand([first]) { IsFromSplit = true }, current.Bet) { IsSplitAces = aces };
        hands.Add(new PlayerHand(new Hand([moved]) { IsFromSplit = true }, current.Bet) { IsSplitAces = aces });
        await NotifyAsync(static (o, n, c) => o.OnHandSplitAsync(n, c), new HandSplitNotification(index, hands.Count - 1, moved), ct);
        await DealToPlayerAsync(hands, index, ct);
    }

    private IReadOnlySet<PlayerAction> AllowedActions(List<PlayerHand> hands, int index, decimal insurance)
    {
        var current = hands[index];
        var hand = current.Hand;

        // A lost insurance bet is no longer available for Double and Split.
        var committed = insurance;
        foreach (var h in hands)
        {
            committed += h.Bet;
        }

        // Double and Split both add the hand's bet once more.
        var covered = Bankroll is not { } available || committed + current.Bet <= available;
        var twoCards = hand.Cards.Count == 2;

        var canHit = !current.IsSplitAces;
        var canDouble = twoCards && covered && !current.IsSplitAces && (!hand.IsFromSplit || _rules.DoubleAfterSplit);
        var canSplit = twoCards && covered && hand.IsPair && hands.Count < _rules.MaxSplitHands
                       && (!current.IsSplitAces || _rules.ResplitAces);

        var mask = (canHit ? 1 : 0) | 2 | (canDouble ? 4 : 0) | (canSplit ? 8 : 0);
        return ActionSets[mask];
    }

    private async Task<RoundResult> SettleAsync(List<PlayerHand> hands, decimal initialBet, decimal insurance, Hand dealer, CancellationToken ct)
    {
        var results = new List<HandResult>(hands.Count);
        var insuranceNet = insurance == 0m ? 0m : InsuranceNet(insurance, dealer);
        var net = insuranceNet;
        var totalBet = 0m;
        for (var i = 0; i < hands.Count; i++)
        {
            var (hand, bet) = (hands[i].Hand, hands[i].Bet);
            var outcome = Settle(hand, dealer);
            var handNet = outcome switch
            {
                HandOutcome.BlackjackWin => bet * _rules.BlackjackPayout,
                HandOutcome.Win or HandOutcome.DealerBust => bet,
                HandOutcome.Push => 0m,
                _ => -bet,
            };
            net += handNet;
            totalBet += bet;
            results.Add(new HandResult(
                hand.Cards.ToList().AsReadOnly(), hand.Total, hand.IsSoft, bet, outcome, handNet, hands[i].IsDoubled, hand.IsFromSplit));
            await NotifyAsync(static (o, n, c) => o.OnHandSettledAsync(n, c), new HandSettledNotification(i, outcome, handNet), ct);
        }

        Bankroll += net;
        var result = new RoundResult(
            totalBet,
            results.AsReadOnly(),
            dealer.Cards.ToList().AsReadOnly(),
            dealer.Total,
            net,
            Bankroll,
            initialBet,
            insurance,
            insuranceNet);
        await NotifyAsync(static (o, r, c) => o.OnRoundCompletedAsync(r, c), result, ct);
        return result;
    }

    private static HandOutcome Settle(Hand hand, Hand dealer)
    {
        if (dealer.IsBlackjack)
        {
            return hand.IsBlackjack ? HandOutcome.Push : HandOutcome.DealerBlackjack;
        }

        if (hand.IsBlackjack)
        {
            return HandOutcome.BlackjackWin;
        }

        if (hand.IsBust)
        {
            return HandOutcome.PlayerBust;
        }

        if (dealer.IsBust)
        {
            return HandOutcome.DealerBust;
        }

        return hand.Total.CompareTo(dealer.Total) switch
        {
            > 0 => HandOutcome.Win,
            < 0 => HandOutcome.Loss,
            _ => HandOutcome.Push,
        };
    }

    private async Task DealToPlayerAsync(List<PlayerHand> hands, int index, CancellationToken ct)
    {
        var card = _cards.Draw();
        hands[index].Hand.Add(card);
        await NotifyAsync(static (o, n, c) => o.OnCardDealtAsync(n, c), new CardDealtNotification(CardRecipient.Player(index), card, true), ct);
    }

    private async Task<Card> DealToDealerAsync(Hand dealer, CancellationToken ct)
    {
        var card = _cards.Draw();
        dealer.Add(card);
        await NotifyAsync(static (o, n, c) => o.OnCardDealtAsync(n, c), new CardDealtNotification(CardRecipient.Dealer, card, true), ct);
        return card;
    }

    private Task RevealHoleCardAsync(Card holeCard, CancellationToken ct) =>
        NotifyAsync(static (o, n, c) => o.OnHoleCardRevealedAsync(n, c), new HoleCardRevealedNotification(holeCard), ct);

    private async Task FinishHandsAsync(List<PlayerHand> hands, CancellationToken ct)
    {
        for (var i = 0; i < hands.Count; i++)
        {
            await FinishHandAsync(hands, i, ct);
        }
    }

    private Task FinishHandAsync(List<PlayerHand> hands, int index, CancellationToken ct)
    {
        var hand = hands[index].Hand;
        var notification = new PlayerHandFinishedNotification(index, hand.Total, hand.IsSoft, hand.IsBlackjack, hand.IsBust);
        return NotifyAsync(static (o, n, c) => o.OnPlayerHandFinishedAsync(n, c), notification, ct);
    }

    private async Task NotifyAsync<T>(Func<IGameObserver, T, CancellationToken, ValueTask> notify, T argument, CancellationToken ct)
    {
        foreach (var observer in _observers)
        {
            ct.ThrowIfCancellationRequested();
            await notify(observer, argument, ct);
        }
    }
}
