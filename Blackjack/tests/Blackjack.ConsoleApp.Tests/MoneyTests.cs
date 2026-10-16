namespace Blackjack.ConsoleApp.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("975", "975")]
    [InlineData("22.5", "22.5")]
    [InlineData("22.50", "22.5")]
    [InlineData("12.25", "12.25")]
    [InlineData("-25", "-25")]
    public void Format(string amount, string expected) =>
        Assert.Equal(expected, Money.Format(D(amount)));

    [Theory]
    [InlineData("15", "+15")]
    [InlineData("-25", "-25")]
    [InlineData("0", "0")]
    public void FormatSigned(string amount, string expected) =>
        Assert.Equal(expected, Money.FormatSigned(D(amount)));

    [Theory]
    [InlineData("12.5", "12.5")]
    [InlineData("12,5", "12.5")]
    [InlineData(" 25 ", "25")]
    [InlineData("-10", "-10")]
    public void ParsesBothDecimalSeparators(string text, string expected)
    {
        Assert.True(Money.TryParse(text, out var amount));
        Assert.Equal(D(expected), amount);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1e3")]
    public void RejectsNonNumbers(string text) =>
        Assert.False(Money.TryParse(text, out _));

    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
