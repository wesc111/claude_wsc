using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Blackjack.Gui.Views;

/// <summary>
/// The print on the felt between dealer and player (spec E7): two lines of text and a banner, on arcs around a centre
/// above the control (the dealer). Letters are placed one by one along the arcs; the lowest point of the banner touches
/// the bottom of the control.
/// </summary>
public sealed class TablePrint : Control
{
    /// <summary>The large first line, e.g. <c>BLACKJACK PAYS 3 TO 2</c>.</summary>
    public static readonly StyledProperty<string?> PayoutTextProperty =
        AvaloniaProperty.Register<TablePrint, string?>(nameof(PayoutText));

    /// <summary>The small second line, e.g. <c>Dealer Must Stand on Soft 17</c>.</summary>
    public static readonly StyledProperty<string?> RuleTextProperty =
        AvaloniaProperty.Register<TablePrint, string?>(nameof(RuleText));

    /// <summary>The text in the banner, drawn in small capitals.</summary>
    public static readonly StyledProperty<string?> BannerTextProperty =
        AvaloniaProperty.Register<TablePrint, string?>(nameof(BannerText));

    // Radii of the baselines and of the banner edges; the larger the radius, the flatter the arc.
    private const double PayoutRadius = 1100;
    private const double RuleRadius = PayoutRadius + 21;
    private const double BannerInner = RuleRadius + 10;
    private const double BannerOuter = BannerInner + 28;
    private const double BannerHalfWidth = 290;

    // The tails of the banner lie behind its ends, shifted towards the centre.
    private const double TailShift = 13;
    private const double TailLength = 46;
    private const double TailNotch = 12;

    private const double PayoutSize = 27;
    private const double RuleSize = 15;
    private const double BannerSize = 20;

    private static readonly Typeface Serif = new(new FontFamily("Times New Roman, Georgia, serif"), FontStyle.Normal, FontWeight.Bold);
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#e6f4f1e8"));
    private static readonly IPen Outline = new Pen(new SolidColorBrush(Color.Parse("#d9bf4a")), 1.6);

    // The felt colour of MainWindow, so the banner hides its tails.
    private static readonly IBrush Felt = new SolidColorBrush(Color.Parse("#0b5d2a"));
    private static readonly IBrush TailFelt = new SolidColorBrush(Color.Parse("#094d23"));

    static TablePrint()
    {
        AffectsRender<TablePrint>(PayoutTextProperty, RuleTextProperty, BannerTextProperty);
    }

    /// <summary>The large first line.</summary>
    public string? PayoutText
    {
        get => GetValue(PayoutTextProperty);
        set => SetValue(PayoutTextProperty, value);
    }

    /// <summary>The small second line.</summary>
    public string? RuleText
    {
        get => GetValue(RuleTextProperty);
        set => SetValue(RuleTextProperty, value);
    }

    /// <summary>The text in the banner.</summary>
    public string? BannerText
    {
        get => GetValue(BannerTextProperty);
        set => SetValue(BannerTextProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var centre = new Point(Bounds.Width / 2, Bounds.Height - BannerOuter - 2);
        var half = Math.Asin(BannerHalfWidth / BannerOuter);

        var tail = (TailLength + TailNotch) / BannerOuter;
        foreach (var side in new[] { -1, 1 })
        {
            context.DrawGeometry(TailFelt, Outline, Tail(centre, side * half, side, tail));
        }

        context.DrawGeometry(Felt, Outline, Band(centre, half));

        DrawArcText(context, centre, PayoutText, PayoutRadius, PayoutSize, smallCaps: false);
        DrawArcText(context, centre, RuleText, RuleRadius, RuleSize, smallCaps: false);
        DrawArcText(context, centre, BannerText, BannerOuter - 8, BannerSize, smallCaps: true);
    }

    /// <summary>A point on the circle around <paramref name="centre"/>; the angle is measured from straight down, positive to the right.</summary>
    private static Point OnArc(Point centre, double radius, double angle) =>
        new(centre.X + radius * Math.Sin(angle), centre.Y + radius * Math.Cos(angle));

    private static StreamGeometry Band(Point centre, double half)
    {
        var band = new StreamGeometry();
        using var g = band.Open();
        g.BeginFigure(OnArc(centre, BannerInner, -half), true);
        g.ArcTo(OnArc(centre, BannerInner, half), new Size(BannerInner, BannerInner), 0, false, SweepDirection.CounterClockwise);
        g.LineTo(OnArc(centre, BannerOuter, half));
        g.ArcTo(OnArc(centre, BannerOuter, -half), new Size(BannerOuter, BannerOuter), 0, false, SweepDirection.Clockwise);
        g.EndFigure(true);
        return band;
    }

    /// <summary>A tail behind one end of the banner, with a V notch at its outer end.</summary>
    private static StreamGeometry Tail(Point centre, double end, int side, double length)
    {
        var inner = BannerInner - TailShift;
        var outer = BannerOuter - TailShift;
        var start = end - side * 0.02;
        var tip = end + side * length;
        var notch = end + side * (length - TailNotch / BannerOuter);
        var tail = new StreamGeometry();
        using var g = tail.Open();
        g.BeginFigure(OnArc(centre, inner, start), true);
        g.LineTo(OnArc(centre, inner, tip));
        g.LineTo(OnArc(centre, (inner + outer) / 2, notch));
        g.LineTo(OnArc(centre, outer, tip));
        g.LineTo(OnArc(centre, outer, start));
        g.EndFigure(true);
        return tail;
    }

    /// <summary>
    /// Draws <paramref name="text"/> centred on the arc with the given baseline radius, the letters upright towards
    /// the centre. Small capitals draw lower-case letters as capitals at 78 % size.
    /// </summary>
    private static void DrawArcText(DrawingContext context, Point centre, string? text, double radius, double size, bool smallCaps)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var tracking = size * 0.05;
        var letters = text.Select(c => smallCaps && char.IsLower(c)
                ? Letter(char.ToUpperInvariant(c), size * 0.78)
                : Letter(c, size))
            .ToList();
        var length = letters.Sum(l => l.WidthIncludingTrailingWhitespace + tracking) - tracking;

        var position = -length / 2;
        foreach (var letter in letters)
        {
            var width = letter.WidthIncludingTrailingWhitespace;
            var angle = (position + width / 2) / radius;
            var at = OnArc(centre, radius, angle);
            // Rotating by -angle keeps the letter upright towards the centre; its baseline lies on the arc.
            using (context.PushTransform(Matrix.CreateRotation(-angle) * Matrix.CreateTranslation(at.X, at.Y)))
            {
                context.DrawText(letter, new Point(-width / 2, -letter.Baseline));
            }

            position += width + tracking;
        }
    }

    private static FormattedText Letter(char c, double size) =>
        new(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Serif, size, Ink);
}
