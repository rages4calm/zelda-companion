# Treasure Shuffle v1 · Zelda Companion 0.3.1

Treasure Shuffle adds reproducible native adventures for solo and eight-player
co-op. It changes a small, explicitly allowed set of chest rewards while retaining
the original game progression. It is an independently authored Companion feature.

## Start a seed

1. Set up your original, unmodified USA ROM on **Play**.
2. Open **Randomizer**. Enter a number or phrase; **New seed** makes a random number.
3. Choose **Balanced** or **Surprise**, and optionally include chest heart pieces.
4. Choose **Generate seed**, then **Play selected seed**.

Your seed stays in the library. The same seed, options, generator version and
original game data produce the same output on another PC. Generating it again
reuses its existing adventure and preserves its saves. **Play → Launch solo**
continues your original adventure, independently of the randomizer selection.

## Mandatory progression protection

There is no switch to disable protection. Only item bytes in ordinary chests with
rupee rewards may change: 46 locations among the native table's 168 chests. The
heart-piece option adds six chest locations, for 52 eligible locations total.

The following remain exactly as in the original assets and engine:

- Required equipment and upgrades, including lantern, bow, hammer and hookshot.
- Every small key, big key, map, compass and every big chest.
- Bomb and arrow chest supplies, plus pots, shops and farmable rewards.
- Bosses, pendants, crystals, entrances, dungeons, puzzles and story events.
- All other game assets and executable behavior.

No required item is moved behind a requirement for that item. No entrance or key
placement is randomized. This preserves the original progression route instead of
introducing a new item-placement dependency graph. This protection concerns the
shuffle; existing game bugs, experimental co-op behavior, arbitrary save-state
rewinds and external modifications are not certified by it.

**Balanced** keeps 50/100/300-rupee rewards in their existing high-value group;
smaller rupees and optional pieces mix separately. **Surprise** mixes all eligible
rewards together. Both keep the exact original reward counts and total heart
pieces. **Spoiler log** lists room/chest indices and before/after rewards.

## Play the same seed online

1. Select the seed and choose **Export recipe**.
2. Send the small `.z3seed.json` file to friends. It contains seed/options and
   verification hashes, with no ROM, extracted assets, saves or soundtrack.
3. Each friend sets up their own original USA game data and chooses **Import recipe**.
4. Choose **Use for online**, or select that seed under **Online adventure**.
5. Choose a new or continued campaign for that seed, create/join the Internet room
   and share the normal invitation. Use Companion 0.3.1 for this workflow.

Every player must select the same adventure. The relay checks the exact output
asset hash, engine and gameplay settings. A different seed or original adventure
cannot join that room. Music, keyboard/controller bindings and name visibility
remain personal. Existing co-op features and limitations still apply: consumables
and chests are personal, and shared heart-piece progress converges to the greatest
collected total rather than adding simultaneous different pieces.

## Saves and verification

| Adventure | Save location |
| --- | --- |
| Original solo | `Game/saves` |
| Original co-op | `UserData/Online/<campaign>` |
| Treasure Shuffle solo | `UserData/Randomizer/<seed ID>/Game/saves` |
| Treasure Shuffle co-op | `UserData/Online/Seeds/<seed ID>/<campaign>/saves` |

Original or other-seed saves are never copied into a newly generated seed. The
Online campaign selector only lists the selected adventure's campaigns. Save
normally before closing. Backups are made before launching; solo seed backups
are under its `Backups` folder. Recipes reproduce treasure, not your progress.

The launcher regenerates the expected output from the known original USA assets
and compares the recipe and complete seed-data hash before launch. Unsupported
versions, altered recipes, other base assets and changed seed files are refused.
The files and saves are left intact when validation fails. Keep a backup of your
portable installation before manually editing any generated files.

## ALttPR is a different project

The established [ALttP Randomizer](https://alttpr.com/en) offers major equipment
randomization, logic, goals and entrance options. Its [setup guide](https://alttpr.com/en/start)
uses a Japanese 1.0 ROM and produces patched SNES games. Those patches modify
SNES machine code; this native C engine does not execute that patched code.
ALttPR seed codes and patched ROMs cannot be imported as Companion recipes.

Treasure Shuffle does not reproduce ALttPR's full feature set, presets, spoiler
logic or generation service. The [ALttPR repository](https://github.com/sporchia/alttp_vt_randomizer)
was researched for compatibility; its randomizer code was not copied into this
generator. Fully randomizing progression here requires separate native-engine
work, a reachability solver and wider campaign validation.

## Verified coverage and limits

- 1,000 seeds across both styles and both heart settings: every byte outside the
  allowed chest item positions was unchanged; all reward totals were preserved.
- Deterministic reproduction, recipe import/export, version and identity checks,
  tampered-data refusal, exact save backups and repeat-generation save preservation.
- Eight relay clients accepted matching randomized data; original-data client refused.
- Real launcher controls generated and launched a seed with the actual native game;
  original saves/data and continued seeded online saves stayed separate and intact.
- Five fixture seeds exercised 840 real native chest lookups and 260 real chest
  reward receipts, including all four option combinations.

These checks do **not** constitute a complete randomized solo or co-op playthrough.
No new progression logic, boss synchronization or full multi-machine campaign
claim is made. Detailed results and prior co-op limits are in `Validation.txt`.
