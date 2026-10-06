# Public source snapshot

The v0.3.1 repository starts from the explicit Source subset of the tested portable
release. It retains the native engine, extractor, dependency source/notices and
the repaired original launcher. Current Companion/Core/Relay code matches that
tested release; public documentation, source audits and build/CI scripts are added
for repository users.

The upstream Switch icon containing game sprite art is omitted from the public
source and ZIP. Windows game binaries are unchanged by that source-only cleanup.
The original offline `main.c` is retained as `engine/src/main-offline.c.txt`;
`engine/src/main.c` is the Companion online edition.

Player ROMs, generated game data, saves, music, recordings, personal logs, compiler
outputs and workspace backups are excluded. Public documentation screenshots show
only the native launcher UI with a demonstration seed. Resource provenance and
approved hashes are in `distribution/source-resources.json`.

The current clean software archive and manifest are attached to
https://github.com/rages4calm/zelda-companion/releases/tag/v0.3.1
