---
name: openrpg-publish-main
description: "Portée : checkout historique C#/Godot uniquement. Publier intentionnellement un périmètre validé d'OpenRPG Forge sur la branche main et vérifier le résultat distant. Utiliser uniquement lorsque l'utilisateur demande explicitement de commit, pousser, publier ou mettre à jour Git sur main. Ne jamais invoquer automatiquement à la suite d'une implémentation ou d'une validation. Pour Rust, consulter ses instructions propres sans transposer les chemins ou validations historiques."
---

# Publier OpenRPG sur main

Traiter la publication comme une action distante distincte de l'implémentation et de la validation.

## Vérifier l’autorisation et le périmètre

1. Identifier l'action explicitement autorisée : commit local, push ou publication. Une instruction
   encore active et non ambiguë conserve sa portée entre les tours ; ne pas redemander l'autorisation
   de la même action. Un commit local n'autorise aucun push ni publication.
2. Vérifier avec Git la racine `E:\OpenRPG_Forge\OpenRPG_Forge`, la branche, le remote et le suivi de `main`.
3. Examiner `git status --short`, les diffs et les fichiers non suivis. Préserver les modifications utilisateur sans rapport avec le lot.
4. Si les changements autorisés ne peuvent pas être séparés sûrement du reste du worktree, arrêter et demander une décision.
5. Ne jamais supprimer, restaurer, déplacer ou ignorer un fichier pour rendre le statut artificiellement propre.

## Valider avant publication

1. Pour un push ou une publication, utiliser `$openrpg-delivery-gate` sur le périmètre à publier.
   Pour un commit local seul, vérifier les critères du lot et la validité de ses preuves existantes ;
   rejouer les contrôles affectés si nécessaire. Le commit seul ne déclenche pas automatiquement le
   palier `Publication` ou `Full` ; un autre motif de jalon conserve ses exigences propres.
2. Vérifier les artefacts générés et `git diff --check`.
3. Vérifier que les documents de statut décrivent l'état réellement livré.
4. Ne pas publier après un gate incomplet, sauf instruction explicite et éclairée de publier malgré l'échec ; documenter alors précisément l'échec.

## Commit et push

1. Ajouter uniquement les fichiers du périmètre approuvé, avec des chemins explicites lorsque le worktree est partagé.
2. Examiner le diff indexé et créer un commit intentionnel.
3. Réexécuter `git status --short` après le commit pour détecter les nouveaux fichiers oubliés.
4. Vérifier que le commit contient le lot complet et aucun fichier étranger.
5. Seulement si le push est explicitement autorisé, pousser `main` vers son remote configuré sans
   réécriture d'historique. Pour une demande de commit seul, terminer après vérification du commit local.
6. Si un push a été effectué, vérifier le hash local et la référence distante.

Rapporter le hash, le message, les validations, la branche distante et tout changement local non publié. Ne jamais déclarer une publication distante réussie sur la seule base d'un commit local.
