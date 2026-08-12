# P1 R076 — Expérience des compagnons

## Périmètre

Lot canonique `R076 / FC-COMPANION-P1-03`, limité à trois fonctions :

- `ORF-CMP-017` — mort, inconscience et résurrection ;
- `ORF-CMP-018` — téléportation de rattrapage ;
- `ORF-CMP-022` — interface de groupe.

Le lot relie les calculs RPG Kit existants au document canonique, à l’interface d’auteur, à la
persistance, à ForgeScript, au MCP et au runtime. Il n’ouvre aucune fonction P2 intercalée
(`ORF-CMP-019`, `020`, `021` ou `023`), ni AV7–AV10, ni R077.

## Audit source avant modification

Les blocs complets `ORF-CMP-017` et `018`, lignes 1210 à 1353, puis `ORF-CMP-022`, lignes 1578 à
1649 de `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/17_COMPANION.md`, ont été
relus avant modification. Les blocs P2 situés entre eux ont été explicitement exclus.

| Critère source | État avant R076 | Limite réelle fermée par R076 |
|---|---|---|
| Mort et récupération | six transitions calculées hors du module | politique persistante avec durée, ressource, remplacement, mémorial, inventaire, événements et effets |
| Cohésion | navigation, portail et téléportation calculés hors du module | seuils ordonnés, portails, ancres sûres, audit et blocages en combat ou sous le regard des joueurs |
| HUD de groupe | configuration validée sans surface runtime | panneau runtime visible avec portrait, santé, ressources, statuts, commandes rapides et réordonnancement |
| Accessibilité | booléens isolés | clavier, manette, libellés lecteur d’écran et contraste configurables et matérialisés |
| Surfaces produit | aucune liaison canonique complète | UI transactionnelle, round-trip, handle typé, ressource MCP filtrée, pré-build et nœuds moteur |

## Matrice d’acceptation

| Critère commun immuable | Preuve R076 |
|---|---|
| Domaine canonique avant moteur | `CompanionWorldProfile.Experience` porte survie, cohésion et HUD sans dépendance moteur |
| Identité | expérience et trois sous-profils portent UUID, schéma et provenance uniques |
| Validation structurée | diagnostics localisés pour politique de mort, seuils/ancres, accessibilité et extension requise |
| Interface visible, vide et erreur | sections Survie, Cohésion et HUD, résumé, états vide/erreur exercés par le smoke |
| UI et commande uniques | construction du catalogue puis `SetWorldFeatureCatalogHandler`, dry-run/apply/reject et undo/redo |
| ForgeScript typé | `CompanionHandle` expose survie, cohésion, HUD et état requis |
| MCP filtré | ressource structurée filtrable par accessibilité avec mutation complète en dry-run |
| Runtime moteur | racine `CompanionExperienceAdapters`, trois capacités et panneau HUD réellement visible |
| Optionnel/requis | absence optionnelle ignorée et expérience requise absente diagnostiquée |

## Validation exécutée

- tests ciblés : RPG Kit R076 3/3, ModuleFormat R076 5/5 et MCP R076 1/1 ;
- suite complète .NET : 1 535/1 535 tests passants ;
- capture moteur `OPENRPG_FORGE_R076_COMPANION_EXPERIENCE_UI_CAPTURE_PASS` ;
- capture `docs/aurora-parity/evidence/r076-companion-experience-ui.png`, 1450×940, inspectée : seuils
  10/30/60, ancre sûre, HUD, clavier/manette et résumé survie/cohésion/accessibilité sont lisibles ;
- format de la solution vérifié sans modification résiduelle ;
- registres régénérés : 510 fonctions `Tested`, 206 `Implemented`, 1 `Researched`, avec 130 lots
  clos, 56 à compléter/prouver et 1 à livrer ;
- documentation générée fraîche ; graphe Roslyn déterministe à 6 946 nœuds, 44 137 relations et
  0 diagnostic ;
- smokes Toolset et runtime avec marqueurs exacts R076, puis portail intégré terminé sur
  `OPENRPG_FORGE_VERIFICATION=PASS` ; transcript : `.artifacts/r076-full-verify-transcript.log`.

## Décision du palier

Les trois promotions sont bornées à R076. Le marqueur exact `OPENRPG_FORGE_VERIFICATION=PASS`
autorise l’ouverture de R077 uniquement ; aucune fonction P2, AV7–AV10 ou publication n’a été ouverte.
