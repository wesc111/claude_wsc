namespace Blackjack.Core;

/// <summary>Data of <see cref="IGameObserver.OnInsuranceSettledAsync"/>.</summary>
/// <param name="Bet">The insurance bet (half the bet).</param>
/// <param name="Net">+2 × <paramref name="Bet"/> if the dealer has Blackjack, otherwise −<paramref name="Bet"/>.</param>
public sealed record InsuranceSettledNotification(decimal Bet, decimal Net);
