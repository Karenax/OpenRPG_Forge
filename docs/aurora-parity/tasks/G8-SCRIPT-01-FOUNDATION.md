# G8 SCRIPT-01 — Édition, wizard, tâches, combat et effets

`ForgeScriptAuthoringService` fournit complétion, signature, documentation, snippets, symboles,
formatage et diagnostics. Le wizard produit un AST typé avec source descriptive et ForgeScript
relisible. La file de tâches est déterministe par priorité/date/ID, contrôlée par autorité réseau,
annulable et complétable avec révision et diff.

`ScriptGameplayService` exécute attaques seedées par transaction serveur avec DamageSpec,
AttackContext, critique, attribution et tags d'effet. Les effets décrivent durée, périodicité, source,
six modes de stacking, dispel et sérialisation.

R097 raccorde désormais ces contrats au document source canonique, à l'éditeur visible, au runtime,
à la sandbox native, à l'API documentée et à MCP. La preuve de clôture détaillée est
`P1-R097-SCRIPT-AUTHORING-GAMEPLAY-ACCEPTANCE.md`.

La prochaine tranche unique est `R098 / FC-SCRIPT-P1-02`. Aucun P2 n'est ouvert.
