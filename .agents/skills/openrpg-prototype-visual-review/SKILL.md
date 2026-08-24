---
name: openrpg-prototype-visual-review
description: Examiner visuellement un prototype, le Toolset, le runtime ou une fonctionnalité UI d'OpenRPG Forge. Utiliser lorsqu'une demande exige un prototype fonctionnel, une validation visuelle, une capture d'écran, une revue UX, un contrôle de fenêtre Godot, de terrain, de caméra, de HUD, de progression ou d'interaction. Ne pas déclencher pour des changements purement backend sans résultat visible.
---

# Examiner visuellement le prototype OpenRPG

Compléter les tests automatisés par une observation réelle du résultat utilisateur.

## Préparer la revue

1. Lire les critères d'acceptation visuels et le document du sous-système.
2. Identifier le bon scénario : Toolset, runtime générique, module ou démonstration 3D.
3. Utiliser les scripts existants adaptés, notamment `scripts/Open-Toolset.ps1`, `scripts/Run-Prototype.ps1` et `scripts/Capture-ForgeWindow.ps1`.
4. Réutiliser un build déjà validé quand il correspond au changement ; ne pas présenter un frontend ou une scène partielle comme l'application complète.

## Observer le comportement réel

1. Ouvrir l'application avec les outils desktop disponibles lorsque l'interaction est nécessaire.
2. Reproduire le parcours utilisateur prévu, y compris les états vide, chargement, succès et erreur pertinents.
3. Capturer des images lisibles aux étapes probantes.
4. Vérifier au minimum lisibilité, hiérarchie, libellés, contrôles, caméra, sélection, feedback, accessibilité évidente et absence de chevauchement ou de clipping.
5. Pour un monde 3D, vérifier aussi échelle, contact au sol, collisions visibles, densité, éclairage, navigation perçue et stabilité de la caméra.

## Séparer les niveaux de preuve

- Un marqueur headless prouve un comportement automatisé, pas la qualité visuelle.
- Un PNG généré prouve une capture, pas une revue humaine complète.
- Une inspection de l'image prouve uniquement ce qui est visible dans cette vue.
- Une interaction réelle prouve le parcours exécuté, pas tous les parcours possibles.

Ne jamais revendiquer une revue manuelle, un rendu réel ou une interaction non effectuée.

## Corriger et revérifier

Pour une demande de revue seulement, diagnostiquer sans modifier. Pour une demande de construction ou de correction, appliquer les changements dans le périmètre autorisé, relancer les tests ciblés, puis refaire exactement le parcours visuel affecté.

Rendre compte avec le scénario, l'environnement, les captures, les observations, les défauts restants et la frontière entre preuve automatisée et inspection visuelle.
