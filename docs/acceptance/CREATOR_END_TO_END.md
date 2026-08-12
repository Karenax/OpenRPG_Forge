# Revue Creator de bout en bout

Durée cible : 25 minutes. Utiliser un dossier temporaire vide et le profil **Débutant**. Ne pas
modifier Crypte des Cendres ni Forgotten Forest.

1. Ouvrir le Creator avec `scripts/Open-Toolset.ps1` et vérifier la marque OpenRPG Forge.
2. Créer un module vierge et une zone intérieure. Ajouter ensuite une zone extérieure de 64 × 64 m.
3. Dans **Assets**, choisir un modèle CC0 avec hash et provenance visibles, prévisualiser l'import,
   puis le placer dans **Monde** depuis **Blueprints**.
4. Créer une créature, une porte, un trigger et une rencontre. Relier une faction à la créature.
5. Créer un dialogue de deux répliques et une quête à trois états ; lier la fin du dialogue à la
   transition de quête. Compiler un script court depuis **Scripts**.
6. Dans **Validation**, ouvrir un diagnostic, prévisualiser son correctif, l'appliquer puis l'annuler
   et le rétablir.
7. Dans **Playtests**, jouer depuis le point de départ, déplacer la caméra, interagir, sauvegarder,
   fermer le runtime puis reprendre la sauvegarde.
8. Exporter un `.orpmod`, l'installer dans un second dossier de workspace et vérifier projet, zones,
   assets, dialogue et quête.
9. Dans **Assistant**, demander une modification bornée : lire le plan, lancer le dry-run, comparer le
   diff, approuver, tester et revert. Vérifier qu'aucun shell ni chemin arbitraire n'est proposé.

## Revue d'interface

Rejouer aux résolutions 1280 × 720 et 1920 × 1080, aux échelles 100 %, 150 % et 200 %. Vérifier le
parcours clavier, le focus visible, les libellés non tronqués et les contrastes. Le profil Débutant
doit montrer en premier **Monde**, **Blueprints**, **Playtests** et **Assistant**. Les neuf espaces
canoniques réapparaissent avec les profils spécialisés ; Données, Réseau et Live Director restent
dans **Outils avancés**.

Consigner date, commit, Windows, GPU, résolution, échelle, résultat et chemins des captures dans le
rapport de qualification de la future release. Toute étape nécessitant une édition JSON est un échec
du parcours, même si le format sous-jacent est valide.
