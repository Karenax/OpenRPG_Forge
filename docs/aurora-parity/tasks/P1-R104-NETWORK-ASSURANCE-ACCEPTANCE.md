# R104 — Acceptation assurance réseau

## Périmètre

`R104 / FC-MULTI-P1-03` ferme exactement trois fonctions P1 :

- `ORF-MP-029` — logs structurés ;
- `ORF-MP-031` — anti-cheat par conception ;
- `ORF-MP-036` — tests réseau déterministes.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/21_MULTI.md`, lignes 2252–2329,
2408–2485 et 2798–2875.

## Réduction des dépendances

QA fournit déjà la politique de confidentialité et le format structuré ; le serveur fournit
l'autorité, la persistance et les sessions ; les tests réseau fournissent la simulation de paquets.
R104 ne remplace aucune de ces briques. Il ajoute une seule configuration canonique
`Forge.Network` qui relie l'auteur, MCP, ForgeScript, le runtime et le serveur aux mêmes politiques.

## Matrice d'écarts auditée

| Exigence source | Présent avant R104 | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| JSON/texte, catégories, corrélation et filtres privés | journal structuré, catégories, corrélation, UUID source/transaction, rétention et masquage | rotation, sorties texte et adaptateurs distants non configurables par l'auteur | politique canonique, routeur borné et tests de sinks |
| Intents, limites et autorité serveur | rejet des UUID, révisions, replays, cadence et déplacements impossibles | contenu signé, RNG autoritaire, seuil d'anomalie et refus explicite des sauvegardes client non unifiés | politique anti-cheat et scénario serveur |
| Clients simulés, latence, perte et réordonnancement | harness déterministe à deux clients avec perte, duplication et ordre inversé | replay multi-passes, soak borné et assertion d'état autoritaire non orchestrés | runner déterministe et empreinte stable |
| UUID, schéma, provenance, scope, révision et audit communs | informations dispersées | aucun profil assurance global validé | profil/validateur et diagnostics localisés |
| UI, commande, Undo/Redo, roundtrip, ForgeScript et MCP | services techniques appelés directement | aucune frontière auteur transactionnelle couvrant les trois capacités | écran assurance, historique/store, handles et MCP |
| Extension runtime optionnelle/requise | capacités supposées disponibles | fallback optionnel et diagnostic requis absents | adaptateur et smoke runtime |

## Critères de sortie

- Une interface spécialisée configure logs, anti-cheat et campagne déterministe sans édition JSON et
  montre état vide, erreur, permissions, confirmation et audit.
- Preview/apply/reject, révision optimiste et compensations Undo/Redo partagent la commande canonique.
- Le JSON canonique survit à un aller-retour exact ; UUID, schéma, scope et références sont validés.
- ForgeScript lit des handles typés ; MCP filtre par ID/recherche et sépare plan dry-run et apply.
- Un scénario serveur prouve rotation/sinks, contenu signé, RNG autoritaire, anomalie, refus des
  sauvegardes client, replay déterministe, soak borné et assertion d'état.
- L'extension absente est ignorée si optionnelle et diagnostiquée si requise.

## Validations prévues

- `dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj --filter NetworkAssuranceProfileTests`
- `dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --filter NetworkAssuranceAgentServiceTests`
- `dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj`
- smokes Toolset, runtime et serveur avec marqueurs R104 exacts ; capture 1500×920 inspectée ;
- régénération tracker/backlog/documentation/graphe puis `scripts/Verify.ps1`.

## Preuves obtenues avant portail global

- 7/7 tests ciblés `NetworkAssuranceProfileTests`, 3/3 tests MCP, puis 63/63 tests réseau et
  114/114 tests MCP complets ;
- smoke serveur : `OPENRPG_FORGE_SERVER_R104_NETWORK_ASSURANCE_PASS` et
  `OPENRPG_FORGE_NETWORK_BETA=PASS` ;
- marqueurs Toolset et runtime R104 exacts ;
- capture `docs/aurora-parity/evidence/r104-network-assurance-ui.png`, inspectée à 1500×920 : les
  trois politiques, champs éditables, permissions, preview/apply, Undo/Redo, audit et résultats
  serveur sont lisibles sans chevauchement ;
- transcripts ciblés sous `.artifacts/r104-*.log`.

## Portail consolidé

Le transcript `.artifacts/r104-full-verify-final.log` confirme 1 743 tests .NET, 39 tests Python,
le graphe Roslyn à 7 602 nœuds / 48 861 relations, les marqueurs Toolset/runtime/serveur R104, les
parcours 3D, exports, MCP et sauvegardes, puis se termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- service SIEM concret, bannissement global partagé ou service public persistant ;
- lots R105+, P2, Asset/Map AV7–AV10, commit ou push avant portail R104 vert.
