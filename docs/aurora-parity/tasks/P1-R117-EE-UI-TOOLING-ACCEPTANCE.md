# R117 — Acceptation EE interface et outillage runtime

## Périmètre

`R117 / FC-EE-P1-04` couvre exactement quatre fonctions P1 :

- `ORF-EE-024` — améliorations NUI, déjà `Tested` et reconfirmée ;
- `ORF-EE-026` — TrueType et scaling UI ;
- `ORF-EE-027` — interpolation de transforms, déjà `Tested` et reconfirmée ;
- `ORF-EE-031` — améliorations compilateur, déjà `Tested` et reconfirmée.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/23_EE.md`, lignes 1619–1685,
1753–1886 et 2088–2150.

## Matrice d’écarts auditée

| Exigence source | Présent avant R117 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Toolkit NUI versionné, layout, formulaires, listes, canvas, accessibilité, localisation et inspecteur | toolkit 2.0 canonique, auteur transactionnel et rendu runtime | aucune limite source réelle | preuves G11-P maintenues et smoke R117 de non-régression |
| TrueType, shaping, DPI, échelle utilisateur, reflow, focus manette et captures | profil UI/texte, `SystemFont`, manifests de police avec shaping/locales/DPI, HUD scalable, navigation focus et suite visuelle | la note tracker antérieure n’intégrait pas les livraisons ultérieures G11-T et G11-S | consolidation R117 et portail qui exige 8 paires PNG à deux résolutions |
| Tween transform/material/state avec easing, annulation, autorité et sauvegarde | définition canonique, runtime d’interpolation, séquences, persistance et smoke cinématique | aucune limite source réelle | preuves G11-K maintenues et smoke R117 de non-régression |
| Conformance compilateur, diagnostics stables, incrémental et télémétrie | compilation ordonnée, cache transitif, diagnostics, quotas, télémétrie, CLI/runtime/MCP | aucune limite source réelle | preuves G11-P maintenues et smoke R117 de non-régression |

## Contrat fermé

- Les familles TrueType et fallbacks sont gouvernés comme chunks de police avec scripts de shaping,
  locales de test, paliers DPI et bornes d’échelle.
- L’éditeur applique l’échelle UI et texte au thème global ; le HUD runtime applique son profil texte
  et utilise ancres, containers et retour à la ligne.
- La roue d’actions et les boutons runtime sont focusables au clavier et à la manette, avec premier
  focus explicite et repli en liste accessible.
- La suite G11-S produit réellement huit références, huit candidates et huit revues JSON pour deux
  résolutions, deux caméras et deux qualités ; elle échoue au-delà de la tolérance.
- NUI, interpolation et compilateur conservent leurs commandes, persistance, diagnostics et replis
  runtime déjà testés.

## Preuves obtenues avant portail global

- 5/5 tests Domain ciblés ;
- 6/6 tests Application ciblés ;
- 12/12 tests ModuleFormat ciblés ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r117-toolset-smoke.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R117_EE_UI_TOOLING_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 655 `Tested`, 61 `Implemented`, 1 `Researched`, 167 lots clos ;
- programme P1 terminé : 298/298 fonctions et 70/70 lots `Tested` ;
- documentation générée puis vérifiée sans dérive ;
- graphe Roslyn déterministe : 7 698 nœuds, 49 447 relations, zéro diagnostic ;
- 1 767 tests .NET et 39 tests Python passent ;
- la suite visuelle produit 8 références PNG, 8 candidates PNG et 8 revues JSON à 2 résolutions ;
- `artifacts/verification/toolset.log` contient le marqueur exact R117 ;
- `.artifacts/p1-r117-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- moteur de shaping ou rasteriseur de police propriétaire, la plateforme et le moteur fournissant ces services ;
- comparaison visuelle artistique manuelle de toutes les combinaisons de matériel ;
- P2, Asset/Map AV7–AV10, commit ou push.
