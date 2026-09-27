# Proposition de refonte Studio Gold — 24 septembre 2026

Statut : proposition approuvée ; première implémentation Gold et qualification locale en cours.
Le suivi factuel est dans `docs/evidence/gate2/STUDIO_GOLD_IMPLEMENTATION_20260924.md`.
Les paysages des maquettes sont illustratifs et ne constituent aucune preuve du moteur.

## Constats vérifiés

- La passe du 23 septembre a catégorisé les contrôles et ajouté une palette ardoise/dorée, mais conserve les longs formulaires (`docs/evidence/gate2/STUDIO_UX_REVIEW_20260923.md`).
- `authoring_assets/placement_controls/inspector.rs` empile position, duplication, pivot, remplacement, répétition, orientation, translation locale et gizmos. Le parcours simple reste noyé dans les variantes techniques.
- `authoring_paths.rs` place le bouton de dessin après la liste complète des points. Chaque point ajoute plusieurs boutons et champs. La hauteur augmente avec la complexité du chemin.
- `trace_path` modifie les points du brouillon ; les repères utilisent un signal initialisé à false. Examiner puis Appliquer restent nécessaires. Cela explique une absence possible de retour visible, sans prouver à lui seul la cause de chaque échec rencontré par l'utilisateur.
- La référence exacte de l'ancienne interface Forge « gold » n'a pas été identifiée dans les sources inspectées. Une conversation nommée « Retrouver UI Apple Gold » concerne une référence macOS externe : ne pas l'assimiler sans preuve à une maquette Forge validée.

## Architecture proposée

Barre supérieure persistante : projet, zone, Annuler/Rétablir, Enregistrer, Jouer.
Barre d'outils avec icônes ET libellés : Sélection, Terrain, Nature, Chemins, Eau, Objets.
Arbre de scène compact à gauche ; scène au centre ; inspecteur contextuel à droite.
Bibliothèque de vignettes en tiroir bas uniquement pour choisir des objets ; masquée pendant le tracé.
Historique, diagnostics, provenance détaillée et identifiants techniques dans des vues dédiées.
Les propriétés avancées ouvrent une sous-vue identifiée avec Retour, pas un nouvel empilement illimité.

Palette : charbon chaud, texte ivoire, accents champagne/dorés. Texte lisible 14–16 px,
contraste contrôlé, focus visible, panneaux redimensionnables, animations de 120–180 ms
respectant la préférence de réduction du mouvement. La scène conserve sa boucle de rendu.

## Objet : parcours principal

1. Choisir une vignette ou la glisser vers la scène.
2. Afficher immédiatement l'objet fantôme au sol, avec position valide/invalide explicite.
3. Cliquer pour fixer l'aperçu ; ajuster avec Déplacer / Tourner / Échelle.
4. Valider le placement dans le panneau contextuel ; Enregistrer conserve son rôle séparé.

Inspecteur initial : nom, modes de transformation, Poser au sol, Grille, rotation, échelle.
Coordonnées XYZ précises, pivot et options spécialisées dans Réglages avancés.
Sélection directe dans la scène ; Cadrer accessible ; duplication et suppression dans le menu contextuel.
Échap abandonne uniquement le geste ou l'aperçu actif. Undo reste disponible après validation.

## Chemin : parcours principal

1. Chemins → Nouveau chemin, mode Par points par défaut.
2. Clics sur le terrain : points et segments visibles immédiatement, segment suivant sous le curseur.
3. Entrée ou Terminer termine le dessin et conserve l'aperçu ; Retour arrière retire le dernier point.
4. Ajuster largeur/bordure et déplacer les poignées dans la scène.
5. Créer le chemin valide la commande préparée ; Enregistrer écrit la zone.

Main levée est une alternative clairement nommée. Repères visibles automatiquement pendant l'édition.
Les coordonnées de tous les points ne sont jamais rendues en longue liste dans le parcours principal.
Si le terrain n'est pas prêt ou le tracé invalide, montrer la raison et l'action de résolution près de l'outil.
Le bouton final reste visible sans défilement. Les termes métier des contrôles doivent correspondre
aux effets réels : ne pas renommer un accotement « bordure douce » sans vérifier le comportement.

## Contrats à conserver

La simplification de l'interface ne supprime pas validation, aperçu, révision de base, audit,
inverse et sauvegarde atomique. L'aperçu est préparé automatiquement lorsque possible,
puis le bouton métier valide la commande correspondante. Un aperçu périmé reste refusé.
Les deux maquettes proposent des interactions supplémentaires : elles ne prouvent pas leur disponibilité.

## Recette de la refonte

- À 1920×1080, poser un objet et créer un chemin sans défilement du panneau principal.
- À 2560×1440 et 3840×2160, conserver une taille de texte confortable et une scène dominante.
- Un chemin de 2 ou 128 points garde le même encombrement de panneau.
- Vérifier par Computer Use : sélection d'asset, placement, déplacement, annulation, validation,
  sauvegarde/réouverture ; puis chemin par points, main levée, modification, annulation et playtest.
- Tester l'état terrain indisponible, hors terrain, chemin verrouillé et aperçu périmé avec messages actionnables.
- Vérifier que navigation et redimensionnement ne perdent pas le brouillon sans avertissement.
- Capturer les panneaux avec un objet réellement sélectionné et un chemin complexe, pas seulement leurs états repliés.
- Ce travail ne clôt pas implicitement la Gate 2 ; les preuves restent rattachées aux critères effectifs.

## Maquettes

Images produites avec l'outil ImageGen intégré ; aucune compilation.

- `output/studio-gold-20260924/placement-proposal.png`
- `output/studio-gold-20260924/paths-proposal.png`

Brief de génération : interface desktop RPG Gold, charbon/ivoire/champagne, scène centrale,
arbre gauche, inspecteur droit court, actions persistantes, français lisible, sans paragraphes techniques.
Vue placement : catalogue de six vignettes, maison sélectionnée et gizmo, pose au sol, rotation/échelle,
Annuler et Valider le placement. Vue chemins : mode Par points/Main levée, largeur et bordure,
poignées dans la scène, consigne de clics/Entrée, Annuler et Créer le chemin.

Limites graphiques : les deux images diffèrent légèrement dans le placement de la barre de sauvegarde
et le pictogramme de marque ; l'implémentation doit utiliser une seule coque et les icônes du produit.
Le nombre de poignées dessiné n'est pas une preuve de cohérence avec le compteur de la maquette.

## Références ergonomiques

- Nielsen Norman Group, Progressive Disclosure : https://www.nngroup.com/articles/progressive-disclosure/
- Nielsen Norman Group, Complex Application Design : https://www.nngroup.com/articles/complex-application-design/
- Blender, Asset Browser : https://docs.blender.org/manual/en/5.0/editors/asset_browser.html

Ces références motivent l'affichage contextuel et la bibliothèque visuelle ; la proposition précise
est un choix de conception Forge à vérifier sur les tâches réelles de l'utilisateur.
