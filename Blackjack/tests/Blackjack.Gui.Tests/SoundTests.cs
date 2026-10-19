using Blackjack.Gui.Sounds;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E8: the table plays a sound for each event; deal order: player, dealer, player, hole card, …</summary>
public sealed class SoundTests
{
    [Fact]
    public void ARoundSoundsInOrder()
    {
        var (t, sounds) = Start("10 9 9 8");

        t.Execute(t.ViewModel.DealCommand);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal([Sound.CardDeal, Sound.CardDeal, Sound.CardDeal, Sound.CardDeal, Sound.CardTurn, Sound.Win], sounds.Played);
    }

    [Theory]
    [InlineData("10 9 7 10", Sound.Lose)]
    [InlineData("10 9 9 8", Sound.Win)]
    public void TheResultOfTheRound(string cards, Sound expected)
    {
        var (t, sounds) = Start(cards);

        t.Execute(t.ViewModel.DealCommand);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal(expected, sounds.Played[^1]);
    }

    [Fact]
    public void APushMakesNoResultSound()
    {
        var (t, sounds) = Start("10 10 8 8");

        t.Execute(t.ViewModel.DealCommand);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal("Push 0", t.ViewModel.LastResult);
        Assert.Equal(Sound.CardTurn, sounds.Played[^1]);
    }

    [Fact]
    public void APlayerBlackjack()
    {
        var (t, sounds) = Start("A 9 K 8");

        t.Execute(t.ViewModel.DealCommand);

        Assert.Equal(Sound.Blackjack, sounds.Played[^1]);
        Assert.DoesNotContain(Sound.Win, sounds.Played);
    }

    [Fact]
    public void Chips()
    {
        var (t, sounds) = Start("10 9 7 8");

        t.Chip(25);
        t.Do(() => t.ViewModel.RemoveChipCommand.Execute(25));
        t.Execute(t.ViewModel.ClearBetCommand);
        t.Execute(t.ViewModel.ClearBetCommand);

        Assert.Equal([Sound.ChipPlace, Sound.ChipTake, Sound.ChipTake], sounds.Played);
    }

    [Fact]
    public void TheDefaultBetIsSilent()
    {
        var (t, sounds) = Start("10 9 9 8");
        t.Execute(t.ViewModel.DealCommand);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Equal(TablePhase.Betting, t.ViewModel.Phase);
        Assert.NotEmpty(t.ViewModel.BetStacks);
        Assert.Equal(Sound.Win, sounds.Played[^1]);
    }

    [Fact]
    public void Reshuffle()
    {
        var t = new Table(1000m, "10 9 7 8");
        var sounds = new RecordingSoundPlayer();
        t.ViewModel.SoundPlayer = sounds;
        t.Do(() => t.LastSource!.NeedsReshuffle = true);

        t.Do(t.ViewModel.Start);

        Assert.Equal([Sound.Shuffle], sounds.Played);
    }

    [Fact]
    public void NothingWhenSwitchedOff()
    {
        var (t, sounds) = Start("10 9 9 8");
        t.ViewModel.IsSoundOn = false;

        t.Chip(5);
        t.Execute(t.ViewModel.DealCommand);
        t.Execute(t.ViewModel.StandCommand);

        Assert.Empty(sounds.Played);
    }

    [Fact]
    public void SoundIsOnAtStart() => Assert.True(Table.Start("10 9 7 8").ViewModel.IsSoundOn);

    [Fact]
    public void EverySoundHasExistingFiles()
    {
        var folder = FindSoundFolder();

        Assert.All(Enum.GetValues<Sound>(), s => Assert.NotEmpty(SoundFiles.Variants[s]));
        Assert.All(SoundFiles.Variants.Values.SelectMany(v => v),
            n => Assert.True(File.Exists(Path.Combine(folder, n + ".wav")), $"{n}.wav is missing"));
    }

    private static (Table Table, RecordingSoundPlayer Sounds) Start(string cards)
    {
        var t = Table.Start(cards);
        var sounds = new RecordingSoundPlayer();
        t.ViewModel.SoundPlayer = sounds;
        return (t, sounds);
    }

    private static string FindSoundFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Blackjack.Gui", "Assets", "Sounds");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/Blackjack.Gui/Assets/Sounds not found above the test directory.");
    }

    private sealed class RecordingSoundPlayer : ISoundPlayer
    {
        public List<Sound> Played { get; } = [];

        public void Play(Sound sound) => Played.Add(sound);
    }
}
