namespace Blackjack.Gui.ViewModels;

/// <summary>A casino chip with its value and colour.</summary>
/// <param name="Value">The value of the chip, one of <see cref="Values"/>.</param>
public sealed record ChipViewModel(int Value)
{
    /// <summary>The chip values in the tray, ascending.</summary>
    public static IReadOnlyList<int> Values { get; } = [5, 10, 25, 100];

    /// <summary>The colour of the chip as hex RGB: 5 red, 10 blue, 25 green, 100 black.</summary>
    public string Color => Value switch
    {
        5 => "#c62828",
        10 => "#1565c0",
        25 => "#2e7d32",
        100 => "#212121",
        _ => "#757575",
    };

    /// <summary>The value as text, e.g. <c>25</c>.</summary>
    public string Text => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Splits an amount into as few chips as possible, largest first (e.g. 35 = 25 + 10).
    /// A rest smaller than the smallest chip is dropped.
    /// </summary>
    public static IReadOnlyList<int> Split(decimal amount)
    {
        var chips = new List<int>();
        for (var i = Values.Count - 1; i >= 0; i--)
        {
            while (amount >= Values[i])
            {
                chips.Add(Values[i]);
                amount -= Values[i];
            }
        }

        return chips;
    }
}
