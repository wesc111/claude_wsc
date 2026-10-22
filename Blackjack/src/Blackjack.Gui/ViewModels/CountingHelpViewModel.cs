using Blackjack.Core;

namespace Blackjack.Gui.ViewModels;

/// <summary>
/// The tables of the counting help window (spec E14). They are built from the Core counting code, so the help always
/// shows what the count trainer calculates.
/// </summary>
public sealed class CountingHelpViewModel
{
    // The true counts at which the bet spread of CountingStrategy.BetUnits changes.
    private static readonly decimal[] SpreadSteps = [2m, 3m, 4m, 5m];

    /// <summary>The Hi-Lo values, highest first: <c>2 3 4 5 6</c> +1, <c>7 8 9</c> 0, <c>10 J Q K A</c> -1.</summary>
    public IReadOnlyList<HelpCardValue> CardValues { get; } =
        Enum.GetValues<Rank>()
            .GroupBy(rank => HiLoCounter.Value(new Card(rank, Suit.Spades)))
            .OrderByDescending(group => group.Key)
            .Select(group => new HelpCardValue(
                string.Join(" ", group.Select(rank => new Card(rank, Suit.Spades).ToString()[..^1])),
                DisplayText.Count(group.Key)))
            .ToList();

    /// <summary>The bet spread of <see cref="CountingStrategy.BetUnits"/>, from <c>below +2</c> to <c>+5 and above</c>.</summary>
    public IReadOnlyList<HelpBetRow> BetSpread { get; } = CreateBetSpread();

    /// <summary>Insurance first, then the 17 index plays of <see cref="CountingStrategy.IndexPlays"/> in their order.</summary>
    public IReadOnlyList<HelpIndexPlay> IndexPlays { get; } =
    [
        new("Insurance", "A", "No", "Take", When(CountingStrategy.InsuranceIndex, atOrAbove: true)),
        .. CountingStrategy.IndexPlays.Select(play => new HelpIndexPlay(
            play.IsPair ? $"{play.Total},{play.Total}" : $"Hard {play.Total}",
            play.Upcard == 11 ? "A" : play.Upcard.ToString(System.Globalization.CultureInfo.InvariantCulture),
            play.Basic.ToString(),
            play.Deviation.ToString(),
            When(play.Index, play.AtOrAbove))),
    ];

    private static string When(decimal index, bool atOrAbove) => $"TC {(atOrAbove ? "≥" : "<")} {DisplayText.Index(index)}";

    private static List<HelpBetRow> CreateBetSpread()
    {
        var rows = new List<HelpBetRow> { new($"below {DisplayText.Index(SpreadSteps[0])}", Units(SpreadSteps[0] - 1m)) };
        for (var i = 0; i < SpreadSteps.Length; i++)
        {
            var range = i + 1 < SpreadSteps.Length
                ? $"{DisplayText.Index(SpreadSteps[i])} to below {DisplayText.Index(SpreadSteps[i + 1])}"
                : $"{DisplayText.Index(SpreadSteps[i])} and above";
            rows.Add(new HelpBetRow(range, Units(SpreadSteps[i])));
        }

        return rows;
    }

    private static string Units(decimal trueCount) =>
        CountingStrategy.BetUnits(trueCount) is var units && units == 1 ? "1 unit" : $"{units} units";
}
