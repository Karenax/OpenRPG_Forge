# G9 Network Beta — preuve de fermeture

## Verdict

G9 est fermé lorsque la validation globale finale produit `OPENRPG_FORGE_VERIFICATION=PASS`. Les 30 exigences assignées sont `Implemented` avec des preuves ciblées et des limites `Partial:` explicites.

## Scénario de passage

`NetworkBetaScenario` exécute deux clients distincts sur une session autoritaire :

1. fingerprints et versions compatibles;
2. deux identités, personnages et états prêts;
3. groupe partagé et instance de la Crypte;
4. progression `entered-crypt` et brasier répliqués avec le même digest;
5. déconnexion/reconnexion du client B à sa dernière révision;
6. tentative forgée `world.set-state` refusée par `NET_INTENT_FORBIDDEN`.

Marqueurs serveur :

- `OPENRPG_FORGE_NETWORK_TWO_CLIENTS_PASS`;
- `OPENRPG_FORGE_NETWORK_RECONNECT_PASS`;
- `OPENRPG_FORGE_NETWORK_AUTHORITY_PASS`;
- `OPENRPG_FORGE_NETWORK_BETA=PASS`.

## Interface auteur et revue visuelle

Le workspace `Réseau` ouvre une surface Aurora 2026 dédiée avec configuration du serveur, synthèse sécurité/contenu/résilience/observabilité et test intégré à deux clients. La capture interne vérifiée est conservée localement sous `artifacts/verification/visual-g9/network-beta-final.png`.

La revue a confirmé : valeurs 5121/8 visibles, action principale claire, état vert, révisions client A/B identiques, reconnexion acceptée et mutation forgée refusée. La capture écran Windows était noire avec le rendu accéléré; la preuve finale provient donc directement de la texture du viewport Toolset.

## Validation attendue

- 35 tests `Forge.Network.Tests`;
- 467 tests .NET globaux;
- 24 tests Python de gouvernance;
- graphe déterministe frais;
- smokes Toolset, runtime, packaging, MCP et sauvegarde;
- scénario Network Beta permanent dans `scripts/Verify.ps1`.
