# G13-A — Raccourcis configurables

## Périmètre

Cette tranche ferme `ORF-SHELL-022` sans ajouter de format moteur ni modifier le snapshot de
recherche. Le keymap est un document local au workspace : il améliore le Toolset mais n’entre ni
dans le projet canonique ni dans le module `.orpmod`.

## Contrat livré

- `EditorKeymapProfile` porte un UUID stable, un schéma v1, une provenance, un preset et quatorze
  bindings typés sans type Godot ;
- les profils Standard, Aurora et Blender sont complets et sans conflit ; toute modification
  ponctuelle produit un profil Custom ;
- le catalogue permet de rechercher par nom, catégorie, identifiant ou geste ;
- `EditorKeyGesture` normalise les gestes auteur et refuse les touches inconnues ;
- `EditorKeymapProfileValidator` détecte schéma, provenance, commande absente/inconnue/dupliquée et
  raccourci partagé avec des diagnostics stables sous `.forge/editor-keymap` ;
- `SetEditorKeymapHandler` produit transaction, révision, dry-run, diff, inverse et audit ; le même
  handler est appelé par l’interface ;
- `EditorKeymapProfileStore` sauvegarde atomiquement `.forge/editor-keymap.json`, recharge exactement
  le profil et revient au profil Standard avec diagnostic explicite si un document invalide existe ;
- l’écran Aurora « Raccourcis & commandes » expose presets, recherche, attribution, conflits,
  prévisualisation, application, undo/redo, identité et états vide/erreur ;
- le routeur Toolset applique réellement sauvegarde, undo/redo, clipboard, duplication, suppression,
  cadrage 3D, playtest, recherche et navigation entre espaces, sans intercepter la saisie texte ;
- le runtime ignore ce document optionnel puisqu’il reste dans l’espace local `.forge`.

## Preuves

- `EditorKeymapProfileTests` : 11 cas avec presets, raccourci de coupe, absence de conflit,
  diagnostics et normalisation ;
- `EditorKeymapCommandTests` : 3 cas couvrant preview/apply, conflit, révision, recherche et undo/redo ;
- `EditorKeymapProfileStoreTests` : 4 cas couvrant absence, round-trip déterministe, migration du
  profil à treize commandes et repli explicite ;
- compilation Toolset : zéro avertissement, zéro erreur ;
- smoke éditeur :
  `OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_PASS profiles=standard-aurora-blender commands=searchable
  bindings=14 conflicts=blocked input=executed ui=visible-empty-error uuid=stable schema=v1
  provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored
  persistence=roundtrip runtime=optional` ;
- le même smoke atteint ensuite `OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS`.

## Limites explicites

- les quatorze commandes couvrent les actions Forge transversales déjà stabilisées ; les écrans
  spécialisés pourront enregistrer leurs commandes dans le catalogue lors de futurs lots ;
- les raccourcis sont locaux à un workspace : leur synchronisation entre machines reste volontairement
  hors du module distribuable ;
- les gestes souris et manette restent gérés par leurs profils gameplay et caméra existants.

## Fermeture

`ORF-SHELL-022` passe à `Tested`. La prochaine tranche unique est **G13-B — identité et
représentation des créatures** (`ORF-CRE-006`, `007`, `009`, `010`, `029`).
