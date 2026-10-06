# v0.3.1 verification and limits

Release-date coverage: **2026-10-05**. Historical details remain in
[Validation.txt](../Validation.txt); the concise machine-readable summary is
[validation/v0.3.1.json](../validation/v0.3.1.json).

| Check | Verified scope |
| --- | --- |
| Generator | 1,000 seeds and all four style/heart combinations; every byte outside the independent allowed mask unchanged, exact reward totals and Balanced tiers retained. |
| Recipes/storage | 22 assertions covering reproducibility, options/identity, canonical seed text, strict base, import/export, unsupported/tampered input refusal, repeated-seed save preservation and exact backup. |
| Native chest functions | Five private instrumented full-engine fixtures, 168 lookups each and 52 cash/heart receipts each: 840 lookups and 260 receipts. All four option combinations covered; exit 0. |
| Seed networking | Eight matching-data relay clients accepted; an original-data client refused. |
| Launcher | Real Generate, Use for online and Play selected seed controls; actual native game launch, locked active controls, original/seed campaign separation and continued-save preservation. |
| Clean package | Full manifest audit, bundled first-run extraction reproducing the exact supported original assets, real native seed launch, original ROM unchanged. |
| Existing co-op | 51 protocol assertions; earlier eight local game processes and separate public WSS eight-client checks; 60 focused native bridge/recoil checks. |
| Installation | Software hash verification and unchanged existing ROM/assets, settings, saves and personal soundtrack files. |
| Public source build | Documented Build.ps1 published managed software and compiled both native engines. Both opened isolated owned windows and exited 0. Package.py produced a clean audited test archive. Two existing TCC macro-redefinition warnings remain. |

The native fixture modifies only a private test copy to expose actual chest and
receipt routines. It does not instrument the released engine. The focused combat
harness stubs visual callbacks and unrelated enemy effects; it is not a full
combat encounter. Local/public-WSS tests on one PC do not reproduce every
multi-PC or Starlink latency case.

## Not yet established

- A complete randomized solo or multiplayer campaign and every engine edge case.
- Full ALttPR compatibility or shuffled progression equipment, keys and entrances.
- New eight-player long-distance multi-PC playthrough, boss/puzzle authority or reconnect.
- Complete synchronized enemies, NPCs, objects, projectiles or player collision.
- Every physical controller model or a full OPUZ/Deluxe soundtrack playthrough.
- Guaranteed Quick Tunnel uptime, production relay security or anti-cheat.

The user reported a successful separate-PC friend connection with the previous
two-player release. That is preserved as prior evidence, rather than promoted to
a complete eight-player campaign test.

Progression protection is structural: required placements remain original. It
does not certify arbitrary save-state rewinds, external edits or pre-existing
experimental co-op bugs. Test inputs/data and private recordings are excluded from
the public repository and release. Documentation captures show only launcher UI.
