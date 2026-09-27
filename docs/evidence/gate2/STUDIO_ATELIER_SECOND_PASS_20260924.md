# Atelier — deuxième passe native

Chantier en cours, aucune clôture de Gate 2.

## Candidat avant recompaction finale

SHA-256 exécutable :
`D93AF4B3E64D92E392BB627EBF24314B07C270F2DB63462317D00B0EB37C2F2D`.

Parcours Computer Use, session vide sans modification de carte :

- clic Composer puis Deux écrans ; fenêtre secondaire sur l'autre moniteur ;
- catalogue secondaire : huit références du kit local ;
- clic sur `vv-tree-oak-a` dans le secondaire ; carte correspondante surlignée
  ensuite dans le principal ;
- clic Rapatrier : retour à une seule fenêtre, sélection conservée ;
- fermeture normale de cette session d'essai ; inventaire des fenêtres du
  candidat vide avant remplacement. Aucun processus utilisateur fermé.

Diagnostic principal : **1920 × 1080 CSS, DPR 1**, navigation complète visible,
canvas 1314 × 565 CSS, panneau contenu, aucun débordement horizontal.
La bibliothèque échoue : contenu 231 px / hauteur disponible 218 px.
Les styles effectivement observés sont des cartes de 110 px et des marges
héritées de 8 px ; les correctifs source à 96 px et marges supprimées avaient été
écrits après leur lecture par le compilateur et ne sont pas dans ce candidat.
Ne pas confondre les sources et le binaire : reconstruction en cours.

Preuves locales : `output/studio-gold-20260924/atelier-review/library-geometry.json`
et `.png`, ainsi que `library-compact.json` et `.png`. Les captures Computer Use
de sélection partagée et rapatriement sont dans la conversation de réalisation.

## Vérifications de code

`cargo test -p forge_studio --locked` : **103 PASS, 1 ignoré**, zéro échec
(95 tests bibliothèque + 8 natifs). Le test ignoré est une comparaison CPU
optionnelle, pas une preuve de performance du renderer.

Formatage Studio effectué. `cargo fmt --check` global signale aussi l'ordre des
modules dans `crates/forge_engine/src/lib.rs` du travail Tidewater concurrent :
hors périmètre, laissé intact. Pas de verdict global PASS.

La sélection inter-fenêtres est démontrée pour le catalogue. L'édition des
propriétés depuis le secondaire, la conservation des brouillons détachés,
le redémarrage/restauration et la matrice complète DPI restent à réaliser.

## Correctif recompilé et observé

SHA-256 du candidat suivant :
`E2810CA01256485D665C7D1BCEE8D92177D57F4D6AA685CDC4435B127016DA95`.
Build WASM release PASS (2 min 37 s), candidat Tauri PASS (18,57 s).
`cargo fmt -p forge_studio --check`, syntaxe du diagnostic Node et diff-check PASS.

Après clic Composer, `library-fixed.json` / `.png` : **PASS**, 1920 × 1080 CSS,
DPR 1 ; huit cartes de 96 px entièrement visibles ; hauteur de bibliothèque
218 px utiles / 218 px de contenu, aucun débordement vertical. Le premier appel
diagnostique était encore sur Projet après un clic non exécuté ; le fichier a
été remplacé après le clic Composer réellement observé, actif `objects` vérifié.

Après repli par clic : `library-folded.json` / `.png`, **PASS**, canvas passant
de 565 à 745 px de hauteur. Ces contrôles portent sur une session vide et ne
qualifient ni une scène chargée ni les autres résolutions.

Persistance observée : panneau Objets replié avec son bouton « − », fermeture
normale, redémarrage du même candidat/profil, clic Composer : panneau restauré
replié (bouton « + »). Puis panneau redéployé. Cette preuve couvre le repli par
sujet, pas encore le déplacement flottant ni les autres ancrages.
Défaut supplémentaire observé : replier le panneau Objets masque la bibliothèque
mais laisse sa réserve de hauteur dans le viewport. À corriger avec les variantes
d'ancrage gauche/bas/flottant avant de qualifier ces dispositions.
