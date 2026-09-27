# R2-046 — ordre, copie et retrait des couches de végétation

## Parcours Studio acquis — 15 septembre 2026, F5B45FD1

Copie isolée `E:/OpenRPG_Forge/.gate2-evidence/vegetation-recipe-preview-ui-20260915`
depuis `scene-index-assets-ui-20260914`. Zone 81de8d42-89b9-58b5-b090-406cf4c63d06,
seed 44, arbres 35 %, herbe 65 %, masque peint verrouillé et arche existants.
Sans reconstruction du candidat : densité arbres saisie à zéro, Examiner.
Les arbres disparaissent réellement avant application ; session synchronisée.
Le bouton Abandonner restitue la forêt et désactive Appliquer. Archive identique
à la source à ce stade : FF89547BE4DFBF6C9345A2D4E9EFE340C51495158A69A08AB63C5D435ED868C0.

Nouvel examen, Apply : arbres absents et retouches non sauvegardées. Annuler
restitue la forêt ; Rétablir la retire ; Sauvegarder retourne Session synchronisée.
Captures conservées : trees-preview.jpg, trees-restored.jpg, trees-applied.jpg,
trees-undone.jpg, trees-redone-saved.jpg. Pas de nouvelle acceptation artistique.

Archive finale : 56AEDC0DDE3C27F32E46C3E5F413C3C2F5B491D03D1B3395AE9AF5BE69B4C128.
Le décodeur natif `inspect_zone_archive summary` rejoue les deux archives :
11 → 14 reçus, seed 44 conservée, corrections et terrain de base de mêmes hashes.
Tous les champs végétaux sont identiques hormis acceptance_probability de
editor_trees, 0,35 → 0 ; herbe reste 0,65. Comparaison en mémoire seulement.
Tête finale : 6dc38ad346e15473bad5ed823de7bd57c427d04799487d34715c973350a8e027.
Le résumé du lecteur a été complété avec recette végétale et hashes terrain/
corrections (build 0,77 s, Clippy exemple strict 0,38 s). Aucun fichier écrit
par la relecture, hash final inchangé après inspection.

Échap essayé sans disparition de la proposition : non qualifié, cause de
réception/focus non établie ; ne pas assimiler ce geste au bouton Abandonner.
Copie/retrait/réordre/renommage, réouverture graphique et playtest du nouveau
raccord restent à qualifier ; ce parcours ne clôture pas R2-046 ou Gate 2.
Fenêtre fermée ; PID 13264 et descendants WebView 25244/33536/4696/17912/34580/
30640 tous absents au contrôle. Source d'origine conservée ; aucun push.

## Aperçu de recette végétale — raccord du 15 septembre 2026

Le Studio demande désormais une projection végétale temporaire depuis le reçu
de régénération examiné : seed, réglages, copie/retrait et ordre. Il utilise le
même générateur et les mêmes exclusions chemins/rivières/bâtiments que la version
acceptée. Aucun changement de terrain, eau ou bâtiment n'est demandé par cet aperçu.
Une recette qui change aussi le terrain ou les corrections est explicitement refusée
par cette projection végétale seule ; le diff et l'application restent distincts.

La source acceptée dérivée est conservée pour Abandonner/Échap/saisie suivante.
La prochaine réponse native remplace la demande de projection et la source de
restauration ; les états affichés distinguent proposition et végétation acceptée.
Le mode jeu refuse un nouvel aperçu. Aucun second monde ECS, fichier ou tête
canonique n'est créé. Le reçu est contrôlé par hash/révision source, inverse,
corrections inchangées et hash résultant avant dérivation.

Test ciblé `vegetation_preview_uses_the_reviewed_recipe_without_mutating_the_session`
PASS (10,73 s compilation, moins de 0,01 s exécution) : seed projetée différente,
couches conservées, archive identique, refus de reçu périmé/incohérent, de correction
modifiée et de terrain changé. Clippy WASM strict final PASS (7,10 s), après
extraction du libellé pour respecter la limite de taille de fonction. Avertissement
de compatibilité future `proc-macro-error2` inchangé.

Limites : raccord source compilé, pas encore lancé/capturé. Rendu effectif,
restauration après abandon, application et retour du playtest ne sont pas prouvés
par ce test pur. Aucun verdict de qualification graphique ni clôture R2-046.

Candidat regroupé construit ensuite :
`E:/OpenRPG_Forge/.gate2-evidence/vegetation-recipe-preview-studio-20260915/forge-studio-candidate.exe`,
SHA-256 F5B45FD1171C841B3DE5A676B1DEAC663F8D07B8E819769B96B06B7567D617B4.
Web terminé code 0 (compilation Rust 2 min 37 s), packaging code 0, preuve
BUILD_ONLY avec runtime_verified=false et gate_qualified=false. Kit 40A55F75
inchangé. Fenêtre configurée (-1840,90), 1600×900 ; position non observée.
Aucun lancement graphique ni nouveau processus d'essai. Réutiliser ce candidat
pour le parcours groupé des couches au lieu de reconstruire sans changement.

## Noms des couches — 15 septembre 2026

Renommage ajouté à la commande de recette commune et au panneau Studio.
Le nom est nettoyé des espaces périphériques puis soumis au validateur existant
(non vide, 128 octets maximum). L'ID stable ne change pas. La revue affiche
l'ancien et le nouveau nom ; la saisie invalide l'aperçu précédent. Le changement
reste soumis à Examiner/Appliquer/Sauvegarder et au garde-fou de zone.

Test métier ciblé PASS (13,86 s compilation, 0,01 s exécution) : nom invalide,
couche absente, no-op, aperçu sans mutation, retry idempotent, recette identique
hors nom, masques peints/verrouillés conservés, archive rouverte, Undo/Redo.
Clippy strict du test PASS (2,20 s). Test de présentation ciblé PASS
(23,71 s compilation, moins de 0,01 s exécution) : ancien/nouveau nom visibles
dans le texte de proposition non appliquée. Pas de qualification graphique
de ce nouveau champ ; le candidat 1551BFCA ne le contient pas.
Clippy Studio/Web WASM strict final PASS (29,77 s), avec l'avertissement
de compatibilité future déjà connu de `proc-macro-error2`, sans warning Forge.

Complément du 15 septembre 2026 : copie et retrait implémentés via
`prepare_layer_membership_request`, avec aperçu puis application explicite.
La copie reçoit un ID déterministe neuf et reprend les paramètres acceptés,
sans dupliquer les masques peints. Le retrait refuse `DensityLayerRemoved`
si des corrections deviendraient orphelines ; aucune correction n'est effacée.
Les boutons Studio sont raccordés à la commande commune et protégés par zone.

Test étendu PASS : refus du retrait de la couche peinte verrouillée, aperçu
sans mutation, copie idempotente, corrections identiques et conservation exacte
des 8 404 instances des couches d'origine. Retirer la nouvelle copie restitue
la recette initiale ; réordre et relecture restent couverts. Un premier échec
a révélé des tirets interdits dans l'ID de copie : génération corrigée en
underscores, sans affaiblir le validateur. Dernière exécution ciblée : 1 PASS,
0 échec, 0,06 s ; Clippy du test strict PASS (cache, 0,30 s).
Clippy Studio/Web WASM strict du code final PASS en 29,29 s ; avertissement
de compatibilité future de la dépendance `proc-macro-error2` conservé.
Ces preuves sont des tests de données, pas une qualification graphique.

Complément ciblé : masque peint 2×2 [0,255,128,0] puis verrouillé, réordre,
archive et nouvelle session. Les corrections sont exactement conservées ;
les 8 404 instances générées avant/après sont égales, après tri par ID, dans
tous leurs champs (positions, orientation, échelle, vent, etc.), pas seulement
leur nombre. Test `layer_order_preserves_painted_locked_masks_and_generated_instances`
PASS (compilation 1,10 s, exécution 0,03 s). Cette preuve synthétique complète
le test à corrections vides ci-dessous ; elle ne prouve ni geste Studio ni
ajout/suppression de couches à cette étape initiale (complément ci-dessus).
Aucun lancement graphique.

Implémenté le 15 septembre 2026 : `prepare_layer_order_request` déplace une
couche d'une position par ID stable. Même route prepare_regeneration, reçu,
Preview/Apply, Undo/Redo et persistance que les autres retouches de recette.
Seed, valeurs des couches et corrections ne sont pas réécrites. Couche absente
et mouvement au-delà des extrémités refusés. CommandId déterministe lié à la
source et au mouvement ; aucun écrit implicite.

Studio : boutons Monter/Descendre dans chaque couche, protection de zone du
callback, diff de l'ordre avant/après. Ils réordonnent les valeurs acceptées,
pas les brouillons densité/espacement ; Appliquer et Sauvegarder restent explicites.

Test ciblé PASS : aperçu immuable, retry idempotent, valeurs et seed conservées,
relecture d'archive, Undo puis Redo, extrémité et ID absent. Les corrections de
ce test sont vides : il ne constitue pas une nouvelle preuve de masque peint
verrouillé ni d'identités des instances générées. Rust compile 13,57 s, test
0,01 s ; Clippy Studio/Web strict PASS 29,71 s, formatage effectué.

Non qualifié graphiquement ; absent du candidat 1551BFCA. Le critère R2-046
demeure ouvert : la preuve ci-dessus concerne les couches de végétation,
pas toutes les familles de features ni le geste Studio du nouveau lot.
Aucun asset acquis ni publication Git.
