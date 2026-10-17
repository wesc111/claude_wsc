using Blackjack.Simulation.Players;
using static Blackjack.Simulation.Players.StrategyCode;

namespace Blackjack.Simulation.Tests;

/// <summary>
/// SPEC 6.2 and 7.6 – every cell of both basic strategy tables. The expected values are written as rules
/// rather than as the table data, so the tests are independent of the data format.
/// </summary>
public sealed class BasicStrategyTableTests
{
    private static readonly string[] Upcards = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "A"];

    private static readonly IReadOnlySet<PlayerAction> All =
        new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand, PlayerAction.Double, PlayerAction.Split };

    private static readonly IReadOnlySet<PlayerAction> HitOrStand =
        new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand };

    public static TheoryData<int, string, string> HitStandHardCells() => Cells(4, 21, HitStandHard);

    public static TheoryData<int, string, string> HitStandSoftCells() => Cells(12, 21, HitStandSoft);

    public static TheoryData<int, string, string> FullHardCells() => Cells(4, 21, FullHard);

    public static TheoryData<int, string, string> FullSoftCells() => Cells(12, 21, FullSoft);

    public static TheoryData<int, string, string> FullPairCells() => Cells(2, 11, FullPair);

    [Theory]
    [MemberData(nameof(HitStandHardCells))]
    public void HitStandHardTotals(int total, string upcard, string expected) =>
        Assert.Equal(expected, BasicStrategyTable.HitStand.Lookup(total, false, Cards.ParseOne(upcard)).ToString());

    [Theory]
    [MemberData(nameof(HitStandSoftCells))]
    public void HitStandSoftTotals(int total, string upcard, string expected) =>
        Assert.Equal(expected, BasicStrategyTable.HitStand.Lookup(total, true, Cards.ParseOne(upcard)).ToString());

    [Theory]
    [MemberData(nameof(FullHardCells))]
    public void FullHardTotals(int total, string upcard, string expected) =>
        Assert.Equal(expected, BasicStrategyTable.Full.Lookup(total, false, Cards.ParseOne(upcard)).ToString());

    [Theory]
    [MemberData(nameof(FullSoftCells))]
    public void FullSoftTotals(int total, string upcard, string expected) =>
        Assert.Equal(expected, BasicStrategyTable.Full.Lookup(total, true, Cards.ParseOne(upcard)).ToString());

    [Theory]
    [MemberData(nameof(FullPairCells))]
    public void FullPairs(int pairValue, string upcard, string expected) =>
        Assert.Equal(expected, BasicStrategyTable.Full.LookupPair(pairValue, Cards.ParseOne(upcard)).ToString());

    [Fact]
    public void HitStandTableNeverSplits() =>
        Assert.Equal(NoSplit, BasicStrategyTable.HitStand.LookupPair(8, Cards.ParseOne("6")));

    [Theory]
    [InlineData("10")]
    [InlineData("J")]
    [InlineData("Q")]
    [InlineData("K")]
    public void AllTenValuedUpcardsShareAColumn(string upcard) =>
        Assert.Equal(Hit, BasicStrategyTable.HitStand.Lookup(16, false, Cards.ParseOne(upcard)));

    [Theory]
    [InlineData("8 8", "10", PlayerAction.Split)]
    [InlineData("A A", "6", PlayerAction.Split)]
    [InlineData("10 10", "6", PlayerAction.Stand)]
    [InlineData("5 5", "6", PlayerAction.Double)]
    [InlineData("6 5", "10", PlayerAction.Double)]
    [InlineData("6 5", "A", PlayerAction.Hit)]
    [InlineData("A 7", "4", PlayerAction.Double)]
    [InlineData("A 7", "2", PlayerAction.Stand)]
    [InlineData("A 7", "9", PlayerAction.Hit)]
    [InlineData("9 9", "7", PlayerAction.Stand)]
    [InlineData("4 4", "6", PlayerAction.Split)]
    [InlineData("4 4", "7", PlayerAction.Hit)]
    public void DecideWithEverythingAllowed(string cards, string upcard, PlayerAction expected) =>
        Assert.Equal(expected, BasicStrategyTable.Full.Decide(Context(cards, upcard, All)));

    [Theory]
    [InlineData("8 8", "10", PlayerAction.Hit)]
    [InlineData("6 5", "10", PlayerAction.Hit)]
    [InlineData("A 7", "4", PlayerAction.Stand)]
    [InlineData("A A", "6", PlayerAction.Hit)]
    public void FallbackWhenDoubleAndSplitAreNotAllowed(string cards, string upcard, PlayerAction expected) =>
        Assert.Equal(expected, BasicStrategyTable.Full.Decide(Context(cards, upcard, HitOrStand)));

    [Fact]
    public void ThreeCardHandsDoNotDouble() =>
        Assert.Equal(PlayerAction.Hit, BasicStrategyTable.Full.Decide(Context("3 2 6", "6", HitOrStand)));

    [Fact]
    public void ResplittableAcesOnlyStandOrSplit()
    {
        var standOrSplit = new HashSet<PlayerAction> { PlayerAction.Stand, PlayerAction.Split };
        var noSplitRules = new BasicStrategyTable(
            [(4, 21, "S S S S S S S S S S")],
            [(12, 21, "H H H H H H H H H H")],
            [(2, 11, "- - - - - - - - - -")]);

        Assert.Equal(PlayerAction.Split, BasicStrategyTable.Full.Decide(Context("A A", "6", standOrSplit)));
        Assert.Equal(PlayerAction.Stand, noSplitRules.Decide(Context("A A", "6", standOrSplit)));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(22, false)]
    [InlineData(11, true)]
    public void TotalOutOfRangeThrows(int total, bool soft) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BasicStrategyTable.HitStand.Lookup(total, soft, Cards.ParseOne("7")));

    [Fact]
    public void RowWithWrongLengthIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 21, "H H H")], [(12, 21, "S S S S S S S S S S")]));

    [Fact]
    public void MissingTotalIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 20, "H H H H H H H H H H")], [(12, 21, "S S S S S S S S S S")]));

    [Fact]
    public void DuplicateTotalIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable(
            [(4, 21, "H H H H H H H H H H"), (12, 12, "S S S S S S S S S S")], [(12, 21, "S S S S S S S S S S")]));

    [Theory]
    [InlineData("H H H H H H H H H X")]
    [InlineData("H H H H H H H H H P")]
    public void UnknownCodeInTotalTableIsRejected(string row) =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 21, row)], [(12, 21, "S S S S S S S S S S")]));

    [Fact]
    public void HitCodeInPairTableIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable(
            [(4, 21, "S S S S S S S S S S")], [(12, 21, "S S S S S S S S S S")], [(2, 11, "H H H H H H H H H H")]));

    // SPEC 6.2 (Hit/Stand only).
    private static StrategyCode HitStandHard(int total, int up) => total switch
    {
        <= 11 => Hit,
        12 => up is >= 4 and <= 6 ? Stand : Hit,
        <= 16 => up <= 6 ? Stand : Hit,
        _ => Stand,
    };

    private static StrategyCode HitStandSoft(int total, int up) => total switch
    {
        <= 17 => Hit,
        18 => up <= 8 ? Stand : Hit,
        _ => Stand,
    };

    // SPEC 7.6 (6 decks, S17, DAS, no surrender).
    private static StrategyCode FullHard(int total, int up) => total switch
    {
        <= 8 => Hit,
        9 => up is >= 3 and <= 6 ? DoubleOrHit : Hit,
        10 => up <= 9 ? DoubleOrHit : Hit,
        11 => up <= 10 ? DoubleOrHit : Hit,
        12 => up is >= 4 and <= 6 ? Stand : Hit,
        <= 16 => up <= 6 ? Stand : Hit,
        _ => Stand,
    };

    private static StrategyCode FullSoft(int total, int up) => total switch
    {
        12 => Hit,
        13 or 14 => up is 5 or 6 ? DoubleOrHit : Hit,
        15 or 16 => up is >= 4 and <= 6 ? DoubleOrHit : Hit,
        17 => up is >= 3 and <= 6 ? DoubleOrHit : Hit,
        18 => up switch { 2 or 7 or 8 => Stand, >= 3 and <= 6 => DoubleOrStand, _ => Hit },
        _ => Stand,
    };

    private static StrategyCode FullPair(int value, int up) => value switch
    {
        2 or 3 => up <= 7 ? Split : NoSplit,
        4 => up is 5 or 6 ? Split : NoSplit,
        6 => up <= 6 ? Split : NoSplit,
        7 => up <= 7 ? Split : NoSplit,
        8 or 11 => Split,
        9 => up is 7 or 10 or 11 ? Stand : Split,
        _ => NoSplit,
    };

    private static TheoryData<int, string, string> Cells(int from, int to, Func<int, int, StrategyCode> expected)
    {
        var data = new TheoryData<int, string, string>();
        for (var row = from; row <= to; row++)
        {
            foreach (var upcard in Upcards)
            {
                data.Add(row, upcard, expected(row, upcard == "A" ? 11 : int.Parse(upcard, System.Globalization.CultureInfo.InvariantCulture)).ToString());
            }
        }

        return data;
    }

    private static DecisionContext Context(string cards, string upcard, IReadOnlySet<PlayerAction> allowed)
    {
        var hand = new Hand(Cards.Parse(cards));
        return new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, Cards.ParseOne(upcard), allowed);
    }
}
