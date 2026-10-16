# Blackjack – Requirements Specification

Version 1.1 · Status: Draft for implementation with Claude Code

Changes in 1.1: outcome names unified (`DealerBlackjack` added), player can leave via `null` bet, cancellation semantics, async `IGameObserver` instead of .NET events, hole-card visibility rules, `HandCompleted` split into `PlayerHandFinished`/`HandSettled`, unlimited bankroll via `null`, bet validation defined, `Rules` shape with H17 / payout / decks / penetration supported from M1 (no-peek rejected), shoe behaviour (2.6) and `FixedCardSource` defined.

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
│  └─ Blackjack.Gui/             Avalonia app                              (M5)
├─ tests/
│  ├─ Blackjack.TestSupport/     Shared test helpers: FixedCardSource, card parsing (M2)
│  ├─ Blackjack.Core.Tests/      xUnit
│  ├─ Blackjack.ConsoleApp.Tests/ xUnit                                     (M2)
│  └─ Blackjack.Simulation.Tests/ xUnit                                     (M3)
├─ docs/SPEC.md
└─ CLAUDE.md
```

### 3.3 Main types (Blackjack.Core)

| Type | Kind | Responsibility |
|---|---|---|
| `Suit` | enum | Clubs, Diamonds, Hearts, Spades |
| `Rank` | enum | Two … Ten, Jack, Queen, King, Ace |
| `Card` | readonly record struct | Suit, Rank, `IsTenValued`, base value |
| `ICardSource` | interface | `Card Draw()`, `bool NeedsReshuffle`, `void Shuffle()`, `int CardsRemaining` |
| `Shoe` | class | Implements `ICardSource`; n decks, Fisher–Yates shuffle, penetration, seedable `Random` (see 2.6) |
| `Hand` | class | Cards, `Total`, `IsSoft`, `IsBlackjack`, `IsBust`; later `CanSplit`, `IsFromSplit`, `IsDoubled` |
| `Rules` | record | Table rules and bet limits (see 2.1) |
| `PlayerAction` | enum | `Hit`, `Stand` (later `Double`, `Split`, `Surrender`) |
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

- The engine only offers actions that are currently legal (M1: always `Hit` and `Stand`).
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
| `MimicDealerPlayer` | Plays like the dealer: hit ≤ 16, stand ≥ 17 |
| `NeverBustPlayer` | Stands on hard 12+, otherwise hits |

Strategies always use a fixed bet (configurable).

### 6.2 Basic strategy (Hit/Stand only, S17, multi-deck)

The table must be stored as data (e.g. a 2-D array or JSON file) so it can later be extended for Double/Split.

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

- Command-line tool: strategy, number of rounds, seed, bet, number of parallel workers.
- Parallel execution with independent engine instances and derived seeds; results reproducible for identical parameters.
- Unlimited bankroll in simulation mode (no ruin): starting bankroll `null` (see 2.5).
- Performance target: 1 million rounds in well under one minute on a current machine.

### 6.4 Statistics (output)

| Metric | Description |
|---|---|
| Rounds | number of rounds played |
| Total wagered | sum of all bets |
| Net result | sum of all net payouts |
| House edge | −net result / total wagered, in % |
| Standard error, 95 % CI | of the per-round result (in % of bet) |
| Win / loss / push rates | in % |
| Blackjack rate | player Blackjacks in % |
| Bust rates | player and dealer |
| Breakdown | result per dealer upcard and per player starting total |

- Output as a formatted table on the console, optionally as CSV.
- Plausibility check: `MimicDealerPlayer` should yield a house edge of approx. 5–6 %.

---

## 7. Milestone 4 – Double and Split

Extension of the rules and engine; all existing tests must remain green.

| Rule | Value |
|---|---|
| Double | only on the first two cards of a hand; bet doubled, exactly one more card, then automatic Stand |
| Split | two cards of equal **value** (also e.g. K + 10); each new hand receives the same bet |
| Re-split | up to max. 4 hands; Aces cannot be re-split |
| Split Aces | each Ace receives exactly one more card, then automatic Stand |
| DAS | Double after split allowed (except on split Aces) |
| Blackjack after split | counts as 21, not as Blackjack (paid 1:1) |
| Peek | dealer Blackjack: player loses only the original bet |

- `DecisionContext` is extended accordingly (hand index, number of hands, `AllowedActions` includes Double/Split only when legal).
- Bankroll must cover additional bets; otherwise Double/Split are not offered.
- Basic strategy table extended with Double and pair-split decisions.
- Simulation plausibility check: Basic strategy with these rules yields a house edge of approx. 0.4–0.5 %.

---

## 8. Milestone 5 – Avalonia GUI

- Avalonia (current stable version), MVVM with CommunityToolkit.Mvvm.
- `GuiPlayer : IPlayerStrategy` with `TaskCompletionSource`; buttons (Hit, Stand, later Double, Split) complete the pending task; buttons enabled only for `AllowedActions`.
- Graphical card rendering; animated dealing driven by an `IGameObserver` that awaits each `CardDealt` animation.
- Closing the window cancels the pending round via the `CancellationToken`; a "Leave table" action makes `GuiPlayer` return `null` from `GetBetAsync`.
- Display of bankroll, bet (chips or input field), totals, round result.
- Card graphics: decision to be made at the start of M5 – either a free SVG card set (license to be checked) via `Avalonia.Svg.Skia`, or cards drawn as a custom Avalonia control.
- Must run on macOS (Apple Silicon); Windows and Linux desirable.

---

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
