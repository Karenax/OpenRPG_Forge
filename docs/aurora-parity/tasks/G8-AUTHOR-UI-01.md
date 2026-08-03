# G8 AUTHOR-UI-01 — Atelier RPG Kit

`RpgKitAuthoringWindow` ajoute au workspace **Quêtes** un atelier sombre/braise présentant les neuf
domaines RPG Kit, les métriques du projet courant et un formulaire de blueprint canonique. Les
auteurs choisissent le type, le slug et le nom sans toucher au JSON, puis utilisent
**Prévisualiser**, **Appliquer**, **Annuler** ou **Rétablir**. Les diagnostics structurés et les diffs
aller/inverse sont visibles. Des raccourcis ouvrent Dialogues et Scripts & build.

`PreviewOrApplyRpgBlueprint` construit toujours un `UpsertBlueprintCommand`. Le smoke G8 vérifie
qu'un dry-run ne change ni révision ni contenu, que l'application passe par l'historique, et que
l'undo restaure le projet.

La prochaine tranche unique est la **revue visuelle G8 nominale/maximisée**, puis l'audit de fermeture.
