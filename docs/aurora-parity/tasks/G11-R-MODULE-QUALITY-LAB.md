# G11-R — Laboratoire qualité du module

Date de fermeture : 5 août 2026.

## Résultat observable

Le bouton **Qualité** ouvre dans Creator un laboratoire à cinq volets : localisation, ForgeScript,
dialogues, quêtes et rencontres. Chaque volet affiche un statut textuel, sa couverture et ses
erreurs ou avertissements. Le CLI `forge quality run`, le runtime et l’outil MCP
`RunProjectQualityLab` exécutent le même contrat et renvoient la même empreinte déterministe.

Sur **Crypte des Cendres**, la passe réelle exerce deux langues, deux scripts natifs, un dialogue,
une quête et neuf simulations de rencontre. Le français est complet ; les traductions anglaises
manquantes restent des avertissements visibles et n’occultent ni les placeholders ni les pluriels
invalides, qui sont bloquants.

## Contrats livrés

- `LocalizationQualityService` calcule couverture par langue, cohérence des placeholders, formes
  plurielles, estimation de débordement, pseudo-localisation et preuve SVG déterministe ;
- `ForgeScriptSandboxTestService` compile et exécute des cas bornés avec quotas, événements
  attendus, appels au monde simulé et couverture ;
- `NarrativeQualityService` énumère les branches, exécute conditions/actions et mesure nœuds,
  choix, états et transitions ; les quêtes vérifient événements, portée et save/resume ;
- `EncounterQualityService` simule les créatures placées avec profils solo, groupe et vétéran,
  seeds stables, navigation réelle et nettoyage vérifié ;
- `WorkspaceQualityLabService` agrège ces résultats, limite le nombre de scripts, produit une
  empreinte SHA-256 et confine l’éventuelle preuve à `.forge/quality/pseudo-localization.svg` ;
- `QualityLabWindow`, CLI, runtime et MCP consomment ce rapport sans voie de mutation parallèle.

## Preuves automatiques

- `ProjectQualityServicesTests` couvre localisation, SVG, narration, quêtes, navigation et seeds ;
- `ForgeScriptSandboxTestServiceTests` utilise le compilateur/runtime natif, les quotas, le monde
  simulé et la couverture d’événements ;
- `WorkspaceQualityLabServiceTests` couvre l’agrégation, le fichier SVG et l’empreinte stable ;
- `ForgeProjectAgentServiceTests` prouve la lecture MCP déterministe, sans révision ni fichier ;
- markers `OPENRPG_FORGE_TOOLSET_G11R_QUALITY_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11R_QUALITY_PASS` et `OPENRPG_FORGE_QUALITY_LAB_PASS` ;
- `scripts/Verify.ps1` impose les trois surfaces dans le gate global.
- gate global validé le 5 août 2026 : 677 tests .NET, 24 tests Python généraux, 11 tests Python
  du pipeline d’assets, graphe frais, Toolset/runtime réels, CLI qualité, worker Blender 4.5 réel et
  marqueur final `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

Le lot ne prétend pas remplacer une campagne de traduction humaine ni une revue d’équilibrage
artistique. La simulation v1 part des créatures déjà placées et des statistiques génériques ; les
rencontres procédurales futures devront injecter leurs générateurs et profils de menace. L’aperçu
SVG est une preuve déterministe de texte expansé, pas une comparaison pixel de chaque écran ; les
tests visuels multi-résolutions appartiennent à G11-S.
