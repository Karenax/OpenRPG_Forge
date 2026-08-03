# G8 SCRIPT-01 — Édition, wizard, tâches, combat et effets

`ForgeScriptAuthoringService` fournit complétion, signature, documentation, snippets, symboles,
formatage et diagnostics. Le wizard produit un AST typé avec source descriptive et ForgeScript
relisible. La file de tâches est déterministe par priorité/date/ID, contrôlée par autorité réseau,
annulable et complétable avec révision et diff.

`ScriptGameplayService` exécute attaques seedées par transaction serveur avec DamageSpec,
AttackContext, critique, attribution et tags d'effet. Les effets décrivent durée, périodicité, source,
six modes de stacking, dispel et sérialisation.

Preuves : `ForgeScriptAuthoringServiceTests`, `ScriptGameplayServiceTests`, suites ciblées et solution
globale.

La prochaine tranche unique est **G8 SCRIPT-02**.
