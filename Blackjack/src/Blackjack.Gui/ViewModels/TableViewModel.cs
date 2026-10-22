using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Blackjack.Gui.Settings;
using Blackjack.Reports;

namespace Blackjack.Gui.ViewModels;

/// <summary>
/// The Blackjack table: runs the session loop, offers the button commands and shows the course of each round.
/// It is the engine's <see cref="IGameObserver"/> and awaits the <see cref="CardAnimator"/> for each card
/// (or a short delay without one), so the view can animate it.
/// The loop runs on the UI thread; the engine keeps that synchronization context.
/// </summary>
public sealed class TableViewModel : ObservableObject, IGameObserver
{
    private readonly Func<Rules, ICardSource> _cardSourceFactory;
    private readonly TimeSpan _dealDelay;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IRelayCommand[] _commands;
    private readonly Action<GameSettings>? _saveSettings;

    // The rules of the running game, and those chosen for the next one (deck selector, settings dialog).
    private Rules _rules;
    private Rules _nextRules;
    private decimal _startingBankroll;

    // The bankroll a new game starts with and the one saved: the bankroll after the last round, or the starting
    // bankroll after Game over and Reset bankroll (spec E9).
    private decimal _nextBankroll;

    private BlackjackGame _game;
    private ICardSource _cardSource = null!;
    private bool _restartRequested;
    private string _notice = "";
    private int _shoeRemaining;
    private int _shoeTotal;
    private readonly Func<SessionLog, string>? _saveLog;
    private readonly Func<DateTimeOffset>? _clock;
    private SessionLog _log;
    private bool _logSaved;
    private TaskCompletionSource? _newGame;
    private TablePhase _phase = TablePhase.Dealing;
    private decimal _bankroll;
    private decimal _bet;
    private decimal? _lastBet;
    private string _status = "";
    private string _lastResult = "";
    private bool _shuffled;
    private Task? _session;
    private bool _isSoundOn = true;
    private HintMode _hints;
    private string _hint = "";
    private bool _isHintMistake;
    private PlayerAction? _recommendedAction;

    /// <summary>Creates the table; call <see cref="Start"/> to begin playing.</summary>
    /// <param name="rules">The table rules.</param>
    /// <param name="cardSourceFactory">Creates the shoe for each new game from the rules (with the selected number of decks).</param>
    /// <param name="startingBankroll">The bankroll of a new game after Game over or Reset bankroll.</param>
    /// <param name="dealDelay">Pause after each card without <see cref="CardAnimator"/> and after a split; <see cref="TimeSpan.Zero"/> in tests.</param>
    /// <param name="saveLog">Saves the statistics files at the end of a session and returns the path to show; <c>null</c> = no files.</param>
    /// <param name="clock">The time source of the log; the local time by default.</param>
    /// <param name="bankroll">The saved bankroll to continue with; <c>null</c> or below the minimum bet = the starting bankroll.</param>
    /// <param name="saveSettings">Saves the settings whenever they or the bankroll change; <c>null</c> = not saved.</param>
    public TableViewModel(Rules rules, Func<Rules, ICardSource> cardSourceFactory, decimal startingBankroll, TimeSpan dealDelay,
                          Func<SessionLog, string>? saveLog = null, Func<DateTimeOffset>? clock = null,
                          decimal? bankroll = null, Action<GameSettings>? saveSettings = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(cardSourceFactory);
        _rules = _nextRules = rules;
        _cardSourceFactory = cardSourceFactory;
        _startingBankroll = startingBankroll;
        _nextBankroll = bankroll is { } saved && saved >= rules.MinBet ? saved : startingBankroll;
        _dealDelay = dealDelay;
        _saveLog = saveLog;
        _clock = clock;
        _saveSettings = saveSettings;
        _game = NewGame();
        _log = NewLog();

        Player.BetRequested += OnBetRequested;
        Player.DecisionRequested += OnDecisionRequested;

        AddChipCommand = new RelayCommand<int>(AddChip, CanAddChip);
        RemoveChipCommand = new RelayCommand<int>(RemoveChip, value => Phase == TablePhase.Betting && BetStacks.Any(s => s.Chip.Value == value));
        ClearBetCommand = new RelayCommand(ClearBet, () => Phase == TablePhase.Betting);
        DealCommand = new RelayCommand(Deal, () => Phase == TablePhase.Betting && Player.PendingBet?.IsValidBet(Bet) == true);
        LeaveTableCommand = new RelayCommand(() => Player.LeaveTable(), () => Phase == TablePhase.Betting);
        HitCommand = new RelayCommand(() => Choose(PlayerAction.Hit), () => Player.CanChoose(PlayerAction.Hit));
        StandCommand = new RelayCommand(() => Choose(PlayerAction.Stand), () => Player.CanChoose(PlayerAction.Stand));
        DoubleCommand = new RelayCommand(() => Choose(PlayerAction.Double), () => Player.CanChoose(PlayerAction.Double));
        SplitCommand = new RelayCommand(() => Choose(PlayerAction.Split), () => Player.CanChoose(PlayerAction.Split));
        NewGameCommand = new RelayCommand(() => _newGame?.TrySetResult(), () => IsSessionOver);
        ToggleSoundCommand = new RelayCommand(() => IsSoundOn = !IsSoundOn);
        _commands = [AddChipCommand, RemoveChipCommand, ClearBetCommand, DealCommand, LeaveTableCommand, HitCommand, StandCommand, DoubleCommand, SplitCommand, NewGameCommand];
    }

    /// <summary>The human player whose requests the buttons complete.</summary>
    public GuiPlayer Player { get; } = new();

    /// <summary>The dealer's hand.</summary>
    public HandViewModel Dealer { get; } = new("Dealer");

    /// <summary>Animates dealing and turning over cards; <c>null</c> = only the pause after each card.</summary>
    public ICardAnimator? CardAnimator { get; set; }

    /// <summary>Plays the sounds of the table (spec E8); <c>null</c> = silent.</summary>
    public ISoundPlayer? SoundPlayer { get; set; }

    /// <summary>Whether sounds are played; the "Sound" switch in the header (saved).</summary>
    public bool IsSoundOn
    {
        get => _isSoundOn;
        set
        {
            if (SetProperty(ref _isSoundOn, value))
            {
                SaveSettings();
            }
        }
    }

    /// <summary>The strategy hints (spec E11, a Pro feature; saved).</summary>
    public HintMode Hints
    {
        get => _hints;
        set
        {
            if (SetProperty(ref _hints, value))
            {
                SaveSettings();
            }
        }
    }

    /// <summary>The basic strategy action before a decision (Show), or the last decision that differed from it in this round (Feedback).</summary>
    public string Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    /// <summary>Whether <see cref="Hint"/> reports a decision that differed from basic strategy.</summary>
    public bool IsHintMistake
    {
        get => _isHintMistake;
        private set => SetProperty(ref _isHintMistake, value);
    }

    /// <summary>The basic strategy action of the pending decision in the mode Show; <c>null</c> otherwise.</summary>
    public PlayerAction? RecommendedAction
    {
        get => _recommendedAction;
        private set
        {
            if (SetProperty(ref _recommendedAction, value))
            {
                OnPropertyChanged(nameof(IsHitRecommended));
                OnPropertyChanged(nameof(IsStandRecommended));
                OnPropertyChanged(nameof(IsDoubleRecommended));
                OnPropertyChanged(nameof(IsSplitRecommended));
            }
        }
    }

    /// <summary>Whether the Hit button is highlighted as the basic strategy action.</summary>
    public bool IsHitRecommended => RecommendedAction == PlayerAction.Hit;

    /// <summary>Whether the Stand button is highlighted as the basic strategy action.</summary>
    public bool IsStandRecommended => RecommendedAction == PlayerAction.Stand;

    /// <summary>Whether the Double button is highlighted as the basic strategy action.</summary>
    public bool IsDoubleRecommended => RecommendedAction == PlayerAction.Double;

    /// <summary>Whether the Split button is highlighted as the basic strategy action.</summary>
    public bool IsSplitRecommended => RecommendedAction == PlayerAction.Split;

    // The hints in effect: none while the Pro features are locked.
    private HintMode ActiveHints => ProFeatures.IsUnlocked ? Hints : HintMode.Off;

    /// <summary>The settings to save: the rules for the next game, the bankrolls, the Sound switch and the strategy hints.</summary>
    public GameSettings CurrentSettings => GameSettings.From(_nextRules, _startingBankroll, _nextBankroll, IsSoundOn, Hints);

    /// <summary>Whether the table waits between rounds, so the settings can be opened and the bankroll reset.</summary>
    public bool IsBetweenRounds => Phase == TablePhase.Betting || IsSessionOver;

    /// <summary>The player's hands (more than one after a split).</summary>
    public ObservableCollection<HandViewModel> PlayerHands { get; } = [];

    /// <summary>The statistics of the current session.</summary>
    public SessionStatistics Statistics => _log.Statistics;

    /// <summary>The log of the current session.</summary>
    public SessionLog Log => _log;

    /// <summary>What the table is waiting for.</summary>
    public TablePhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsSessionOver));
                OnPropertyChanged(nameof(IsBetweenRounds));
                OnPropertyChanged(nameof(CanChangeDecks));
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

    /// <summary>The first line of the table print, e.g. <c>BLACKJACK PAYS 3 TO 2</c>.</summary>
    public string PayoutPrint => DisplayText.PayoutPrint(_rules.BlackjackPayout);

    /// <summary>The second line of the table print, e.g. <c>Dealer Must Stand on Soft 17</c>.</summary>
    public string DealerRulePrint => DisplayText.DealerRulePrint(_rules.DealerHitsSoft17);

    /// <summary>The banner of the table print.</summary>
    public string InsurancePrint => DisplayText.InsurancePrint;

    /// <summary>The chips of the tray, one per value, ascending.</summary>
    public IReadOnlyList<ChipViewModel> ChipTray { get; } = [.. ChipViewModel.Values.Select(v => new ChipViewModel(v))];

    /// <summary>The chips in the bet area: one stack per value, ascending.</summary>
    public ObservableCollection<ChipStackViewModel> BetStacks { get; } = [];

    /// <summary>The bet for the next round: the sum of the chips in the bet area.</summary>
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

    /// <summary>Width of the shoe progress bar in the view; the reshuffle marker is placed relative to it.</summary>
    public const double ShoeBarWidth = 200;

    /// <summary>The selectable numbers of decks.</summary>
    public IReadOnlyList<int> DeckOptions { get; } = [.. Enumerable.Range(Rules.MinDecks, Rules.MaxDecks - Rules.MinDecks + 1)];

    /// <summary>
    /// The number of decks. Can be changed only before the first round of a game (a fresh shoe is created at once)
    /// or after the session ended (applies to New game).
    /// </summary>
    public int Decks
    {
        get => _nextRules.Decks;
        set
        {
            if (value == Decks || !CanChangeDecks || value < Rules.MinDecks || value > Rules.MaxDecks)
            {
                OnPropertyChanged();
                return;
            }

            ChangeRules(_nextRules with { Decks = value });
            SaveSettings();
        }
    }

    /// <summary>Whether <see cref="Decks"/> can be changed now; changed table rules then apply at once (or to New game).</summary>
    public bool CanChangeDecks => IsSessionOver || (Phase == TablePhase.Betting && _log.Statistics.Rounds == 0);

    /// <summary>
    /// Applies the settings dialog (spec E9). Sound and starting bankroll apply at once. Changed table rules apply at
    /// once before the first round of a game, otherwise to the next game. <paramref name="resetBankroll"/> starts a new
    /// game with the starting bankroll (ending the session if rounds were played); only between rounds.
    /// </summary>
    public void ApplySettings(GameSettings settings, bool resetBankroll)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _isSoundOn = settings.IsSoundOn;
        OnPropertyChanged(nameof(IsSoundOn));
        _hints = settings.Hints;
        OnPropertyChanged(nameof(Hints));
        _startingBankroll = settings.StartingBankroll;
        var rules = settings.ToRules(_nextRules);
        if (resetBankroll && IsBetweenRounds)
        {
            _nextRules = rules;
            OnPropertyChanged(nameof(Decks));
            ResetBankroll();
        }
        else if (rules != _nextRules)
        {
            ChangeRules(rules);
        }

        SaveSettings();
    }

    // Before the first round the loop waits for the first bet: end that request and start again with the new rules.
    private void ChangeRules(Rules rules)
    {
        _nextRules = rules;
        OnPropertyChanged(nameof(Decks));
        if (Phase == TablePhase.Betting && _log.Statistics.Rounds == 0)
        {
            RequestRestart();
        }
    }

    private void ResetBankroll()
    {
        _nextBankroll = _startingBankroll;
        _notice = "Bankroll reset.";
        if (Phase == TablePhase.Betting)
        {
            RequestRestart();
        }
        else
        {
            _newGame?.TrySetResult();
        }
    }

    private void RequestRestart()
    {
        _restartRequested = true;
        Player.LeaveTable();
    }

    /// <summary>Cards left in the shoe.</summary>
    public int ShoeRemaining
    {
        get => _shoeRemaining;
        private set
        {
            if (SetProperty(ref _shoeRemaining, value))
            {
                OnPropertyChanged(nameof(ShoeText));
            }
        }
    }

    /// <summary>All cards of the shoe.</summary>
    public int ShoeTotal
    {
        get => _shoeTotal;
        private set
        {
            if (SetProperty(ref _shoeTotal, value))
            {
                OnPropertyChanged(nameof(ShoeText));
            }
        }
    }

    /// <summary>The shoe as text, e.g. <c>Shoe: 247 / 312</c>.</summary>
    public string ShoeText => $"Shoe: {ShoeRemaining} / {ShoeTotal}";

    /// <summary>Position of the reshuffle marker on the shoe bar: where the cards left reach 1 − penetration.</summary>
    public double ReshuffleMarkerLeft => ShoeBarWidth * (double)(1 - _rules.Penetration);

    /// <summary>Puts a chip into the bet area (parameter: its value); disabled if the bet would exceed the maximum.</summary>
    public IRelayCommand<int> AddChipCommand { get; }

    /// <summary>Takes one chip of the given value out of the bet area.</summary>
    public IRelayCommand<int> RemoveChipCommand { get; }

    /// <summary>Removes all chips from the bet area.</summary>
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

    /// <summary>Turns the sounds on or off (key M, spec E10).</summary>
    public IRelayCommand ToggleSoundCommand { get; }

    /// <summary>The running session loop (completed after <see cref="Shutdown"/>).</summary>
    public Task Session => _session ?? Task.CompletedTask;

    /// <summary>Starts the session loop. The first bet request is shown before this method returns.</summary>
    public void Start() => _session ??= RunAsync(_shutdown.Token);

    /// <summary>
    /// Aborts the current round (bankroll restored), ends the loop and saves the statistics files if they were not
    /// saved yet, e.g. when the window closes.
    /// </summary>
    public void Shutdown()
    {
        _shutdown.Cancel();
        SaveLog();
    }

    /// <inheritdoc />
    public ValueTask OnShoeShuffledAsync(CancellationToken ct)
    {
        _shuffled = true;
        UpdateShoe();
        Play(Sound.Shuffle);
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
        ClearHint();
        Status = "Dealing …";
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        var hand = notification.Recipient.IsDealer ? Dealer : PlayerHands[notification.Recipient.HandIndex];
        var card = new CardViewModel(notification.Card);
        UpdateShoe();
        Play(Sound.CardDeal);
        if (CardAnimator is { } animator)
        {
            card.IsDealing = true;
            hand.Cards.Add(card);
            try
            {
                await animator.DealAsync(card, ct);
            }
            finally
            {
                card.IsDealing = false;
            }

            hand.UpdateTotal();
        }
        else
        {
            hand.Cards.Add(card);
            hand.UpdateTotal();
            await PauseAsync(ct);
        }
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
            Play(Sound.CardTurn);
        }

        if (hidden is not null && CardAnimator is { } animator)
        {
            await animator.TurnOverAsync(hidden, () => hidden.Card = notification.Card, ct);
            hidden.Card = notification.Card;
            Dealer.UpdateTotal();
            return;
        }

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
            _nextBankroll = bankroll;
            SaveSettings();
        }

        if (ResultSound(result) is { } sound)
        {
            Play(sound);
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
                StartNewGame();
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
                // New game starts with the starting bankroll, also after the next start of the program.
                _nextBankroll = _startingBankroll;
                SaveSettings();
                Status = $"Your bankroll is below the minimum bet. {Summary()}{SaveLog()} Press New game to play again.";
                Phase = TablePhase.GameOver;
                return;
            }

            var result = await _game.PlayRoundAsync(Player, ct);
            if (result is null && _restartRequested)
            {
                // The rules were changed before the first round, or the bankroll was reset: this session ends.
                _restartRequested = false;
                if (_log.Rounds.Count > 0)
                {
                    _notice = $"{_notice} {Summary()}{SaveLog()}";
                }

                StartNewGame();
                continue;
            }

            if (result is null)
            {
                Status = $"You left the table. {Summary()}{SaveLog()}";
                Phase = TablePhase.Left;
                return;
            }

            _log.Record(result);
            OnPropertyChanged(nameof(CanChangeDecks));
        }
    }

    private string Summary() =>
        $"Rounds: {_log.Statistics.Rounds}, wins: {_log.Statistics.Wins}, losses: {_log.Statistics.Losses}, " +
        $"pushes: {_log.Statistics.Pushes}, net: {DisplayText.SignedMoney(_log.Statistics.Net)}.";

    // A new game with the rules and bankroll chosen for it, and a cleared table.
    private void StartNewGame()
    {
        _game = NewGame();
        _log = NewLog();
        OnPropertyChanged(nameof(CanChangeDecks));
        _lastBet = null;
        Dealer.Cards.Clear();
        Dealer.UpdateTotal();
        PlayerHands.Clear();
        LastResult = "";
        ClearHint();
        SaveSettings();
    }

    private BlackjackGame NewGame()
    {
        _rules = _nextRules;
        OnPropertyChanged(nameof(PayoutPrint));
        OnPropertyChanged(nameof(DealerRulePrint));
        OnPropertyChanged(nameof(ReshuffleMarkerLeft));
        _cardSource = _cardSourceFactory(_rules);
        UpdateShoe();
        Bankroll = _nextBankroll;
        return new BlackjackGame(_rules, _cardSource, _nextBankroll, [this]);
    }

    private SessionLog NewLog()
    {
        _logSaved = false;
        return new SessionLog(_rules, _nextBankroll, _clock);
    }

    private void SaveSettings() => _saveSettings?.Invoke(CurrentSettings);

    // Saves the statistics files once per session (only with rounds); returns the text for the status line.
    private string SaveLog()
    {
        if (_saveLog is null || _logSaved || _log.Rounds.Count == 0)
        {
            return "";
        }

        _logSaved = true;
        try
        {
            return $" Statistics saved to {_saveLog(_log)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $" Could not write the statistics files: {ex.Message}";
        }
    }

    private void UpdateShoe()
    {
        ShoeTotal = _cardSource.TotalCards;
        ShoeRemaining = _cardSource.CardsRemaining;
    }

    private void OnBetRequested(object? sender, BetContext context)
    {
        var max = context.MaxAllowedBet;
        SetChips(ChipViewModel.Split(Math.Clamp(_lastBet ?? _rules.MinBet, _rules.MinBet, max)));
        var notice = _notice.Length > 0 ? _notice.Trim() + " " : "";
        _notice = "";
        var shuffled = _shuffled ? "The shoe has been shuffled. " : "";
        _shuffled = false;
        Status = $"{notice}{shuffled}Place your bet ({DisplayText.Money(_rules.MinBet)}–{DisplayText.Money(max)}) and press Deal.";
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
        if (ActiveHints == HintMode.Show)
        {
            var recommended = BasicStrategyTable.Full.Decide(context);
            RecommendedAction = recommended;
            Hint = DisplayText.StrategyHint(recommended, context);
            IsHintMistake = false;
        }

        Phase = TablePhase.Playing;
        RefreshCommands();
    }

    private bool CanAddChip(int value) =>
        Phase == TablePhase.Betting && ChipViewModel.Values.Contains(value)
        && Player.PendingBet is { } context && Bet + value <= context.MaxAllowedBet;

    private void AddChip(int value)
    {
        if (!ChipViewModel.Values.Contains(value))
        {
            return;
        }

        var stack = BetStacks.FirstOrDefault(s => s.Chip.Value == value);
        if (stack is not null)
        {
            stack.Count++;
        }
        else
        {
            var index = BetStacks.TakeWhile(s => s.Chip.Value < value).Count();
            BetStacks.Insert(index, new ChipStackViewModel(new ChipViewModel(value)));
        }

        Play(Sound.ChipPlace);
        UpdateBet();
    }

    private void RemoveChip(int value)
    {
        var stack = BetStacks.FirstOrDefault(s => s.Chip.Value == value);
        if (stack is null)
        {
            return;
        }

        if (stack.Count > 1)
        {
            stack.Count--;
        }
        else
        {
            BetStacks.Remove(stack);
        }

        Play(Sound.ChipTake);
        UpdateBet();
    }

    private void ClearBet()
    {
        if (BetStacks.Count > 0)
        {
            Play(Sound.ChipTake);
        }

        SetChips([]);
    }

    /// <summary>One sound per round from the net over all hands; none for a push.</summary>
    private static Sound? ResultSound(RoundResult result) => result.Net switch
    {
        > 0 when result.PlayerHands.Any(h => h.Outcome == HandOutcome.BlackjackWin) => Sound.Blackjack,
        > 0 => Sound.Win,
        < 0 => Sound.Lose,
        _ => null,
    };

    private void Play(Sound sound)
    {
        if (IsSoundOn)
        {
            SoundPlayer?.Play(sound);
        }
    }

    private void SetChips(IEnumerable<int> chips)
    {
        BetStacks.Clear();
        foreach (var group in chips.GroupBy(c => c).OrderBy(g => g.Key))
        {
            BetStacks.Add(new ChipStackViewModel(new ChipViewModel(group.Key)) { Count = group.Count() });
        }

        UpdateBet();
    }

    private void UpdateBet()
    {
        Bet = BetStacks.Sum(s => s.Chip.Value * (decimal)s.Count);

        // The bet setter refreshes only when the sum changes, but Remove depends on the stacks themselves.
        RefreshCommands();
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
        if (Player.PendingDecision is not { } context || !Player.CanChoose(action))
        {
            return;
        }

        // Before the engine goes on: a round without further decisions ends inside Player.Choose.
        var recommended = BasicStrategyTable.Full.Decide(context);
        if (ActiveHints == HintMode.Show)
        {
            ClearHint();
        }
        else if (ActiveHints == HintMode.Feedback && action != recommended)
        {
            Hint = DisplayText.StrategyFeedback(recommended, action, context);
            IsHintMistake = true;
        }

        Phase = TablePhase.Dealing;
        if (!Player.Choose(action))
        {
            Phase = TablePhase.Playing;
        }

        RefreshCommands();
    }

    private void ClearHint()
    {
        Hint = "";
        IsHintMistake = false;
        RecommendedAction = null;
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
