# G9 Network Beta — preuve de fermeture

## Verdict

G9 est fermé verticalement lorsque la validation globale finale produit
`OPENRPG_FORGE_VERIFICATION=PASS`. R102 et R103 ferment les dix fonctions P1 de fondation et
d’opérations réseau avec preuves ciblées ; les autres exigences G9 conservent leur statut tracker
propre jusqu’à leur lot canonique.

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

Le workspace `Réseau` ouvre une surface Aurora 2026 dédiée avec configuration du serveur, du contenu,
du lockfile, des rôles, du chat et du bus d’événements, ainsi qu’un test intégré. La preuve R103
inspectée est `docs/aurora-parity/evidence/r103-network-operations-ui.png`.

La revue a confirmé : valeurs 5121/8 visibles, action principale claire, état vert, révisions client A/B identiques, reconnexion acceptée et mutation forgée refusée. La capture écran Windows était noire avec le rendu accéléré; la preuve finale provient donc directement de la texture du viewport Toolset.

## Validation historique et actuelle

- 56 tests `Forge.Network.Tests` après R103 ;
- le total .NET et Python est repris dans la fiche d’acceptation du dernier lot validé ;
- graphe déterministe frais;
- smokes Toolset, runtime, packaging, MCP et sauvegarde;
- scénario Network Beta permanent dans `scripts/Verify.ps1`.
