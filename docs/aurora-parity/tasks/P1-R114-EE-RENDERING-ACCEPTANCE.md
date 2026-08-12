# R114 — Acceptation EE workspace et rendu

## Périmètre

`R114 / FC-EE-P1-01` couvre exactement cinq fonctions P1 :

- `ORF-EE-001` — transformations visuelles runtime, déjà `Tested` et reconfirmée ;
- `ORF-EE-003` — plusieurs onglets de zone ;
- `ORF-EE-005` — rendu PBR ;
- `ORF-EE-006` — tone mapping et HDR ;
- `ORF-EE-007` — éclairage par pixel.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/23_EE.md`, lignes 63–132,
200–267 et 334–535.

## Matrice d’écarts auditée

| Exigence source | Présent avant R114 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Workspace multi-document avec split et sélection protégée | huit onglets persistants, zone active/comparaison, deux `ProjectPreview3D`, comparaison en lecture seule, fermeture et réconciliation sûres | l’ancienne note tracker affirmait à tort que deux viewports restaient futurs | `MultiAreaSplitViewWindow`, smoke R059 et marqueur R114 |
| Conventions PBR, instances, validation et profils | profil de traitement multi-canaux, formats KTX2/GLB, validation gouvernée, metallic-roughness, matériaux par pixel et UI | aucune ; le raccord automatique de textures arbitraires à un GLB curaté n’est pas demandé par le contrat | tests d’import, plan moteur et matériau réel dans le smoke R114 |
| Profils colorimétriques, exposition, tone mapper, LUT, HDR et scène de test | AgX/ACES/Filmic/Linear, exposition, point blanc, chemin LUT et profil HDR persistants | le moteur n’appliquait ni la LUT ni le profil HDR au viewport | chargement réel de la LUT, diagnostic stable de ressource absente et `UseHdr2D` Toolset/runtime |
| Forward+, qualité, budgets de lumières/ombres, limites et debug | profil qualité, Forward+, per-pixel, budgets plateforme, seuils, atlas, diagnostics et logs backend | aucune ; un benchmark GPU matériel n’est pas un critère fonctionnel | tests de budgets, Toolset/runtime et marqueur R114 |

## Contrat fermé

- Les onglets et le split conservent deux identités de zone distinctes et empêchent la comparaison
  de devenir une voie de mutation parallèle.
- Les canaux PBR sont typés, confinés et validés avant build ; les matériaux moteur utilisent le
  shading par pixel et le workflow metallic-roughness.
- Une LUT disponible est chargée comme ressource moteur et active la correction colorimétrique ;
  une LUT absente conserve un rendu neutre avec `render.color.lut_missing`.
- Le profil HDR configure le viewport Toolset et le viewport runtime ; tone mapper, exposition et
  point blanc utilisent le même profil canonique persisté.
- Les budgets plateforme et leurs seuils produisent des diagnostics stables avant le rendu.

## Preuves obtenues avant portail global

- 2/2 tests `AreaRenderingProfileTests` ;
- 1/1 test `AreaRenderPlanBuilderTests` ;
- 1/1 test `EditorWorkspaceProfilePlannerTests` ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r114-toolset-smoke-c8e0269a3e844065a54e073a1c081918.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R114_EE_RENDERING_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 651 `Tested`, 65 `Implemented`, 1 `Researched`, 164 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 684 nœuds, 49 355 relations, zéro diagnostic ;
- 1 763 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R114 ;
- `.artifacts/r114-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- certification physique d’un moniteur HDR particulier ;
- benchmark GPU dépendant d’un modèle de carte graphique ;
- réécriture automatique des matériaux de modèles tiers curatés ;
- P2, Asset/Map AV7–AV10, commit ou push.
