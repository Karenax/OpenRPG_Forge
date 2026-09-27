# Brosses de relief — premier lot fonctionnel

## Réglages et aperçu périmé — 15 septembre 2026

Défaut relevé par lecture : après armement, le rayon du curseur restait capturé
mais la préparation du relief relisait les autres champs au retour du geste.
Changer l'outil pouvait donc laisser un geste armé avec l'ancienne opération ;
changer un champ après aperçu laissait aussi l'ancienne proposition applicable.

Les événements input/change du panneau annulent maintenant le picker et la
proposition, via `authoring_brush_pointer::settings_changed`. Même raccord sur
la peinture de matériaux, et helper réutilisé par les réglages de densité.
La mise à jour programmatique de X/Z issue du clic ne déclenche pas ces événements
DOM. La cancellation existante reste persistante face à une réponse native tardive.
Pas d'application automatique ni de mutation de document ajoutée.

Clippy WASM strict PASS (7,15 s) ; première tentative refusée à 101 lignes,
préparation point/trait simplifiée sans changement de contrat ni lint désactivé.
Formatage/diff contrôlés. Pas de nouveau test du domaine inchangé ni de rebuild
graphique pour ce seul raccord. À vérifier dans le prochain candidat groupé :
armement puis réglage puis clic sans effet, aperçu puis réglage désactivant Apply,
réarmement utilisant les nouvelles valeurs. 8C6A960A ne contient pas ce correctif.
R2-B et Gate 2 restent ouverts ; aucune qualification graphique déduite.

## Implémenté

- Préparation déterministe de lever/abaisser, aplatir à une altitude absolue et
  lisser vers la moyenne des quatre voisins du terrain source (sans dépendance
  à l'ordre de parcours).
- Coordonnées en mètres, rayon 2–128 m, intensité (0, 1], atténuation radiale.
- Conservation des échantillons hors rayon ; préparation sans mutation du document.
- Commande `SetHeightDeltas` avec inverse exact dans le bus existant, preview,
  Apply, Undo/Redo et restauration d'archive. Pas de document canonique Bevy.
- Contrôles Studio numériques raccordés à ce parcours. Projection par le chemin
  existant de mise à jour locale des chunks, pas un remplacement de toute la carte.

## Vérifié le 12 septembre 2026

`cargo test -p forge_world --test terrain_brush --locked --offline` : 3 PASS.
Couverture : hausse/baisse, altitude cible, lissage, hors rayon inchangé, rejet
de paramètres invalides, déterminisme, Undo/Redo, archive restaurée et cache
partiel identique à la génération complète (moins de 64 chunks reconstruits).
Clippy world/project all-targets et Studio WASM strict : PASS. Une première
erreur Clippy de comparaison flottante dans les tests a été corrigée par une
comparaison exacte des bits ; les tests et Clippy ont ensuite été rejoués.

## Limites et suite

Contrôles compilés, non encore rejoués graphiquement. Le preview est textuel et
donne la taille du relief sparse, pas encore une prévisualisation 3D du geste.
Placement du centre à la souris et cercle de rayon/intensité restent à raccorder.
Bruit, pente et matériaux ne font pas partie de ce lot. La conservation des
overrides après régénération reste distincte et non qualifiée.
La commande stocke une image de la couche sparse pour son inverse : la limite
d'archive de 4 MiB reste applicable. Optimisation des patches non revendiquée.
Aucune validation complète de R2-B ou de Gate 2 ; aucun essai graphique lancé.

## Raccord du geste souris et du cercle

Implémentation ajoutée : armement d'une brosse dans les contrôles, coordonnées
normalisées du canvas, rayon caméra puis sélection limitée aux heightfields
terrain Rapier. Le cercle de 64 segments échantillonne le relief courant ; le plan
de hauteur est conservé jusqu'au changement de hash, sans régénération par frame.
Un clic valide renseigne X/Z et appelle la même préparation de commande que le
mode numérique. Une absence d'intersection ne prépare aucune commande.
Sortie du canvas, abandon et entrée en playtest annulent la brosse armée.

Le raccord est compilé pour Studio WASM ; contrôle Clippy initial arrêté sur un
point-virgule manquant puis corrigé ; replay strict PASS en 3,38 s. Aucun replay graphique de ce complément
encore effectué : le cercle, le picking et le geste restent non qualifiés.
Le clic produit toujours un aperçu textuel avant application explicite ; pas
encore de relief fantôme ni de trait continu au glisser.

## Bruit et pente

Deux opérations ajoutées : bruit déterministe (amplitude, longueur d'onde, seed)
réutilisant l'algorithme de terrain versionné ; plan incliné défini par l'altitude
centrale et les gradients X/Z, avec le même mélange radial que les autres outils.
Studio : sélecteur unique des cinq opérations, options contextuelles de bruit
et pente, préparation numérique ou choix dans la vue.

Suite ciblée brosses : **4 tests PASS**, dont identité à seed égale, variation à
seed différente, amplitude bornée, annulation du bruit, direction et valeur de
pente après atténuation, rejet des paramètres invalides. Clippy world/all-targets
PASS. Les résultats de la brosse sont persistés comme échantillons retouchés ;
la recette procédurale du geste n'est pas une couche régénérable indépendante.
Pas de qualification graphique des brosses ni de la peinture de matériaux.
Clippy Studio WASM strict également PASS sur les cinq outils (26,66 s).

## Aperçu géométrique avant application

Préparation d'un overlay filaire dérivé du dry-run accepté : écarts verticaux
orange et relief proposé cyan, au maximum 2 048 échantillons affichés. Le nombre
affiché et le nombre réellement modifié sont distingués dans le texte d'aperçu.
Le terrain et ses collisions ne sont pas remplacés avant Apply. L'overlay est
lié au hash source et supprimé lors d'une nouvelle commande, d'un changement du
terrain projeté ou du bouton Abandonner l'aperçu.

Vérification : **5 tests brosses PASS**, dont géométrie proposée au centre,
immuabilité de la source, borne d'affichage et aperçu d'abaissement. Clippy
world/all-targets et Studio WASM PASS après correction de comparaisons exactes
dans le test et extraction du formatage d'aperçu. Pas de désactivation de lint.
Rendu encore non inspecté : lisibilité de l'abaissement sous la surface opaque
et cercle/picking à vérifier ensemble dans le prochain replay graphique.

## Tracé continu — lot fonctionnel du 12 septembre 2026

Implémenté : `TerrainBrushStroke` prépare une seule `SetHeightDeltas` pour tout
le chemin. Empreintes espacées de la moitié du rayon, indépendantes du temps
d'immobilité. Les empreintes successives lisent le résultat de la précédente,
sur une copie non canonique ; seule Apply publie le résultat via le bus existant.
Limites : 1 024 points de chemin, 128 empreintes ; dépassement refusé entièrement,
jamais tronqué silencieusement. Le dernier reliquat inférieur à un espacement
n'ajoute pas d'empreinte, afin de ne pas surcharger la fin du geste.

Studio : « Tracer dans la vue », bouton gauche maintenu, collecte des intersections
terrain, aperçu filaire au relâchement puis Apply explicite. Échap, sortie du
canvas, perte de focus du canvas, changement de source et playtest annulent le
geste armé. Le mode ponctuel et les coordonnées numériques restent disponibles.
La collecte se fait à la cadence du rendu, pas à chaque événement DOM : un geste
plus rapide qu'une frame peut perdre son point initial. Cette ergonomie, ainsi
que la latence de préparation sur les grands gestes, reste à qualifier visuellement.
Pas encore de modification du relief pendant le glisser, ni de peinture matériau.

Vérifications réellement exécutées :

- `cargo test -p forge_world --test terrain_brush --locked --offline` : 7 PASS,
  0 échec, 0 ignoré (0,04 s d'exécution hors compilation). Nouveaux cas : trait
  sans trou sur parcours droit, même résultat avec événements stationnaires ou
  subdivision colinéaire, source intacte, Undo/Redo du geste entier, restauration
  de l'archive et refus atomique des chemins invalides/excessifs.
- Clippy world/all-targets PASS (2,31 s), Studio WASM strict PASS (3,45 s final).
  Corrections de lint : must_use et extraction des parseurs UI, sans désactivation.
  Avertissement futur upstream `proc-macro-error2` inchangé.
- Fichiers Rust modifiés formatés. Aucun essai graphique ni nouveau bundle de
  cette version : cercle/picking/overlay/geste restent NON QUALIFIÉS.

Pas de publication Git, de Gate 2 acquise ni de nouvelle validation Gate 1.
