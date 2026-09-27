# Candidat regroupé des outils terrain — 12 septembre 2026

Implémentation : panneau auteur latéral défilant, vue 3D indépendante occupant
l'espace restant, navigation caméra, cinq brosses numériques/ponctuelles et trait
continu, aperçu filaire lié au hash avant Apply. Les brosses sont désactivées
sans document, pendant une commande et pendant le playtest. Une préparation
refusée efface l'ancien aperçu pour éviter une application involontaire.

Clippy Studio WASM strict PASS (3,53 s). Contrôle initial arrêté à 103 lignes
pour AuthoringControls : remise à zéro de l'aperçu centralisée à l'entrée de
send, sans désactivation de lint. Suite domaine du lot précédent : 7 tests PASS ;
non rejouée pour le seul changement de disposition et de garde UI.

## Candidat et essai regroupé

Config externe : `.gate2-evidence/tauri-authoring-brush-workspace.conf.json` sous
`E:/OpenRPG_Forge`. Distinct des candidats de preuve antérieurs.
Fenêtre configurée sur l'écran gauche (-1920, 52), 1920 × 1080.

Build Trunk PASS (Cargo WASM 2 min 32 s), Tauri debug/no-bundle PASS (1 min).
Pas de publication ni d'installer. Le candidat diagnostic précédent a été
conservé hors Git avant remplacement du binaire de travail.

À observer dans cet essai : panneau sans recouvrement, zoom, aperçu numérique,
application et Undo, rayon/picking et tracé ; conserver les limites non vérifiées.
La comparaison des captures ou un hash d'exécutable ne suffit pas à qualifier
la sauvegarde des retouches ou la totalité de Gate 2.

## Résultat de l'essai réel

Binaire `forge-studio.exe`, SHA-256
`31a29fc1bb9b64387df5e1ae293a9240cef96fc565a13110a58a7502950d074d`.
Attention : la ligne finale de Tauri annonce forge_studio.exe, mais le [[bin]]
et le fichier réellement produit utilisent un tiret. Premier lancement tenté
sur ce chemin annoncé inexistant sans effet, puis résolution du vrai fichier.
Le répertoire autorisé doit préexister au lancement ; première ouverture sur
un répertoire absent refusée, processus fermé, répertoire créé puis relance.

Observé par captures Windows dans la session :

- panneau latéral scrollable et terrain neuf visible sans recouvrement ;
- bouton Rapprocher : changement de cadrage, aperçu cyan visible ;
- aperçu numérique : 109 échantillons proposés sans changement de hash source ;
- Apply puis acquittement moteur/Rapier avec hash
  `74a5270408e1366dac4d52a10ff8ecd9a082abd9893c90981dcc9b8ce4252526` ;
- Save depuis Studio : reçu edit révision 2, 109 samples, inverse vide dans l'archive ;
- Undo puis acquittement hash
  `353e655d5d80b0578c2a39e986e0260ac609192fbe0506cadf34ff13c2dd33ea` ;
- Save final : reçu undo révision 3, zéro échantillon appliqué, archive conservée.

Archive externe sous `.gate2-evidence/authoring-brush-workspace-projects/`
`81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`, SHA-256
`1ebd3dec9350c5b256c25b5071989df58225682ca242c20eec5e00db04152fe3`.
Aucune édition JSON manuelle ; lecture de contrôle seulement. Réouverture du
candidat actuel et contrôle intégral des valeurs après recharge non rejoués.

**Défaut concret restant :** Tracer + glisser rapide de (1000,650) à (1280,650)
produit un aperçu ponctuel de 113 échantillons, pas une bande continue. Le picking
et la préparation aboutissent, mais la collecte limitée aux frames ne conserve
pas toutes les positions du geste. Tracé continu NON QUALIFIÉ : conserver les
événements du chemin, les projeter explicitement puis traiter un geste complet.
Cercle de rayon, rotation/déplacement caméra et outils autres que lever/abaisser
restent à qualifier, pas de succès déduit du seul build.

HUD hérité affichant encore Gate 1 / Visual Valley sur ce terrain auteur :
libellé trompeur et encombrement à corriger, sans modifier la fixture historique.
Cadence instantanée observée environ 43–150 FPS selon le focus de cet essai
interactif ; ni benchmark isolé ni verdict de performance Gate 2.

Nettoyage : PID initial 29712 puis essai 27652 fermés ; enfants de l'essai
28444,14976,26480,1308,25324,15784,27716 absents après fermeture. Pas de session
graphique laissée en cours, build terminé, aucune publication Git.

## Correctif de collecte après cet essai

La préparation de brosse ne dépend plus du seul dernier hit de la frame.
L'adapter DOM conserve les positions normalisées du début, des mouvements et
du relâchement, dans l'ordre. Le début est enregistré après activation du geste,
le relâchement avant désactivation : même une paire press/release entre deux
frames fournit ses deux positions. La commande reste préparée une fois au
relâchement, sur le bus existant.

Le système moteur projette le lot de positions sur les colliders terrain via
la même caméra ; chaque position reçoit un hit ou une absence explicite. Un
miss refuse le geste au lieu de relier des segments séparés. Files bornées à
1 024 points et borne globale du chemin maintenue, débordement refusé ; aucune
troncature silencieuse. Le cercle de rayon continue de suivre le dernier point.

Clippy Studio WASM strict PASS, moteur inclus (23,23 s). Fichiers modifiés
formatés. Le binaire 31a29fc... de l'essai ci-dessus **ne contient pas** ce
correctif ; reconstruction et replay du glisser nécessaires avant tout verdict
de correction graphique. Aucun test d'historique inchangé relancé pour masquer
cette validation manquante. Les points sont projetés avec la caméra de leur
frame de traitement ; un changement de caméra concurrent n'est pas qualifié.

## Diagnostics de l'espace auteur

Ajout d'un titre optionnel de présentation moteur ; absent dans les anciennes
scènes de preuve, il conserve leur libellé. Studio auteur fournit « Terrain
auteur / Gate 2 ». Overlay masqué initialement, case « Afficher les mesures dans
la vue 3D » dans les diagnostics rendu/playtest. La collecte n'est pas désactivée.
Clippy Studio WASM strict PASS (3,49 s) après extraction de launch_viewport ;
aucune désactivation de lint. Nouveau candidat événements + diagnostics en
construction sous config tauri-authoring-brush-events.conf.json et dist distinct.
Le candidat précédent 31a29fc... est conservé comme
`.gate2-evidence/studio-authoring-brush-workspace-tested.exe` avant remplacement.

## Replay du correctif — résultat acquis

Trunk PASS (Cargo WASM 2 min 15 s), Tauri debug/no-bundle PASS (39,83 s).
Binaire effectivement lancé `forge-studio.exe`, SHA-256
`442d30658d1ad981aa203bd7101ba8c5a9297030ba7e801e75911e02ef243e20`.

Réouverture de la zone existante : terrain affiché, session synchronisée,
hash moteur `353e655d...` identique à l'archive précédente, sans recréation.
Le même glisser rapide de (1000,650) à (1280,650), rayon 12 m, intensité 0,5,
lever 2 m, fournit **618 échantillons** et une bande filaire continue visible,
au lieu de l'empreinte ponctuelle précédente. Le cadrage est cette fois la vue
initiale sans zoom ; ce n'est pas une comparaison pixel-identique des empreintes.
Apply : bande de relief effectivement affichée, acquittement CPU/Rapier hash
`ffe022eba9182bc36f0335c20177f0f594c748e9b3d976e004b9ed7c2d874dc7`.
Save depuis Studio : archive contenant une seule nouvelle commande edit,
révision 4, 618 samples, inverse vide. SHA-256 final de l'archive
`dd54b814484c1401b256539b91e205e520657f019e9585e1de3884a905b496a9`.
Le trait sauvegardé reste dans le répertoire d'essai ; l'Undo de ce trait exact
n'a pas été rejoué graphiquement (contrat déjà couvert par le test de stroke).

Overlay absent initialement ; case de diagnostics activée : mesures réellement
visibles et titre « OpenRPG Forge | Terrain auteur / Gate 2 » correct. Capture
interactive autour de 75,8 FPS lors de l'affichage, non benchmark isolé.

Limites : courbes rapides, très longs gestes, cercle de rayon, autres opérations,
ergonomie proche, rotation/déplacement caméra et reprise du trait sauvegardé
restent à qualifier. Cette preuve clôt le défaut observé du glisser droit rapide,
pas la totalité des brosses ni Gate 2. Aucun nouvel audit Gate 1 lancé.
PID 19900 fermé ; descendants 28872,20324,1952,30632,11184,29956,29404 vérifiés
absents. Aucun processus d'essai conservé, aucune publication Git.
## Qualification locale complémentaire — 20 septembre 2026

Le candidat apparié 4DB02C62 / WASM 8FE20704 qualifie localement R2-007 à
R2-014 avec les preuves historiques encore valides. Les opérations
lever/abaisser, lisser, aplatir, bruit et pente ont chacune un aperçu courant ;
les cycles Apply/Undo/Redo/Save ont été rejoués sur le candidat courant et une
réouverture finale groupée a restauré 422 deltas de hauteur. Le trait continu
de 618 échantillons du candidat 442d3065 reste la preuve dédiée au drag rapide.
Le picking courant expose cercle, rayon/intensité, 65/65 échantillons et un
appel natif de 109,9 ms. Les tests world 28/28 et engine terrain_update 10/10
établissent le recalcul partiel CPU/Rapier ; la granularité GPU n'est pas
revendiquée. Rapport :
`artifacts/forge-runtime/gate2-resume-20260919/r2-007-014-cu-20260920/r2-007-014-cu-report-20260920.md`.
