# R2-032/033 — Raccord persistant prévu

14 septembre 2026. **Plan partiellement implémenté, qualification globale ouverte.**
Le raccord initial corrections v5 / archives 13–15 et les commandes de bâtiment
sont implémentés. Deux tests d'intégration PASS couvrent retouche de porte,
redimensionnement, régénération, relecture des trois formats et Undo/Redo ;
Clippy projet tous targets PASS. UI, projection, sauvegarde disque et qualification
historique complète restent dues. Voir R2_BUILDING_LAYOUT.md pour preuves et limites.
Complément technique à ADR-0014 dans la tête de zone existante ; pas de deuxième
format projet, stockage ECS ou fichier canonique propre au générateur.

Avancement : payload `RoomDimensions` maintenant déplacé dans forge_project,
validé/sérialisable et consommé par forge_world ; décalage local absolu de porte
ajouté. Ce transfert est implémenté, pas le raccord persistant décrit ci-dessous.

## État vérifié avant modification

`ZoneCorrections` possède placements, chemins et rivières optionnels ; les têtes
v1–v4 et archives intégrales v1/2/4/8/10, compactes v3/5/9/11, delta v6/7/12
sont déjà dispatchées et vérifiées contre leurs reçus. `building_layout` est
un calcul isolé dans forge_world, pas une recette enregistrable.

## Contrat à implémenter

- Ajouter `buildings: Option<Vec<AuthoredBuilding>>` dans les mêmes corrections.
  Omission = hashes anciens intacts ; présence, même vide = tête v5. Maximum
  32 bâtiments, IDs uniques triés. Ne pas convertir à l'ouverture.
- Déplacer les paramètres métriques validés dans forge_project, sans dépendance
  vers forge_world. Le calcul géométrique reste dans forge_world et lit ces
  paramètres. Aucun cycle entre documents et générateur.
- Recette : ID, nom, version du générateur, pose moteur-neutre, dimensions,
  références de style vérifiables et verrou. Les limites de taille et les bornes
  de la zone sont contrôlées avant proposition. Aucun asset choisi implicitement.
- Retouches : cibles sémantiques persistantes par bâtiment/partie, transformations
  absolues explicitement définies dans le repère local du bâtiment, exclusions
  explicites et verrous. La porte garde sa propre identité ; modification de la
  largeur du bâtiment ne réattribue pas sa retouche à une autre ouverture.
- `Insert/Replace/Remove/SetLocked` et retouches produisent ZoneRequest, avec les
  mêmes Preview/Apply/Undo/Redo, retry, hash/révision et audit. Les verrous sont
  revalidés dans la session pour les commandes Edit brutes aussi.
- Regenerate conserve ces recettes et retouches, puis revalide bornes et conflits.
  Suppression d'une cible corrigée, changement incompatible du générateur ou
  intrusion d'une retouche dans le passage : conflit explicite, pas de suppression
  ou de repositionnement silencieux pour rendre le test vert.

## Compatibilité du journal

Réserver v13 intégrale, v14 compacte, v15 delta au domaine bâtiment. Étendre les
champs Keep/Set et les détecteurs de domaines en examinant tous les reçus, même
après Undo. Relabel vers v1–v12 refusé. Anciennes archives toujours relues à
l'identique. Limites 4 Mio fichier, 64 Mio dépliés, 1 024 commandes conservées.
Tests de round-trip dans les trois encodages et de refus sans mutation requis.

## Raccord moteur et Studio

Le hash de la tête et la recette identifient les caches de meshes/collisions.
Les deux adapters consomment les mêmes parties géométriques. Tant que la
projection des bâtiments manque, un playtest qui les contient doit être refusé
explicitement plutôt que les omettre. Aucun faux acquittement de disponibilité.

Parcours à livrer : placer une pièce, éditer dimensions/porte, prévisualiser,
appliquer, annuler/rétablir, régénérer en conservant la porte retouchée, sauvegarder,
fermer/rouvrir et franchir le passage en playtest. Les assemblages temporaires de
cubes, tests CPU et captures seules ne suffisent pas à cocher R2-032/033.
