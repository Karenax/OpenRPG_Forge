# R2-035 — Résumé terrain avant application

## Observation réelle B4754D5D

Zone mon-projet/zone-1 ouverte dans une copie `authoring-review-ui-20260914`.
Saisie d’altitude 6 puis Prévisualiser : texte réellement affiché « 0 m
(héritée) → 6 m (retouchée) », non appliqué et source 7b962cbc3496082108d65deda6894503151e796b75083cc55ba6feaec38f5f54.
Capture `terrain-diff.jpg` dans cette copie. Aucun Apply/Save effectué.
Cette observation qualifie ce résumé, pas le diff densité ni les inspecteurs.
Fenêtre fermée ; racine 28856 et sept processus du candidat désormais absents.
Le scan de parenté avait aussi inclus 27368 : relecture du chemin identifie
le service Brother SoftwareUpdateNotification, donc préservé et exclu du lot.

## Complément densité — 14 septembre 2026

Le résumé des masques identifie désormais les couches touchées, une nouvelle
retouche ou son retrait (retour à la recette), les échantillons modifiés sur
une même grille, un remplacement de grille, les échantillons nuls et le verrou
avant/après. Les couches inchangées ne sont pas répétées. Comparaison en lecture
seule des corrections du reçu, sans nouvelle commande ou mutation de carte.

Test ciblé `density_review_distinguishes_paint_lock_and_removal` : 1 PASS
(peinture, verrou seul, ajout, retrait, inchangé et entrée préservée).
Compilation initiale refusée pour un unwrap superflu dans le test, corrigé.
Clippy initial refusé pour nom similaire et comptage d’octets ; renommage et
fold sans dépendance ajoutée. Clippy Web/WASM final PASS (6,91 s), fmt check
PASS. Test antérieur à ces deux ajustements locaux, pas de suite globale rejouée.
Nouvel affichage non encore observé dans le Studio. Le nombre d’échantillons
n’est ni une surface en m², ni le nombre d’arbres retirés. R2-035 reste ouvert.

## Implémenté le 14 septembre 2026

Le résumé utilise les corrections avant/après du reçu examiné, et non le
formulaire mutable. Le nom et l’altitude affichent leurs valeurs effectives et
leur origine (recette héritée ou retouche). Les couches de relief et matériaux
distinguent héritage, remplacement vide explicite et nombre d’échantillons.
Le contenu modifié est signalé même lorsque les nombres restent identiques.
Un reçu qui ne correspond plus au hash de la tête courante demande un réexamen.
Aucun nouveau writer, format persistant ou chemin d’application.

## Vérifié

- Test `terrain_diff_shows_effective_values_and_explicit_empty_without_writing` :
  PASS (1 test), transition 0 m hérité → 6 m retouché, vide explicite, source
  absente/périmée et archive inchangée.
- `cargo fmt --all --check` : PASS.
- Clippy Studio lib Web/WASM sans features par défaut, `-D warnings` : PASS
  après correction de la signature du helper en `Option<&[T]>` (6,95 s).
  Le test a précédé cette correction de signature ; pas de répétition de suite.
- Avertissement futur de la dépendance proc-macro-error2 2.0.1 conservé.

## Limites

Pas de nouveau packaging ou lancement graphique : le candidat F1C5DB66 ne
contient pas ces textes. Leur affichage réel reste à vérifier lors du prochain
lot Studio groupé. Les couches ne présentent pas encore un diff spatial de
chaque échantillon. R2-035 et Gate 2 restent ouverts. Aucune publication Git.
# Protection commune des propositions périmées — 15 septembre 2026

`authoring_zone::preview_label` vérifie maintenant hash ET révision de la tête
avant toute branche de diff (départ, bâtiment, rivière, chemin, placements,
densité, régénération et terrain). En cas de désaccord ou de zone absente,
les aperçus sont retirés via le mécanisme existant et la proposition est
signalée périmée, sans demander de nouvel aperçu. Les validations natives
d'application restent inchangées ; ce contrôle ne les remplace pas.

Test ciblé de `matches_source` PASS : source valide, source absente, contenu
différent et révision attendue différente ; archive inchangée. Compilation
du test 11,08 s, exécution < 0,01 s. Clippy Web/WASM strict PASS (7,68 s),
formatage effectué. Aucun build de candidat ni qualification GUI de cette
protection : le candidat 284B775A ne la contient pas. R2-035 reste ouvert.
