# P1 R074 — Fondation des compagnons

## Périmètre

Lot canonique `R074 / FC-COMPANION-P1-01`, limité à cinq fonctions :

- `ORF-CMP-001` — créature recrutabile ;
- `ORF-CMP-002` — conversation avant recrutement ;
- `ORF-CMP-003` — conversation avec son maître ;
- `ORF-CMP-005` — recrutement et renvoi ;
- `ORF-CMP-006` — plusieurs compagnons.

Le lot réutilise les calculs de `CompanionService` et `CompanionControlService`, mais ferme leur
ancienne isolation : le contrat est désormais attaché au document de monde, éditable, persistant,
inspectable par ForgeScript et MCP, puis matérialisé par le runtime. Il n'ouvre ni `ORF-CMP-004`,
ni P2, ni AV7–AV10, ni R075.

## Audit source avant modification

Les blocs complets `ORF-CMP-001`, `002`, `003`, `005` et `006`, lignes 56 à 487 de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/17_COMPANION.md`, ont été relus avant
modification.

| Critère source | État avant R074 | Limite réelle fermée par R074 |
|---|---|---|
| Créature et politique de recrutement | calculs RPG Kit isolés du projet | `FoundationProfile` persistant avec composant, politique, identité stable et prérequis explicites |
| Dialogue avant/après recrutement | contextes calculés sans contrat auteur principal | profils `NotRecruited` et `RecruitedBySpeaker`, offre, refus, coût, réputation, conséquences, commandes et départ |
| Recrutement et renvoi | transactions testées hors document de monde | aperçu et application déterministes, capacité, propriété, fonds, inventaire, position, persistance, renvoi et rollback atomique |
| Plusieurs compagnons | `PartyPolicy` disponible dans le RPG Kit seulement | quotas compagnons, invocations, familiers, alliés temporaires, rôles et population serveur attachés au module |
| Surfaces produit | validation R037 sans fondation P1 complète | interface transactionnelle, round-trip, lecture typée, ressource MCP filtrée, diagnostics pré-build et vrais nœuds moteur |

## Matrice d'acceptation

| Critère commun immuable | Preuve R074 |
|---|---|
| Domaine canonique avant moteur | `CompanionWorldProfile.Foundation` porte tout le contrat sans dépendance moteur |
| Identité et déterminisme | fondation et cinq sous-profils portent UUID, schéma et provenance ; identifiants de transaction et plans sont déterministes |
| Validation structurée | diagnostics stables et chemins localisés pour identités, politique, dialogues, transaction et quotas |
| Interface visible, vide et erreur | onglet `COMPAGNONS`, état requis, règles, recrutement, coût, réputation, contextes, atomicité et quotas visibles ; états vide/erreur exercés par le smoke |
| UI et commande uniques | construction du catalogue puis `SetWorldFeatureCatalogHandler`, dry-run/apply/reject et undo/redo |
| ForgeScript typé | `CompanionHandle` expose fondation, contextes, transaction, persistance et limites de groupe |
| MCP filtré | ressource structurée filtrable par propriété et contexte, avec mutation complète en dry-run |
| Runtime moteur | racine `CompanionFoundationAdapters`, créature et cinq capacités comme vrais nœuds |
| Optionnel/requis | absence optionnelle ignorée et fondation requise absente diagnostiquée |

## Validation exécutée

- tests ciblés : RPG Kit R074 3/3, ModuleFormat R074 3/3 et MCP R074 1/1 ;
- suite complète .NET : 1 527/1 527 tests passants sur les projets de tests ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R074_COMPANION_FOUNDATION_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R074_COMPANION_FOUNDATION_PASS` avec nœuds moteur réels ;
- capture moteur `OPENRPG_FORGE_R074_COMPANION_FOUNDATION_UI_CAPTURE_PASS` ;
- capture `docs/aurora-parity/evidence/r074-companion-foundation-ui.png`, 1450×940, inspectée :
  fondation requise, politique, voies de recrutement, contextes, transaction atomique, quotas C/I/F/A,
  rôle et limite serveur sont lisibles sans chevauchement global ;
- format de la solution vérifié sans modification résiduelle ;
- registres régénérés : 502 fonctions `Tested`, 214 `Implemented`, 1 `Researched`, avec 128 lots
  fermés, 58 à compléter/prouver et 1 à livrer ;
- graphe Roslyn frais et déterministe : 6 923 nœuds, 43 969 relations, 0 diagnostic.

## Décision du palier

Les cinq limites fonctionnelles disposent de preuves ciblées indépendantes et leurs promotions sont
bornées à R074. La clôture autorise l'ouverture de R075 uniquement après que le portail final
`scripts/Verify.ps1` a produit le marqueur terminal exact `OPENRPG_FORGE_VERIFICATION=PASS` sur cet
arbre documenté.
