# Studio Gold — implémentation du 24 septembre 2026

Travail local sur Rust, sans sous-agent, sans commit ni publication. La Gate 2 reste ouverte.
Cette passe implémente la coque approuvée ; elle ne qualifie pas encore toute la recette de la proposition.

## Implémenté

- Coque charbon/ivoire/champagne, sept onglets iconés et nommés, commandes persistantes.
- Arbre de scène à gauche, viewport central, inspecteur contextuel à droite.
- Bibliothèque d'objets en tiroir inférieur, glisser-déposer conservé, provenance dans une fiche repliable.
- Inspecteur simple avec Déplacer/Tourner/Échelle ; coordonnées et variantes en sous-vue avancée.
- Chemins : dessin par clics successifs, segments et points visibles, Entrée termine, Retour arrière retire le dernier point, Échap restaure le brouillon précédent.
- Un seul point détaillé affiché pour chemins et rivières, indépendamment du nombre de points.
- Terrain : Sculpter/Peindre séparés ; Nature : outil contextuel unique ; Eau séparée des chemins.
- Aucune nouvelle dépendance, commande métier ou représentation canonique. Aperçu/Appliquer/Sauvegarder restent distincts.

## Contrôles réalisés

- Formatage et Clippy WASM avec warnings projet en erreurs : PASS.
- Build WASM release et candidat Tauri natif : PASS, dernières corrections comprises. SHA-256 du candidat : `27ACAC157E113716679887F7ADC334D4FBDFE024ABEF807B7B03E9E620C2E363`.
- Tests Studio natifs : 82 PASS, 1 benchmark ignoré, aucun échec.
- Computer Use dans le candidat réel, copie isolée : trois points par clics successifs, retrait du dernier par Retour arrière, Entrée, aperçu, application et sauvegarde du chemin : PASS.
- Glisser-déposer du chêne du catalogue vers le terrain, aperçu, application, sélection dans l'arbre et sauvegarde : PASS.
- Redémarrage du candidat corrigé, réouverture : chemin et chêne retrouvés. Poignée X glissée, aperçu appliqué (X de -55,10 à -50,08), Annuler puis sauvegarde : retour à -55,10 confirmé visuellement.
- Objet sélectionné à 1920×1080 : panneau 340×918 sans défilement, bibliothèque 1358×280 sans débordement ; huit cartes entièrement visibles. Capture `proof/objects-selected.png`, mesures `proof/objects-selected.json`.
- À 1920×1080, Chemins : inspecteur 340×918 sans débordement vertical ; canvas 1358×846 ; sept onglets ; aucune barre horizontale.
- Deux problèmes observés puis corrigés : fin involontaire du dessin après le premier point ; options de gizmo visibles et bibliothèque trop basse.

Preuves locales ignorées : `output/studio-gold-20260924/proof/paths-saved.{png,json}`.
Le fichier `proof/project.png` montre en réalité Terrain : il ne constitue pas une preuve de l'onglet Projet.
Le script `scripts/Inspect-StudioGoldLayout.mjs` lit la géométrie DOM et capture le rendu sans action ni mutation produit.

## Limites et recette restante

- Cartes du catalogue avec icônes génériques, pas encore de vraies miniatures des modèles.
- Pas de fantôme suivant le curseur avant le clic, de segment élastique ni de poignées de chemin glissables.
- Panneaux responsifs à largeurs fixes, pas encore redimensionnables manuellement.
- Le nouveau parcours complet, la main levée, les états invalides/verrouillés, les grandes résolutions et le playtest doivent encore être requalifiés. Les anciennes preuves Gate 2 ne sont pas automatiquement transférées à la coque Gold.
- Les captures de maquette restent illustratives ; seules les captures du candidat prouvent son interface réelle.

## Lancement et stockage

`scripts/Start-StudioGoldReview.ps1` ouvre le candidat déjà construit avec la copie isolée `output/studio-gold-20260924/authoring-root`.
Il ne compile pas, ne recopie pas de projet et refuse un second candidat simultané.
Un seul exécutable candidat est remplacé, cache Cargo partagé `E:/OpenRPG_Forge/.build/cargo-target`, compilation incrémentale désactivée. Aucun historique volumineux supplémentaire.
