namespace Blackjack.ConsoleApp.Tests;

public sealed class TableRendererTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StringWriter _output = new() { NewLine = "\n" };
    private readonly TableRenderer _renderer;

    public TableRendererTests()
    {
        _renderer = new TableRenderer(_output, new CardFormatter(false));
    }

    private string[] Lines => _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task DealAsync(string player1, string upcard, string player2)
    {
        await _renderer.OnRoundStartedAsync(new RoundStartedNotification(25m, 1000m), Ct);
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne(player1), true), Ct);
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Dealer, Cards.ParseOne(upcard), true), Ct);
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne(player2), true), Ct);
        Assert.Empty(_output.ToString());
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Dealer, null, false), Ct);
    }

    [Fact]
    public async Task InitialDealHidesHoleCardAndShowsSoftTotal()
    {
        await DealAsync("AH", "KS", "6C");

        Assert.Equal(["Dealer:  K♠ [??]", "You:     A♥ 6♣   (soft 17)"], Lines);
    }

    [Fact]
    public async Task PlayerCardsAndBust()
    {
        await DealAsync("10S", "KS", "6S");
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne("6H"), true), Ct);

        Assert.Equal("You:     10♠ 6♠ 6♥   (22 bust)", Lines[^1]);
    }

    [Fact]
    public async Task PlayerBlackjack()
    {
        await DealAsync("AS", "9S", "KS");

        Assert.Equal("You:     A♠ K♠   (Blackjack)", Lines[^1]);
    }

    [Fact]
    public async Task RevealedHoleCardAndDealerDrawsShowTotals()
    {
        await DealAsync("10S", "KS", "8S");
        await _renderer.OnHoleCardRevealedAsync(new HoleCardRevealedNotification(Cards.ParseOne("5H")), Ct);
        await _renderer.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Dealer, Cards.ParseOne("AD"), true), Ct);

        Assert.Equal(["Dealer:  K♠ 5♥   (15)", "Dealer:  K♠ 5♥ A♦   (16)"], Lines[^2..]);
    }

    [Theory]
    [InlineData(HandOutcome.Loss, "-25", "975", "Result:  You lose  -25   Bankroll: 975")]
    [InlineData(HandOutcome.BlackjackWin, "37.5", "1037.5", "Result:  Blackjack! You win  +37.5   Bankroll: 1037.5")]
    [InlineData(HandOutcome.Push, "0", "1000", "Result:  Push  0   Bankroll: 1000")]
    public async Task ResultLine(HandOutcome outcome, string net, string bankroll, string expected)
    {
        var n = decimal.Parse(net, System.Globalization.CultureInfo.InvariantCulture);
        var b = decimal.Parse(bankroll, System.Globalization.CultureInfo.InvariantCulture);
        await _renderer.OnHandSettledAsync(new HandSettledNotification(0, outcome, n), Ct);
        await _renderer.OnRoundCompletedAsync(new RoundResult(25m, [], [], 0, n, b), Ct);

        Assert.Equal(expected, Lines[^1]);
    }

    [Fact]
    public void EveryOutcomeHasItsOwnText()
    {
        var texts = Enum.GetValues<HandOutcome>().Select(TableRenderer.Describe).ToList();

        Assert.Equal(texts.Count, texts.Distinct().Count());
        Assert.All(texts, t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }

    [Fact]
    public async Task ShuffleIsAnnounced()
    {
        await _renderer.OnShoeShuffledAsync(Ct);

        Assert.Equal(["The shoe is shuffled."], Lines);
    }

    [Fact]
    public async Task NewRoundStartsWithEmptyTable()
    {
        await DealAsync("10S", "KS", "8S");
        _output.GetStringBuilder().Clear();

        await DealAsync("2S", "3S", "4S");

        Assert.Equal(["Dealer:  3♠ [??]", "You:     2♠ 4♠   (6)"], Lines);
    }
}
