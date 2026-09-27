# Atelier ivoire — première passe native, 24 septembre 2026

État : chantier en cours, ni livraison finale ni clôture de Gate 2.

## Candidat observé

`output/studio-gold-20260924/candidate/forge-studio-candidate.exe`

SHA-256 : `1D5B477671D33FCE2FA5C70B265759563A22E92A12E746F5A3115ECC3487CAA6`.
Ce candidat précède les améliorations de persistance, récupération hors écran,
compaction de bibliothèque et catalogue secondaire décrites ci-dessous.

## Parcours réellement exécuté

- Computer Use : ouverture du candidat ; accueil ivoire, activités et dock visibles.
- Clic « Deux écrans » : seconde fenêtre locale affichée sur l'autre écran.
- Clic « Terrain » dans le secondaire : panneau Terrain et activité Sculpter
  activés dans le principal, vérifiés dans l'arbre d'accessibilité.
- Clic « Rapatrier » dans le principal : le secondaire disparaît ; inventaire
  des fenêtres limité à une fenêtre Studio ; le sujet Terrain est conservé.
- Agrandissement du principal sur l'écran WQHD ; ouverture de `R2-004 Final B`.
  Terrain, chemin et arbre affichés ; aucune commande de modification appliquée.
- Choix Composer : bibliothèque séparée du panneau d'inspection, mais filtres
  trop hauts masquant les cartes. Défaut conservé comme constat, corrigé ensuite
  dans les sources ; nouvelle capture du correctif encore due.

Lecture CDP exclusivement diagnostique :
`output/studio-gold-20260924/atelier-review/wqhd-project.json` et `.png`.
Malgré le nom du fichier, le sujet actif mesuré après ouverture est **Terrain**.
Surface CSS mesurée : 2560 × 1369 ; navigation visible, aucun débordement
horizontal, panneau entièrement contenu. Le DPR n'a pas été relevé dans cette
passe : ce n'est pas une qualification de toute la matrice DPI.

Archive de test après parcours : SHA-256
`62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`,
identique à l'entrée. Aucun enregistrement de la carte n'a été demandé.

## Suite de code, non encore couverte par ce candidat

- Préférences de disposition JSON v1, bornées et indépendantes des documents ;
  rappel par sujet et repli des panneaux flottants lors d'un redimensionnement.
- Réduction des reconstructions périodiques de l'index de scène par mémos ;
  suppression des notifications réactives secondaires inchangées.
- Récupération native d'une fenêtre dont la barre de titre n'intersecte plus
  les moniteurs ; nettoyage si l'affichage initial échoue.
- Filtres de bibliothèque sur une ligne, cartes en ruban horizontal.
- Catalogue secondaire et sélection d'asset partagés, sans deuxième renderer
  ni contournement des commandes de placement principales.

Après les trois premiers correctifs : tests Studio 99 PASS, 1 ignoré
(comparaison CPU explicitement optionnelle), Clippy natif et WASM PASS.
Après ajout du catalogue secondaire : 100 tests PASS, 1 ignoré, Clippy WASM
strict PASS. Nouveau build release en cours ; aucune réussite runtime de cet
ajout n'est déduite des tests de code. La session graphique de cette première
passe a été fermée sans enregistrement, processus candidat confirmé absent.

## Restant avant livraison

Inspecteurs détachés éditables avec brouillons protégés ; cartes visuelles plus
représentatives ; tests réels de sélection d'objet/asset inter-fenêtres ;
redémarrage/restauration ; disparition d'écran ; matrice Full HD/WQHD/4K et DPI ;
non-régression des parcours d'édition ; validation globale adaptée au contrat.
Les pictogrammes actuels ne sont pas des vignettes réelles des modèles.

Un seul cache et un seul candidat sont conservés. E: relevé à 152,4 Gio libres.
Aucune suppression de fichiers, publication ou délégation dans ce chantier.
