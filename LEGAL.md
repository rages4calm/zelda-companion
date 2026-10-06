# Project status and licensing scope

Zelda Companion is an unofficial community project. It is not affiliated with or
endorsed by Nintendo. The names, characters and original game content belong to
their respective rights holders. A software license here grants no permission
to distribute a ROM, extracted game assets or soundtrack audio.

## What the public project contains

The repository contains Companion source, the modified native foundation,
extractor source, generic software resources, shaders, tests and documentation.
The Windows release contains those sources and the software runtimes needed for
local setup. Neither contains a ROM, generated `zelda3_assets.dat`, save or music
pack. The upstream Switch icon with game sprite art is omitted from public
source/package copies; Switch builds are outside the supported release target.

Documentation images show the actual Companion UI with a demonstration seed.
They contain no game sprites or gameplay footage and do not assert a full campaign
test. The native extractor's generic 3x5 font and upstream application icons are
software resources, with their original license/attribution retained.

Each player supplies their own original USA ROM. The launcher verifies it and
extracts data locally without modifying or uploading the selected ROM. An
initialized installation contains player-owned game data and must not be shared
as the clean software package.

## License boundaries

| Component | License / notice |
| --- | --- |
| Original Companion, Core, Relay, tests and new native additions | [MIT](LICENSE), Carl Prewitt Jr. |
| snesrev native engine | [MIT and bundled Opus notices](engine/LICENSE.txt), snesrev / elzo_d |
| Repaired RadzPrower launcher | [MIT](repaired-launcher/LICENSE) |
| xBRZ shader/scaler components | Original Hyllian MIT and Zenju GPLv3 notices in [source](shaders/xbrz/shaders/xbrz-freescale.glsl); [GPLv3 text](Licenses/GPL-3.0.txt) |
| SDL, Python, Pillow, PyYAML and Opus | Respective notices in `Licenses/` and vendored source/runtime directories |
| .NET release runtime | License and third-party notices included in the published runtime |
| Unmodified cloudflared helper | [Apache 2.0](Tools/LICENSE-cloudflared.txt) and [third-party notices](Tools/THIRD-PARTY-NOTICES-cloudflared.mdx) |

The top-level MIT license applies to original project additions. It does not
relicense third-party material. Preserve component notices and meet their terms
when redistributing modified source or binaries. Full shader source and GPL text
accompany the Windows release; the engine foundation's actual MIT grant is
retained, independently of the licensing questions documented for EarthBound.

## Online privacy and release limits

Automatic Internet hosting sends names, positions, avatar pixels and typed
progress/combat snapshots through Cloudflare and the room relay. It does not send
ROM files or complete save states. Room codes authorize access to open slots;
keep them private. Logs omit codes/names where implemented, but inspect local
logs before posting an issue.

This preview has no uptime guarantee, anti-cheat or complete multi-PC campaign
certification. [Validation](docs/VALIDATION.md) states what was tested. Those
technical limits and these notices do not grant rights to copyrighted game data.
