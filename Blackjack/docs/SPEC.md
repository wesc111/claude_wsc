# Blackjack – Requirements Specification

Version 1.2 · Status: Draft for implementation with Claude Code

Changes in 1.1: outcome names unified (`DealerBlackjack` added), player can leave via `null` bet, cancellation semantics, async `IGameObserver` instead of .NET events, hole-card visibility rules, `HandCompleted` split into `PlayerHandFinished`/`HandSettled`, unlimited bankroll via `null`, bet validation defined, `Rules` shape with H17 / payout / decks / penetration supported from M1 (no-peek rejected), shoe behaviour (2.6) and `FixedCardSource` defined.

Changes in 1.2: M2 and M3 decisions (console app, simulation, measured reference values); M4 rules as `Rules` properties, allowed actions, split order (new hands appended), acceptance criteria, full basic strategy tables, house edge per initial bet.

---

## 1. Purpose and Scope

A Blackjack game in C# consisting of:

1. A **core library** containing the game engine, card handling, rules and the dealer (bank) strategy.
2. A **console application** in which a human plays against the dealer.
3. A **simulation tool** in which an automated player (AI) plays many rounds against the dealer and statistics are evaluated.
4. Later: extended rules (Double, Split) and an **Avalonia GUI** with graphical card rendering.

The project is developed in milestones (see section 10). **Only the milestone currently requested is implemented.** Later milestones are described so the design accommodates them without restructuring.

---

## 2. Game Rules

### 2.1 Table rules (defaults)

| Rule | Value | Meaning |
|---|---|---|
| Decks | 6 | 6 × 52 = 312 cards in one shoe |
| Penetration | 75 % | Shoe is reshuffled **between rounds** once 75 % of the cards have been dealt |
| Dealer soft 17 | **S17** | Dealer stands on soft 17 |
| Blackjack payout | **3:2** | Bet 10 → win 15 |
| Hole card | **Peek** | Dealer checks for Blackjack when the upcard is an Ace or ten-valued |
| Tie | **Push** | Bet is returned |

All rules are held in a `Rules` configuration object (not hard-coded). The values above are the defaults:

```csharp
public sealed record Rules
{
    public int Decks { get; init; } = 6;                    // 1–8
    public decimal Penetration { get; init; } = 0.75m;      // > 0 and ≤ 1
    public bool DealerHitsSoft17 { get; init; } = false;    // false = S17, true = H17
    public decimal BlackjackPayout { get; init; } = 1.5m;   // 1.5 = 3:2, 1.2 = 6:5; > 0
    public bool DealerPeeks { get; init; } = true;
    public decimal MinBet { get; init; } = 10m;             // > 0
    public decimal MaxBet { get; init; } = 500m;            // ≥ MinBet
    public static Rules Default { get; } = new();
}
```

- `Decks`, `Penetration`, `DealerHitsSoft17` and `BlackjackPayout` are fully supported from M1 on, with any value in the ranges given.
- `DealerPeeks = false` (no-peek / ENHC) is not supported: the `BlackjackGame` constructor throws `NotSupportedException`.
- A value outside its range throws `ArgumentOutOfRangeException`. Each property is checked when it is set; `MaxBet ≥ MinBet` is checked by the `BlackjackGame` constructor, because `init` properties can be set in any order.

### 2.2 Card values

| Rank | Value |
|---|---|
| 2–10 | face value |
| Jack, Queen, King | 10 |
| Ace | 11 or 1 |

### 2.3 Hand evaluation

- **Total:** Sum of all cards. Aces count 11 as long as the total does not exceed 21; otherwise individual Aces count 1 until the total is ≤ 21 or all Aces count 1.
- **Soft hand:** At least one Ace currently counts as 11.
- **Hard hand:** No Ace currently counts as 11.
- **Blackjack:** Exactly two cards, an Ace and a ten-valued card, in the **initial** hand. (Later: not after a split.)
- **Bust:** Total > 21.

### 2.4 Round sequence (milestone 1: Hit and Stand only)

1. **Reshuffle check:** If the penetration mark has been reached, reshuffle the shoe.
2. **Bet:** Player places a bet, or returns `null` to leave the table (see 2.5).
3. **Deal:** Player card (up), dealer card (up = *upcard*), player card (up), dealer card (down = *hole card*).
4. **Peek:** If the upcard is an Ace or ten-valued, the dealer checks the hole card:
   - Dealer has Blackjack, player has Blackjack → **Push**
   - Dealer has Blackjack, player does not → **DealerBlackjack**, player loses the bet, round ends
   - No dealer Blackjack → continue
5. **Player Blackjack:** If the player has Blackjack (and the dealer does not), it is paid **3:2** immediately; round ends.
6. **Player turn:** The player chooses **Hit** or **Stand** repeatedly.
   - Total > 21 → **Bust**, player loses immediately; the dealer does not play out.
   - Total = 21 → the turn ends automatically (implicit Stand).
7. **Dealer turn:** Reveal the hole card. Dealer draws while total ≤ 16 and stands on hard 17+ and soft 17+ (S17).
8. **Settlement:**

| Situation | `HandOutcome` | Payout (net) |
|---|---|---|
| Player Blackjack, dealer no Blackjack | `BlackjackWin` | +1.5 × bet |
| Dealer Blackjack, player no Blackjack | `DealerBlackjack` | −bet |
| Player bust | `PlayerBust` | −bet |
| Dealer bust (player not bust) | `DealerBust` | +bet |
| Player total > dealer total | `Win` | +bet |
| Player total < dealer total | `Loss` | −bet |
| Equal totals / both Blackjack | `Push` | 0 |

`HandOutcome` has the extension methods `IsWin()` (`BlackjackWin`, `DealerBust`, `Win`) and `IsLoss()` (`DealerBlackjack`, `PlayerBust`, `Loss`).

Amounts are `decimal` (3:2 on odd bets produces fractional amounts).

**Hole card:** The hole card is always revealed (`HoleCardRevealed`) before the round completes, including when the round ends by dealer Blackjack, player Blackjack or player bust.

### 2.5 Bets, bankroll and leaving the table

- **Rules validation:** see 2.1.
- **Valid bet:** any `decimal` with `MinBet` ≤ bet ≤ `MaxBet` and bet ≤ bankroll. Fractional bets are allowed. Zero and negative bets are therefore always invalid.
- **Invalid bet:** the engine throws `ArgumentOutOfRangeException`; the bankroll is unchanged and no cards are dealt.
- **Bankroll too small:** if the bankroll is below `MinBet` when `PlayRoundAsync` is called, the engine throws `InvalidOperationException` without asking for a bet.
- **Leaving the table:** if `GetBetAsync` returns `null`, no round is played: no cards are dealt, the bankroll is unchanged, and `PlayRoundAsync` returns `null`.
- **Unlimited bankroll:** a starting bankroll of `null` means unlimited (simulation mode). The bankroll check is skipped and `Bankroll` stays `null`; net results are reported per round.
- **Cancellation:** if the `CancellationToken` is cancelled during a round, the round is aborted: the bankroll is restored to its value before the round, no `RoundCompleted` notification is sent, and `OperationCanceledException` is propagated. Cards already drawn stay drawn.
- **Concurrency:** calling `PlayRoundAsync` while a round is running throws `InvalidOperationException`.

### 2.6 Shoe

- **Construction:** `new Shoe(decks, penetration, seed?)`. The constructor shuffles the shoe. The engine sends no `ShoeShuffled` notification for this initial shuffle.
- **Random source:** with a seed, `new Random(seed)`; without one, `new Random()`. `Random.Shared` is never used, so parallel simulation workers stay independent.
- **Shuffle:** `Shuffle()` collects all cards, including those already dealt, and shuffles the full shoe with Fisher–Yates. It keeps using the same `Random` instance (no reseeding), so a seeded shoe is reproducible across reshuffles.
- **Reshuffle mark:** `NeedsReshuffle` is true when cards dealt ≥ total cards × penetration, compared exactly in `decimal` (6 decks, 75 %: 234 of 312).
- **No mid-round reshuffle:** if the mark is passed during a round, dealing continues; the engine reshuffles before the next round (2.4 step 1).
- **Empty source:** `Draw()` on an empty source throws `InvalidOperationException`.
- **Burn card:** none.

---

## 3. Architecture

### 3.1 Principles

- **The core library has no UI dependencies:** no `Console`, no Avalonia, no file I/O.
- **Player decisions come exclusively through an interface** (`IPlayerStrategy`). Human player (console, GUI) and AI are interchangeable.
- **Asynchronous decision interface**, so a GUI can wait for button clicks without blocking.
- **Determinism:** The random source is injectable and seedable. Tests can specify exact card sequences.
- **State and notifications instead of output:** The engine reports state changes to `IGameObserver`s (see 3.5) and returns a result object per round.
- **No external NuGet packages in `Blackjack.Core`.**

### 3.2 Solution structure

```
Blackjack.slnx
├─ src/
│  ├─ Blackjack.Core/            Class library: cards, shoe, hand, rules, engine, interfaces
│  ├─ Blackjack.ConsoleApp/      Console game for a human player          (M2)
│  ├─ Blackjack.Simulation/      AI players, simulator, statistics          (M3)
│  ├─ Blackjack.Reports/         Session statistics files for console and GUI (E2)
│  └─ Blackjack.Gui/             Avalonia app                              (M5)
├─ tests/
│  ├─ Blackjack.TestSupport/     Shared test helpers: FixedCardSource, card parsing (M2)
│  ├─ Blackjack.Core.Tests/      xUnit
│  ├─ Blackjack.ConsoleApp.Tests/ xUnit                                     (M2)
│  ├─ Blackjack.Simulation.Tests/ xUnit                                     (M3)
│  └─ Blackjack.Gui.Tests/       xUnit (ViewModels, GuiPlayer)              (M5)
├─ docs/SPEC.md
└─ CLAUDE.md
```

### 3.3 Main types (Blackjack.Core)

| Type | Kind | Responsibility |
|---|---|---|
| `Suit` | enum | Clubs, Diamonds, Hearts, Spades |
| `Rank` | enum | Two … Ten, Jack, Queen, King, Ace |
| `Card` | readonly record struct | Suit, Rank, `IsTenValued`, base value |
| `ICardSource` | interface | `Card Draw()`, `bool NeedsReshuffle`, `void Shuffle()`, `int CardsRemaining`, `int TotalCards` (since E1) |
| `Shoe` | class | Implements `ICardSource`; n decks, Fisher–Yates shuffle, penetration, seedable `Random` (see 2.6) |
| `Hand` | class | Cards, `Total`, `IsSoft`, `IsBlackjack`, `IsBust`; from M4 `IsPair`, `IsFromSplit` (see 7.4) |
| `Rules` | record | Table rules and bet limits (see 2.1) |
| `PlayerAction` | enum | `Hit`, `Stand`; from M4 `Double`, `Split` (`Surrender` is a non-goal) |
| `HandOutcome` | enum | `BlackjackWin`, `Win`, `DealerBust`, `Push`, `Loss`, `PlayerBust`, `DealerBlackjack`; extension methods `IsWin()`, `IsLoss()` |
| `DealerStrategy` | class | Decides dealer Hit/Stand according to `Rules` |
| `BlackjackGame` | class | Engine: runs a round, peek, settlement, bankroll |
| `IPlayerStrategy` | interface | Bet and action decisions |
| `IGameObserver` | interface | Async notifications about the course of a round (see 3.5) |
| `HandResult` | record | Snapshot of one player hand: cards, total, soft flag, bet, outcome, net amount |
| `RoundResult` | record | Snapshot of the round: total bet, `IReadOnlyList<HandResult>`, dealer cards, dealer total, net amount, new bankroll (`null` if unlimited). Contains no mutable `Hand` objects. |

### 3.4 Player interface

```csharp
public interface IPlayerStrategy
{
    /// <returns>The bet, or <c>null</c> to leave the table.</returns>
    Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default);
    Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default);
}

/// <param name="Bankroll">Current bankroll, or <c>null</c> if unlimited.</param>
public sealed record BetContext(decimal? Bankroll, Rules Rules)
{
    /// <summary>The largest valid bet: MaxBet, limited by the bankroll.</summary>
    public decimal MaxAllowedBet { get; }
    /// <summary>True if the bet satisfies the rules of 2.5. The engine uses the same check.</summary>
    public bool IsValidBet(decimal bet);
}

public sealed record DecisionContext(
    IReadOnlyList<Card> PlayerCards,
    int PlayerTotal,
    bool IsSoft,
    Card DealerUpcard,
    IReadOnlySet<PlayerAction> AllowedActions);
```

- The engine only offers actions that are currently legal (M1: always `Hit` and `Stand`; from M4 see 7.2).
- If a strategy returns an action not in `AllowedActions`, the engine throws an `InvalidOperationException`.
- A player hand may later consist of several hands (split). The engine must therefore internally manage a **list of player hands**, even if M1 only ever uses one.

### 3.5 Observer notifications

`BlackjackGame` reports the course of a round through the async interface `IGameObserver` (not through .NET `event`s). Each notification is a method returning `ValueTask` and taking a `CancellationToken`; the engine **awaits** each call before continuing, so a GUI can complete an animation before the next card is dealt. Observers that do nothing return a completed `ValueTask`, so simulation pays almost no overhead.

| Notification | Data |
|---|---|
| `ShoeShuffled` | – |
| `RoundStarted` | bet, bankroll (`null` if unlimited) |
| `CardDealt` | recipient (player hand index / dealer), card (`null` when dealt face down), face-up flag |
| `HoleCardRevealed` | card |
| `PlayerHandFinished` | hand index, total, soft flag, Blackjack flag, bust flag – sent when the player's turn for that hand ends |
| `HandSettled` | hand index, outcome, net amount – sent during settlement |
| `RoundCompleted` | `RoundResult` |

- `ShoeShuffled` (if a reshuffle happens) comes before `RoundStarted`.
- Observers are passed to the engine constructor. They are called one after another in registration order, on the thread that runs `PlayRoundAsync`. A GUI observer is responsible for dispatching to the UI thread.
- An exception thrown by an observer aborts the round in the same way as a cancellation (bankroll restored) and is propagated.

Requirement: All cards visible on the table are reported. A face-down card is never reported until it is revealed. (This later allows card-counting strategies without changes to the engine.)

### 3.6 Engine API (minimum)

```csharp
public sealed class BlackjackGame
{
    /// <param name="startingBankroll"><c>null</c> = unlimited (simulation mode).</param>
    public BlackjackGame(Rules rules, ICardSource cards, decimal? startingBankroll,
                         IEnumerable<IGameObserver>? observers = null);
    public decimal? Bankroll { get; }
    /// <returns>The round result, or <c>null</c> if the player left the table.</returns>
    public Task<RoundResult?> PlayRoundAsync(IPlayerStrategy player, CancellationToken ct = default);
}
```

Error and cancellation behaviour is defined in 2.5.

---

## 4. Milestone 1 – Core library

**Scope:** Section 2 and 3 with actions Hit and Stand only.

### 4.1 Acceptance criteria – hand evaluation

| Cards | Total | Soft | Blackjack | Bust |
|---|---|---|---|---|
| A, K | 21 | yes | yes | no |
| A, 6 | 17 | yes | no | no |
| A, 6, 10 | 17 | no | no | no |
| A, A | 12 | yes | no | no |
| A, A, 9 | 21 | yes | no | no |
| A, A, A, A, 7 | 21 | yes | no | no |
| 10, 6, 6 | 22 | no | no | yes |
| 7, 7, 7 | 21 | no | no | no |
| 5, 6, Q (three cards) | 21 | no | no | no |
| A, 5, 5 (three cards) | 21 | yes | no | no |

### 4.2 Acceptance criteria – dealer strategy (S17)

| Dealer hand | Action |
|---|---|
| 10, 6 (hard 16) | Hit |
| 10, 7 (hard 17) | Stand |
| A, 6 (soft 17) | Stand |
| A, 5 (soft 16) | Hit |
| A, 6, 10 (hard 17) | Stand |
| A, 7 (soft 18) | Stand |
| 2, 3, A (soft 16) | Hit |

With `DealerHitsSoft17 = true` (H17):

| Dealer hand | Action |
|---|---|
| A, 6 (soft 17) | Hit |
| 2, 4, A (soft 17) | Hit |
| A, 6, 10 (hard 17) | Stand |
| A, 7 (soft 18) | Stand |
| 10, 7 (hard 17) | Stand |

### 4.3 Acceptance criteria – round flow (with predefined card sequence)

| Case | Expected |
|---|---|
| Upcard A, dealer Blackjack, player no Blackjack | `DealerBlackjack`, net −bet, player is never asked for a decision |
| Upcard 10, dealer Blackjack, player Blackjack | `Push` |
| Player Blackjack, upcard 9 | `BlackjackWin`, net +1.5 × bet, no dealer turn |
| Player Blackjack, bet 15 | Net +22.5 |
| `BlackjackPayout = 1.2` (6:5), player Blackjack, bet 10 | `BlackjackWin`, net +12 |
| Player hits to 22 | `PlayerBust`, dealer draws no further cards |
| Player stands on 18, dealer ends on 18 | `Push` |
| Player stands on 17, dealer ends on 18 | `Loss`, net −bet |
| Player stands on 19, dealer ends on 18 | `Win`, net +bet |
| Player stands on 12, dealer busts | `DealerBust`, net +bet |
| Player reaches 21 with three cards | Turn ends automatically, no further decision requested |
| Strategy returns illegal action | `InvalidOperationException` |
| Bet below MinBet, above MaxBet, above bankroll, zero or negative | `ArgumentOutOfRangeException`, bankroll unchanged, no card drawn |
| Fractional bet within limits (e.g. 12.5) | Accepted |
| Bankroll below MinBet when the round starts | `InvalidOperationException`, `GetBetAsync` is not called |
| `GetBetAsync` returns `null` | `PlayRoundAsync` returns `null`, no card drawn, bankroll unchanged |
| Starting bankroll `null` (unlimited), bet 500 | Round is played, `Bankroll` and `RoundResult` bankroll are `null`, net amount reported |
| Token cancelled during `DecideAsync` | `OperationCanceledException`, bankroll equals value before the round, no `RoundCompleted` |
| `PlayRoundAsync` called while a round is running | `InvalidOperationException` |
| `Rules` with `MinBet` ≤ 0, `Decks` outside 1–8, `Penetration` ≤ 0 or > 1, `BlackjackPayout` ≤ 0 | `ArgumentOutOfRangeException` when the property is set |
| `MaxBet` < `MinBet` | `ArgumentOutOfRangeException` from the `BlackjackGame` constructor |
| `DealerPeeks = false` | `NotSupportedException` from the `BlackjackGame` constructor |

### 4.3a Acceptance criteria – observer notifications

| Case | Expected |
|---|---|
| Normal round (player stands, dealer plays) | Notifications in order: `RoundStarted`, 4 × `CardDealt`, player `CardDealt`s, `PlayerHandFinished`, `HoleCardRevealed`, dealer `CardDealt`s, `HandSettled`, `RoundCompleted` |
| Player bust | `RoundStarted`, 4 × `CardDealt`, player `CardDealt`s, `PlayerHandFinished`, `HoleCardRevealed`, `HandSettled`, `RoundCompleted` |
| Player Blackjack | `RoundStarted`, 4 × `CardDealt`, `PlayerHandFinished`, `HoleCardRevealed`, `HandSettled`, `RoundCompleted` |
| Dealer Blackjack | `RoundStarted`, 4 × `CardDealt`, `HoleCardRevealed`, `PlayerHandFinished`, `HandSettled`, `RoundCompleted` |
| Any round | `PlayerHandFinished` is sent exactly once per player hand |
| Hole card dealt | `CardDealt` with face-up = false and card = `null` |
| Round ends by player bust | `HoleCardRevealed` is sent before `RoundCompleted` |
| Round ends by player Blackjack | `HoleCardRevealed` is sent before `RoundCompleted` |
| Round ends by dealer Blackjack | `HoleCardRevealed` is sent before `RoundCompleted` |
| Reshuffle needed before a round | `ShoeShuffled` is sent before `RoundStarted` |
| Observer awaits a delay in `CardDealt` | Engine does not deal the next card until the `ValueTask` completes |
| Several observers registered | All receive every notification, in registration order |
| Observer throws | Exception propagates, bankroll restored |

### 4.4 Acceptance criteria – shoe

- New 6-deck shoe contains 312 cards; each rank-suit combination exactly 6 times. A 1-deck shoe contains 52 cards, each exactly once.
- Identical seed → identical card sequence, including across a `Shuffle()`.
- 6 decks, 75 %: `NeedsReshuffle` is false after 233 cards and true after 234.
- After `Shuffle()`, all 312 cards are available again (`CardsRemaining` = 312) and `NeedsReshuffle` is false.
- `Draw()` on an empty source throws `InvalidOperationException`.
- The reshuffle mark passed mid-round: dealing continues, the shoe is reshuffled before the next round (`ShoeShuffled` before `RoundStarted`), not during the current one.

**`FixedCardSource`** (test helper, implements `ICardSource`):
- Deals the given list in order.
- `Draw()` after the list is used up throws `InvalidOperationException` ("unexpected draw"), so tests detect any extra card the engine draws.
- `NeedsReshuffle` is a settable property (default `false`), so tests can trigger a reshuffle.
- `Shuffle()` changes nothing but counts its calls.

---

## 5. Milestone 2 – Console application

- Start: bankroll (default 1000), MinBet 10, MaxBet 500 – configurable via command-line arguments `--bankroll`, `--min-bet`, `--max-bet`. Optional `--seed` for a reproducible shoe; `--help` shows the usage. Invalid arguments (including MaxBet < MinBet or bankroll < MinBet) → message, usage, exit code 1.
- `ConsolePlayer : IPlayerStrategy` reads bet and actions from the console.
- Display:
  - Cards with Unicode suit symbols, e.g. `A♠ 10♥`; red suits colored red if the terminal supports it (ANSI colors; off when output is redirected or `NO_COLOR` is set).
  - Dealer hole card hidden as `[??]` until revealed.
  - Current totals (soft hands as e.g. `soft 17`).
  - Result and new bankroll after each round.
  - Amounts without unnecessary decimals, `.` as decimal point (`22.5`, `975`).
- Bet prompt: Enter repeats the last bet (first round: MinBet), limited to the largest allowed bet; `q` quits. Both `.` and `,` are accepted as decimal separator.
- Commands during the turn: `h` = Hit, `s` = Stand, `q` = quit after the round (the player is told so and asked for h/s again).
- Invalid input → message and repeat prompt.
- Game ends when the player quits, the bankroll falls below MinBet, or the input ends (treated as quitting); a session summary is shown (rounds, wins, losses, pushes, net result).
- Ctrl+C aborts the current round (bankroll restored, round not counted), shows the summary and exits.
- Quitting: `ConsolePlayer` remembers a `q` and returns `null` from the next `GetBetAsync`.
- Display is driven by an `IGameObserver` (no game logic in the console app; totals are computed with Core's `Hand`, bet checks with `BetContext.IsValidBet`).
- Tests (`Blackjack.ConsoleApp.Tests`): argument parsing, input handling, card formatting, display, session statistics and an end-to-end session with a predefined card sequence and scripted input.

---

## 6. Milestone 3 – Simulation and statistics

### 6.1 AI players (implementations of `IPlayerStrategy`)

| Strategy | Description |
|---|---|
| `BasicStrategyPlayer` | Hit/Stand table depending on player total, soft/hard and dealer upcard (see 6.2); flat bet |
| `MimicDealerPlayer` | Plays like the dealer: hit ≤ 16, stand ≥ 17 (uses Core's `DealerStrategy`, so it also follows H17 when that rule is set) |
| `NeverBustPlayer` | Hard hands: stands on 12+, otherwise hits. Soft hands: hits up to soft 17, stands on soft 18+ |

Strategies always use a fixed bet (configurable).

### 6.2 Basic strategy (Hit/Stand only, S17, multi-deck)

The table must be stored as data (e.g. a 2-D array or JSON file) so it can later be extended for Double/Split. Implementation: one row of action letters per player total, one column per upcard 2–A (e.g. hard 12 = `HHSSSHHHHH`); M4 adds `D` and `P`. The same S17 table is used under H17 (the Hit/Stand differences are negligible).

**Hard totals** (columns: dealer upcard)

| Player | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | A |
|---|---|---|---|---|---|---|---|---|---|---|
| ≤ 11 | H | H | H | H | H | H | H | H | H | H |
| 12 | H | H | S | S | S | H | H | H | H | H |
| 13–16 | S | S | S | S | S | H | H | H | H | H |
| ≥ 17 | S | S | S | S | S | S | S | S | S | S |

**Soft totals**

| Player | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | A |
|---|---|---|---|---|---|---|---|---|---|---|
| ≤ soft 17 | H | H | H | H | H | H | H | H | H | H |
| soft 18 | S | S | S | S | S | S | S | H | H | H |
| ≥ soft 19 | S | S | S | S | S | S | S | S | S | S |

### 6.3 Simulator

- Command-line tool `blackjack-sim`: `--strategy basic|basic-hs|mimic|neverbust` (required; `basic` = full basic strategy of 7.6 from M4, `basic-hs` = Hit/Stand table of 6.2), `--rounds` (default 1,000,000), `--seed` (default: random, printed so the run can be repeated), `--bet` (default 10), `--workers` (default: number of CPU cores), `--csv <file>`, `--help`.
- Optional rule arguments: `--decks`, `--h17`, `--payout`, `--penetration` (defaults as in 2.1).
- The table limits are set to the bet (`MinBet = MaxBet = bet`).
- Parallel execution with independent engine instances and derived seeds; results reproducible for identical parameters (including the number of workers). Worker *i* plays a fixed share of the rounds with a seed derived from the main seed.
- Elapsed time and rounds per second are reported.
- Unlimited bankroll in simulation mode (no ruin): starting bankroll `null` (see 2.5).
- Performance target: 1 million rounds in well under one minute on a current machine.

### 6.4 Statistics (output)

| Metric | Description |
|---|---|
| Rounds | number of rounds played |
| Total wagered | sum of all bets |
| Net result | sum of all net payouts |
| House edge | −net result / sum of initial bets, in % (from M4, see 7.7; identical to −net / total wagered without Double and Split) |
| Standard error, 95 % CI | of the per-round result (in % of bet) |
| Win / loss / push rates | in % of rounds |
| Blackjack rate | player Blackjacks in % of rounds |
| Bust rates | player: in % of rounds; dealer: in % of the rounds in which the dealer played out the hand (player not bust, no Blackjack on either side) |
| Breakdown | result per dealer upcard (2–9, 10 for all ten-valued cards, A) and per player starting total (`hard 4` … `hard 20`, `soft 12` … `soft 20`, `Blackjack`); each row with rounds, house edge, win/loss/push rates |

- Statistics are computed from the returned `RoundResult`s; money totals in `decimal`, variance in `double` (in units of the bet).
- Output as a formatted table on the console, optionally as CSV (`--csv`): one table with columns `Category,Key,Rounds,InitialBets,Wagered,Net,HouseEdgePct,WinPct,LossPct,PushPct,BlackjackPct,PlayerBustPct,DealerBustPct,DoublePct,SplitPct` and rows `Total`, `DealerUpcard/<key>`, `StartingTotal/<key>`; invariant culture.
- Plausibility checks (tests, 1 million rounds, fixed seed; 95 % CI ≈ ±0.23 %):
  - `MimicDealerPlayer` yields a house edge of approx. 5–6 %.
  - `BasicStrategyPlayer` with the Hit/Stand table (`basic-hs`): 2.37 % ± 0.4 %.
  - `NeverBustPlayer`: 6.03 % ± 0.4 %.
  - Ranking: basic-hs < mimic < never-bust. (Planned as basic < never-bust < mimic; the measurement showed that without doubling, standing on 12–16 against a dealer 7–A costs never-bust more than mimic-the-dealer loses by busting.)
  - Player Blackjack rate ≈ 4.75 % (theory for 6 decks), never-bust never busts.
  - 1 million rounds take less than 60 seconds.
- Reference values measured in M3 with 2 × 20 million rounds (seeds 11 and 12, defaults of 2.1): basic 2.36 % / 2.39 %, mimic 5.68 % / 5.70 %, never-bust 6.04 % / 6.02 % (SE 0.02 % each). Speed: about 6 million rounds per second on an Apple Silicon Mac (Release build).

---

## 7. Milestone 4 – Double and Split

Extension of the rules and engine; all existing tests must remain green.

### 7.1 Rules

| Rule | Value |
|---|---|
| Double | only on the first two cards of a hand; bet doubled, exactly one more card, then automatic Stand |
| Split | two cards of equal **value** (also e.g. K + 10); each new hand receives the same bet |
| Re-split | up to max. 4 hands; Aces cannot be re-split |
| Split Aces | each Ace receives exactly one more card, then automatic Stand |
| DAS | Double after split allowed (except on split Aces) |
| Blackjack after split | counts as 21, not as Blackjack (paid 1:1) |
| Peek | the dealer checks for Blackjack before any player decision, so on a dealer Blackjack the round ends before any Double or Split and the player loses only the original bet |

The values above are the defaults of three new `Rules` properties, all fully supported:

| Property | Default | Range |
|---|---|---|
| `DoubleAfterSplit` | `true` | – |
| `MaxSplitHands` | 4 | 1–4 (1 = no splitting) |
| `ResplitAces` | `false` | – |

### 7.2 Allowed actions and bankroll

- `Hit`, `Stand`: as before. The turn ends automatically at 21 and on split Aces after their one card.
- `Double`: exactly two cards; not on split Aces; on a hand from a split only with `DoubleAfterSplit`; additional bet covered.
- `Split`: `Hand.IsPair` (two cards of equal value); fewer than `MaxSplitHands` hands; split Aces only with `ResplitAces`; additional bet covered.
- Additional bet covered: additional bet ≤ bankroll − sum of the bets already placed in this round. With unlimited bankroll (`null`) always covered.
- `Hand.IsPair` only describes the cards; whether a split is allowed is decided by the engine.

### 7.3 Round flow with several hands

- **Double:** the hand's bet is doubled (`HandDoubled`), the hand receives exactly one card, then its turn ends.
- **Split:** the second card moves to a new hand that is **appended** at the end (`HandSplit`), so the indices of existing hands never change. The current hand immediately receives a new card; the new hand receives its second card when its turn starts. Hands are played in ascending index order.
- After all hands: the hole card is revealed; the dealer plays out if at least one hand is not bust. Each hand is settled with its own bet (`HandSettled` per hand).
- A Blackjack is only possible on the initial (unsplit) hand.
- Cancellation or any other abort restores the bankroll as before (2.5).

### 7.4 Types

- `PlayerAction`: + `Double`, `Split`.
- `Hand`: + `IsFromSplit` (a split hand is never a Blackjack), `IsPair`.
- `DecisionContext`: + `HandIndex`, `HandCount` (optional trailing parameters, default 0 and 1).
- `HandResult`: + `IsDoubled`, `IsFromSplit`. `RoundResult`: + `InitialBet`; `TotalBet` includes the additional bets of Double and Split.
- `IGameObserver`: + `OnHandSplitAsync` (hand index, new hand index, moved card) and `OnHandDoubledAsync` (hand index, new bet), both with empty default implementations.

### 7.5 Acceptance criteria – Double and Split

| Case | Expected |
|---|---|
| Double on two cards | Bet doubled, exactly one card dealt, no further decision, net ±2 × bet |
| Hand with three cards | Double not offered |
| Bankroll does not cover the additional bet | Double and Split not offered |
| Split 8,8 | Two hands, each with its own bet, played and settled separately |
| Split K,10 | Allowed (equal value) |
| Two cards of different value | Split not offered |
| Re-split | Possible up to 4 hands; with 4 hands Split is no longer offered |
| Split Aces | Each Ace receives exactly one card, no decision is requested, no re-split |
| A + 10 after a split | `Win`, paid 1:1, not `BlackjackWin` |
| Double after split (DAS) | Offered on a split hand, not on split Aces |
| Dealer Blackjack (peek) | No decision requested, only the original bet is lost |
| All split hands bust | Dealer draws no card; if one hand stands, the dealer plays out |
| Strategy returns Double or Split when not allowed | `InvalidOperationException` |
| Notifications | `HandSplit` / `HandDoubled` in order, `CardDealt` with the correct hand index, `PlayerHandFinished` exactly once per hand |
| Cancellation during a split round | Bankroll restored |
| `DoubleAfterSplit = false` | Double not offered on split hands |
| `MaxSplitHands = 2` | No re-split |
| `ResplitAces = true` | Split Aces can be split again |
| `MaxSplitHands` outside 1–4 | `ArgumentOutOfRangeException` |

### 7.6 Basic strategy with Double and Split (6 decks, S17, DAS, no surrender)

Strategy `basic` uses these tables; the Hit/Stand table of 6.2 remains available as strategy `basic-hs`. Codes: `H` Hit, `S` Stand, `D` Double (otherwise Hit), `Ds` Double (otherwise Stand), `P` Split, `-` no split (use the total tables). If Double or Split is not allowed, the fallback action is used.

**Hard totals**

| Player | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | A |
|---|---|---|---|---|---|---|---|---|---|---|
| 4–8 | H | H | H | H | H | H | H | H | H | H |
| 9 | H | D | D | D | D | H | H | H | H | H |
| 10 | D | D | D | D | D | D | D | D | H | H |
| 11 | D | D | D | D | D | D | D | D | D | H |
| 12 | H | H | S | S | S | H | H | H | H | H |
| 13–16 | S | S | S | S | S | H | H | H | H | H |
| 17–21 | S | S | S | S | S | S | S | S | S | S |

**Soft totals**

| Player | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | A |
|---|---|---|---|---|---|---|---|---|---|---|
| soft 12 | H | H | H | H | H | H | H | H | H | H |
| soft 13–14 | H | H | H | D | D | H | H | H | H | H |
| soft 15–16 | H | H | D | D | D | H | H | H | H | H |
| soft 17 | H | D | D | D | D | H | H | H | H | H |
| soft 18 | S | Ds | Ds | Ds | Ds | S | S | H | H | H |
| soft 19–21 | S | S | S | S | S | S | S | S | S | S |

**Pairs**

| Pair | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | A |
|---|---|---|---|---|---|---|---|---|---|---|
| 2,2 / 3,3 | P | P | P | P | P | P | - | - | - | - |
| 4,4 | - | - | - | P | P | - | - | - | - | - |
| 5,5 | - | - | - | - | - | - | - | - | - | - |
| 6,6 | P | P | P | P | P | - | - | - | - | - |
| 7,7 | P | P | P | P | P | P | - | - | - | - |
| 8,8 | P | P | P | P | P | P | P | P | P | P |
| 9,9 | P | P | P | P | P | S | P | P | S | S |
| 10,10 | - | - | - | - | - | - | - | - | - | - |
| A,A | P | P | P | P | P | P | P | P | P | P |

### 7.7 Simulation

- **House edge = −net result / sum of initial bets** (standard definition, also used for the 0.4–0.5 % below). The total wagered including Double and Split is reported as well.
- New rates: Double rate and Split rate (in % of rounds).
- A + 10 after a split does not count as player Blackjack; the starting total of a split round is the original pair.
- Plausibility check: `basic` with 10 million rounds (95 % CI ≈ ±0.07 %) yields a house edge between 0.3 % and 0.6 %; also basic < basic-hs.
- Reference values measured in M4 with 2 × 20 million rounds (seeds 11 and 12, defaults): `basic` 0.41 % / 0.43 % (SE 0.03 %), Double rate 10.3 %, Split rate 2.5 %, total wagered 1.13 × initial bets. `basic-hs` reproduces the M3 value exactly (2.36 % with seed 11).

### 7.8 Console app

- New commands `d` = Double and `p` = Split; the prompt only lists the actions currently allowed, e.g. `(h)it, (s)tand, (d)ouble, s(p)lit, (q)uit after this round:`.
- With several hands, they are shown as "Hand 1", "Hand 2", …; the result is shown per hand plus a total line with the bankroll.

---

## 8. Milestone 5 – Avalonia GUI

- Avalonia 12 (current stable version 12.1.4: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`), MVVM with CommunityToolkit.Mvvm 8.4.
- Project `src/Blackjack.Gui`, tests in `tests/Blackjack.Gui.Tests` (ViewModels and `GuiPlayer` without UI; no headless UI tests). The window is checked manually / by screenshot.
- `GuiPlayer : IPlayerStrategy` with `TaskCompletionSource`; buttons (Hit, Stand, Double, Split) complete the pending task; buttons enabled only for `AllowedActions`.
- Graphical card rendering; animated dealing driven by an `IGameObserver` that awaits each `CardDealt` animation (duration configurable, 0 in tests). The hole card shows the card back and is turned over on `HoleCardRevealed`.
- The game loop runs on the UI thread, so all notifications arrive there (the engine keeps the synchronization context).
- Closing the window cancels the pending round via the `CancellationToken` (bankroll restored); a "Leave table" action makes `GuiPlayer` return `null` from `GetBetAsync` and shows the session summary.
- Display of bankroll, bet, dealer and player hands with totals (e.g. `soft 17`), bet and result per hand.
- Bet with chips 5, 10, 25, 100 (added up) and "Clear"; default is the last bet; checked with `BetContext.IsValidBet`. When the bankroll falls below the minimum bet, a message with "New game" is shown.
- Labels in English, like the rest of the project.
- Card graphics: the PNG set in `cards-png/` (see `docs/CARDS.md`) including `card_back.png`, embedded as Avalonia resources. Jack, queen and king use the alternative court designs `*_of_*2.png` (e.g. `jack_of_hearts2.png`); the plain court images are not embedded. Each card is drawn on a white card with a margin of 3 % of its width and height and a thin grey edge; the thin outline of the PNG is cropped, because the symbols of the set reach almost to the edge. SVG is not used because `Avalonia.Svg.Skia` is only available for Avalonia 11.
- `SessionStatistics` (rounds, wins, losses, pushes, net) moves from the console app to Core, so console and GUI share it.
- Must run on macOS (Apple Silicon); Windows and Linux desirable.

---

## 8a. Extensions after M5

### E1 – Shoe display and number of decks

- `ICardSource` gets `int TotalCards` (all cards of the source, dealt or not). `Shoe` already has it; `FixedCardSource` returns the length of its list. The display shows only the number of cards left in the shoe, never their values.
- Console app: new argument `--decks <1-8>` (default 6). Before each bet prompt (after a possible reshuffle) a line `Shoe: 247 of 312 cards left.` is shown.
- GUI:
  - Header shows `Shoe: 247 / 312` and a progress bar of the cards left, with a marker where the shoe will be reshuffled (at `Penetration`, i.e. 25 % left with the default 75 %). Updated after each dealt card and after a reshuffle.
  - Deck selector `Decks` 1–8 (default 6). Enabled only before the first round of a game (at start and after Game over / Leave table); a change before the first round creates a fresh shoe immediately, a change after the session ended applies to New game. Disabled while a game has rounds.

### E2 – Session statistics files

- When a session ends with at least one completed round, the GUI and the console app write three files into the directory `log` below the current working directory (created if missing; ignored by git; for the macOS app see E3):
  - `blackjack_<yyyy-MM-dd_HH-mm-ss>.txt` – readable report: header (start/end time, rules), session statistics, then the list of all rounds (one line per hand, bet, cards with totals, dealer cards, result, net, bankroll after the round).
  - `blackjack_<…>_statistics.csv` – the statistics as `Metric,Value` rows.
  - `blackjack_<…>_rounds.csv` – one row per hand: `Round,Time,Hand,Bet,PlayerCards,PlayerTotal,Doubled,FromSplit,DealerCards,DealerTotal,Outcome,Net,BankrollAfter`.
  - CSV files are UTF-8 with BOM (card symbols), invariant culture; the timestamp is the session start in local time.
- Session end: GUI – Leave table, bankroll below the minimum bet, closing the window; console – quit, bankroll below the minimum bet, end of input, Ctrl+C. Aborted rounds are not included. Files are written once per session.
- The path of the text file is shown (GUI status line, console after the summary). If writing fails, an error message is shown and the program continues.
- Statistics (`SessionStatistics` in Core, extended): rounds, hands, wins/losses/pushes per hand, player Blackjacks, player busts (hands), dealer busts (rounds), doubles and doubles won, split rounds, initial bets, total wagered, net (also in % of initial bets), start/end/highest/lowest bankroll, biggest win and biggest loss with round number.
- The report code lives in the new library `Blackjack.Reports` (references Core, no packages), used by both apps; Core stays free of file I/O.

### E3 – macOS app and DMG

- `scripts/make-macos-app.sh` builds `dist/Blackjack.app` and `dist/Blackjack-<version>.dmg` (`dist/` is ignored by git):
  `dotnet publish` Release for `osx-arm64`, self-contained (no .NET needed on the target Mac); Apple Silicon only.
- Bundle: `Info.plist` (name Blackjack, identifier `com.wesc111.blackjack`, version from `<Version>` in `Blackjack.Gui.csproj`, minimum macOS 13), icon `Blackjack.icns` created from `src/Blackjack.Gui/Assets/icon-1024.png` (jack of hearts and ace of spades) with `sips` and `iconutil`.
- Signing: every file in `Contents/MacOS` and then the bundle are signed ad-hoc (`codesign -s -`). No notarization: on other Macs the first start needs right-click → Open.
- The DMG contains the app and a link to `/Applications` for drag-and-drop installation.
- Log directory: started from an app bundle, the statistics files of E2 go to `~/Documents/Blackjack/log` (the working directory is `/` when started from the Finder); otherwise `log` below the working directory as before.
- The application name shown by macOS (menu bar) is "Blackjack".

### E4 – Chips and bet area (GUI)

- Chips are drawn as vector graphics in XAML (no image files): a coloured disc with white edge spots, a white inner field and the value in the middle. Each value has its own colour: 5 red, 10 blue, 25 green, 100 black.
- Layout: two columns. Left: the bet area and below it the bet buttons (chip tray, "Clear"). Middle: dealer and player hands, the result of the last round, below them the play buttons (Hit, Stand, Double, Split, New game), then set apart at the right the "Deal" button, highlighted (gold, larger, framed) because it starts the next round; below them the status line. Bet buttons and play buttons are in one row. Cards are shown at 150 × 218 (1.5 × the M5 size); the window is 1320 × 900.
- Chip tray: clicking a chip puts it into the bet area. A chip is disabled when it would raise the bet above `BetContext.MaxAllowedBet` (MaxBet, limited by the bankroll), and outside the betting phase.
- Bet area: an upright oval below the bankroll. One stack per chip value, the stacks above each other in ascending order of value; each stack is slightly offset so its height is visible (at most 8 chips drawn), with the number of chips beside it; the bet total below the stacks.
- Clicking a stack in the bet area removes one chip of that value; "Clear" removes all chips.
- Default bet: the area starts with the last bet (first round: MinBet), limited to `MaxAllowedBet`, split into as few chips as possible (e.g. 35 = 25 + 10). An amount that cannot be made with chips (fractional bankroll) is rounded down to a multiple of the smallest chip.
- The chips stay in the bet area during the round; the bet per hand (Double, Split) is shown at the hands as before.
- Tests (ViewModel): adding, removing, Clear, stack order and counts, splitting into chips, chips disabled at the maximum and outside the betting phase.

### E5 – Continuous integration

- GitHub Actions workflow `.github/workflows/blackjack.yml` in the root of the repository `wesc111/claude_wsc` (the Blackjack folder is part of that monorepo).
- Runs on push and pull request to `main` when files below `Blackjack/` or the workflow itself change, and manually (`workflow_dispatch`).
- Jobs on `macos-latest` (Apple Silicon, the target platform) and `ubuntu-latest`, independent of each other (`fail-fast: false`).
- Steps: checkout, .NET SDK from `Blackjack/global.json`, NuGet cache, `dotnet build -c Release` (warnings are errors), `dotnet test -c Release --no-build` (all tests, including the simulation plausibility checks).
- A change is only complete when the CI run is green. Building the app bundle and DMG in CI belongs to the release workflow (next step), not to this one.

### E6 – Dealing animation (GUI)

- Header from the left: bankroll, `Shoe: 247 / 312` with the bar of E1, deck selector, "Leave table". The top right corner of the window shows the shoe as a vector graphic in XAML (no image file, after the example `misc/shoe_example.png`): a metal plate filling the corner as a triangle, its edge at 45° with a semicircular finger notch, and the next card (card back) lying at 45° under the plate, showing below its edge and in the notch.
- Each dealt card is pulled out from under the plate and flies to its place in the hand (0.4 s, decelerating), starting at the size and 45° angle of the card in the shoe and turning straight. A card dealt face up leaves the shoe face down and turns over during the last part of the flight (scaled to zero width, image swapped, scaled back). The hole card flies face down.
- When the hole card is revealed, it turns over in place (0.3 s).
- The card in the hand becomes visible when the flight ends; the total of the hand is updated then. Its place is reserved from the start, so the layout does not jump.
- The engine awaits each animation through the `IGameObserver` notifications, as before. The view model uses a small interface `ICardAnimator` (deal, turn over) implemented by the main window; without an animator (tests) it falls back to the configurable pause of M5 (0 in tests).
- Split: the moved card changes hands without a flight, with the pause as before.
- Closing the window cancels a running animation together with the round.
- This replaces the slide-in animation of M5.
- Tests (ViewModel, with a recording animator): every dealt card is passed to the animator in deal order and hidden until its animation ends; the total is updated afterwards; the engine waits for an unfinished animation; the hole card is passed face down and turned over on reveal, its value set during the turn; without an animator nothing changes.

### E7 – Table print (GUI)

- Between the dealer and the player hands the felt shows a print as on a casino table (after the example `misc/graphics table.png`), on arcs around the dealer and behind the cards (cards may cover it; the layout does not change):
  1. `BLACKJACK PAYS 3 TO 2` in large letters,
  2. `Dealer Must Stand on Soft 17` in smaller letters,
  3. a banner (curved band with folded ends, gold outline) with `Insurance Pays 2 to 1` in small capitals.
- Texts 1 and 2 follow the rules: the payout as the smallest fraction (1.5 → `3 TO 2`, 1.2 → `6 TO 5`), `Must Hit` for H17. Text 3 is fixed: Insurance is a planned feature (still in section 9 for now); the print is shown already.
- The player hands sit at the bottom of the table area (above the buttons), so the print stays visible between them and the dealer; split hands that wrap into a second row may cover it.
- Serif font, cream colour, slightly transparent; drawn by a control of the GUI (letters placed one by one along the arc; no package).
- Tests (ViewModel): texts for 3:2 and 6:5, S17 and H17.

### E8 – Sounds (GUI)

- Sounds from Kenney "Casino Audio" (CC0, see `docs/SOUNDS.md`), as mono WAV files in `src/Blackjack.Gui/Assets/Sounds/`, embedded as resources. The downloaded pack (`audio/`) is not part of the repository.
- Events and sounds (frequent events pick one of several variants at random):

  | Event | Sound files |
  |---|---|
  | A card is pulled out of the shoe (start of the flight) | `card-slide-1…4` |
  | The hole card turns over | `card-place-2`, `card-place-4` |
  | The shoe is reshuffled (engine notification; a new shoe at the start of a game is shuffled silently) | `card-shuffle` |
  | A chip is put into the bet area | `chip-lay-1…3` |
  | A chip is taken back, or Clear with chips in the bet area | `chips-handle-3`, `chips-handle-4` |
  | Round won (net > 0) | `chips-stack-1`, `chips-stack-3` |
  | Round with a player Blackjack won | `chips-handle-5` |
  | Round lost (net < 0) | `card-shove-1`, `card-shove-2` |
  | Push (net = 0) | none |

  One result sound per round, from the net over all hands. The default bet placed at the start of a round makes no sound.
- The view model knows only `ISoundPlayer.Play(Sound)` (`Sound` = the events above). On macOS the GUI plays the files with `afplay` (one process per sound, not awaited, sounds may overlap; no NuGet package); the files are extracted from the resources into a temporary directory on first use. On other platforms the GUI is silent.
- A "Sound" switch in the header (on at start, not saved yet) turns all sounds off.
- Tests (ViewModel, with a recording sound player): sounds of a round in order, chips, Clear, shuffle, the result sounds for win, Blackjack, loss and push, nothing when switched off; every variant file exists.

## 9. Non-goals (not part of any current milestone)

- Insurance, Surrender, Even Money
- No-peek (ENHC) as an active option (only prepared via `Rules.DealerPeeks`)
- Multiple players / seats at one table
- Network play, persistence, user accounts, real money
- Card counting strategies (prepared via `IGameObserver`)

---

## 10. Milestones overview

| # | Content | Done when |
|---|---|---|
| M1 | Core library, Hit/Stand, tests | all acceptance criteria in section 4 covered by passing xUnit tests |
| M2 | Console app | game playable end-to-end, criteria in section 5 met |
| M3 | Simulation + statistics | criteria in section 6 met, plausibility check passes |
| M4 | Double and Split | criteria in section 7 met, all previous tests green |
| M5 | Avalonia GUI | criteria in section 8 met |

---

## 11. Technical requirements

- .NET 10, C# (latest language version)
- `Nullable` enabled, `TreatWarningsAsErrors` enabled
- Tests: xUnit; no commercial assertion libraries
- Public API with XML documentation comments
- All identifiers, comments and documentation in English
