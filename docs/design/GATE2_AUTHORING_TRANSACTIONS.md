# Gate 2 — première tranche d'édition

12 septembre 2026. Démarrage demandé explicitement par le propriétaire :
« ok commence le travail de gate 2 ». Cette instruction autorise ce lot ; elle
ne déclare pas Gate 1 validée et ne modifie pas les dérogations ADR-0013.
La tâche de clôture Gate 1 utilise sa copie de preuve isolée ; ne pas la modifier.

## Parcours visé

Créer une zone → retoucher → annuler/rétablir → sauvegarder → rouvrir → Jouer.
Commencer par ce parcours, puis étendre les brosses, routes, végétation et placements.
Le Studio, les commandes et les futures opérations IA doivent partager les mêmes règles.

## Premier incrément implémenté

`forge_project::terrain_edit` travaille sur le `TerrainDocument` existant : pas
de nouvelle source canonique, pas de modèle Bevy persistant ni de dépendance ajoutée.

- Identité `CommandId` dans forge_core, distincte des identités d'entité.
- Commandes typées pour nom et élévation de base en mètres ; Undo/Redo.
- Révision ET hash attendus ; rejet d'une commande périmée sans mutation.
- Aperçu validé sans effet, reçu avec action, inverse, nouvelle révision et hash.
- Retry identique : reçu historique rendu sans réappliquer ; réutilisation d'ID
  avec une autre demande refusée. Le reçu historique ne représente pas l'état courant.
- Révisions monotones, y compris lors d'Undo ; édition après Undo invalide le Redo.
- Audit en mémoire conservé ; historique borné à 1024 commandes acceptées, sans
  snapshots complets de terrain par entrée. La limite refuse sans effacer l'historique.

## Limites explicites et prochaine intégration

Le service est relié au disque par l'adapter natif `FileTerrainSession`, mais pas
encore aux boutons Studio. Détails et limites : `R2_FILE_SESSION.md` dans les
preuves Gate 2. La reprise explicite du staging existe désormais pour une session
déjà enregistrée : deux versions archivées, validation avant promotion ; voir
`R2_INTERRUPTED_SAVE_RECOVERY.md`. Les cas d'ouverture sans enregistré et le
dialogue Studio restent à compléter.
L'enveloppe `forge.terrain-edit-session` v1 conserve maintenant le document initial
et les reçus, avec contrôle du hash final. La réouverture rejoue les commandes et
vérifie leurs inverses, révisions et hashes ; Undo/Redo et déduplication sont restaurés.
Limites : 4 Mio et 1024 commandes ; aucune troncature silencieuse. Le document courant
reste dérivé du document initial et du journal, sans copie canonique concurrente.
Ces contrôles détectent les incohérences, pas une falsification cohérente et resignée.
Voir `docs/evidence/gate2/R2_SESSION_ARCHIVE.md` depuis la racine.

Avant le parcours produit :

1. Session d'application et enveloppe persistante versionnée : acteur, audit durable,
   déduplication et révision de base de stockage ; aucun chemin arbitraire fourni au frontend.
2. Sauvegarde atomique/récupération et conflit externe, via adapter natif borné au projet.
3. Panneau Studio créer/ouvrir/retoucher, Undo/Redo et état modifié ; commandes identiques.
4. Projection du document courant et playtest isolé, puis lancement/capture/inspection.

Les overrides, verrous et conflits de régénération R2-044 exigent leur ADR propre
avant implémentation : ce service ne prétend pas déjà les résoudre. Les hauteurs
peintes existantes restent inchangées par l'édition de l'élévation de base.
La proposition est maintenant décrite dans
`docs/ADR/ADR-0014-authoring-regeneration-overrides.md` (depuis la racine), sans
acceptation ni implémentation déduite du document.
Le contrat général de commandes du produit n'est donc pas encore entièrement rempli.

## Coordination et portée

Workspace Rust existant, aucun commit/push, aucune retouche de Visual Valley.
Ne pas modifier les sources ou rapports de la copie de qualification Gate 1.
La clôture Gate 1 a libéré les fichiers canoniques. Son snapshot qualifié exclut
les changements Gate 2 et demeure intact.
