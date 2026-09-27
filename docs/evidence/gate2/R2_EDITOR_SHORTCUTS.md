# Raccourcis d'édition — 12 septembre 2026

## Focus après commande — correction du 15 septembre

Constat F5B45FD1 : Échap n'a pas annulé l'aperçu végétal, alors que le bouton
Abandonner l'a restauré. L'ancien écouteur résidait sur main.authoring-shell.
Un bouton désactivé pendant l'appel natif peut perdre le focus vers body, hors
de ce sous-arbre ; cause plausible du résultat, pas prouvée par la seule capture.

Le code installe maintenant un unique écouteur keydown de fenêtre, uniquement
pendant le montage d'AuthoringWorkspace, retiré par on_cleanup. Le handler racine
est supprimé pour éviter le double dispatch. Échap appelle la même annulation
sans consommer le comportement natif ; IME et répétitions sont respectés.
Ctrl+S/Z/Y/Entrée conservent le filtre des champs éditables, le garde anti-répétition
et le bus de commandes. Aucun focus forcé, aucune mutation ni sauvegarde ajoutée.

Clippy WASM strict PASS (6,99 s), après correction de formatage d'un commentaire
documentaire ; avertissement amont proc-macro-error2 conservé.

Replay ciblé PASS le 15 septembre : candidat
DDCC9C5FDFC8BE693B2E4B09C1CE84950DB18E8B0209F43D07C79A2062CAF450,
`layer-index-keyboard-studio-20260915`, Web/packaging PASS (Rust 1 min).
Sur copie isolée `layer-index-keyboard-ui-20260915`, sélection Arbres puis
Examiner une copie de recette : forêt plus dense en aperçu. Échap efface la
proposition, désactive Apply et restaure visiblement la végétation acceptée.
Captures `copy-preview.jpg` et `escape-restored.jpg` dans cette copie sous
`E:/OpenRPG_Forge/.gate2-evidence/`. Aucun Apply/Save ; archive et source restent
SHA-256 FF89547BE4DFBF6C9345A2D4E9EFE340C51495158A69A08AB63C5D435ED868C0.
Processus 29428 et six WebViews 29320/31772/31004/32792/33336/32252 fermés,
absence vérifiée. Ce replay ne requalifie pas tous les raccourcis ni IME.

## Extension du 13 septembre — application explicite

Ctrl+Entrée ajouté hors champs de saisie : envoie uniquement la demande
Apply/ApplyZone déjà issue d'un aperçu natif examiné. Sans aperçu, session
absente ou playtest : refus explicite. Pas de reconstruction d'une commande
depuis les champs ni de sauvegarde implicite. Révision/hash restent ceux de
l'aperçu, revalidés par le backend. Les gardes DOM et anti-répétition existants
s'appliquent aussi à ce raccourci ; Ctrl+Maj+Entrée reste non attribué.

Correction associée : `send` valide maintenant un Result local avant d'effacer
l'aperçu précédent. Une erreur de parsing/préparation locale affiche son message
sans détruire l'aperçu déjà examiné. Ceci ne promet pas de restaurer cet aperçu
après un rejet asynchrone du backend : seules les erreurs locales sont concernées.

Test de correspondance des raccourcis PASS (1 test, compilation 52,44 s), dont
Ctrl+Entrée et refus sans Ctrl/avec Alt/Meta/Maj. Clippy Studio WASM strict PASS
(5,25 s), formatage PASS. Lecture du chemin `send` confirme que l'erreur locale
sort avant effacement des signaux. Parcours clavier/focus de Ctrl+Entrée et maintien
visuel d'un aperçu après erreur locale non rejoués. Les anciens replays ci-dessous
ne couvrent pas cette extension.

## Implémenté

Dans l'espace terrain auteur : Ctrl+S sauvegarde la session ; Ctrl+Z annule ;
Ctrl+Y ou Ctrl+Maj+Z rétablit. Le même bus que les boutons prépare les commandes
sur la révision/hash courants. Aucune voie de mutation directe du frontend.
Session absente, playtest et action indisponible refusés avec message. Une
commande déjà en cours empêche un envoi concurrent depuis ces contrôles.

Les champs input/textarea/select, contenteditable et role=textbox gardent leurs
raccourcis locaux. Composition IME et événements déjà traités ignorés. Les
modificateurs Alt/Meta ne sont pas détournés ; maintenir une touche ne produit
pas une rafale de mutations. Gestionnaire de fenêtre monté avec l'espace auteur,
callback retiré au démontage. Un raccourci accepté annule le geste de brosse et
de caméra en cours avant la commande. Texte d'aide visible près de l'historique.
Sauvegarder ne promeut pas un aperçu : appliquer la retouche d'abord, comme
avec le bouton existant.

## Validation

Test ciblé `cargo test -p forge_studio --lib --no-default-features editor_shortcut
--locked --offline` : 1 PASS, 0 échec, 0 ignoré, 57 filtrés. La compilation froide
de cette combinaison de features a pris 3 min 42 s ; le test lui-même 0,00 s.
Table vérifiée : Ctrl+S/Z/Y, Ctrl+Maj+Z, casse, absence de Ctrl, Alt/Meta et
combinaisons non attribuées. Ce test ne prouve pas la distribution DOM des touches.
Clippy Studio WASM strict PASS (3,81 s après extraction du raccord raccourcis
pour respecter la taille de fonction). Avertissement amont proc-macro-error2
future-incompatibility inchangé. Formatage/diff contrôlés.

Le parcours graphique et la protection du focus doivent être vérifiés avec
l'historique dans le prochain candidat groupé. Le binaire 64a59560... précédent
ne contient pas ce lot. Aucun nouveau format, dépendance ou changement de gate ;
aucune publication Git. Aucun processus graphique lancé ou laissé par ce lot.

Replay groupé effectué ensuite, candidat c999bcfa... : Ctrl+Z retire réellement
le relief, Ctrl+Y le rétablit, Ctrl+S écrit les reçus/révisions attendus ; Ctrl+Z
dans le champ Nom retire seulement la saisie locale, terrain intact. Captures,
hashes, fixture isolée, limites non rejouées et terminaison des processus :
`R2_AUTHORING_HISTORY.md`, section « Replay graphique groupé ».
