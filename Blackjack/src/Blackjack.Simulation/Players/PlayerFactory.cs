namespace Blackjack.Simulation.Players;

/// <summary>Creates the automated players by their command-line name.</summary>
internal static class PlayerFactory
{
    /// <summary>The valid strategy names.</summary>
    public static IReadOnlyList<string> Names { get; } = ["basic", "basic-hs", "mimic", "neverbust", "hilo"];

    /// <summary>The bet spread of <c>hilo</c>: the maximum bet is this many units (SPEC E13).</summary>
    public const int HiLoMaxUnits = 8;

    /// <summary>Creates the player named <paramref name="name"/>; <paramref name="shoe"/> is needed by <c>hilo</c> only.</summary>
    /// <exception cref="ArgumentException">The name is unknown, or <c>hilo</c> without a shoe.</exception>
    public static IPlayerStrategy Create(string name, decimal bet, Rules rules, ICardSource? shoe = null) => name switch
    {
        "basic" => new BasicStrategyPlayer(bet, BasicStrategyTable.Full),
        "basic-hs" => new BasicStrategyPlayer(bet, BasicStrategyTable.HitStand),
        "mimic" => new MimicDealerPlayer(bet, rules),
        "neverbust" => new NeverBustPlayer(bet),
        "hilo" => new HiLoPlayer(shoe ?? throw new ArgumentException("The strategy hilo needs the shoe.", nameof(shoe))),
        _ => throw new ArgumentException($"Unknown strategy '{name}'. Valid: {string.Join(", ", Names)}.", nameof(name)),
    };
}
