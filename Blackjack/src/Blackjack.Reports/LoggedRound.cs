namespace Blackjack.Reports;

/// <summary>One completed round of a session log.</summary>
/// <param name="Number">The round number, starting at 1.</param>
/// <param name="Time">When the round was completed.</param>
/// <param name="Result">The result returned by the engine.</param>
public sealed record LoggedRound(int Number, DateTimeOffset Time, RoundResult Result);
