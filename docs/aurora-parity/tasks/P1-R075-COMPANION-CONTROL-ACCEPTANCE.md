# P1 R075 — Contrôle des compagnons

## Périmètre

Lot canonique `R075 / FC-COMPANION-P1-02`, limité à cinq fonctions :

- `ORF-CMP-007` — propriété du compagnon ;
- `ORF-CMP-008` — ordres de déplacement ;
- `ORF-CMP-009` — ordres de combat ;
- `ORF-CMP-010` — comportement autonome ;
- `ORF-CMP-012` — équipement du compagnon.

Le lot relie les calculs RPG Kit existants au document de monde canonique, à l’interface d’auteur,
à la persistance, à ForgeScript, au MCP et à des adaptateurs moteur observables. Il n’ouvre ni
`ORF-CMP-011` ou `ORF-CMP-013` en P2, ni AV7–AV10, ni R076.

## Audit source avant modification

Les blocs complets `ORF-CMP-007`, `008`, `009`, `010` et `012`, lignes 488 à 927 de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/17_COMPANION.md`, ont été relus avant
modification. Le bloc P2 `ORF-CMP-011` situé entre `010` et `012` a été explicitement exclu.

| Critère source | État avant R075 | Limite réelle fermée par R075 |
|---|---|---|
| Propriété et transfert | cinq modes et audit calculés seulement dans le RPG Kit | profil persistant avec contrôleurs, cibles autorisées, révision, aperçu/application déterministes et diff sémantique |
| Ordres de déplacement | six ordres typés isolés | Move, Follow, Hold, Rally, Guard et Interact exposés avec destination, cible, waypoint, priorité et autorité |
| Ordres tactiques | six ordres typés isolés | Attack, Defend, FocusFire, Protect, Disengage et UseAbility exposés avec cible, posture, capacité et file priorisée |
| IA autonome | calcul validé hors du module | autonomie, risque, ressources, tir allié, rôle, distance et capacités avec plan, transaction et autorisation |
| Équipement | permission, verrou et comparaison isolés | permission, propriétaire, objets verrouillés, loadout suggéré, comparaison et diff sémantique persistants |
| Surfaces produit | aucune configuration principale commune | interface transactionnelle, round-trip, handle typé, ressource MCP filtrée, diagnostics pré-build et vrais nœuds moteur |

## Matrice d’acceptation

| Critère commun immuable | Preuve R075 |
|---|---|
| Domaine canonique avant moteur | `CompanionWorldProfile.Control` porte propriété, commandes, IA et équipement sans dépendance moteur |
| Identité et déterminisme | contrôle et sous-profils portent UUID, schéma et provenance ; transferts et IA portent plans, transactions et diffs stables |
| Validation structurée | diagnostics localisés pour identité, ownership, commandes, IA, équipement et extension requise absente |
| Interface visible, vide et erreur | onglet `COMPAGNONS`, section de contrôle, 12 commandes, IA, équipement et résumé ; états vide/erreur exercés par le smoke |
| UI et commande uniques | construction du catalogue puis `SetWorldFeatureCatalogHandler`, dry-run/apply/reject et undo/redo |
| ForgeScript typé | `CompanionHandle` expose propriété, commandes, IA, équipement et état requis |
| MCP filtré | ressource structurée filtrable par propriétaire avec mutation complète en dry-run |
| Runtime moteur | racine `CompanionControlAdapters`, compagnon et cinq capacités comme vrais nœuds |
| Optionnel/requis | absence optionnelle ignorée et contrôle requis absent diagnostiqué |

## Validation exécutée

- tests ciblés : RPG Kit R075 3/3, ModuleFormat R075 4/4 et MCP R075 1/1 ;
- capture moteur `OPENRPG_FORGE_R075_COMPANION_CONTROL_UI_CAPTURE_PASS` ;
- capture `docs/aurora-parity/evidence/r075-companion-control-ui.png`, 1450×940, inspectée : les
  12 commandes, la propriété et les transferts, l’IA et l’équipement sont lisibles sans
  chevauchement global ;
- format de la solution vérifié sans modification résiduelle ;
- suite complète .NET : 1 531/1 531 tests passants sur les projets de tests ;
- registres régénérés : 507 fonctions `Tested`, 209 `Implemented`, 1 `Researched`, avec 129 lots
  clos, 57 à compléter/prouver et 1 à livrer ;
- graphe Roslyn frais et déterministe : 6 936 nœuds, 44 065 relations, 0 diagnostic ;
- smokes Toolset et runtime avec marqueurs exacts R075, puis portail intégré terminé sur
  `OPENRPG_FORGE_VERIFICATION=PASS` ; transcript : `.artifacts/r075-full-verify-transcript.log`.

## Décision du palier

Les cinq limites fonctionnelles disposent de preuves ciblées indépendantes et leurs promotions sont
bornées à R075. Le marqueur terminal exact `OPENRPG_FORGE_VERIFICATION=PASS` autorise l’ouverture de
R076 uniquement ; aucune fonction P2, AV7–AV10 ou publication n’a été ouverte.
