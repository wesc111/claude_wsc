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
    private static readonly IReadOnlySet<PlayerAction> HitOrStand =
        new[] { PlayerAction.Hit, PlayerAction.Stand }.ToFrozenSet();

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

        // A list of hands although M1 only ever uses one; splits (M4) will add more.
        var hands = new List<Hand> { new() };
        var dealer = new Hand();

        await DealToPlayerAsync(hands, 0, ct);
        var upcard = await DealToDealerAsync(dealer, ct);
        await DealToPlayerAsync(hands, 0, ct);
        var holeCard = _cards.Draw();
        dealer.Add(holeCard);
        await NotifyAsync(static (o, n, c) => o.OnCardDealtAsync(n, c), new CardDealtNotification(CardRecipient.Dealer, null, false), ct);

        var peekable = upcard.IsAce || upcard.IsTenValued;
        if (peekable && dealer.IsBlackjack)
        {
            await RevealHoleCardAsync(holeCard, ct);
            await FinishHandsAsync(hands, ct);
        }
        else if (hands[0].IsBlackjack)
        {
            await FinishHandsAsync(hands, ct);
            await RevealHoleCardAsync(holeCard, ct);
        }
        else
        {
            for (var i = 0; i < hands.Count; i++)
            {
                await PlayHandAsync(player, hands, i, upcard, ct);
                await FinishHandAsync(hands, i, ct);
            }

            await RevealHoleCardAsync(holeCard, ct);
            if (hands.Exists(h => !h.IsBust))
            {
                while (_dealerStrategy.ShouldHit(dealer))
                {
                    await DealToDealerAsync(dealer, ct);
                }
            }
        }

        return await SettleAsync(hands, bet, dealer, ct);
    }

    private void ValidateBet(decimal bet)
    {
        if (bet < _rules.MinBet || bet > _rules.MaxBet)
        {
            throw new ArgumentOutOfRangeException(nameof(bet), bet, $"The bet must be between {_rules.MinBet} and {_rules.MaxBet}.");
        }

        if (Bankroll is { } available && bet > available)
        {
            throw new ArgumentOutOfRangeException(nameof(bet), bet, $"The bet exceeds the bankroll ({available}).");
        }
    }

    private async Task PlayHandAsync(IPlayerStrategy player, List<Hand> hands, int index, Card upcard, CancellationToken ct)
    {
        var hand = hands[index];
        while (!hand.IsBust && hand.Total < 21)
        {
            var context = new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, upcard, HitOrStand);
            var action = await player.DecideAsync(context, ct);
            ct.ThrowIfCancellationRequested();
            if (!context.AllowedActions.Contains(action))
            {
                throw new InvalidOperationException($"The strategy returned the action '{action}', which is not allowed.");
            }

            if (action == PlayerAction.Stand)
            {
                return;
            }

            await DealToPlayerAsync(hands, index, ct);
        }
    }

    private async Task<RoundResult> SettleAsync(List<Hand> hands, decimal bet, Hand dealer, CancellationToken ct)
    {
        var results = new List<HandResult>(hands.Count);
        var net = 0m;
        for (var i = 0; i < hands.Count; i++)
        {
            var hand = hands[i];
            var outcome = Settle(hand, dealer);
            var handNet = outcome switch
            {
                HandOutcome.BlackjackWin => bet * _rules.BlackjackPayout,
                HandOutcome.Win or HandOutcome.DealerBust => bet,
                HandOutcome.Push => 0m,
                _ => -bet,
            };
            net += handNet;
            results.Add(new HandResult(hand.Cards.ToList().AsReadOnly(), hand.Total, hand.IsSoft, bet, outcome, handNet));
            await NotifyAsync(static (o, n, c) => o.OnHandSettledAsync(n, c), new HandSettledNotification(i, outcome, handNet), ct);
        }

        Bankroll += net;
        var result = new RoundResult(
            bet * hands.Count,
            results.AsReadOnly(),
            dealer.Cards.ToList().AsReadOnly(),
            dealer.Total,
            net,
            Bankroll);
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

    private async Task DealToPlayerAsync(List<Hand> hands, int index, CancellationToken ct)
    {
        var card = _cards.Draw();
        hands[index].Add(card);
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

    private async Task FinishHandsAsync(List<Hand> hands, CancellationToken ct)
    {
        for (var i = 0; i < hands.Count; i++)
        {
            await FinishHandAsync(hands, i, ct);
        }
    }

    private Task FinishHandAsync(List<Hand> hands, int index, CancellationToken ct)
    {
        var hand = hands[index];
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
