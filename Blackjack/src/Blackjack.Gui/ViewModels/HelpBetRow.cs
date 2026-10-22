namespace Blackjack.Gui.ViewModels;

/// <summary>A row of the bet spread in the counting help (spec E14), e.g. <c>+2 to below +3</c>: <c>2 units</c>.</summary>
/// <param name="TrueCount">The true count range.</param>
/// <param name="Bet">The bet in units.</param>
public sealed record HelpBetRow(string TrueCount, string Bet);
