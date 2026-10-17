namespace Blackjack.Gui.ViewModels;

/// <summary>One chip drawn in a stack of the bet area.</summary>
/// <param name="Chip">The chip.</param>
/// <param name="Lift">Distance from the bottom of the stack in pixels.</param>
public sealed record ChipLayer(ChipViewModel Chip, double Lift);
