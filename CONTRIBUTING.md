# Contributing

Start with [BUILDING.md](BUILDING.md), [UPSTREAMS.txt](UPSTREAMS.txt) and
[test coverage](docs/VALIDATION.md). Keep Companion setup understandable to someone
who has never used the engine, ROM extraction or a relay.

For a bug report, include the release, Windows version, what you did, expected
result and actual result. For co-op, include host/join role, player count, whether
the issue occurs on separate PCs and the sharing/combat choices. For Treasure
Shuffle, the generator version, seed and options are useful; a recipe is optional.

Never upload a ROM, generated asset pack, soundtrack, save containing embedded
game content, private room code, access token or unredacted personal log. Describe
the reproduction and use sanitized text instead. Security issues belong in the
[security reporting flow](SECURITY.md).

Pull requests should explain the concrete behavior change and relevant checks.
Preserve original saves/settings and online campaign boundaries. Network changes
must account for typed bounds, compatibility hashes and permanent acquisitions.
Randomizer changes must preserve all protected bytes and reward totals, and bump
the generator/recipe version when output semantics change. A future progression
randomizer needs a reachability model and wider validation; do not extend the
item allowlist to keys/equipment without that work.

Preserve upstream authorship and licenses. New original additions use the scoped
MIT license. Run source checks and protocol tests before submitting. Tests needing
your private ROM or assets run locally and must not upload their output to CI.
