# G8 SCRIPT-05 — Interop éditeur et MCP

`R101 / ORF-SCR-041` ferme l’interop ForgeScript sur une façade commune à l’éditeur, MCP et au
runtime. Elle expose l’API versionnée, la création/mise à jour des sources `.as`, compilation,
diagnostics, tests sandboxés et bindings d’événement. Les mutations exigent transaction, révision de
base, provenance et validation ; un conflit, un doublon, un dépassement de quota ou une source
invalide ne modifie ni document ni révision.

Le profil canonique conserve UUID, schéma, provenance, API, binding et quotas. Preview/apply/reject,
Undo/Redo et snapshot JSON exact partagent la même frontière ; l’adaptateur runtime applique les
mêmes quotas, ignore une extension optionnelle absente, diagnostique une extension requise et refuse
les handles obsolètes. MCP ne reçoit aucun accès hors sandbox.

Les preuves sont centralisées dans
`docs/aurora-parity/tasks/P1-R101-SCRIPT-INTEROP-ACCEPTANCE.md` : 10 tests ciblés, marqueurs Toolset
et runtime, capture inspectée `docs/aurora-parity/evidence/r101-script-interop-ui.png` à 1680×1000 et
portail intégré. La famille SCRIPT P1 sélectionnée pour G8 est complète.

La prochaine tranche canonique est `R102 / FC-MULTI-P1-01`, uniquement après portail R101 vert.
