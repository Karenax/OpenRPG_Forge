# R2-F — Playtest de la zone auteur

## Implémenté, non encore qualifié visuellement

Commandes natives `StartPlaytest { expected_hash }` et `StopPlaytest`.
Le démarrage exige le hash exact de la session ouverte, y compris ses retouches
non sauvegardées. Pendant le jeu, toutes les commandes d'édition, sauvegarde et
changement de zone sont refusées côté hôte (pas seulement par l'interface).
Inspect et les retries de démarrage identiques restent possibles. Le retour ne
sauvegarde pas et conserve la session et son historique. Le mode de simulation
est transitoire, absent de l'archive auteur.

Le Studio expose Jouer / Retour à l'édition. La navigation dispose le viewport
précédent et relit la même session native ; le mode jeu raccorde le contrôleur
original sans assets à la géométrie du document courant. Le point initial est
encore calculé par l'adapter terrain, pas choisi depuis un inspecteur d'auteur.
Le profil de contrôleur existant est réutilisé, pas la carte Visual Valley.

Le raccord d'entrée auteur ajoute ensuite une capture pointeur limitée au mode
jeu. Le contrôleur reçoit les entrées uniquement lorsque le document a le focus
et que le canvas auteur possède cette capture. Échap libère la souris, Espace
ne fait pas défiler la page pendant le jeu. Ce complément doit encore passer
le replay Clippy et le test graphique ; il n'est pas couvert par le PASS WASM
antérieur ci-dessous.

## Vérifié le 12 septembre 2026

- Test `playtest_freezes_exact_unsaved_document_without_touching_archive` PASS :
  absence de zone, hash périmé, snapshot exact, retry, refus des mutations,
  archive inchangée, retour sans sauvegarde puis sauvegarde/réouverture explicite.
- Suite core/project : **17 + 84 = 101 tests PASS**, aucun ignoré ; journal
  `E:/OpenRPG_Forge/.gate2-evidence/authoring-playtest-domain.log`.
- Clippy project/all-targets strict PASS ; Clippy Studio WASM strict PASS.

## Reste à vérifier

Construction du binaire puis entrée réelle dans le jeu, contrôle du personnage,
collision sur le terrain retouché, retour à l'édition et conservation des sources.
Les diagnostics de simulation et le point de départ éditable restent à compléter.
Ce rapport ne clôture ni R2-041, ni le parcours complet, ni Gate 2.

Build de test en cours, session 90433 : journaux `authoring-playtest-trunk.log`
et `authoring-playtest-native.log` sous `E:/OpenRPG_Forge/.gate2-evidence`.
L'exécutable précédent est conservé sous `studio-authoring-navigation-tested.exe`
avec SHA-256 vérifié `06c4e7a8151c5a8ab389e391570e003ee62685648392ad127e77f787519874d9`.

## Premier replay graphique du candidat

Build session 90433 terminé exit 0 : Cargo WASM 3 min 04 s, Tauri natif
1 min 24 s. Clippy incluant le complément de focus PASS dans
`authoring-playtest-focus-clippy.log` (session 36538, exit 0).
Binaire testé SHA-256 :
`36aeca410202afa1af779ef96cbd744c12c82cb0dc3a8a028b12323457cfcf3e`.

Dans le Studio réel : ouverture de Vallée test auteur à 8 m, clic Jouer,
rechargement et apparition du personnage avec caméra de jeu sur le terrain.
Clic dans le canvas : notification WebView de capture pointeur visible.
La commande V n'a pas produit de changement de caméra observable ; l'accessibilité
a ensuite signalé le focus sur le volet hôte. Les contrôles de déplacement et
caméra restent donc NON QUALIFIÉS ; distinguer défaut produit et interaction
de l'outil de test nécessite les diagnostics d'entrée/mouvement.

Retour à l'édition observé : personnage retiré, caméra d'édition restaurée,
hash moteur inchangé `42b8905a985e50495ef29ab38ee232a279c264e3ef89b002c3d3f9a6252a3248`.
Archive relue après le jeu, SHA-256 fichier toujours
`a11fd4aa8cdf5ac2496b59b1ec5d59437c622c453d7990abb5e85c19158b2430`.
Captures inspectées dans la conversation, sans PNG autonome archivé.
Pas de benchmark déduit des FPS glissants ; aucun passage de R2-041.

Test fermé par Alt+F4 : PID 30064, console 17968 et WebView 14924 ainsi que
29876/30372/28016/23164/25176 absents au contrôle CIM suivant.

### Instrumentation du diagnostic d'entrée

Ajout d'un panneau repliable qui réutilise les observations de capture/événement
DOM existantes et expose le snapshot réel du contrôleur : tick fixe, dernière
séquence appliquée, position en mètres, contact au sol et mode caméra. La mise
à jour du texte est bornée à 4 Hz, sans écriture dans les documents auteur.
Clippy WASM strict PASS (3,38 s). Le rendu et le diagnostic causal de l'entrée
restent à observer sur le binaire reconstruit ; aucun contrôle clavier déclaré PASS.

## Analyse du raccord agrégé — 13 septembre 2026

Inspection actuelle, pas implémentation ni preuve de playtest unifié :

- `AuthoringHost::start_playtest` passe par la session terrain uniquement. Le
  refus des zones agrégées reste volontaire ; ne pas simplement retirer ce garde.
- `authoring_controls::prepare_playtest` et `viewport_plugin` identifient un
  terrain, pas une tête contenant terrain et végétation. Le snapshot accepté doit
  porter le hash de tête et les hashes des deux documents effectifs.
- `authoring_vegetation::project` utilise `can_preview_asset`, faux en mode jeu :
  cette condition empêche la projection de la végétation authorée au démarrage.
  Séparer disponibilité de projection et autorisation de manipulation d'assets.
- `PlayerInputFocus` coupe les commandes, mais pas la gravité/simulation dans
  `drive_player_controller`. Le démarrage doit attendre les caches terrain et
  végétation correspondant au snapshot, sans qualifier une projection périmée.

Prochain raccord prévu : snapshot effectif immutable et identifié, garde de mode
côté host, remise au renderer unique, barrière de simulation/diagnostic d'attente,
puis retour édition sans écriture. Tester tête périmée, refus des mutations pendant
jeu, caches absents/périmés et préservation archive/dirty ; parcours graphique
Jouer/déplacement/retour sur terrain avec clairière. Le bouton agrégé reste désactivé
jusqu'à ce raccord. Aucun nouveau verdict R2-041/R2-050.

### Contrat de préparation implémenté

`authoring::playtest::PlaytestSnapshot` ajouté : construction depuis tête validée
ou terrain historique, refus de hash périmé, copies effectives privées exposées en
lecture seule. Identité séparant hash d'autorité, terrain et végétation optionnelle.
La barrière métier distingue source périmée, terrain absent/périmé, végétation
absente/périmée et concordance exacte ; elle n'atteste ni frame présentée ni état GPU.
Une projection végétale inattendue est refusée même quand le snapshot n'en comporte pas.

`PreparePlaytest` raccordé au host et au protocole : réponse d'identité sans mutation,
sans dirty supplémentaire ni mode jeu. La préparation n'est pas un verrou de session
et doit être revalidée au futur démarrage. Les documents restent ceux de l'autorité
active ; pas de snapshot persistant supplémentaire, pas d'archive modifiée.
`StartPlaytest` agrégé reste refusé, bouton inchangé : le raccord renderer et le gel
de simulation sont encore dus, ne pas présenter ce contrat comme un jeu jouable.

Tests ciblés : density_inspection (2 PASS, dont snapshot/barrière), zone_host PASS
(préparation et garde agrégée, identité terrain dérivée refusée, état inchangé).
Clippy project/all-targets et Studio WASM/web strict PASS. Extraction de la
préparation en helper natif pour respecter la longueur du dispatcher, sans lint
désactivé. Nouveau test host rejoué après cette extraction. Aucun lancement GPU.

### Barrière du contrôleur — raccord en cours

`PlayerSimulationReady` ajouté au moteur, ouvert par défaut pour les consommateurs
existants. Fermé par le host avant le démarrage, il suspend les ticks du personnage,
la gravité et les déplacements ; annule les commandes en attente sans réinitialiser
leur séquence et remet les vitesses à zéro. Les sorties Rapier précédentes ne sont
pas réutilisées comme déplacement courant pendant cette attente. Ce n'est pas une
pause de la simulation du monde entier ni une preuve de présentation GPU.

La projection végétale Studio utilise désormais la disponibilité de la projection,
indépendamment de l'autorisation de retoucher les assets. Les outils restent
interdits pendant le playtest. Le raccord de l'identité agrégée au verrou moteur
reste à réaliser ; `StartPlaytest` agrégé n'est pas activé par cette modification.

Validation ciblée : `cargo test -p forge_engine --lib player_controller::tests
--offline --locked` PASS, 17 tests dont attente en suspension sans chute, commandes
abandonnées, reprise de gravité et nouvelle fermeture sans déplacement résiduel.
Compilation 3 min 33 s, exécution 1,13 s ; aucun lancement graphique. Ce résultat
ne qualifie pas encore le parcours Studio Jouer/retour ni tout le workspace.
Clippy moteur/lib natif et Studio/lib WASM/web avec `-D warnings` PASS.
Cargo signale séparément une incompatibilité future de `proc-macro-error2 2.0.1` ;
ce n'est pas un warning de code Forge supprimé. Processus de test/compilation
terminés ; aucune fenêtre graphique ouverte par ce lot. `git diff --check` PASS.

### Raccord unifié suivant — implémenté, qualification en cours

Le host conserve maintenant le snapshot effectif du playtest, avec hash de tête
pour une zone agrégée et hash terrain pour l'ancien format. Inspect conserve cette
identité ; Stop libère le mode sans Save ni modification des corrections/Undo.
`PlaytestSnapshot::from_state` refuse une tête, une identité ou une vue terrain
incohérentes après transport JSON. Une identité de préparation seule ne démarre rien.

Le Studio envoie la tête depuis Jouer, valide la réponse et remet cette identité
au renderer unique. Sa barrière FixedUpdate précède Drive : terrain, deux hashes
végétaux, absence de rejet et identité du viewport doivent concorder. Une source
végétale absente utilise le hash du document vide dérivé, jamais des instances
anciennes arbitraires. Arrêt/remplacement/réponse invalide referment la barrière.
Les diagnostics distinguent attente de reconstruction et simulation active ;
aucune concordance CPU n'est annoncée comme frame GPU présentée.

Tests domaine/host PASS : snapshot agrégé avec végétation, transport et réponses
altérées, refus de Save/Close/Apply pendant jeu, tête/dirty/Undo et archive inchangés,
retour édition ; parcours terrain historique toujours PASS. Clippy project et
Studio WASM PASS après corrections de compilation/lint sans nouvelle dépendance.

Défaut supplémentaire identifié dans le raccord moteur : une modification des
sources végétales ne remplaçait pas les collisions des troncs. Correctif en cours
de validation : remplacer les obstacles canoniques avant l'acquittement des nouveaux
hashes, seulement pour changement de source ; un changement de qualité conserve
les entités physiques. Aucun profil de collision n'est inventé pour une couche
qui n'en déclare pas (la recette auteur existante peut ne pas en avoir).

Build/replay du nouveau candidat et qualification workspace ADR-0006 encore dus.
Ne pas utiliser les captures du candidat e4e75bc6 comme preuve de ce raccord.

### Replay du candidat unifié — 13 septembre 2026

Candidat natif SHA-256
`6c932781c7b42e52d5206736f02d673957fa624d968ca88b5d287efc1c5316a9`,
construit par Trunk puis Tauri debug sans bundle. Copie de zone sous
`E:/OpenRPG_Forge/.gate2-evidence/authoring-density-ui-projects`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`. Fenêtre observée sur l'écran secondaire
(origine -1913,52 ; capture 1913×1080).

Acquis par l'interface réelle : liste → ouvrir la zone → terrain/végétation et
clairière visibles → Jouer → capsule au sol avec la végétation authorée → capture
du curseur → Échap → retour édition. Diagnostics : `Simulation active · sources
CPU concordantes`, tick 1233, position (16,00 ; 3,91 ; 8,00), au sol true.
Au retour : vue éditoriale restaurée, `Session synchronisée`, Jouer disponible,
Retour désactivé ; terrain effectif `e1c72262…` inchangé. Captures inspectées dans
la conversation, pas exportées en PNG dans le dossier de preuves.

Limite distincte : les entrées w, Shift+w et espace injectées par l'outil Windows
ont été reçues avec `code physique absent`. Aucune translation ni saut prouvé par
ces injections ; elles ne valent pas validation du déplacement clavier dans la
WebView. Les tests de contrôleur natif restent des preuves séparées.
Le déplacement réel et l'attente pendant une reconstruction lente restent à
qualifier dans le Studio ; aucun verdict performance ni qualité visuelle globale.

Aucun Save pendant l'essai. Archive avant/après SHA-256
`0b3bf389156f08b067558d5c0240aeda84747fd1c95b47379429ac6c71e1452d`.
PID Studio 19180, descendants 17336,29376,30080,30356,4180,3048,25988 :
fermés et absence vérifiée. Logs locaux `aggregate-playtest-ui.stdout.log` et
`aggregate-playtest-ui.stderr.log` sous `.gate2-evidence`. Aucun processus graphique
conservé. Gate 2 reste ouverte, qualification workspace ADR-0006 non acquise.

Tests moteur après le correctif des troncs : `vegetation::quality::tests` donne
3 PASS, 1 probe CPU explicitement ignoré (benchmark non demandé). Le test auteur
configure des collisions déclarées, vérifie IDs/transformations de la seed finale,
leur conservation après source invalide ; le cycle de qualité conserve les entités
physiques. Compilation 3 min 32 s, exécution 4,62 s.

Clippy tests a ensuite signalé trois comparaisons flottantes exactes dans le test
de barrière précédent et un passage par valeur inutile dans le nouveau helper de
collisions. Corrections : comparaison des bits (gel exact maintenu), emprunt pour
construire le bundle possédé. Pas de tolérance ajoutée, pas de lint désactivé.
Ces ajustements postérieurs au binaire 6c932781 ne changent pas son comportement ;
ils ne constituent pas un nouveau candidat graphique qualifié.
Clippy moteur/tests et Studio WASM/web strict PASS après ces corrections.
Sessions de build/test terminées, diff sans erreur ; avertissement Cargo de
compatibilité future `proc-macro-error2 2.0.1` inchangé. Aucun push/commit.

## Passage joué du bâtiment et retour édition — 22 septembre 2026

Le replay natif automatisé v3 clôt localement R2-033 sur le candidat exact
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`.
Après Save/Close/Open d'un bâtiment verrouillé, « Jouer devant cette porte »
recharge la projection ; l'audit persistant confirme un unique
`start_playtest_at` résolu. La simulation prête démarre devant la façade. W place
le personnage dans la pièce puis contre le mur arrière avec deux contacts ; S le
ramène dehors. Coordonnées locales Z : `-5,2538 → -2,2629 → 2,6346 → -3,8566`.
Le X intérieur `1,6002` reste dans l'ouverture centrée à `1,6`. Pointer-lock
acquis sur le canvas puis libéré par Échap. « Retour à l'édition » recharge à
nouveau ; un unique `stop_playtest` résolu, `Session synchronisée` et le mode
édition sont exigés. L'archive reste strictement identique au snapshot sauvegardé.

Ledger, rapport et quatre PNG :
`artifacts/forge-runtime/gate2-resume-20260919/r2-032-033-building-playtest-v7-20260922-v3/`.
Luna max a inspecté les captures : bâtiment, ouverture et personnage cohérents ;
le texte de diagnostic historique « Simulation active » subsiste sur la capture
finale alors que les boutons et le ledger prouvent le retour édition. Inspection
agent, pas revue humaine. `gate_qualified=false`, CI
`NOT RUN — infrastructure billing`. Aucun verdict Gate 3 ni publication.
