namespace Blackjack.Core.Tests;

public sealed class HandOutcomeExtensionsTests
{
    [Theory]
    [InlineData(HandOutcome.BlackjackWin, true, false)]
    [InlineData(HandOutcome.Win, true, false)]
    [InlineData(HandOutcome.DealerBust, true, false)]
    [InlineData(HandOutcome.Push, false, false)]
    [InlineData(HandOutcome.Loss, false, true)]
    [InlineData(HandOutcome.PlayerBust, false, true)]
    [InlineData(HandOutcome.DealerBlackjack, false, true)]
    public void Classification(HandOutcome outcome, bool isWin, bool isLoss)
    {
        Assert.Equal(isWin, outcome.IsWin());
        Assert.Equal(isLoss, outcome.IsLoss());
    }

    [Fact]
    public void EveryOutcomeIsCovered() =>
        Assert.Equal(7, Enum.GetValues<HandOutcome>().Length);
}
