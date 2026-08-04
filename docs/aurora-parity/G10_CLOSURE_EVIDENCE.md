# G10 Live Director — preuve de fermeture

## Verdict

Les 32 exigences G10 sont `Implemented` avec preuves automatisées et limites `Partial:` explicites. La fermeture exige en plus le scénario GM intégré, la surface Toolset dédiée, la revue visuelle et `OPENRPG_FORGE_VERIFICATION=PASS`.

## Scénario intégré

`LiveDirectorScenario` réutilise `NetworkBetaScenario`, conserve deux clients synchronisés, applique une création live et une transition de quête comme GM, refuse la même création à un joueur, puis vérifie la chaîne d'audit.

Marqueurs serveur :

- `OPENRPG_FORGE_LIVE_DIRECTOR_NETWORK_PASS`;
- `OPENRPG_FORGE_LIVE_DIRECTOR_AUTHORITY_PASS`;
- `OPENRPG_FORGE_LIVE_DIRECTOR_AUDIT_PASS`;
- `OPENRPG_FORGE_LIVE_DIRECTOR=PASS`.

## Revue visuelle

La surface `Live Director` présente joueurs et cibles, actions live, scène préparée, état de session et chronologie d'audit. La capture interne vérifiée est conservée localement sous `artifacts/verification/visual-g10/live-director-final.png`.

La revue confirme une hiérarchie claire, des actions lisibles, un état synchronisé visible, le refus du joueur et le hash d'audit. Le vocabulaire et l'identité restent OpenRPG Forge.

## Validation attendue

- 26 tests `Forge.LiveDirector.Tests`;
- 493 tests .NET globaux;
- 24 tests Python;
- graphe déterministe frais;
- smokes Toolset, runtime, Network Beta, Live Director, packaging, MCP et sauvegarde;
- `OPENRPG_FORGE_VERIFICATION=PASS`.
