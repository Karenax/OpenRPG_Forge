---
name: openrpg-publish-main
description: Publier intentionnellement un périmètre validé d'OpenRPG Forge sur la branche main et vérifier le résultat distant. Utiliser uniquement lorsque l'utilisateur demande explicitement de commit, pousser, publier ou mettre à jour Git sur main. Ne jamais invoquer automatiquement à la suite d'une implémentation ou d'une validation.
---

# Publier OpenRPG sur main

Traiter la publication comme une action distante distincte de l'implémentation et de la validation.

## Confirmer l'autorisation et le périmètre

1. Exiger une demande explicite de publication dans le tour courant ou dans une instruction encore active et non ambiguë.
2. Confirmer la racine Git `E:\OpenRPG_Forge\OpenRPG_Forge`, la branche, le remote et le suivi de `main`.
3. Examiner `git status --short`, les diffs et les fichiers non suivis. Préserver les modifications utilisateur sans rapport avec le lot.
4. Si les changements autorisés ne peuvent pas être séparés sûrement du reste du worktree, arrêter et demander une décision.
5. Ne jamais supprimer, restaurer, déplacer ou ignorer un fichier pour rendre le statut artificiellement propre.

## Valider avant publication

1. Utiliser `$openrpg-delivery-gate` sur le périmètre à publier.
2. Vérifier les artefacts générés et `git diff --check`.
3. Confirmer que les documents de statut décrivent l'état réellement livré.
4. Ne pas publier après un gate incomplet, sauf instruction explicite et éclairée de publier malgré l'échec ; documenter alors précisément l'échec.

## Commit et push

1. Ajouter uniquement les fichiers du périmètre approuvé, avec des chemins explicites lorsque le worktree est partagé.
2. Examiner le diff indexé et créer un commit intentionnel.
3. Réexécuter `git status --short` après le commit pour détecter les nouveaux fichiers oubliés.
4. Vérifier que le commit contient le lot complet et aucun fichier étranger.
5. Pousser `main` vers son remote configuré sans réécriture d'historique.
6. Vérifier le hash local et la référence distante après le push.

Rapporter le hash, le message, les validations, la branche distante et tout changement local non publié. Ne jamais déclarer une publication distante réussie sur la seule base d'un commit local.
