namespace Blackjack.Core.Tests.Helpers;

/// <summary>Records every notification as a short log entry and keeps the typed notifications.</summary>
public sealed class RecordingObserver : IGameObserver
{
    private readonly string _prefix;

    public RecordingObserver(List<string>? sharedLog = null, string prefix = "")
    {
        Log = sharedLog ?? [];
        _prefix = prefix;
    }

    public List<string> Log { get; }

    public List<CardDealtNotification> CardsDealt { get; } = [];

    public List<HoleCardRevealedNotification> HoleCardsRevealed { get; } = [];

    public List<PlayerHandFinishedNotification> HandsFinished { get; } = [];

    public List<HandSettledNotification> HandsSettled { get; } = [];

    public List<RoundStartedNotification> RoundsStarted { get; } = [];

    public List<RoundResult> RoundsCompleted { get; } = [];

    public List<HandSplitNotification> Splits { get; } = [];

    public List<HandDoubledNotification> Doubles { get; } = [];

    public List<InsuranceSettledNotification> Insurances { get; } = [];

    public ValueTask OnInsuranceSettledAsync(InsuranceSettledNotification notification, CancellationToken ct)
    {
        Insurances.Add(notification);
        return Record("InsuranceSettled");
    }

    public ValueTask OnHandSplitAsync(HandSplitNotification notification, CancellationToken ct)
    {
        Splits.Add(notification);
        return Record("HandSplit");
    }

    public ValueTask OnHandDoubledAsync(HandDoubledNotification notification, CancellationToken ct)
    {
        Doubles.Add(notification);
        return Record("HandDoubled");
    }

    public ValueTask OnShoeShuffledAsync(CancellationToken ct) => Record("ShoeShuffled");

    public ValueTask OnRoundStartedAsync(RoundStartedNotification notification, CancellationToken ct)
    {
        RoundsStarted.Add(notification);
        return Record("RoundStarted");
    }

    public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
    {
        CardsDealt.Add(notification);
        return Record(notification.Recipient.IsDealer ? "CardDealt:Dealer" : "CardDealt:Player");
    }

    public ValueTask OnHoleCardRevealedAsync(HoleCardRevealedNotification notification, CancellationToken ct)
    {
        HoleCardsRevealed.Add(notification);
        return Record("HoleCardRevealed");
    }

    public ValueTask OnPlayerHandFinishedAsync(PlayerHandFinishedNotification notification, CancellationToken ct)
    {
        HandsFinished.Add(notification);
        return Record("PlayerHandFinished");
    }

    public ValueTask OnHandSettledAsync(HandSettledNotification notification, CancellationToken ct)
    {
        HandsSettled.Add(notification);
        return Record("HandSettled");
    }

    public ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct)
    {
        RoundsCompleted.Add(result);
        return Record("RoundCompleted");
    }

    private ValueTask Record(string entry)
    {
        Log.Add(_prefix + entry);
        return ValueTask.CompletedTask;
    }
}
