using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Blackjack.Gui.ViewModels;

/// <summary>
/// The Blackjack table: runs the session loop, offers the button commands and shows the course of each round.
/// It is the engine's <see cref="IGameObserver"/> and awaits a short delay after each card, so the view can animate it.
/// The loop runs on the UI thread; the engine keeps that synchronization context.
/// </summary>
public sealed class TableViewModel : ObservableObject, IGameObserver
{
    private readonly Rules _rules;
    private readonly Func<ICardSource> _cardSourceFactory;
    private readonly decimal _startingBankroll;
    private readonly TimeSpan _dealDelay;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IRelayCommand[] _commands;

    private BlackjackGame _game;
    private SessionStatistics _statistics = new();
    private TaskCompletionSource? _newGame;
    private TablePhase _phase = TablePhase.Dealing;
    private decimal _bankroll;
    private decimal _bet;
    private decimal? _lastBet;
    private string _status = "";
    private string _lastResult = "";
    private bool _shuffled;
    private Task? _session;

    /// <summary>Creates the table; call <see cref="Start"/> to begin playing.</summary>
    /// <param name="rules">The table rules.</param>
    /// <param name="cardSourceFactory">Creates the shoe for each new game.</param>
    /// <param name="startingBankroll">The bankroll of each new game.</param>
    /// <param name="dealDelay">Pause after each card for the deal animation; <see cref="TimeSpan.Zero"/> in tests.</param>
    public TableViewModel(Rules rules, Func<ICardSource> cardSourceFactory, decimal startingBankroll, TimeSpan dealDelay)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(cardSourceFactory);
        _rules = rules;
        _cardSourceFactory = cardSourceFactory;
        _startingBankroll = startingBankroll;
        _dealDelay = dealDelay;
        _game = NewGame();
        _bankroll = startingBankroll;

        Player.BetRequested += OnBetRequested;
        Player.DecisionRequested += OnDecisionRequested;

        AddChipCommand = new RelayCommand<string>(AddChip, _ => Phase == TablePhase.Betting);
        ClearBetCommand = new RelayCommand(() => Bet = 0m, () => Phase == TablePhase.Betting);
        DealCommand = new RelayCommand(Deal, () => Phase == TablePhase.Betting && Player.PendingBet?.IsValidBet(Bet) == true);
        LeaveTableCommand = new RelayCommand(() => Player.LeaveTable(), () => Phase == TablePhase.Betting);
        HitCommand = new RelayCommand(() => Choose(PlayerAction.Hit), () => Player.CanChoose(PlayerAction.Hit));
        StandCommand = new RelayCommand(() => Choose(PlayerAction.Stand), () => Player.CanChoose(PlayerAction.Stand));
        DoubleCommand = new RelayCommand(() => Choose(PlayerAction.Double), () => Player.CanChoose(PlayerAction.Double));
        SplitCommand = new RelayCommand(() => Choose(PlayerAction.Split), () => Player.CanChoose(PlayerAction.Split));
        NewGameCommand = new RelayCommand(() => _newGame?.TrySetResult(), () => IsSessionOver);
        _commands = [AddChipCommand, ClearBetCommand, DealCommand, LeaveTableCommand, HitCommand, StandCommand, DoubleCommand, SplitCommand, NewGameCommand];
    }

    /// <summary>The human player whose requests the buttons complete.</summary>
    public GuiPlayer Player { get; } = new();

    /// <summary>The dealer's hand.</summary>
    public HandViewModel Dealer { get; } = new("Dealer");

    /// <summary>The player's hands (more than one after a split).</summary>
    public ObservableCollection<HandViewModel> PlayerHands { get; } = [];

    /// <summary>The statistics of the current session.</summary>
    public SessionStatistics Statistics => _statistics;

    /// <summary>What the table is waiting for.</summary>
    public TablePhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsSessionOver));
                RefreshCommands();
            }
        }
    }

    /// <summary>True when the game is over or the player left the table.</summary>
    public bool IsSessionOver => Phase is TablePhase.GameOver or TablePhase.Left;

    /// <summary>The current bankroll.</summary>
    public decimal Bankroll
    {
        get => _bankroll;
        private set
        {
            if (SetProperty(ref _bankroll, value))
            {
                OnPropertyChanged(nameof(BankrollText));
            }
        }
    }

    /// <summary>The bankroll as text, e.g. <c>Bankroll: 1000</c>.</summary>
    public string BankrollText => $"Bankroll: {DisplayText.Money(Bankroll)}";

    /// <summary>The bet for the next round, built with the chips.</summary>
    public decimal Bet
    {
        get => _bet;
        private set
        {
            if (SetProperty(ref _bet, value))
            {
                OnPropertyChanged(nameof(BetText));
                RefreshCommands();
            }
        }
    }

    /// <summary>The bet as text, e.g. <c>Bet: 25</c>.</summary>
    public string BetText => $"Bet: {DisplayText.Money(Bet)}";

    /// <summary>What the player should do next, or what happened.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>The result of the last round, e.g. <c>You win +25</c>; empty while a round is running.</summary>
    public string LastResult
    {
        get => _lastResult;
        private set => SetProperty(ref _lastResult, value);
    }

    /// <summary>Adds a chip (parameter: its value as text).</summary>
    public IRelayCommand<string> AddChipCommand { get; }

    /// <summary>Sets the bet to 0.</summary>
    public IRelayCommand ClearBetCommand { get; }

    /// <summary>Places the bet and deals.</summary>
    public IRelayCommand DealCommand { get; }

    /// <summary>Leaves the table and shows the session summary.</summary>
    public IRelayCommand LeaveTableCommand { get; }

    /// <summary>Hit.</summary>
    public IRelayCommand HitCommand { get; }

    /// <summary>Stand.</summary>
    public IRelayCommand StandCommand { get; }

    /// <summary>Double.</summary>
    public IRelayCommand DoubleCommand { get; }

    /// <summary>Split.</summary>
    public IRelayCommand SplitCommand { get; }

    /// <summary>Starts a new game after Game over or Leave table.</summary>
    public IRelayCommand NewGameCommand { get; }

    /// <summary>The running session loop (completed after <see cref="Shutdown"/>).</summary>
    public Task Session => _session ?? Task.CompletedTask;

    /// <summary>Starts the session loop. The first bet request is shown before this method returns.</summary>
    public void Start() => _session ??= RunAsync(_shutdown.Token);

    /// <summary>Aborts the current round (bankroll restored) and ends the loop, e.g. when the window closes.</summary>
    public void Shutdown() => _shutdown.Cancel();

    /// <inheritdoc />
    public ValueTask OnShoeShuffledAsync(CancellationToken ct)
    {
        _shuffled = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnRoundStartedAsync(RoundStartedNotification notification, CancellationToken ct)
    {
        Dealer.Cards.Clear();
        Dealer.UpdateTotal();
        PlayerHands.Clear();
        PlayerHands.Add(new HandViewModel("You") { BetText = $"Bet {DisplayText.Money(notification.Bet)}" });
        LastResult = "";
        Status = "Dealing …";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        var hand = notification.Recipient.IsDealer ? Dealer : PlayerHands[notification.Recipient.HandIndex];
        hand.Cards.Add(new CardViewModel(notification.Card));
        hand.UpdateTotal();
        await PauseAsync(ct);
    }

    /// <inheritdoc />
    public async ValueTask OnHandSplitAsync(HandSplitNotification notification, CancellationToken ct)
    {
        var hand = PlayerHands[notification.HandIndex];
        hand.Cards.RemoveAt(hand.Cards.Count - 1);
        hand.IsFromSplit = true;
        var bet = hand.BetText;
        var newHand = new HandViewModel("") { IsFromSplit = true, BetText = bet };
        newHand.Cards.Add(new CardViewModel(notification.MovedCard));
        PlayerHands.Add(newHand);
        for (var i = 0; i < PlayerHands.Count; i++)
        {
            PlayerHands[i].Title = $"Hand {i + 1}";
            PlayerHands[i].UpdateTotal();
        }

        await PauseAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask OnHandDoubledAsync(HandDoubledNotification notification, CancellationToken ct)
    {
        PlayerHands[notification.HandIndex].BetText = $"Bet {DisplayText.Money(notification.NewBet)} (doubled)";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct)
    {
        var hidden = Dealer.Cards.FirstOrDefault(c => !c.IsFaceUp);
        if (hidden is not null)
        {
            hidden.Card = notification.Card;
        }

        Dealer.UpdateTotal();
        await PauseAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask OnPlayerHandFinishedAsync(PlayerHandFinishedNotification notification, CancellationToken ct)
    {
        PlayerHands[notification.HandIndex].IsActive = false;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnHandSettledAsync(HandSettledNotification notification, CancellationToken ct)
    {
        PlayerHands[notification.HandIndex].ResultText =
            $"{DisplayText.Outcome(notification.Outcome)} {DisplayText.SignedMoney(notification.Net)}";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct)
    {
        LastResult = result.PlayerHands.Count == 1
            ? PlayerHands[0].ResultText
            : $"Result {DisplayText.SignedMoney(result.Net)}";
        if (result.Bankroll is { } bankroll)
        {
            Bankroll = bankroll;
        }

        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await PlaySessionAsync(ct);
                _newGame = new TaskCompletionSource();
                RefreshCommands();
                await _newGame.Task.WaitAsync(ct);
                _newGame = null;
                _game = NewGame();
                _statistics = new SessionStatistics();
                _lastBet = null;
                Bankroll = _startingBankroll;
                Dealer.Cards.Clear();
                Dealer.UpdateTotal();
                PlayerHands.Clear();
                LastResult = "";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The window was closed; the engine has restored the bankroll of the aborted round.
        }
        catch (Exception ex)
        {
            Phase = TablePhase.GameOver;
            Status = $"Error: {ex.Message}";
        }
    }

    private async Task PlaySessionAsync(CancellationToken ct)
    {
        while (true)
        {
            if (_game.Bankroll < _rules.MinBet)
            {
                Phase = TablePhase.GameOver;
                Status = $"Your bankroll is below the minimum bet. {Summary()} Press New game to play again.";
                return;
            }

            var result = await _game.PlayRoundAsync(Player, ct);
            if (result is null)
            {
                Phase = TablePhase.Left;
                Status = $"You left the table. {Summary()}";
                return;
            }

            _statistics.Record(result);
        }
    }

    private string Summary() =>
        $"Rounds: {_statistics.Rounds}, wins: {_statistics.Wins}, losses: {_statistics.Losses}, " +
        $"pushes: {_statistics.Pushes}, net: {DisplayText.SignedMoney(_statistics.Net)}.";

    private BlackjackGame NewGame() => new(_rules, _cardSourceFactory(), _startingBankroll, [this]);

    private void OnBetRequested(object? sender, BetContext context)
    {
        var max = context.MaxAllowedBet;
        Bet = Math.Clamp(_lastBet ?? _rules.MinBet, _rules.MinBet, max);
        var shuffled = _shuffled ? "The shoe has been shuffled. " : "";
        _shuffled = false;
        Status = $"{shuffled}Place your bet ({DisplayText.Money(_rules.MinBet)}–{DisplayText.Money(max)}) and press Deal.";
        Phase = TablePhase.Betting;
        RefreshCommands();
    }

    private void OnDecisionRequested(object? sender, DecisionContext context)
    {
        for (var i = 0; i < PlayerHands.Count; i++)
        {
            PlayerHands[i].IsActive = i == context.HandIndex;
        }

        Status = context.HandCount > 1 ? $"Hand {context.HandIndex + 1}: your move." : "Your move.";
        Phase = TablePhase.Playing;
        RefreshCommands();
    }

    private void AddChip(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chip))
        {
            Bet += chip;
        }
    }

    private void Deal()
    {
        // Remember the bet first: a round without decisions (e.g. Blackjack) completes inside PlaceBet,
        // and the next bet request already uses the last bet as default.
        var bet = Bet;
        var previous = _lastBet;
        _lastBet = bet;
        Phase = TablePhase.Dealing;
        if (!Player.PlaceBet(bet))
        {
            _lastBet = previous;
            Phase = TablePhase.Betting;
        }
    }

    private void Choose(PlayerAction action)
    {
        Phase = TablePhase.Dealing;
        if (!Player.Choose(action))
        {
            Phase = TablePhase.Playing;
        }

        RefreshCommands();
    }

    private async Task PauseAsync(CancellationToken ct)
    {
        if (_dealDelay > TimeSpan.Zero)
        {
            await Task.Delay(_dealDelay, ct);
        }
    }

    private void RefreshCommands()
    {
        // The constructor sets the phase before the commands exist.
        if (_commands is null)
        {
            return;
        }

        foreach (var command in _commands)
        {
            command.NotifyCanExecuteChanged();
        }
    }
}
