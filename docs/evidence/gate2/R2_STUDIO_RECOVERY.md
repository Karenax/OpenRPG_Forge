# Récupération explicite — 12 septembre 2026

## Implémentation

Protocole applicatif InspectRecovery/Recover et panneau Studio raccordés au
stockage existant. Aucune modification du format terrain ou archive persistante.
Examen sous verrou natif, zone fermée obligatoire, racine autorisée contrôlée.
Le panneau propose aussi les zones dont la première sauvegarde a été interrompue.
Il présente les versions, leur validité, nom, révision, projet, altitude de base,
nombre de samples retouchés et SHA-256 des archives. Confirmation puis choix
explicite : promouvoir le candidat valide ou conserver le principal et archiver
le candidat. Les deux versions sont conservées en recovery-<hash>.json avant
mutation du staging ; aucune sélection automatique. Rafraîchir et ouvrir ensuite.

L'application revalide sous verrou les deux hashes, le replay de l'archive,
l'identité de zone et, lorsque le principal est valide, l'identité de projet.
Une session active ou un playtest empêche la récupération. Un candidat invalide
ne peut pas être promu ; il peut être archivé sans modifier le principal.
L'examen peut créer authoring.lock, mais ne modifie pas les deux archives.

## Preuves ciblées

`cargo test -p forge_project --test authoring_recovery --test authoring_catalog
--locked --offline` : 4 PASS, 0 échec, 0 ignoré (0,04 s hors compilation).
Récupération : première sauvegarde, promotion puis réouverture exacte, refus
session ouverte, changement du candidat et du principal après examen, corruption,
autre projet/zone, conservation exacte des archives principal/candidat et rejet
archivé. Fixtures synthétiques isolées sur E:, pas de modification d'un projet
utilisateur. Catalogue : les 2 scénarios intégrés existants restent réussis.

Clippy forge_project/all-targets strict PASS et Studio WASM strict PASS après
correction des avertissements locaux. L'avertissement amont future-incompatibility
proc-macro-error2 reste distinct. Formatage et diff contrôlés.

## Limites

Panneau codé et compilé mais parcours graphique non lancé dans ce lot : il sera
qualifié avec le catalogue, sans reprendre les audits 3D. Le binaire graphique
442d3065... antérieur ne contient pas ces fonctions. Pas de verdict visuel acquis.
Pas de preuve de coupure électrique réelle, ni de sandbox contre un processus
hostile remplaçant simultanément les chemins. Fichiers illisibles/surdimensionnés
refusés ; la récupération générique des backups archivés n'est pas une UI acquise.
Qualification partagée ADR-0006 encore due. R2-039 et Gate 2 restent ouverts.
ADR-0014/0015 non approuvés, inchangés. Aucune publication Git.

## Parcours Studio effectué

Le replay groupé catalogue/récupération a ensuite été effectué dans le candidat
natif `forge-studio.exe` SHA-256
`64a595607007eb05cdae3f5d3da939a9d7439d19af7c9d8210b770f480700f54`.
Trunk/WASM et Tauri debug sans bundle PASS (2 min 33 s + 59,86 s). Configuration
locale `.gate2-evidence/tauri-authoring-recovery.conf.json` à côté du worktree,
fenêtre 1920×1080 sur écran secondaire x=-1920, y=52. Pas de benchmark 3D.

Fixture isolée dans `.gate2-evidence/authoring-recovery-ui-projects`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`. Copie exacte de la précédente archive
de brosse, renommée staging dans cette fixture uniquement ; aucun principal au
départ. Le catalogue affiche 0 sauvegarde/1 indisponible avec le motif correct.
Sélection, examen et affichage réel : première sauvegarde, candidat révision 4,
618 samples, altitude de base 0, hash archive attendu. Boutons désactivés avant
confirmation ; activation puis promotion explicitement déclenchées dans le Studio.
Message de succès observé ; après actualisation : 1 sauvegarde/0 indisponible.
Ouverture depuis le catalogue : session synchronisée, terrain affiché et bande
de relief visible, hash document
`ffe022eba9182bc36f0335c20177f0f594c748e9b3d976e004b9ed7c2d874dc7`.

Les fichiers authoring.json et recovery-<hash>.json font chacun 97 195 octets,
SHA-256 identique au staging initial :
`dd54b814484c1401b256539b91e205e520657f019e9585e1de3884a905b496a9`.
L'archive de brosse originale hors fixture garde ce même hash après essai.
Captures affichées et inspectées dans la conversation, pas de PNG local prétendu.
Le clic indexé sur une option de liste native WebView a été refusé par le helper
(fenêtre popup distincte) ; sélection terminée au clavier après réobservation.

Fermeture Alt-F4 : PID Studio 28944 et descendants 29268, 744, 19512, 10584,
21668, 19480, 31160 tous absents au contrôle final. Aucun navigateur utilisateur
arrêté. Ancien binaire 442d3065... préservé comme
`.gate2-evidence/studio-authoring-brush-events-tested.exe`.

Le chemin graphique première sauvegarde → récupération → ouverture est prouvé.
Le choix d'archiver sans promotion et les conflits restent prouvés par tests
natifs, non rejoués graphiquement. Les limites générales et qualifications de
gate ci-dessus demeurent ; ne pas déduire la validation complète de Gate 2.

## Consolidation avec la campagne fraîche — 22 septembre 2026

La campagne indépendante
`E:/OpenRPG_Forge/.gate2-evidence/persistence-failure-20260922-v1/`
a réussi 13/13 tests exacts et confirme sur le HEAD
`f786a38aca2023f08d708687f99a47d14f34e6d4` les fautes et invariants de
sauvegarde/récupération : interruption après sync, staging abandonné, candidat
tronqué/invalide, choix obsolète, conflit, identité étrangère, versions
préservées, promotion et réouverture exacte. Le replay Studio ci-dessus apporte
le geste graphique explicite que ce harness ne rejoue volontairement pas.

Par composition de ces deux preuves, R2-039 est clos localement. Cela ne change
pas les champs du ledger frais : `human_review_performed=false` et
`gate_qualified=false`. Le replay graphique utilise un candidat antérieur ; le
candidat exact qui scellera Gate 2 devra donc encore être qualifié. Le choix
« archiver sans promouvoir » et les conflits sont couverts par tests natifs mais
ne sont pas revendiqués comme replay graphique. CI distante :
`NOT RUN — infrastructure billing`.
