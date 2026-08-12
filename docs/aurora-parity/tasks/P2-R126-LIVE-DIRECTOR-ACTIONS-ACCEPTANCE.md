# P2 R126 — Live Director actions acceptance

Lot `FC-DM-P2-02` : téléportation, destruction, soin, limbo et possession.

| ID | Fermeture réelle |
|---|---|
| `ORF-DM-007` | navigation runtime, consentement joueur, politique groupe, orientation et preview |
| `ORF-DM-008` | cause/confirmation, politique de loot, impact quête et limite de rollback visibles |
| `ORF-DM-009` | scopes santé/ressources/effets/tout, raison, preview et adaptateur RPG |
| `ORF-DM-010` | identité préservée, états IA/mouvement/collision/visibilité et adaptateur moteur |
| `ORF-DM-012` | durée bornée, contexte d’entrée, point de retour, consentement protégé, expiration et adaptateur runtime |

`LiveDirectorActionProfile` ajoute configuration visible, diagnostics localisés, preview/apply,
compensation, round-trip, handle ForgeScript, runtime optionnel/requis et MCP filtré aux services
autoritaires existants. Marqueur : `OPENRPG_FORGE_TOOLSET_R126_LIVE_DIRECTOR_ACTIONS_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
