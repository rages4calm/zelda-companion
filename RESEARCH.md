# Compatibility and source research

## Native game, rather than patched-ROM execution

Companion builds local assets from the original USA game and launches the
[snesrev native C engine](https://github.com/snesrev/zelda3). Arbitrary IPS/BPS or
other SNES ROM patches can change machine code that this native implementation
does not execute. A useful ROM-hack feature needs explicit native code/data work
and independent validation; a successfully patched ROM alone is insufficient.

Smooth 1080p uses the existing xBRZ shader. It cleans pixel edges and supports a
wide view but does not provide replacement HD textures or newly drawn art.

## Full ALttP Randomizer

[ALttPR](https://alttpr.com/en/start) uses a Japanese 1.0 ROM and generates
patched SNES games. Its [source](https://github.com/sporchia/alttp_vt_randomizer)
and [options](https://alttpr.com/en/options) were reviewed for item/logic features
and compatibility. Its seed codes, ROM patches, entrance logic and major-item
placements are not supported by Companion.

Companion's Treasure Shuffle is independently authored. It changes only explicitly
allowed ordinary chest item bytes, preserves all other bytes and refuses unknown
base data. This makes original progression preservation testable without claiming
to implement ALttPR's reachability solver. See [RANDOMIZER.md](RANDOMIZER.md).

## Multiplayer references

- [ALttPO](https://github.com/alttpo/alttpo): native network additions used its
  published RAM/address and acquisition/world exception research as a reference.
  The inspected commit and source-boundary notes are in `UPSTREAMS.txt`.
- [Z3R](https://github.com/xander-haj/Z3R),
  [Z3R Launcher](https://github.com/xander-haj/Z3R-Launcher),
  [wiki](https://xander-haj.github.io/Z3R-Wiki/index.html) and
  [beta](https://github.com/xander-haj/Z3R-Beta) were examined as feature references.
  Their multiplayer implementation is not included here.

The new C/C# bridge, relay, identity overlays and sharing implementation are
original Companion additions. Their eight-player/combat scope is documented in
the Guide and validation record; no full ALttPO/Z3R feature parity is claimed.

## Soundtracks

The native engine already has MSU-1 support. Companion adds local import,
validation, selection and settings UI. The
[Zeldix ALttP catalogue](https://www.zeldix.net/t791-the-legend-of-zelda-a-link-to-the-past)
is linked for creator-listed packs. A real JUD6MENT Orchestral 2023 PCM set was
used in bounded local validation; its audio is not redistributed here. OPUZ/Deluxe
fixtures do not establish a full extended soundtrack playthrough.
