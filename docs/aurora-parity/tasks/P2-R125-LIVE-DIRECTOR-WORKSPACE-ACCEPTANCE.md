# P2 R125 — Live Director workspace acceptance

## Périmètre

`ORF-DM-001`, `ORF-DM-003`, `ORF-DM-004`, `ORF-DM-005`, `ORF-DM-006`.

| Fonction | Limite fermée | Preuve |
|---|---|---|
| `ORF-DM-001` | application visuelle séparée non contractualisée | politique client séparé, indépendant du moteur, authentifié/MFA, expiration, révocation et audit |
| `ORF-DM-003` | miniatures et palette visuelle absentes | Creator avec allowlist, filtres package/texte/type/coût/zone/permission et miniatures |
| `ORF-DM-004` | coordonnées de formation et undo sûr absents | spawn borné single/formation/scatter, coordonnées, preview atomique, IDs stables et compensation |
| `ORF-DM-005` | navigation visuelle hiérarchique absente | Chooser zones/instances hiérarchique, filtres, diagnostics et confidentialité |
| `ORF-DM-006` | rendu de focus caméra absent | focus spectateur cross-zone, lecture seule, override permissionné et audit |

Le profil fournit identité canonique, validation localisée, commande preview/apply, undo/redo,
round-trip, ForgeScript, runtime optionnel/requis, UI Live Director et MCP filtré. Les moteurs
existants restent les autorités d’exécution. Marqueur : `OPENRPG_FORGE_TOOLSET_R125_LIVE_DIRECTOR_WORKSPACE_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
