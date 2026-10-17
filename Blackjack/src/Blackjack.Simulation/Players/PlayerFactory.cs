namespace Blackjack.Simulation.Players;

/// <summary>Creates the automated players by their command-line name.</summary>
internal static class PlayerFactory
{
    /// <summary>The valid strategy names.</summary>
    public static IReadOnlyList<string> Names { get; } = ["basic", "basic-hs", "mimic", "neverbust"];

    /// <summary>Creates the player named <paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException">The name is unknown.</exception>
    public static FlatBetPlayer Create(string name, decimal bet, Rules rules) => name switch
    {
        "basic" => new BasicStrategyPlayer(bet, BasicStrategyTable.Full),
        "basic-hs" => new BasicStrategyPlayer(bet, BasicStrategyTable.HitStand),
        "mimic" => new MimicDealerPlayer(bet, rules),
        "neverbust" => new NeverBustPlayer(bet),
        _ => throw new ArgumentException($"Unknown strategy '{name}'. Valid: {string.Join(", ", Names)}.", nameof(name)),
    };
}
