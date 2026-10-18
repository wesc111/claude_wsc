using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E6: the table awaits the card animator for each dealt card and for turning over the hole card.</summary>
public sealed class CardAnimationTests
{
    [Fact]
    public void DealtCardsGoThroughTheAnimatorInDealOrder()
    {
        var t = Table.Start("10 9 7 8");
        var animator = new RecordingAnimator();
        t.ViewModel.CardAnimator = animator;

        t.Execute(t.ViewModel.DealCommand);

        Assert.Equal(["10_of_spades", "9_of_spades", "7_of_spades", "card_back"], animator.Dealt);
        Assert.All(animator.HiddenWhileDealt, Assert.True);
        Assert.All(t.ViewModel.PlayerHands[0].Cards.Concat(t.ViewModel.Dealer.Cards), c => Assert.False(c.IsDealing));
        Assert.Equal("17", t.ViewModel.PlayerHands[0].TotalText);
        Assert.Equal(TablePhase.Playing, t.ViewModel.Phase);
    }

    [Fact]
    public void TheEngineWaitsForAnUnfinishedAnimation()
    {
        var t = Table.Start("10 9 7 8");
        var hold = new TaskCompletionSource();
        var animator = new RecordingAnimator { Hold = hold };
        t.ViewModel.CardAnimator = animator;

        t.Execute(t.ViewModel.DealCommand);

        var hand = Assert.Single(t.ViewModel.PlayerHands);
        var card = Assert.Single(hand.Cards);
        Assert.True(card.IsDealing, "hidden while it flies");
        Assert.Equal("", hand.TotalText);
        Assert.Empty(t.ViewModel.Dealer.Cards);
        Assert.Equal(TablePhase.Dealing, t.ViewModel.Phase);

        animator.Hold = null;
        t.Do(hold.SetResult);

        Assert.False(card.IsDealing);
        Assert.Equal(2, hand.Cards.Count);
        Assert.Equal("17", hand.TotalText);
        Assert.Equal(2, t.ViewModel.Dealer.Cards.Count);
        Assert.Equal(TablePhase.Playing, t.ViewModel.Phase);
    }

    [Fact]
    public void TheHoleCardTurnsOverOnReveal()
    {
        var t = Table.Start("10 9 9 8");
        var animator = new RecordingAnimator();
        t.ViewModel.CardAnimator = animator;
        t.Execute(t.ViewModel.DealCommand);

        Assert.Empty(animator.Turned);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal([("card_back", "8_of_spades")], animator.Turned);
        Assert.Equal("17", t.ViewModel.Dealer.TotalText);
        Assert.Equal("You win +10", t.ViewModel.LastResult);
    }

    [Fact]
    public void TheHoleCardIsShownEvenIfTheAnimatorDoesNotTurnIt()
    {
        var t = Table.Start("10 9 9 8");
        t.ViewModel.CardAnimator = new RecordingAnimator { SkipTurn = true };
        t.Execute(t.ViewModel.DealCommand);

        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal(["9_of_spades", "8_of_spades"], t.ViewModel.Dealer.Cards.Select(c => c.ImageName));
        Assert.Equal("17", t.ViewModel.Dealer.TotalText);
    }

    [Fact]
    public void WithoutAnimatorCardsAreNeverHidden()
    {
        var t = Table.Start("10 9 7 8");
        var hidden = new List<CardViewModel>();
        t.ViewModel.PlayerHands.CollectionChanged += (_, _) =>
        {
            foreach (var hand in t.ViewModel.PlayerHands)
            {
                hand.Cards.CollectionChanged += (_, e) => hidden.AddRange(e.NewItems!.Cast<CardViewModel>().Where(c => c.IsDealing));
            }
        };

        t.Execute(t.ViewModel.DealCommand);

        Assert.Empty(hidden);
        Assert.Equal(2, t.ViewModel.PlayerHands[0].Cards.Count);
    }

    private sealed class RecordingAnimator : ICardAnimator
    {
        /// <summary>The image of each card when its flight starts.</summary>
        public List<string> Dealt { get; } = [];

        public List<bool> HiddenWhileDealt { get; } = [];

        /// <summary>The image of each turned card before and after the turn.</summary>
        public List<(string Before, string After)> Turned { get; } = [];

        /// <summary>When set, each flight lasts until this task completes.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public bool SkipTurn { get; init; }

        public async Task DealAsync(CardViewModel card, CancellationToken ct)
        {
            Dealt.Add(card.ImageName);
            HiddenWhileDealt.Add(card.IsDealing);
            if (Hold is { } hold)
            {
                await hold.Task;
            }
        }

        public Task TurnOverAsync(CardViewModel card, Action turn, CancellationToken ct)
        {
            var before = card.ImageName;
            if (!SkipTurn)
            {
                turn();
            }

            Turned.Add((before, card.ImageName));
            return Task.CompletedTask;
        }
    }
}
