using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>
/// Drives a <see cref="TableViewModel"/> like the buttons do. Every action runs without synchronization context,
/// so the engine continues synchronously up to its next request, just as on the UI thread.
/// </summary>
public sealed class Table
{
    private readonly Queue<FixedCardSource> _sources;

    public Table(decimal bankroll = 1000m, params string[] shoes)
    {
        _sources = new Queue<FixedCardSource>(shoes.Select(s => new FixedCardSource(s)));
        ViewModel = new TableViewModel(Rules.Default, NextSource, bankroll, TimeSpan.Zero);
    }

    public Table(Func<Rules, ICardSource> factory)
    {
        _sources = new Queue<FixedCardSource>();
        ViewModel = new TableViewModel(Rules.Default, factory, 1000m, TimeSpan.Zero);
    }

    public TableViewModel ViewModel { get; }

    public FixedCardSource? LastSource { get; private set; }

    public List<Rules> UsedRules { get; } = [];

    public static Table Start(string cards, decimal bankroll = 1000m)
    {
        var table = new Table(bankroll, cards, "");
        table.Do(table.ViewModel.Start);
        return table;
    }

    public void Do(Action action)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    public void Execute(CommunityToolkit.Mvvm.Input.IRelayCommand command)
    {
        Assert.True(command.CanExecute(null), "the button must be enabled");
        Do(() => command.Execute(null));
    }

    public void Chip(string value)
    {
        Assert.True(ViewModel.AddChipCommand.CanExecute(value));
        Do(() => ViewModel.AddChipCommand.Execute(value));
    }

    private FixedCardSource NextSource(Rules rules)
    {
        UsedRules.Add(rules);
        LastSource = _sources.Count > 0 ? _sources.Dequeue() : new FixedCardSource("");
        return LastSource;
    }
}
