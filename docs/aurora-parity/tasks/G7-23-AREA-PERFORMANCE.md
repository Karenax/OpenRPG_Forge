# G7-23 — Budget d’assets, occlusion et LOD

Chaque zone dispose maintenant d’un rapport déterministe affiché dans l’espace Zones : nombre
d’entités, assets uniques, unités de rendu estimées, secteurs spatiaux occupés et répartition
proche/moyenne/lointaine. Les budgets par défaut sont bornés et les dépassements produisent des
diagnostics stables exploitables avant le build.

L’analyse reste moteur-indépendante et se reconstruit depuis les instances et blueprints
canoniques. Les secteurs fournissent la base de partition d’occlusion ; les trois bandes de distance
fournissent la base de sélection LOD. Cette tranche ne prétend pas encore générer des HLOD, des
occluders GPU ni profiler le coût réel d’un mesh importé.

Preuves : `AreaPerformanceAnalyzerTests`, build Toolset sans avertissement et gate global.

La prochaine tranche unique est **G7-24 — géométrie et édition de zone**.
