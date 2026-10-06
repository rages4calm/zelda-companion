# Zelda Companion v0.3.1 — Windows native/co-op preview

The first public preview of the portable Windows companion for the snesrev native
A Link to the Past engine. Includes local solo play, smooth xBRZ rendering, visual
controls, MSU-1 imports, experimental eight-player Internet rooms and native
Treasure Shuffle with mandatory progression protection.

## Download and setup

1. Download **ZeldaCompanion-0.3.1-win-x64.zip**, rather than GitHub's automatic
   Source code archive. Extract All into a writable folder.
2. Open **Zelda Companion.exe** at the top level.
3. Choose your own original unmodified **USA** `.sfc` or `.smc` ROM on Play.
4. Launch solo, or use Online for friends. Guide/F1 explains each workflow.

The ZIP bundles .NET, SDL, local asset-extraction tools, source and licenses.
Players do not need a compiler, Python installation, VPN, GitHub build launcher
or router port forwarding for automatic Internet rooms. Every player supplies
their own ROM. No ROM, extracted game-data pack, save or soundtrack is bundled.

**ZIP SHA-256:**

```text
0d3e2885b0036d373bb55bca31e79a1a8a4eb946c6e3a14d2f9b793278bc886e
```

The `.zip.sha256` asset gives the archive checksum;
`ZeldaCompanion-0.3.1-SHA256-manifest.json` lists every packaged software file.
In PowerShell, `Get-FileHash .\ZeldaCompanion-0.3.1-win-x64.zip -Algorithm SHA256`
should match the value above. This unsigned community app may receive a Windows
reputation notice; only run packages from sources you trust.

## Included features

- Smooth 1080p xBRZ/wide-view preset, crisp and original-aspect alternatives.
  The filter smooths existing pixels; it is not replacement HD artwork.
- Visual keyboard/controller and hotkey remapping with combinations, conflict
  handling and settings backups.
- Local MSU-1 ZIP/folder import, volume balance and original-music fallback.
  Soundtrack audio is not included; friends may use different music.
- Up to eight named/color-coded Links with a roster and automatic temporary
  Internet rooms, including the Starlink workflow used during development.
- Optional permanent gear/campaign sharing and continued isolated online saves.
- Optional ordinary-enemy combat beta, **off by default**.
- Native Treasure Shuffle: 46 rupee chests, or 52 locations with optional chest
  heart pieces. Required gear, all keys, maps/compasses, big chests, bomb/arrow
  supplies, entrances and story events stay original. Protection cannot be disabled.
- Deterministic seed recipes, export/import, spoiler logs and per-seed solo/online
  saves. Original adventures stay separate and mismatched seed data is rejected.

## Preview limits

Co-op is experimental. Bosses, NPCs, puzzle objects and projectiles remain
independent; combat is limited to 32 ordinary enemy types. No automatic reconnect,
PvP, chat, anti-cheat or permanent hosted service is included. Cloudflare Quick
Tunnels are a testing service with no uptime guarantee; the host must stay online.

Full ALttPR patched-ROM/seed compatibility is not supported. Treasure Shuffle is
an independent, smaller native generator. A complete randomized solo or co-op
campaign and longer eight-player multi-PC/latency scenarios remain unverified.

## Verification

- 1,000 seeds across all four option combinations: protected bytes and reward
  totals preserved; 22 generator/storage/seed-network assertions passed.
- Five actual native-engine fixtures: 840 chest lookups and 260 reward receipts.
- 51 ROM-free protocol checks passed from the public source snapshot; previous
  local eight-engine and public-WSS eight-client checks are documented.
- Clean ZIP first-run extraction produced the expected original data; actual
  native seed launch and separate original/seed campaign saves passed.
- All 1,279 public package manifest files verified. No ROM/data-pack/save/audio
  payloads or game-sprite image files are included. The unused upstream Switch
  icon was removed from Source; native/managed binaries are unchanged by cleanup.

Those fixtures and short launches do not establish full campaign completion.
[Coverage and limits](https://github.com/rages4calm/zelda-companion/blob/main/docs/VALIDATION.md).

Existing users: back up your installation and follow the
[update guide](https://github.com/rages4calm/zelda-companion/blob/main/docs/UPDATING.md).
For co-op, everyone updates and chooses the same adventure/settings. Never share
an initialized portable folder with your game assets and saves.

Companion/network/shuffle additions: Carl Prewitt Jr. Native foundation: snesrev
and elzo_d. Original launcher: RadzPrower. This unofficial project has no Nintendo
affiliation. [Credits](https://github.com/rages4calm/zelda-companion/blob/main/CREDITS.md)
and [licensing scope](https://github.com/rages4calm/zelda-companion/blob/main/LEGAL.md).
