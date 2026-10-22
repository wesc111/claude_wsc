using Blackjack.Core;
using Blackjack.Gui.Settings;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E14: the counting help; its tables come from the Core counting code.</summary>
public sealed class CountingHelpTests
{
    private const string FiveRounds = "10 10 7 7 10 10 7 7 10 10 7 7 10 10 7 7 10 10 7 7";

    private static readonly CountingHelpViewModel Help = new();

    [Fact]
    public void TheCardValuesAreTheHiLoValues()
    {
        Assert.Equal(
            [new HelpCardValue("2 3 4 5 6", "+1"), new HelpCardValue("7 8 9", "0"), new HelpCardValue("10 J Q K A", "-1")],
            Help.CardValues);
    }

    [Fact]
    public void EveryRankIsShownOnceWithItsCounterValue()
    {
        var shown = Help.CardValues
            .SelectMany(row => row.Cards.Split(' ').Select(rank => (rank, row.Value)))
            .ToList();

        Assert.Equal(13, shown.Count);
        foreach (var rank in Enum.GetValues<Rank>())
        {
            var card = new Card(rank, Suit.Hearts);
            Assert.Contains((card.ToString()[..^1], DisplayText.Count(HiLoCounter.Value(card))), shown);
        }
    }

    [Fact]
    public void TheBetSpreadIsThatOfTheCountingStrategy()
    {
        Assert.Equal(
            [
                new HelpBetRow("below +2", "1 unit"),
                new HelpBetRow("+2 to below +3", "2 units"),
                new HelpBetRow("+3 to below +4", "4 units"),
                new HelpBetRow("+4 to below +5", "6 units"),
                new HelpBetRow("+5 and above", "8 units"),
            ],
            Help.BetSpread);
        Assert.Equal([1, 1, 2, 2, 4, 4, 6, 6, 8, 8],
            new[] { -5m, 1.99m, 2m, 2.99m, 3m, 3.99m, 4m, 4.99m, 5m, 12m }.Select(CountingStrategy.BetUnits));
    }

    [Fact]
    public void TheIndexPlaysAreInsuranceAndTheIllustrious18()
    {
        Assert.Equal(18, Help.IndexPlays.Count);
        Assert.Equal(new HelpIndexPlay("Insurance", "A", "No", "Take", "TC ≥ +3"), Help.IndexPlays[0]);
        for (var i = 0; i < CountingStrategy.IndexPlays.Count; i++)
        {
            var play = CountingStrategy.IndexPlays[i];
            var row = Help.IndexPlays[i + 1];
            Assert.Equal(play.Basic.ToString(), row.Basic);
            Assert.Equal(play.Deviation.ToString(), row.CountPlay);
            Assert.Equal($"TC {(play.AtOrAbove ? "≥" : "<")} {DisplayText.Index(play.Index)}", row.When);
        }
    }

    [Theory]
    [InlineData(1, "Hard 16", "10", "Hit", "Stand", "TC ≥ 0")]
    [InlineData(3, "10,10", "5", "Stand", "Split", "TC ≥ +5")]
    [InlineData(8, "Hard 11", "A", "Hit", "Double", "TC ≥ +1")]
    [InlineData(13, "Hard 13", "2", "Stand", "Hit", "TC < -1")]
    [InlineData(14, "Hard 12", "4", "Stand", "Hit", "TC < 0")]
    public void AnIndexPlayIsShownAsAReadableRow(int row, string hand, string dealer, string basic, string countPlay, string when) =>
        Assert.Equal(new HelpIndexPlay(hand, dealer, basic, countPlay, when), Help.IndexPlays[row]);

    [Theory]
    [InlineData(CountTrainerMode.Off, false)]
    [InlineData(CountTrainerMode.ShowCount, true)]
    [InlineData(CountTrainerMode.Quiz, false)]
    public void TheHelpOpensWhileTheCountIsShown(CountTrainerMode trainer, bool canOpen)
    {
        var t = new Table(1000m, FiveRounds, "");
        t.ViewModel.CountTrainer = trainer;
        t.Do(t.ViewModel.Start);

        Assert.Equal(canOpen, t.ViewModel.CanOpenCountingHelp);
    }

    [Fact]
    public void TheHelpOpensFromTheCountQuiz()
    {
        var t = new Table(1000m, FiveRounds, "");
        t.ViewModel.CountTrainer = CountTrainerMode.Quiz;
        t.Do(t.ViewModel.Start);
        for (var i = 0; i < 5; i++)
        {
            t.Execute(t.ViewModel.DealCommand);
            t.Execute(t.ViewModel.StandCommand);
        }

        Assert.True(t.ViewModel.IsQuizOpen);
        Assert.True(t.ViewModel.CanOpenCountingHelp);
    }
}
