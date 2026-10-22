namespace Blackjack.Gui.ViewModels;

/// <summary>A row of the Illustrious 18 in the counting help (spec E14), e.g. hard 16 vs 10: Hit, Stand at TC ≥ 0.</summary>
/// <param name="Hand">The player hand, e.g. <c>Hard 16</c> or <c>10,10</c>.</param>
/// <param name="Dealer">The dealer upcard, e.g. <c>10</c> or <c>A</c>.</param>
/// <param name="Basic">The basic strategy action.</param>
/// <param name="CountPlay">The action at the index.</param>
/// <param name="When">The condition, e.g. <c>TC ≥ +4</c> or <c>TC &lt; -1</c>.</param>
public sealed record HelpIndexPlay(string Hand, string Dealer, string Basic, string CountPlay, string When);
