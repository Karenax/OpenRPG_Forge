# R102 — Acceptation fondation multijoueur

## Périmètre

`R102 / FC-MULTI-P1-01` ferme exactement cinq fonctions P1 :

- `ORF-MP-001` — serveur dédié ;
- `ORF-MP-002` — serveur autoritaire ;
- `ORF-MP-007` — parties et groupes ;
- `ORF-MP-011` — réplication d’entités ;
- `ORF-MP-012` — interest management.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/21_MULTI.md`, lignes 68–145,
146–223, 536–613, 848–925 et 926–1003.

## Réduction des dépendances

BUILD fournit le binaire serveur, AREA les portées spatiales, GAMEPLAY les composants répliqués,
COMPANION/QUEST les consommateurs de groupe et SCRIPT les handles sûrs. R102 conserve un seul
`Forge.Network` autoritaire : le Toolset, le runtime, ForgeScript et MCP consomment les mêmes profils
et services, sans transport, stockage ou mutation client parallèles.

## Matrice d’écarts auditée

| Exigence source | Présent avant R102 | Limite réelle fermée par R102 | Preuve |
|---|---|---|---|
| Hôte dédié headless, service, santé et indépendance éditeur | Binaire headless et deux marqueurs | Mode service et contrat santé non modélisés dans le profil complet | profil, validateur, serveur et scénario R102 |
| Toute mutation gameplay validée serveur, intentions et état répliqué | Lobby révisionné et refus d’intention inconnue | Autorité globale configurable/persistée et audit de permission incomplets | politique d’autorité, historique et smoke serveur |
| Invitations, rôles, leader, loot, quêtes, marqueurs et séparation | Création, join par leader et requête alliée | Cycle de groupe et politiques de partage absents | `PartyService` et tests ciblés |
| Politiques par composant, delta, ownership, prédiction et réconciliation | Snapshots/deltas révisionnés sous autorité | Pas de règles de composant, budget, prédiction propriétaire ni correction | `ReplicationService`, tests et scénario |
| Intérêt zone, distance, visibilité, groupe, propriété et explicite | Zone, rayon, always-relevant et explicite | Visibilité, groupe et propriété absents | scope enrichi et test des six règles |
| UUID, schéma, provenance, scope, révision et audit communs | Configuration serveur compacte | Aucun profil global validé ni diagnostics avec UUID | profil/validateur et erreurs localisées |
| UI, commande, Undo/Redo, roundtrip, ForgeScript et MCP | Fenêtre Network Beta et preview serveur partiels | Pas de frontière partagée couvrant les cinq capacités | fenêtre, historique/store, handles et agent MCP |
| Extension runtime optionnelle/requise | Services réseau toujours supposés présents | Fallback optionnel et diagnostic requis absents | adaptateur et smoke runtime |

## Acceptation partagée

- `NetworkFoundationProfile` porte les cinq capacités avec UUID uniques, schéma v1, provenance,
  portée serveur, révision et identité d’audit.
- `NetworkFoundationHistory` impose preview/apply/reject, permission administrateur et révision
  optimiste ; Undo/Redo sont des compensations auditées et le store conserve le JSON canonique.
- Le groupe gère invitation expirante, rôles, transfert de leader, loot déterministe, partage de
  quêtes/marqueurs et politique de séparation.
- La réplication filtre les composants, borne le delta, conserve ownership, prédiction et
  réconciliation ; l’intérêt combine zone, distance, visibilité, groupe, propriété et abonnements.
- ForgeScript lit le profil par handles générationnels ; MCP filtre par ID/recherche et n’applique
  qu’un plan serveur prévisualisé avec rôle, diff et audit.
- L’extension absente est ignorée si optionnelle et produit `NET_FOUNDATION_REQUIRED_MISSING` si
  requise.

## Preuves reproductibles

- Tests ciblés : 6/6 fondation réseau et 3/3 agent MCP ; suite `Forge.Network.Tests` : 50/50.
- Smoke serveur : `OPENRPG_FORGE_SERVER_R102_NETWORK_FOUNDATION_PASS` et
  `OPENRPG_FORGE_NETWORK_BETA=PASS`.
- Marqueur Toolset : `OPENRPG_FORGE_TOOLSET_R102_NETWORK_FOUNDATION_PASS`.
- Marqueur runtime : `OPENRPG_FORGE_RUNTIME_R102_NETWORK_FOUNDATION_PASS`.
- Marqueur visuel : `OPENRPG_FORGE_R102_NETWORK_FOUNDATION_UI_CAPTURE_PASS`.
- Preuve visuelle : `docs/aurora-parity/evidence/r102-network-foundation-ui.png`, inspectée à
  1500×920. Configuration, quatre familles, permission, réconciliation, audit et digest sont lisibles
  sans chevauchement.
- Transcript du portail intégré : `.artifacts/r102-full-verify-final.log`.
- Portail consolidé : 1 724 tests .NET, 39 tests Python, marqueurs Toolset/runtime/serveur R102 et
  terminaison `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- `FC-MULTI-P1-02` et les lots P1 suivants avant le portail R102 vert ;
- fonctions P2, Asset/Map AV7–AV10, commit ou push.
