# G11-S — Réseau, visuel, benchmarks, budgets et profilage

Date de fermeture : 5 août 2026.

## Résultat observable

Le bouton **Qualité** de Creator présente désormais onze cartes dans une vue défilante. Aux six
preuves fonctionnelles de G11-R s’ajoutent le réseau hostile, les régressions visuelles, les
benchmarks 3D, les budgets par plateforme et les traces intégrées. Les paramètres correspondants
sont éditables dans le workspace **Données**, persistants et annulables avec le reste du profil
runtime.

Le CLI `forge performance run`, le runtime et l’outil MCP `RunProjectPerformanceLab` exécutent le
même agrégat. Sur **Crypte des Cendres**, il couvre neuf combinaisons latence/perte, huit fixtures
visuelles, neuf mesures intérieur/extérieur/stress, neuf évaluations de budgets et cinq spans sur les
surfaces éditeur, runtime et serveur.

## Contrats livrés

- `AdverseNetworkQualityService` pilote trois clients sous latence, perte et duplication
  déterministes, vérifie la convergence, la reconnexion, le refus d’un contenu incompatible et des
  intentions non autorisées ;
- `PerformanceQualityService` reconstruit réellement la navigation puis mesure ou estime de façon
  stable charge, frame, mémoire, textures, draws, lumières, script, réseau et taille du package ;
- `QualityAssuranceProfile` persiste résolutions, caméras, niveaux qualité, tolérance, scénarios,
  budgets et capture du profiler ;
- `WorkspacePerformanceLabService` agrège les preuves et écrit atomiquement rapport, manifeste
  visuel et trace compatible avec une timeline sous `.forge/quality` ;
- la suite runtime `--g11s-visual-suite` produit huit références, huit candidats, huit revues JSON et
  un rapport global à partir du rendu réel OpenRPG Forge 4.7.1 ;
- Creator, CLI, runtime et MCP partagent le même contrat ; les surfaces de lecture ne changent ni
  la révision du projet ni ses sources.

## Preuves automatiques

- `QualityAssuranceProfileTests`, `AdverseNetworkQualityServiceTests`,
  `ProjectQualityServicesTests`, `WorkspacePerformanceLabServiceTests` et
  `ForgeProjectAgentServiceTests` couvrent validation, déterminisme, sécurité des chemins et lecture
  seule ;
- markers `OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11S_PERFORMANCE_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11S_VISUAL_PASS` et `OPENRPG_FORGE_PERFORMANCE_LAB_PASS` ;
- build Release sans avertissement et 684 tests .NET validés avant le gate intégré ;
- suite visuelle réelle validée localement : 8 cas, 24 artefacts de cas plus le rapport,
  1280×720 et 1600×900, caméras `overview` et `three-quarter`, qualités Medium et High ;
- `scripts/Verify.ps1` impose les quatre surfaces et l’ensemble complet des artefacts.
- gate global validé le 5 août 2026 : 684 tests .NET, 24 tests Python généraux, 11 tests Python
  du pipeline d’assets, graphe frais, Toolset/runtime réels, CLI/MCP, Blender 4.5 réel et marqueur
  final `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

Les coûts frame/mémoire hors navigation sont des modèles déterministes de budget, pas une télémétrie
GPU constructeur. Les captures enregistrent toutefois le rendu réel et la reconstruction de
navigation réelle. Une future ferme matérielle pourra alimenter le même rapport avec des compteurs
GPU/OS sans changer le profil persistant. Les comparaisons pixel protègent la stabilité technique ;
la qualité artistique de la carte finale reste un travail de contenu et de revue humaine.
