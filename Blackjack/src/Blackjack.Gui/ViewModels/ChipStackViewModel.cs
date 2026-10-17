using CommunityToolkit.Mvvm.ComponentModel;

namespace Blackjack.Gui.ViewModels;

/// <summary>A stack of chips of one value in the bet area.</summary>
public sealed class ChipStackViewModel : ObservableObject
{
    /// <summary>The most chips drawn in one stack; the count below the stack shows the real number.</summary>
    public const int MaxVisibleLayers = 8;

    /// <summary>The vertical offset between two chips of a stack, in pixels.</summary>
    public const double LayerOffset = 5;

    private int _count;

    /// <summary>Creates a stack with one chip.</summary>
    public ChipStackViewModel(ChipViewModel chip)
    {
        Chip = chip;
        _count = 1;
    }

    /// <summary>The chip of this stack.</summary>
    public ChipViewModel Chip { get; }

    /// <summary>The number of chips in the stack.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(CountText));
                OnPropertyChanged(nameof(Layers));
            }
        }
    }

    /// <summary>The number of chips as text, e.g. <c>×3</c>.</summary>
    public string CountText => $"×{Count}";

    /// <summary>The chips to draw, bottom first (at most <see cref="MaxVisibleLayers"/>).</summary>
    public IReadOnlyList<ChipLayer> Layers =>
        [.. Enumerable.Range(0, Math.Min(Count, MaxVisibleLayers)).Select(i => new ChipLayer(Chip, i * LayerOffset))];
}
