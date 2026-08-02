# Sources et bibliographie

**Date de vérification : 2026-08-02**

## Règles

- Les sources officielles établissent les capacités natives et Enhanced Edition.
- Le manuel communautaire S03 est une source secondaire extrêmement détaillée; ses éléments structurants ont été recoupés avec les manuels officiels lorsque possible.
- Les projets communautaires sont utilisés comme preuves de besoins et patterns, pas comme fonctions natives.
- Une URL ou un dépôt peut évoluer; toute décision de dépendance doit être revérifiée au moment de l’implémentation.

### S01 — Neverwinter Nights — Original Game Manual, section “Building Your Own Adventures”

- **Autorité :** Official BioWare/Atari manual
- **Date/état :** 2002
- **URL :** https://forums.beamdog.com/uploads/editor/xs/eovsvce00axn.pdf
- **Utilisation :** Native Toolset layout, modules, areas, object categories, conversations, scripting and Dungeon Master Client.

### S02 — Neverwinter Nights: Shadows of Undrentide — Manual, Toolset additions

- **Autorité :** Official BioWare/Atari expansion manual
- **Date/état :** 2003
- **URL :** https://forums.beamdog.com/uploads/editor/8o/3rzv9z3f2kca.pdf
- **Utilisation :** Plot Wizard, Store Setup Wizard, Waypoint Walking, bulk export/import and ERF sharing.

### S03 — The Builders Project’s Guide to Building — The Aurora Toolset Manual v1.06

- **Autorité :** Comprehensive community reference; secondary source
- **Date/état :** NWN/SoU/HotU 1.68 era
- **URL :** https://neverwintervault.org/project/nwn1/other/guide-building-aurora-toolset-manual
- **Utilisation :** Detailed inventory of properties and workflows for modules, areas, creatures, conversations, journal, items, placeables, doors, encounters, sounds, triggers, waypoints, merchants, henchmen and legacy subsystems.

### S04 — NWN Toolset: Enhanced Edition — Toolset update changelog

- **Autorité :** Official Beamdog forum announcement
- **Date/état :** 2019
- **URL :** https://forums.beamdog.com/discussion/75035/nwn-toolset-enhanced-edition-try-the-new-updates-and-share-your-feedback
- **Utilisation :** Visual transforms, multiple area tabs, inventory shortcuts, build/update optimization and undo improvements.

### S05 — NWN:EE Patch 1.80 / 8193.9

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2020-03-17
- **URL :** https://www.beamdog.com/news/nwnee-patch-windows-macos-linux/
- **Utilisation :** Custom weapon feats/VFX, custom caster classes, LOD and data-driven rule expansion.

### S06 — NWN:EE Patch 8193.15

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2020-09-15
- **URL :** https://www.beamdog.com/news/patch-819315-neverwinter-nights-enhanced-edition/
- **Utilisation :** PBR, tone mapping, per-pixel lighting, dynamic lights, water and content creator improvements.

### S07 — NWN:EE Patch 8193.20

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2020-12-14
- **URL :** https://www.beamdog.com/news/new-patch-arrives-neverwinter-nights-enhanced-edition/
- **Utilisation :** Toolset, Dungeon Master Client, navigation, multiplayer and NWScript improvements.

### S08 — NWN:EE Patch 8193.23

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2021-05-19
- **URL :** https://www.beamdog.com/news/patch-819323-launches-neverwinter-nights-enhanced-edition/
- **Utilisation :** Curated content launcher, TrueType fonts, UI scaling, pathfinding, transformations, water and new script commands.

### S09 — NWN:EE Patch 8193.26

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2021-06-24
- **URL :** https://www.beamdog.com/news/patch-819326-launches-neverwinter-nights-enhanced-edition/
- **Utilisation :** Curated content in multiplayer and repository-driven distribution.

### S10 — NWN:EE Patch 8193.32

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2021-09-22
- **URL :** https://www.beamdog.com/news/patch-neverwinter-nights-enhanced-edition-apple-m1-support-performance-improvements-more/
- **Utilisation :** Scriptable NUI, GUI events, device queries, resource manager API, JSON/object serialization, dynamic area/object functions, party control and toolset undo fixes.

### S11 — NWN:EE Patch 8193.34

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2021-12-08
- **URL :** https://www.beamdog.com/news/patch-819334-pathfinding-renderer-other-qol-improvements/
- **Utilisation :** Pathfinding, NWSync repository metadata and quality-of-life improvements.

### S12 — NWN:EE Patch 8193.35

- **Autorité :** Official Beamdog patch notes
- **Date/état :** 2023-05-24
- **URL :** https://www.beamdog.com/news/new-patch-neverwinter-nights-enhanced-edition-arrives-today/
- **Utilisation :** AOE indicators, up to eight classes, major area load improvements, NUI improvements and hundreds of builder additions.

### S13 — NWN:EE 89.8193.37 stable patch series, through 89.8193.37-17

- **Autorité :** Official Steam news / community-developed stable release notes
- **Date/état :** 2025-01-09 through 2025-10-08
- **URL :** https://store.steampowered.com/news/app/704450/view/527608297527705660
- **Utilisation :** Modern antialiasing/filtering, Tracy profiler, structured logging, networking and performance work, script compiler evolution, toolset quality-of-life, maintenance and security fixes. The latest stable release found during the 2026-08-02 review was 89.8193.37-17, published 2025-10-08.

### S14 — Beamdog NWSync

- **Autorité :** Official Beamdog source repository; MIT
- **Date/état :** Repository archived/moved
- **URL :** https://github.com/Beamdog/nwsync
- **Utilisation :** Content manifests, repositories and automatic multiplayer content synchronization.

### S15 — NWN Lexicon — NWScript function categories

- **Autorité :** Long-running community technical reference; secondary source
- **Date/état :** Continuously maintained
- **URL :** https://nwnlexicon.com/Categoryfunction.index.html
- **Utilisation :** Capability map for script API categories; not a source for copying names or implementation.

### S16 — NWNX:EE Unified

- **Autorité :** Open-source third-party server extender
- **Date/état :** Active project
- **URL :** https://github.com/nwnxee/unified
- **Utilisation :** Persistent-world extension patterns, plugins, database, server hooks and functions later adopted by the base game. Not native Aurora.

### S17 — Nasher

- **Autorité :** Open-source community build tool
- **Date/état :** Active project
- **URL :** https://github.com/squattingmonk/nasher
- **Utilisation :** Text-source extraction/rebuild, Git workflows and deterministic module collaboration.

### S18 — neverwinter.nim

- **Autorité :** Open-source library and official compiler/tooling source; MIT
- **Date/état :** Active project
- **URL :** https://github.com/niv/neverwinter.nim
- **Utilisation :** Open script compiler, format tooling and NWSync repository maintenance.

### S19 — NeverBlender

- **Autorité :** Community Blender integration
- **Date/état :** Maintained community tool
- **URL :** https://neverwintervault.org/project/nwn1/other/tool/neverblender-28
- **Utilisation :** Model, walkmesh, animation, material and emitter authoring pipeline. Not native Aurora.

### S20 — Radoub Toolset

- **Autorité :** Open-source modern community toolset
- **Date/état :** Active project
- **URL :** https://github.com/LordOfMyatar/Radoub
- **Utilisation :** Modern cross-platform resource editors, accessibility, project hub and resource-browser patterns. Not native Aurora.

### S21 — Neverwinter Nights: Enhanced Edition product page

- **Autorité :** Official commercial product description
- **Date/état :** Current listing
- **URL :** https://store.steampowered.com/app/704450/Neverwinter_Nights_Enhanced_Edition/
- **Utilisation :** High-level Toolset, multiplayer and Dungeon Master Client positioning.

### S22 — Neverwinter Nights: Enhanced Edition EULA

- **Autorité :** Official license terms
- **Date/état :** Current storefront copy when researched
- **URL :** https://store.steampowered.com/eula/704450_eula_0
- **Utilisation :** IP and redistribution boundaries. Forge must implement original formats, content and rules.

### S23 — NWN Core Framework

- **Autorité :** Open-source third-party event framework
- **Date/état :** Community project
- **URL :** https://github.com/squattingmonk/nwn-core-framework
- **Utilisation :** Event-bus and modular subscription patterns used by persistent worlds. Not native Aurora.
