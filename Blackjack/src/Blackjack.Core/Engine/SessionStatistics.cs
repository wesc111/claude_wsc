namespace Blackjack.Core;

/// <summary>
/// Counts the completed rounds of a playing session (console and GUI). Record only the results that
/// <see cref="BlackjackGame.PlayRoundAsync"/> returns, so aborted rounds are never counted.
/// Wins, losses, pushes, player busts and doubles are counted per hand; Blackjacks, dealer busts and splits per round.
/// </summary>
public sealed class SessionStatistics
{
    private decimal? _startingBankroll;

    /// <summary>Creates empty statistics.</summary>
    /// <param name="startingBankroll">
    /// The bankroll at the start of the session; if <c>null</c>, it is derived from the first round.
    /// </param>
    public SessionStatistics(decimal? startingBankroll = null)
    {
        _startingBankroll = startingBankroll;
        HighestBankroll = startingBankroll;
        LowestBankroll = startingBankroll;
        FinalBankroll = startingBankroll;
    }

    /// <summary>Number of completed rounds.</summary>
    public int Rounds { get; private set; }

    /// <summary>Number of player hands (more than rounds after splits).</summary>
    public int Hands { get; private set; }

    /// <summary>Number of won hands.</summary>
    public int Wins { get; private set; }

    /// <summary>Number of lost hands.</summary>
    public int Losses { get; private set; }

    /// <summary>Number of pushed hands.</summary>
    public int Pushes { get; private set; }

    /// <summary>Number of rounds in which the player was dealt a Blackjack.</summary>
    public int Blackjacks { get; private set; }

    /// <summary>Number of busted player hands.</summary>
    public int PlayerBusts { get; private set; }

    /// <summary>Number of rounds in which the dealer busted.</summary>
    public int DealerBusts { get; private set; }

    /// <summary>Number of doubled hands.</summary>
    public int Doubles { get; private set; }

    /// <summary>Number of doubled hands that won.</summary>
    public int DoublesWon { get; private set; }

    /// <summary>Number of rounds with at least one split.</summary>
    public int Splits { get; private set; }

    /// <summary>Number of rounds in which insurance was taken (SPEC E12).</summary>
    public int Insurances { get; private set; }

    /// <summary>Number of insurance bets that won (dealer Blackjack).</summary>
    public int InsurancesWon { get; private set; }

    /// <summary>Sum of the net amounts of all insurance bets; included in <see cref="Net"/>.</summary>
    public decimal InsuranceNet { get; private set; }

    /// <summary>Sum of the initial bets (before Double and Split).</summary>
    public decimal InitialBets { get; private set; }

    /// <summary>Sum of all bets, including the additional bets of Double and Split.</summary>
    public decimal TotalWagered { get; private set; }

    /// <summary>Sum of the net results.</summary>
    public decimal Net { get; private set; }

    /// <summary>Net result in % of the initial bets; <c>null</c> without rounds.</summary>
    public decimal? NetPercentOfInitialBets => InitialBets == 0 ? null : Net / InitialBets * 100;

    /// <summary>The bankroll at the start of the session, or <c>null</c> if unknown (unlimited bankroll, no rounds).</summary>
    public decimal? StartingBankroll => _startingBankroll;

    /// <summary>The bankroll after the last round, or the starting bankroll without rounds.</summary>
    public decimal? FinalBankroll { get; private set; }

    /// <summary>The highest bankroll during the session, including the start.</summary>
    public decimal? HighestBankroll { get; private set; }

    /// <summary>The lowest bankroll during the session, including the start.</summary>
    public decimal? LowestBankroll { get; private set; }

    /// <summary>The largest positive round result, or <c>null</c> if no round was won.</summary>
    public decimal? BiggestWin { get; private set; }

    /// <summary>The round (1-based) of <see cref="BiggestWin"/>.</summary>
    public int? BiggestWinRound { get; private set; }

    /// <summary>The largest negative round result, or <c>null</c> if no round was lost.</summary>
    public decimal? BiggestLoss { get; private set; }

    /// <summary>The round (1-based) of <see cref="BiggestLoss"/>.</summary>
    public int? BiggestLossRound { get; private set; }

    /// <summary>Records a completed round.</summary>
    /// <param name="result">The result returned by <see cref="BlackjackGame.PlayRoundAsync"/>.</param>
    public void Record(RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Rounds++;
        Net += result.Net;
        InitialBets += result.InitialBet;
        TotalWagered += result.TotalBet;
        if (result.InsuranceBet > 0)
        {
            Insurances++;
            InsuranceNet += result.InsuranceNet;
            if (result.InsuranceNet > 0)
            {
                InsurancesWon++;
            }
        }

        foreach (var hand in result.PlayerHands)
        {
            Hands++;
            if (hand.Outcome.IsWin())
            {
                Wins++;
            }
            else if (hand.Outcome.IsLoss())
            {
                Losses++;
            }
            else
            {
                Pushes++;
            }

            if (hand.Outcome == HandOutcome.PlayerBust)
            {
                PlayerBusts++;
            }

            if (hand.IsDoubled)
            {
                Doubles++;
                if (hand.Outcome.IsWin())
                {
                    DoublesWon++;
                }
            }
        }

        // A + 10 after a split is no Blackjack (SPEC 7.1); the dealer total exceeds 21 only if the dealer played out.
        var first = result.PlayerHands[0];
        if (result.PlayerHands.Count == 1 && !first.IsFromSplit && first.Cards.Count == 2 && first.Total == 21)
        {
            Blackjacks++;
        }

        if (result.DealerTotal > 21)
        {
            DealerBusts++;
        }

        if (result.PlayerHands.Count > 1)
        {
            Splits++;
        }

        if (result.Net > 0 && (BiggestWin is null || result.Net > BiggestWin))
        {
            BiggestWin = result.Net;
            BiggestWinRound = Rounds;
        }

        if (result.Net < 0 && (BiggestLoss is null || result.Net < BiggestLoss))
        {
            BiggestLoss = result.Net;
            BiggestLossRound = Rounds;
        }

        if (result.Bankroll is { } bankroll)
        {
            _startingBankroll ??= bankroll - result.Net;
            HighestBankroll = Max(Max(HighestBankroll, _startingBankroll), bankroll);
            LowestBankroll = Min(Min(LowestBankroll, _startingBankroll), bankroll);
            FinalBankroll = bankroll;
        }
    }

    private static decimal? Max(decimal? a, decimal? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private static decimal? Min(decimal? a, decimal? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
}
