# Zelda Companion 0.3.1 — guided community edition

A portable Windows x64 companion for the snesrev Zelda 3 engine. Includes the
smooth xBRZ display preset, an offline engine and a separate eight-player native
co-op experiment. This is a community project, with no affiliation with Nintendo.

## Start

1. Extract the clean release ZIP to a writable folder.
2. Open **`Zelda Companion.exe` at the top level**. `START HERE.html` beside it is
   the friendly quick guide; `START HERE.txt` is a plain-text fallback. Game/ and
   Relay/ contain supporting software, not the launcher you need to open.
   On **Play**, choose your original USA `.sfc` or `.smc` ROM.
   The tool checks the original USA SHA-1 (copier headers accepted), extracts assets
   locally and logs errors. No download of a ROM is provided.
3. Click **Launch solo**. Audio is enabled in the clean release. **Settings →
   Configure controls** opens the visual keyboard/controller editor. Pick a
   category, click a binding and press the input you want. **Save controls** backs
   up the previous settings and applies changes on the next launch. Alt+Enter
   toggles fullscreen by default. The original engine documentation in
   `Source/engine/README.md` describes its remaining features and controls.

**Guide** in the sidebar explains first launch, Internet play, experimental limitations,
current keyboard controls, saving, troubleshooting and sharing. **F1** in the
launcher opens it. **Activity** and **Open logs folder** contain technical details.
The launcher remembers your look, name and tool-sharing choice locally; it does
not persist room codes. Sound and reduced-flash options are in Settings.
**Music** imports and selects an optional MSU-1 soundtrack without INI editing.
**Randomizer** creates native Treasure Shuffle adventures with mandatory progression
protection, separate saves and exportable recipes for friends.

The ZIP includes the .NET runtime, SDL and Python asset tools. There is no compiler
or runtime installation required for playing. An unsigned Windows application can
still receive a SmartScreen reputation prompt.

## Keyboard, controllers and hotkeys

Connect a compatible USB or Bluetooth controller. **Configure controls** shows its
name and live button input; labels use Xbox button names. The left stick also moves
Link. If several controllers are detected, select the one to configure.

Gameplay actions, pause/fullscreen/volume shortcuts, twenty save-state slots,
loading/replays and extra engine shortcuts can each have keyboard and controller
bindings. Hold Ctrl/Alt/Shift for a keyboard combination. For a controller
combination, hold the first button, press the second and release both. The capture
window needs focus. A manual button picker works if a controller is disconnected.

A duplicate assignment offers to swap the two bindings. Clear removes one binding;
Restore defaults resets the draft; Discard changes leaves saved settings unchanged.
Every successful save creates a settings backup. The editor refuses to overwrite
external changes made while it was open. Each online player may use different
controls. Configure them while the game is closed, then launch again.

**Advanced settings file** opens the INI for other engine options. Normal remapping
does not require text editing. Windows-reserved shortcuts cannot reliably be
captured. Physical controller model coverage is limited; see Validation.txt.

## MSU-1 soundtracks

Choose **Music → Import ZIP** for an A Link to the Past audio pack, or **Import
folder** for extracted numbered `.pcm` or `.opuz` tracks. Extract `.7z`/`.rar`
downloads first. The importer keeps its own local copy and normalizes filenames;
ROM patches and `.msu` video files are not needed by this native port. Imports
ignore other archive contents and validate PCM headers, loop points and frame
alignment, or OPUZ headers/range tables. A ZIP containing multiple prefixes,
folders or formats must be extracted so you can choose one track set.

Select the imported pack and click **Use this soundtrack**. Music balance controls
MSU volume separately from game effects. Resume retains overworld playback position
after a building visit. Enable **MSU Deluxe** only for an author's extended-track
pack. Standard PCM requires 44100 Hz stereo; OPUZ requires 48000 Hz stereo. The
launcher selects the appropriate native mode and sample rate. Settings' audio
switch continues to mute all game audio. Close the game before changing music.

Missing tracks fall back to original music. Choose **Original SNES music** and
apply it to switch back; imported music stays installed. The page reports coverage
of the 34 commonly used standard tracks (1–35, excluding optional track 15).
Reimporting identical audio avoids duplicate packs; damaged copies are replaced
after validation, with their previous folders retained as local backups.

Music resides in `UserData/Soundtracks`; its selected ID/options are in
`UserData/soundtrack-settings.json`. Settings are backed up before applying a
choice, and failed choice commits restore the previous game settings. Relative
track paths are refreshed for solo and isolated online campaigns on launch. A
portable-folder move keeps the music usable. Music settings do not affect the
online gameplay compatibility hash, so friends can use different soundtracks.
Existing manually configured MSU settings are kept until you save a managed choice.

**Find music packs** opens the [Zeldix A Link to the Past catalogue](https://www.zeldix.net/t791-the-legend-of-zelda-a-link-to-the-past).
One tested standard PCM option is JUD6MENT's Master Orchestral Set, August 2023:
[creator-listed ZIP mirror](https://www.mediafire.com/file/9mcvsoalqxloxzf/Link_to_the_Past_Orchestral_2023_by_JUD6MENT.zip/file).
Its 35 tracks were validated and used in short native audio tests. Imported music
is personal data, not part of this software ZIP; retain soundtrack credits and
obtain each pack from its creator-listed source. OPUZ mode/range validation is
tested with fixtures; a full OPUZ/Deluxe soundtrack playthrough is not verified.

## Randomizer: native Treasure Shuffle

Set up your original USA ROM on Play, then choose **Randomizer**. Enter a number
or phrase, choose **Balanced** or **Surprise**, and optionally include chest heart
pieces. **Generate seed** adds an adventure to your library; **Play selected seed**
uses its separate solo saves. The original Play page continues your original game.

Progression protection is always on. Only rupee rewards in 46 ordinary chests
shuffle, or 52 chests with the six optional heart-piece locations. Required gear,
every key, map, compass and big chest, bombs, arrows, bosses, entrances and story
events stay original. Pots, shops and farmable rewards stay original. Reward totals
are preserved. This retains the original progression route without relocating a
required item behind its own requirement.

For co-op, **Export recipe** and send the small `.z3seed.json` file to friends.
Each imports it using their own original game data. Choose **Use for online**,
or select that seed under **Online adventure**. The existing relay checks the exact
randomized data hash, so different adventures cannot join. Each seed's online
campaign list is separate from original and other seed campaigns.

This is an independent native generator, not full ALttPR compatibility.
[ALttPR](https://alttpr.com/en/start) generates patched Japanese-ROM games with
major item/logic options; their patches cannot execute in this native C port.
See **[RANDOMIZER.md](RANDOMIZER.md)** for the supported scope, save paths and
validation. A full randomized campaign has not been played through.

## Up to eight players over the Internet (including Starlink)

1. Everyone needs **0.3.1**, their own original USA ROM and matching adventure,
   display/gameplay settings, shared-progress and combat choices. For Treasure
   Shuffle, everyone imports the same recipe. Old two-player releases cannot join.
2. Host chooses **Online → Host a room → Internet · automatic**, enters a name,
   selects a new or continued online campaign, then **Create room & play**.
   The bundled, checksum-verified Cloudflare helper opens a public WSS connection.
   No router changes, DMZ, port forwarding, VPN, account or GitHub build tool is needed.
   Keep the host launcher open while playing.
3. **Copy invitation** supplies the relay, private room code, release and matching
   choices. Send it privately to friends. Everyone knowing it can take an open slot.
4. Friends choose **Join a room**, enter their name, relay address and room code,
   select their online campaign and match the invitation's choices, then **Join room & play**.
5. Meet in the same area, world and dungeon floor. Names appear above the Links.
   Slots use green, blue, red, purple, gold, cyan, rose and silver. Colors are
   cosmetic: armor defense remains unchanged. The roster lists names, colors and
   whether players are nearby, elsewhere or in menus. Name display is a personal choice.

Menus, transitions, forced blanking and mode-7 maps hide avatars. A departed avatar
expires after about two seconds; the roster updates when the relay detects departure.
The smooth xBRZ preset also filters remote avatars and names. Stock Link is tested;
custom sprite palettes may color different parts. Online color does not indicate armor.

Online campaigns live in **UserData/Online**; solo saves remain in **Game/saves**.
The campaign selector offers new adventures and dated saved campaigns, and remembers
your last choice. To continue after an invitation expires, create/join a new room and
select your own corresponding saved campaign. Each player has their own save files.
The launcher backs them up before launch. No solo save is imported automatically.
Save normally before closing: force-closing cannot guarantee unsaved progress.

The automatic connection uses [Cloudflare Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).
Cloudflare carries co-op traffic. This free testing service has no uptime guarantee.
Closing/disconnecting the host stops the tunnel; a new host session creates a new
address and invitation. The helper runs as a child process, never a Windows service.
Only relay health and room WebSockets are exposed, never game files.
Automatic hosting chooses a free loopback port to avoid other launcher copies.

**Same network / LAN** optionally uses TCP 24816 and may need Windows private-network
permission. Choose the correct LAN adapter for the invitation. LAN addresses only
work on that network. Use automatic Internet for friends in different homes.

## Shared equipment and campaign progress

With sharing enabled, received permanent acquisitions remain in the online save:
bow/boomerang, tools, flute, mirror, gloves, boots, flippers, pearl, sword/shield/armor,
empty bottle ownership, health capacity and heart-piece progress, bomb/arrow capacity
upgrades, pendants, crystals, dungeon maps/compasses/big keys and magic-use upgrades.
Persistent doors, boss-clear flags, explored areas and story milestones also merge.
New arrivals receive the room's accumulated snapshot. Grants wait for normal play
and refresh equipment graphics. Receiving a lantern adds ownership; it does not
force Link to equip or visibly hold it. Check inventory after dialogue closes.

Current health/damage, magic meter, ammunition, money, small keys, chest contents,
bottle contents, selected tool, position and local saves stay personal. Shared bottles
are empty. Heart totals converge to the furthest collected total: simultaneous
different heart pieces are not added together. Opening scripts and resettable water
flags have exceptions to avoid stranding players; some world changes appear on area
re-entry. Players should stay at similar progression. Older save-state loads can
reintroduce divergent state. Shared acquisitions are permanent; use trusted friends.

## Shared enemy combat — optional beta

Off by default; all players must match. Thirty-two ordinary enemy types share
position, health and contributed damage when players are in the same area/floor.
The lowest active slot there leads that area's enemy state; another player takes
over after departure. Only existing matching, explicitly allowed enemies are updated.
Damage reports are bounded and cumulative to avoid applying one report twice.

**Bosses, NPCs, puzzle objects and projectiles are not synchronized.** Independently
spawned enemies and timing can differ, and follower movement may jitter. This is
an early combat implementation, not complete campaign-wide multiplayer. Players
still take damage locally. There is no PvP, chat, player collision, reliable depth
occlusion, rollback, matchmaking, automatic reconnect or anti-cheat.

## Network bounds

Eight players per room, 20 frames/second per game. Fixed, typed 4320-byte snapshots
use bounded binary compression over WebSockets. The relay validates values,
sequences, authenticated names and slot ownership; it fairly retains the latest
frame from each source. Exact engine, extracted assets and gameplay configuration
must match. The loopback UDP bridge has a per-process token. Peers never receive
arbitrary memory-write access, executable files, ROMs or full save states.

## Permanent relay (advanced)

The client accepts `wss://YOUR-RELAY/room`. All players make outbound connections;
they do not need inbound game ports when using a public relay.
Automatic Internet hosting above is the default. For a stable address and a service
you control, host the asset-free relay on a reachable server and choose **Existing
relay · advanced**. No permanent hosted service is included in this release.

The asset-free server is `Relay/ZeldaRelay.exe`:

```powershell
.\ZeldaRelay.exe --bind http://127.0.0.1:24816
```

Use a reachable server and a TLS reverse proxy that passes WebSocket upgrades:

```caddy
relay.example.com {
    reverse_proxy 127.0.0.1:24816
}
```

Build Linux server binaries from `Source/Relay` with .NET 10:
`dotnet publish Relay/Relay.csproj -c Release -r linux-x64 --self-contained true`.
The server has a 64-room, eight-player, 128-total-connection and sixteen-connections-per-IP bound and rejects
browser Origin requests. Add proxy connection/rate limits before a public launch;
this remains a private-test alpha, with no hosted service SLA or production audit.
Use WSS on public networks. The relay sees names, positions, avatar pixels and typed progress/combat snapshots; it does
not receive ROM files or full save states. Logs omit names and room codes.

## Sharing

Share the provided **clean ZIP**, which contains no ROM, compiled game asset file,
extracted Nintendo art/dialogue, save files or private validation captures.
Do not ZIP your installed folder after ROM setup: it then contains local game
data and your saves. Each recipient initializes their own game data.

New companion/network code: MIT, Carl Prewitt Jr. Existing engine: snesrev and
elzo_d under MIT, with bundled dependency notices. xBRZ shader: Hyllian MIT and
Zenju GPLv3 notices retained, with the full shader source and GPL text supplied.
Other licenses are in `Licenses` and the included runtime/extractor directories.
The unmodified cloudflared 2026.9.3 helper, Apache 2.0 license, upstream checksum
and third-party copyright notices are in `Tools`.
The repaired RadzPrower launcher source is included for attribution and reproducibility.
This package does not grant permission to distribute Nintendo's game assets.

## Build and validation

- `dotnet build Companion/Companion.csproj -c Release`
- `dotnet run --project Tests/Tests.csproj -c Release`
- Add `-- --shuffle PATH-TO-ORIGINAL-zelda3_assets.dat WRITABLE-TEST-FOLDER` for
  1,000-seed protection, import/export, save-isolation and eight-client hash checks.
- The isolated Windows diagnostics accepts `--randomizer OUTPUT-FOLDER
  PATH-TO-capture_owned_window.py` with private initialized Game assets. It exercises
  the actual generator/launch controls and campaign namespaces. Never distribute
  its generated data. `validate_randomizer_native.py` builds a private instrumented
  engine and checks real chest lookup and reward receipt routines against fixtures.
- Add `-- --internet PATH-TO-cloudflared.exe WRITABLE-TEST-FOLDER` to run those
  protocol checks through a live public Quick Tunnel instead of loopback.
- `build-engine.ps1 -TccRoot ... -SdlRoot ...` compiles the modified native engine.
- The controls harness is in `Diagnostics.Windows`. Build it into an isolated
  test folder with a `Game` subfolder containing the supplied `SDL2.dll` and
  default `zelda3.ini`, then run `Diagnostics.Windows.dll --controls OUTPUT-FOLDER
  PATH-TO-capture_owned_window.py`. It uses temporary profiles and a process-local
  SDL virtual controller. Keep it separate from an initialized personal install.
- `tests-controls-native.c` exercises the actual engine configuration parser;
  compile it with `engine/src/config.c`, `engine/src/util.c` and SDL2.
- The same isolated Windows diagnostics harness accepts `--music OUTPUT-FOLDER
  PATH-TO-capture_owned_window.py PATH-TO-SOUNDTRACK.zip` for importer/UI checks;
  `--music-functional` repeats the functional checks without taking new captures.
- `tests-msu-native.c`, compiled with the actual parser/util and SDL2, includes
  the native audio player for PCM open/mix/loop/fallback checks. Its unused Opus
  decoder and unrelated SPC/DSP routines are stubs; this is not an Opus test.
- `python run-native-tests.py --tcc-root PATH --sdl-root PATH --sdl-dll PATH` runs
  the native bridge harness. It extracts the actual engine recoil routine; visual
  reloads and unrelated enemy effects are stubs, so this is focused pipeline coverage.
- The isolated Windows diagnostics harness accepts `--coop-ui OUTPUT-FOLDER
  PATH-TO-capture_owned_window.py` with initialized private Game assets. It creates
  an actual Internet room and owned game, plus seven protocol clients. Keep it
  separate from a personal installation. Never distribute its generated game data.
- `build-release.py` creates a clean package from explicitly selected software files.
  It expects the original repair workspace layout and locally published binaries;
  it fails if ROMs, save files or the compiled asset file enter its output.

Test coverage and limits are in `Validation.txt`. Source pins are in `UPSTREAMS.txt`.

Next milestones: broader combat and interaction synchronization, boss/puzzle authority,
stronger save conflict rules, a long multi-machine campaign/latency test, automatic
reconnection and an optional permanent relay service. A browser client would require
a separate WebAssembly/render/audio/input port; the relay alone does not create one.
