using Blackjack.Simulation.Players;

namespace Blackjack.Simulation.Tests;

/// <summary>SPEC 6.2 – every cell of the Hit/Stand basic strategy tables.</summary>
public sealed class BasicStrategyTableTests
{
    private static readonly string[] Upcards = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "A"];

    public static TheoryData<int, string, PlayerAction> HardCells()
    {
        var data = new TheoryData<int, string, PlayerAction>();
        for (var total = 4; total <= 21; total++)
        {
            foreach (var upcard in Upcards)
            {
                data.Add(total, upcard, ExpectedHard(total, UpcardValue(upcard)));
            }
        }

        return data;
    }

    public static TheoryData<int, string, PlayerAction> SoftCells()
    {
        var data = new TheoryData<int, string, PlayerAction>();
        for (var total = 12; total <= 21; total++)
        {
            foreach (var upcard in Upcards)
            {
                data.Add(total, upcard, ExpectedSoft(total, UpcardValue(upcard)));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(HardCells))]
    public void HardTotals(int total, string upcard, PlayerAction expected) =>
        Assert.Equal(expected, BasicStrategyTable.HitStand.Lookup(total, false, Cards.ParseOne(upcard)));

    [Theory]
    [MemberData(nameof(SoftCells))]
    public void SoftTotals(int total, string upcard, PlayerAction expected) =>
        Assert.Equal(expected, BasicStrategyTable.HitStand.Lookup(total, true, Cards.ParseOne(upcard)));

    [Theory]
    [InlineData("10")]
    [InlineData("J")]
    [InlineData("Q")]
    [InlineData("K")]
    public void AllTenValuedUpcardsShareAColumn(string upcard) =>
        Assert.Equal(PlayerAction.Hit, BasicStrategyTable.HitStand.Lookup(16, false, Cards.ParseOne(upcard)));

    [Theory]
    [InlineData(3, false)]
    [InlineData(22, false)]
    [InlineData(11, true)]
    public void TotalOutOfRangeThrows(int total, bool soft) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BasicStrategyTable.HitStand.Lookup(total, soft, Cards.ParseOne("7")));

    [Fact]
    public void RowWithWrongLengthIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 21, "HHH")], [(12, 21, "SSSSSSSSSS")]));

    [Fact]
    public void MissingTotalIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 20, "HHHHHHHHHH")], [(12, 21, "SSSSSSSSSS")]));

    [Fact]
    public void DuplicateTotalIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 21, "HHHHHHHHHH"), (12, 12, "SSSSSSSSSS")], [(12, 21, "SSSSSSSSSS")]));

    [Fact]
    public void UnknownLetterIsRejected() =>
        Assert.Throws<ArgumentException>(() => new BasicStrategyTable([(4, 21, "HHHHHHHHHX")], [(12, 21, "SSSSSSSSSS")]));

    // The spec tables, written as rules rather than letters so the test is independent of the data format.
    private static PlayerAction ExpectedHard(int total, int upcard) => total switch
    {
        <= 11 => PlayerAction.Hit,
        12 => upcard is >= 4 and <= 6 ? PlayerAction.Stand : PlayerAction.Hit,
        <= 16 => upcard <= 6 ? PlayerAction.Stand : PlayerAction.Hit,
        _ => PlayerAction.Stand,
    };

    private static PlayerAction ExpectedSoft(int total, int upcard) => total switch
    {
        <= 17 => PlayerAction.Hit,
        18 => upcard <= 8 ? PlayerAction.Stand : PlayerAction.Hit,
        _ => PlayerAction.Stand,
    };

    private static int UpcardValue(string upcard) => upcard == "A" ? 11 : int.Parse(upcard, System.Globalization.CultureInfo.InvariantCulture);
}
