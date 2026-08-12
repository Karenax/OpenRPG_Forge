# R103 — Acceptation opérations multijoueur

## Périmètre

`R103 / FC-MULTI-P1-02` ferme exactement cinq fonctions P1 :

- `ORF-MP-013` — synchronisation de contenu ;
- `ORF-MP-015` — version locking ;
- `ORF-MP-017` — rôles et permissions ;
- `ORF-MP-020` — chat ;
- `ORF-MP-027` — bus d’événements serveur.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/21_MULTI.md`, lignes 1004–1081,
1160–1237, 1316–1393, 1550–1627 et 2096–2173.

## Réduction des dépendances

CONTENT fournit manifestes, hashes, provenance et cache ; DM fournit l’identité et les décisions
permissionnées ; SCRIPT consomme des handles sûrs et le bus typé. R103 conserve une seule frontière
`Forge.Network` : l’éditeur, MCP, ForgeScript, le runtime et le serveur projettent le même profil
canonique et n’écrivent jamais directement dans le cache ou les nœuds moteur.

## Matrice d’écarts auditée

| Exigence source | Présent avant R103 | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Manifestes, chunks, hashes, cache, reprise, delta et confiance | manifeste signé, cache adressé par hash, reprise et contrôle licence/confiance | paramètres auteur globaux, chunks bornés et delta explicite absents | profil, plan/synchronisation et tests cache |
| Lockfile et fingerprint négociés avant join, rapport lisible | protocole/module/fingerprint refusés avant session | lockfile canonique et détails de mismatch lisibles non modélisés | politique de verrou et tests de comparaison |
| Player, Moderator, Builder, GM, Admin, Spectator et capacités custom | Player, Moderator, Administrator et scope zone | rôles manquants, capacités custom et ABAC incomplets | autorisation enrichie et test serveur |
| Local, party, tell, shout, DM, proximité, langue, logs, accessibilité, bridges | local/party/global/system, mute et séquence | tell/shout/DM, proximité, filtre langue, texte accessible et bridges absents | service chat enrichi et scénario |
| Bus typé, abonnements, priorités, tracing, replay, backpressure | types, abonnement, séquence, corrélation et anti-replay | priorité, trace et file bornée absentes | bus enrichi et scénario réentrant |
| UUID, schéma, provenance, scope, révision et audit communs | dispersés dans plusieurs services | aucun profil opérations global validé | profil/validateur et diagnostics localisés |
| UI, commande, Undo/Redo, roundtrip, ForgeScript et MCP | fenêtre fondation et services directs | aucune frontière auteur couvrant les cinq capacités | onglet opérations, historique/store, handles et MCP |
| Extension runtime optionnelle/requise | capacités supposées disponibles | fallback optionnel et diagnostic requis absents | adaptateur et smoke runtime |

## Critères de sortie

- Une interface spécialisée configure les cinq politiques sans édition JSON et montre vide, erreur,
  permissions, confirmation et audit.
- Preview/apply/reject, révision optimiste et compensations Undo/Redo partagent la commande canonique.
- Le JSON canonique survit à un aller-retour exact ; les UUID sont uniques et les références résolues.
- ForgeScript lit par handles typés ; MCP filtre par ID/recherche et sépare plan dry-run et apply.
- Un scénario serveur vérifie permissions, audit, version mismatch lisible, chat et bus prioritaire borné.
- L’extension absente est ignorée si optionnelle et diagnostiquée si requise.

## Validations prévues

- `dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj --filter NetworkOperationsProfileTests`
- `dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --filter NetworkOperationsAgentServiceTests`
- `dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj`
- smokes Toolset, runtime et serveur avec marqueurs R103 exacts ; capture 1500×920 inspectée ;
- régénération tracker/backlog/documentation/graphe puis `scripts/Verify.ps1`.

## Preuves obtenues avant portail global

- 6/6 tests ciblés `NetworkOperationsProfileTests`, 3/3 tests MCP et 56/56 tests réseau ;
- smoke serveur : `OPENRPG_FORGE_SERVER_R103_NETWORK_OPERATIONS_PASS` et
  `OPENRPG_FORGE_NETWORK_BETA=PASS` ;
- marqueurs Toolset et runtime R103 exacts ;
- capture `docs/aurora-parity/evidence/r103-network-operations-ui.png`, inspectée à 1500×920 :
  les cinq familles, champs éditables, permissions, preview/apply, Undo/Redo, audit et résultats
  serveur sont lisibles sans chevauchement ; le détail long est volontairement scrollable ;
- transcripts ciblés sous `.artifacts/r103-*.log`.

## Portail consolidé

Le transcript `.artifacts/r103-full-verify-final.log` confirme 1 733 tests .NET, 39 tests Python,
le graphe Roslyn à 7 551 nœuds / 48 537 relations, les marqueurs Toolset/runtime/serveur R103, les
deux parcours 3D, les exports, MCP et les sauvegardes, puis se termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- transport voix, administration distante, modération générale et lots R104+ avant portail R103 vert ;
- fonctions P2, Asset/Map AV7–AV10, commit ou push.
