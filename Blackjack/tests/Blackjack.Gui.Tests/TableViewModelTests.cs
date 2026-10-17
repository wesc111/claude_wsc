using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Complete rounds through the table view model; deal order: player, dealer, player, hole card, …</summary>
public sealed class TableViewModelTests
{
    [Fact]
    public void StartsWaitingForTheBet()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(10m, vm.Bet);
        Assert.Equal("Bankroll: 1000", vm.BankrollText);
        Assert.Equal("Place your bet (10–500) and press Deal.", vm.Status);
        Assert.True(vm.DealCommand.CanExecute(null));
        Assert.True(vm.LeaveTableCommand.CanExecute(null));
        Assert.False(vm.HitCommand.CanExecute(null));
        Assert.False(vm.StandCommand.CanExecute(null));
        Assert.False(vm.NewGameCommand.CanExecute(null));
        Assert.False(vm.IsSessionOver);
    }

    [Fact]
    public void ChipsBuildTheBet()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;

        t.Chip(25);
        t.Chip(100);

        Assert.Equal(135m, vm.Bet);
        Assert.Equal("Bet: 135", vm.BetText);

        t.Execute(vm.ClearBetCommand);

        Assert.Equal(0m, vm.Bet);
        Assert.False(vm.DealCommand.CanExecute(null));
        t.Chip(5);
        Assert.False(vm.DealCommand.CanExecute(null), "5 is below the minimum bet");
    }

    [Fact]
    public void ChipAboveTheMaximumBetIsDisabled()
    {
        var t = Table.Start("10 9 7 8");
        for (var i = 0; i < 4; i++)
        {
            t.Chip(100);
        }

        Assert.Equal(410m, t.ViewModel.Bet);
        Assert.False(t.ViewModel.AddChipCommand.CanExecute(100), "510 is above MaxBet 500");
        Assert.True(t.ViewModel.AddChipCommand.CanExecute(25));
        Assert.True(t.ViewModel.DealCommand.CanExecute(null));
    }

    [Fact]
    public void DealShowsCardsWithHiddenHoleCard()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;

        t.Execute(vm.DealCommand);

        Assert.Equal(TablePhase.Playing, vm.Phase);
        var hand = Assert.Single(vm.PlayerHands);
        Assert.Equal("You", hand.Title);
        Assert.Equal(["10_of_spades", "7_of_spades"], hand.Cards.Select(c => c.ImageName));
        Assert.Equal("17", hand.TotalText);
        Assert.Equal("Bet 10", hand.BetText);
        Assert.True(hand.IsActive);
        Assert.Equal(["9_of_spades", "card_back"], vm.Dealer.Cards.Select(c => c.ImageName));
        Assert.Equal("", vm.Dealer.TotalText);
        Assert.Equal("Your move.", vm.Status);
        Assert.True(vm.HitCommand.CanExecute(null));
        Assert.True(vm.StandCommand.CanExecute(null));
        Assert.True(vm.DoubleCommand.CanExecute(null));
        Assert.False(vm.SplitCommand.CanExecute(null));
        Assert.False(vm.DealCommand.CanExecute(null));
        Assert.False(vm.AddChipCommand.CanExecute(5));
        Assert.False(vm.LeaveTableCommand.CanExecute(null));
    }

    [Fact]
    public void StandRevealsTheHoleCardAndSettles()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);

        t.Execute(vm.StandCommand);

        Assert.Equal(["9_of_spades", "8_of_spades"], vm.Dealer.Cards.Select(c => c.ImageName));
        Assert.Equal("17", vm.Dealer.TotalText);
        Assert.Equal("You win +10", vm.PlayerHands[0].ResultText);
        Assert.Equal("You win +10", vm.LastResult);
        Assert.False(vm.PlayerHands[0].IsActive);
        Assert.Equal("Bankroll: 1010", vm.BankrollText);
        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(1, vm.Statistics.Rounds);
    }

    [Fact]
    public void HitAddsACard()
    {
        var t = Table.Start("10 9 2 8 5");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);

        t.Execute(vm.HitCommand);

        Assert.Equal(TablePhase.Playing, vm.Phase);
        Assert.Equal(3, vm.PlayerHands[0].Cards.Count);
        Assert.Equal("17", vm.PlayerHands[0].TotalText);
        Assert.False(vm.DoubleCommand.CanExecute(null), "no Double with three cards");
    }

    [Fact]
    public void BlackjackNeedsNoDecision()
    {
        var t = Table.Start("A 9 K 7");
        var vm = t.ViewModel;

        t.Execute(vm.DealCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal("Blackjack", vm.PlayerHands[0].TotalText);
        Assert.Equal("Blackjack! You win +15", vm.LastResult);
        Assert.Equal(1015m, vm.Bankroll);
    }

    [Fact]
    public void LastBetIsKeptAfterARoundWithoutDecision()
    {
        // Blackjack completes the round inside Deal; the next bet must still default to 35.
        var t = Table.Start("A 9 K 7");
        t.Chip(25);

        t.Execute(t.ViewModel.DealCommand);

        Assert.Equal(35m, t.ViewModel.Bet);
    }

    [Fact]
    public void NextRoundStartsWithTheLastBetAndAnEmptyTable()
    {
        var t = Table.Start("10 9 9 8 10 9 9 8");
        var vm = t.ViewModel;
        t.Chip(25);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        Assert.Equal(35m, vm.Bet);
        t.Execute(vm.DealCommand);

        Assert.Equal("", vm.LastResult);
        Assert.Equal(2, vm.PlayerHands[0].Cards.Count);
        Assert.Equal("Bet 35", vm.PlayerHands[0].BetText);
    }

    [Fact]
    public void SplitAndDouble()
    {
        // 8 8 split; hand 1 draws 3 → 11 and doubles (10 → 21); hand 2 draws 2 → 10 and stands; dealer 10 7.
        var t = Table.Start("8 10 8 7 3 10 2");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);
        Assert.True(vm.SplitCommand.CanExecute(null));

        t.Execute(vm.SplitCommand);

        Assert.Equal(["Hand 1", "Hand 2"], vm.PlayerHands.Select(h => h.Title));
        Assert.True(vm.PlayerHands[0].IsActive);
        Assert.False(vm.PlayerHands[1].IsActive);
        Assert.Equal("11", vm.PlayerHands[0].TotalText);
        Assert.Single(vm.PlayerHands[1].Cards);
        Assert.Equal("Hand 1: your move.", vm.Status);

        t.Execute(vm.DoubleCommand);

        Assert.Equal("Bet 20 (doubled)", vm.PlayerHands[0].BetText);
        Assert.Equal("21", vm.PlayerHands[0].TotalText);
        Assert.True(vm.PlayerHands[1].IsActive);
        Assert.Equal("Hand 2: your move.", vm.Status);

        t.Execute(vm.StandCommand);

        Assert.Equal("You win +20", vm.PlayerHands[0].ResultText);
        Assert.Equal("You lose -10", vm.PlayerHands[1].ResultText);
        Assert.Equal("Result +10", vm.LastResult);
        Assert.Equal(1010m, vm.Bankroll);
        Assert.Equal(TablePhase.Betting, vm.Phase);
    }

    [Fact]
    public void LeaveTableShowsTheSummaryAndNewGameStartsOver()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 7 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Execute(vm.LeaveTableCommand);

        Assert.Equal(TablePhase.Left, vm.Phase);
        Assert.True(vm.IsSessionOver);
        Assert.Equal("You left the table. Rounds: 1, wins: 1, losses: 0, pushes: 0, net: +10. Statistics saved to log/test.txt.", vm.Status);
        Assert.False(vm.DealCommand.CanExecute(null));

        t.Execute(vm.NewGameCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(1000m, vm.Bankroll);
        Assert.Equal(0, vm.Statistics.Rounds);
        Assert.Equal(10m, vm.Bet);
        Assert.Empty(vm.PlayerHands);
        Assert.Empty(vm.Dealer.Cards);
    }

    [Fact]
    public void GameOverWhenTheBankrollIsBelowTheMinimumBet()
    {
        var t = Table.Start("10 9 6 8", bankroll: 10m);
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);

        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.GameOver, vm.Phase);
        Assert.Equal(0m, vm.Bankroll);
        Assert.StartsWith("Your bankroll is below the minimum bet. Rounds: 1, wins: 0, losses: 1", vm.Status, StringComparison.Ordinal);
        Assert.True(vm.NewGameCommand.CanExecute(null));
        Assert.False(vm.DealCommand.CanExecute(null));
    }

    [Fact]
    public async Task ShutdownAbortsTheRoundAndKeepsTheBankroll()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;
        t.Chip(100);
        t.Execute(vm.DealCommand);

        t.Do(vm.Shutdown);
        await vm.Session;

        Assert.Equal(1000m, vm.Bankroll);
        Assert.Equal(0, vm.Statistics.Rounds);
    }

    [Fact]
    public void ShuffleIsAnnounced()
    {
        var t = new Table(1000m, "10 9 7 8");
        t.Do(() => t.LastSource!.NeedsReshuffle = true);
        t.Do(t.ViewModel.Start);

        Assert.StartsWith("The shoe has been shuffled. Place your bet", t.ViewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionButtonsAreDisabledWhileNoDecisionIsPending()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        Assert.False(vm.HitCommand.CanExecute(null));
        Assert.False(vm.StandCommand.CanExecute(null));
        Assert.False(vm.DoubleCommand.CanExecute(null));
        Assert.False(vm.SplitCommand.CanExecute(null));
    }

    [Fact]
    public void ShoeCountsDownWithEveryCard()
    {
        var t = Table.Start("10 9 2 8 5");
        var vm = t.ViewModel;

        Assert.Equal("Shoe: 5 / 5", vm.ShoeText);

        t.Execute(vm.DealCommand);
        Assert.Equal(1, vm.ShoeRemaining);
        Assert.Equal(5, vm.ShoeTotal);

        t.Execute(vm.HitCommand);
        Assert.Equal("Shoe: 0 / 5", vm.ShoeText);
    }

    [Fact]
    public void ShoeIsFullAfterAReshuffle()
    {
        // A real one-deck shoe that needs a reshuffle right away (penetration reached by 52 draws).
        var shoe = new Shoe(1, 1m, seed: 3);
        for (var i = 0; i < 52; i++)
        {
            shoe.Draw();
        }

        var t = new Table(_ => shoe);
        t.Do(t.ViewModel.Start);

        Assert.Equal("Shoe: 52 / 52", t.ViewModel.ShoeText);
        Assert.StartsWith("The shoe has been shuffled.", t.ViewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void ReshuffleMarkerSitsAtTheCardsLeftWhenReshuffling() =>
        Assert.Equal(TableViewModel.ShoeBarWidth * 0.25, Table.Start("10 9 7 8").ViewModel.ReshuffleMarkerLeft);

    [Fact]
    public void DecksDefaultToSixAndCanBeChosenBeforeTheFirstRound()
    {
        var t = new Table(1000m, "10 9 7 8", "A K Q J 10");
        var vm = t.ViewModel;
        t.Do(vm.Start);

        Assert.Equal(6, vm.Decks);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], vm.DeckOptions);
        Assert.True(vm.CanChangeDecks);

        t.Do(() => vm.Decks = 2);

        Assert.Equal(2, vm.Decks);
        Assert.Equal([6, 2], t.UsedRules.Select(r => r.Decks));
        Assert.Equal("Shoe: 5 / 5", vm.ShoeText);
        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.NotEqual(TablePhase.Left, vm.Phase);
        Assert.Equal(1000m, vm.Bankroll);
    }

    [Fact]
    public void DecksAreLockedOnceAGameHasRounds()
    {
        var t = new Table(1000m, "10 9 9 8 10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);

        Assert.False(vm.CanChangeDecks, "locked during the round");
        t.Do(() => vm.Decks = 1);
        Assert.Equal(6, vm.Decks);

        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.False(vm.CanChangeDecks, "locked after the first round");
        Assert.Single(t.UsedRules);
    }

    [Fact]
    public void DecksChosenAfterLeavingApplyToNewGame()
    {
        var t = new Table(1000m, "10 9 9 8", "A K Q J");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        t.Execute(vm.LeaveTableCommand);

        Assert.True(vm.CanChangeDecks);
        t.Do(() => vm.Decks = 1);
        Assert.Single(t.UsedRules);

        t.Execute(vm.NewGameCommand);

        Assert.Equal([6, 1], t.UsedRules.Select(r => r.Decks));
        Assert.True(vm.CanChangeDecks);
        Assert.Equal("Shoe: 4 / 4", vm.ShoeText);
    }

    [Fact]
    public void LeavingSavesTheStatisticsOnce()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Execute(vm.LeaveTableCommand);
        t.Do(vm.Shutdown);

        var log = Assert.Single(t.SavedLogs);
        Assert.Single(log.Rounds);
        Assert.Equal(1010m, log.Statistics.FinalBankroll);
        Assert.EndsWith("Statistics saved to log/test.txt.", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void GameOverSavesTheStatistics()
    {
        var t = Table.Start("10 9 6 8", bankroll: 10m);
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);

        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.GameOver, vm.Phase);
        Assert.Single(t.SavedLogs);
        Assert.Contains("Statistics saved to log/test.txt.", vm.Status, StringComparison.Ordinal);
        Assert.EndsWith("Press New game to play again.", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosingTheWindowSavesTheStatistics()
    {
        var t = Table.Start("10 9 9 8 10 9 7 8");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        t.Execute(vm.DealCommand);

        // Closed during the second round: only the completed first round is in the log.
        t.Do(vm.Shutdown);
        await vm.Session;

        var log = Assert.Single(t.SavedLogs);
        Assert.Single(log.Rounds);
    }

    [Fact]
    public void NothingIsSavedWithoutARound()
    {
        var t = Table.Start("10 9 9 8");

        t.Execute(t.ViewModel.LeaveTableCommand);
        t.Do(t.ViewModel.Shutdown);

        Assert.Empty(t.SavedLogs);
        Assert.Equal("You left the table. Rounds: 0, wins: 0, losses: 0, pushes: 0, net: 0.", t.ViewModel.Status);
    }

    [Fact]
    public void EveryGameGetsItsOwnLog()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 7 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        t.Execute(vm.LeaveTableCommand);
        t.Execute(vm.NewGameCommand);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Execute(vm.LeaveTableCommand);

        Assert.Equal(2, t.SavedLogs.Count);
        Assert.NotSame(t.SavedLogs[0], t.SavedLogs[1]);
        Assert.Equal(HandOutcome.Win, t.SavedLogs[0].Rounds[0].Result.PlayerHands[0].Outcome);
        Assert.Equal(HandOutcome.Push, t.SavedLogs[1].Rounds[0].Result.PlayerHands[0].Outcome);
    }

    [Fact]
    public void LogUsesTheChosenNumberOfDecks()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Do(() => vm.Decks = 3);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Execute(vm.LeaveTableCommand);

        Assert.Equal(3, Assert.Single(t.SavedLogs).Rules.Decks);
    }

    [Fact]
    public void WriteErrorIsShownAndTheGameGoesOn()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        t.FailSaving = true;
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Execute(vm.LeaveTableCommand);

        Assert.EndsWith("Could not write the statistics files: disk full", vm.Status, StringComparison.Ordinal);
        Assert.True(vm.NewGameCommand.CanExecute(null));
    }
}
