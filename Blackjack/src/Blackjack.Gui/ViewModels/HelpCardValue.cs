namespace Blackjack.Gui.ViewModels;

/// <summary>A row of the Hi-Lo card values in the counting help (spec E14), e.g. <c>2 3 4 5 6</c> count <c>+1</c>.</summary>
/// <param name="Cards">The ranks, e.g. <c>2 3 4 5 6</c>.</param>
/// <param name="Value">Their count value, e.g. <c>+1</c>.</param>
public sealed record HelpCardValue(string Cards, string Value);
