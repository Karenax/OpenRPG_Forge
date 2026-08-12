# R105 — Acceptation accès et supervision Live Director

## Périmètre

`R105 / FC-DM-P1-01` ferme exactement cinq fonctions P1 :

- `ORF-DM-002` — authentification GM ;
- `ORF-DM-011` — examiner ;
- `ORF-DM-025` — liste des joueurs ;
- `ORF-DM-031` — journal d’audit GM ;
- `ORF-DM-034` — chat et canaux GM.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/20_DM.md`, lignes 147–224,
853–930, 1948–2025, 2422–2499 et 2656–2733.

## Réduction des dépendances

Les services Live Director existants fournissaient déjà l’authentification, l’inspection, le roster,
le journal et le chat. R105 ne les remplace pas. Il ajoute un profil auteur canonique unique qui
relie ces services à l’interface, ForgeScript, MCP, au runtime et au serveur. Les transports MFA,
voix et archive externe restent des adaptateurs nommés : leur absence est sûre lorsqu’ils sont
optionnels et produit un diagnostic explicite lorsque le profil global est requis.

## Matrice d’écarts auditée

| Exigence source | Présent avant R105 | Limite réelle fermée | Preuve |
|---|---|---|---|
| Rôle GM, MFA, expiration, révocation et audit | service d’authentification et tests unitaires | aucune politique auteur persistable ni adaptateur MFA nommé | profil authentification, UI, scénario et tests |
| Inspection permissionnée en lecture seule | composants, statistiques, variables, scripts, propriété et historique | contrat dispersé, sans configuration auteur commune | politique d’inspection à six sections et preuve intégrée |
| Liste opérationnelle des joueurs | identité, personnage, zone, latence, groupe, permissions, voix et alertes | latence figée, aucun adaptateur de rafraîchissement borné | `RefreshLatencies`, politique à huit colonnes et tests |
| Audit immuable et filtrable | chaîne SHA-256 append-only | vérification limitée au chaînage du hash précédent | recalcul intégral des hash, requête bornée et test d’altération |
| Canaux GM et incarnation | sept canaux, scopes, texte normalisé et journal | raison d’incarnation non obligatoire, gouvernance non configurable | raison requise, politique de modération/logs et scénario |
| UUID, schéma, provenance, scope, révision et audit communs | métadonnées dispersées | aucun profil global validé | profil/validateur et diagnostics localisés |
| UI, commande, Undo/Redo, roundtrip, ForgeScript et MCP | appels directs aux services | aucune frontière transactionnelle couvrant les cinq fonctions | écran R105, historique/store, handles et cinq outils MCP |

## Critères de sortie

- Une interface spécialisée configure les cinq politiques sans édition JSON et montre état vide,
  erreur, permissions, confirmation, audit et résultat serveur.
- Preview/apply/reject, révision optimiste et compensations Undo/Redo partagent la commande canonique.
- Le JSON canonique survit à un aller-retour exact ; UUID, schéma, scope et références sont validés.
- ForgeScript lit des handles typés ; MCP filtre par ID/recherche et sépare plan dry-run et apply.
- Un scénario serveur prouve MFA, inspection sans mutation, roster rafraîchi, hash recalculé, canaux
  GM/incarnation et refus d’une mutation auteur par un joueur.
- L’extension absente est ignorée si optionnelle et diagnostiquée si requise.

## Preuves obtenues avant portail global

- 7/7 tests ciblés `LiveDirectorAccessProfileTests`, puis 35/35 tests Live Director complets ;
- 3/3 tests ciblés `LiveDirectorAccessAgentServiceTests` ;
- smoke serveur : `OPENRPG_FORGE_SERVER_R105_LIVE_DIRECTOR_ACCESS_PASS` et
  `OPENRPG_FORGE_LIVE_DIRECTOR_ACCESS=PASS` ;
- marqueurs Toolset et runtime R105 exacts dans `.artifacts/r105-toolset-smoke-live.log` et
  `.artifacts/r105-runtime-smoke.log` ;
- capture `docs/aurora-parity/evidence/r105-live-director-access-ui.png`, inspectée à 1500×920 :
  cinq politiques, champs éditables, permissions, preview/apply, Undo/Redo, audit et résultats
  serveur sont lisibles sans chevauchement ;
- capture confirmée par `OPENRPG_FORGE_R105_LIVE_DIRECTOR_ACCESS_UI_CAPTURE_PASS` dans
  `.artifacts/r105-live-director-access-capture.log`.

## Portail consolidé

Le transcript `.artifacts/r105-full-verify-pass1.log` confirme 1 753 tests .NET, 39 tests Python,
le graphe Roslyn à 7 642 nœuds / 49 114 relations, les marqueurs Toolset/runtime/serveur R105, les
parcours 3D, exports, MCP et sauvegardes, puis se termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- fournisseur MFA public, transport voix concret, SIEM ou archive externe déployée ;
- lots R106+, P2, Asset/Map AV7–AV10, commit ou push avant portail R105 vert.
