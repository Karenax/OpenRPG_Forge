# Départ de zone persistant — 14 septembre 2026

## Lisibilité du départ — correction après observation

Résumé accepté et diff avant application utilisent désormais le même formateur
X/Z en mètres et cap en degrés. Absence de départ, cap absent et cap explicite
zéro sont distingués ; plus de représentation Debug Some/ZoneStart.
Test ciblé `start_label_distinguishes_absent_heading_from_explicit_zero` PASS
(1), Clippy Web/WASM PASS (7,02 s), formatage effectué. Aucune donnée ou règle
de lancement modifiée. Textes nouveaux non encore affichés dans un candidat ;
les captures 284B775A conservent honnêtement l’ancien défaut.

## Parcours réel acquis — 15 septembre, candidat 284B775A

Copie isolée `E:/OpenRPG_Forge/.gate2-evidence/persistent-start-ui-20260915`.
Gestes Studio : ouvrir mon-projet/zone-1, X=40 m, Z=0 m, cap=90°, Examiner,
Appliquer, Save. Fermeture complète puis nouveau processus : départ accepté
et champs restaurés exactement (40/0/90). Jouer lance la vue avec capsule ;
Retour à l’édition restitue la vue et la session synchronisée.
Archive SHA-256 après Save et après jeu identique :
9E77EF0C7E588E183CCA2695D91235183A8B81585F2EF5BEFDF8C006238BE9B9.
Relecture de head.corrections.start confirme 40/0/90. Captures start-saved.jpg,
start-reopened.jpg, start-playtest.jpg dans la copie. Aucun JSON écrit à la main.

Position/orientation numériques de la capsule non relevées dans le runtime :
la vue de jeu prouve le lancement, pas à elle seule ces coordonnées. Picking,
obstacles, Undo/Redo graphique et métadonnées complètes R2-004 encore dus.
Résumé Some(90.0) constaté, lisibilité à améliorer sans bloquer la persistance.
Premier processus 11700 et six WebViews fermés et absents avant reprise.
Second processus 1156 et six descendants également fermés et tous absents au
contrôle final.
Pas de nouvelle compilation ni publication, Gate 2 toujours ouverte.

## Complément du 15 septembre — saisie multizone

Les champs X/Z/cap reprennent le départ accepté à l’ouverture/changement
d’AreaId ; sans départ, X/Z valent zéro et le cap reste absent. Le message de
picking de l’ancienne zone est retiré. Un Memo d’identité évite de réinitialiser
les saisies sur chaque réponse Inspect/Preview/Save de la même zone.
Aucune commande ni écriture déclenchée par cette synchronisation du formulaire.
Clippy Web/WASM PASS (7,03 s) après remplacement cloned par copied ; formatage
effectué. Comportement graphique multizone encore à qualifier avec le parcours
de départ persistant ; candidat C5C3C3B1 antérieur à ce changement.

Implémenté dans le workspace Rust, non publié : `ZoneStart` moteur-neutre,
correction optionnelle `start`, commande préparée liée au hash/révision,
Preview/Apply, Undo/Redo et historique avant/après. Tête v6, archives v16/17/18,
anciens formats conservés. Détails de représentation dans ADR-0014.

Le host natif utilise le départ authoré pour StartPlaytest. StartPlaytestAt
garde son rôle temporaire et ne réécrit pas le départ authoré. Le Studio expose
X/Z/cap, examen du départ ou de son retrait, état accepté et diff avant Apply.
Toute saisie invalide l'aperçu précédent. Save demeure explicite.

Deux tests nouveaux PASS lors du premier passage : preview immuable, retry,
régénération, trois codecs et refus de versions falsifiées, retrait/Undo/Redo,
entrée invalide directe et préparée ; host réel Create/Save/Close/Open/Jouer,
snapshot utilisant le départ, remplacement temporaire distinct. Après ajustement
d'allocation interne et ajout à l'historique, Clippy projet ciblé et Studio Web
strict PASS (3,58 s / 31,35 s). Passage final groupé : 15 tests PASS sur
zone_start/migration/session/delta_archive/history/host/file_session/regeneration
(18,70 s de compilation). Un replay de fichier Studio opt-in reste ignoré,
non compté comme réussi. Fmt et diff check PASS. Aucun processus graphique lancé.

Complément de saisie : le bouton « Choisir le départ dans la vue » arme le
picking terrain existant. Le clic remplit uniquement le brouillon X/Z, conserve
le cap, revalide ses bornes et refuse zone/brouillon modifiés ou commande/playtest
en cours. Reprise des valeurs acceptées disponible sans mutation, cap absent
conservé comme absent. Examiner/Appliquer/Save restent explicites. Compilation
Web acquise, geste réel non acquis.

Préflight graphique : candidat existant A64D5ABA relancé sans rebuild pour
vérifier le pilotage ; deux refus `foreground window did not report a process id`.
PID 3260 et descendants 1620/29656/29796/5100/33536/16604/5904 arrêtés, absence
vérifiée. Aucune qualification des nouveaux contrôles sur cet ancien binaire.

Limites : aucun nouveau candidat graphique reconstruit ; champs,
orientation et position réelle à qualifier ensemble. Picking raccordé non qualifié,
de validation d'obstacles, de point sur plancher ni de sauvegarde de partie.
Qualification workspace ADR-0006 encore due ; CI NOT RUN — infrastructure billing.
R2-004 et Gate 2 restent ouverts. Aucun fichier utilisateur migré, aucun push.
