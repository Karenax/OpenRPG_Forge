# Collisions des placements manuels — 13 septembre 2026

## Replay Studio du candidat 77049619 — résultat courant

- Binaire local `forge-studio.exe`, SHA-256
  `7704961988370cc6ffe18fe12028fe9e572dda19665a6db2c1aa36a731596f3f`.
  Trunk/WASM PASS (compilation 3 min 49 s), Tauri debug sans bundle PASS
  (1 min 27 s). Premier appel Trunk rejeté avant build par `NO_COLOR=1` ;
  variable corrigée à `true`, pas de modification du code pour ce rejet.
- Lancement 16:07:38, PID 11960, fenêtre secondaire de la configuration locale
  (1920 × 1032 capturés). Une seule fenêtre Forge ouverte par cet essai.
- Catalogue local : ouverture de la zone
  `81de8d42-89b9-58b5-b090-406cf4c63d06`, version 6 existante. Vue générale du
  terrain avec les deux arches ; aucun Apply ni Save pendant ce parcours.
- Jouer déclenche la projection du personnage. Diagnostic réellement lu :
  `Simulation active`, tick 5347, position `(16.00, 3.91, 8.00)`, `au sol true`,
  caméra ThirdPerson. Le franchissement de la barrière sur les placements
  chargés est acquis ; ce n'est pas une preuve de contact avec leurs solides.
- Après clic dans le canvas, les séquences de commandes évoluent. Une impulsion
  `w` n'a produit aucun déplacement observable : position identique aux ticks
  9070 et 9634. Ne pas qualifier le clavier/mouvement ni en déduire sa cause.
- Retour à l'édition via le bouton : session synchronisée, vue générale et
  objets réaffichés. Terrain inchangé
  `7d816331a3647dd0edd03b9aecd6437c75fa471e34f19966c8fe0150d6ff6be2`.
- Fichier `authoring.json` avant/après identique : 2 560 237 octets, SHA-256
  `fc2dbcaa59f8d6f1222c777abdc8b0b1ca5c87fa9195769bf81aec74d15f6f4f`.
- Fermeture Alt+F4, puis absence vérifiée de la fenêtre et des PID 11960,
  32748 (console), 32460 (WebView), ainsi que de leurs enfants directs observés.

Limites et prochaine action : le point de départ fixe `(16,8)` est loin des
arches `(-84,85)` / `(-90,85)`. Le passage sous l'arche, les contacts solides,
les transformations rotation/échelle et le mouvement clavier restent dus.
Prévoir un démarrage de playtest à un endroit choisi par l'auteur, utile à
toutes les zones, puis qualifier le parcours de contact sur ce même objet.
La barre d'actions reste visible au défilement, mais Jouer/Retour et les
diagnostics sont encore trop éloignés. Le ciblage UIA du diagnostic l'a indiqué
hors fenêtre ; le clic sur sa position visible a fonctionné après mise à jour.
Ne pas déclarer ce problème de ciblage définitivement corrigé.

Captures inspectées dans la session de contrôle Windows : ouverture de zone,
mode joueur, diagnostic et retour édition. Aucun benchmark, aucune nouvelle
qualification de Gate 1 ni validation complète de R2-041 déduite.

## Raccord au playtest — état courant

Le refus systématique des placements dans le snapshot est remplacé par une
capture immutable de leur liste, couverte par le hash de tête existant. Le host
peut préparer le jeu sans écriture implicite. Avant chaque Drive, le Studio
exige la concordance snapshot/placements acceptés/projection demandée, aucune
requête de remplacement en attente, les racines exactes (ID, asset/hash,
transform et métadonnées) et leurs primitives appartenant à la même hiérarchie.

Le contrôle physique relit le backend vivant, pas le statut du frame précédent :
contexte par défaut, correspondance entité/handle, forme partagée, pose et échelle
cohérentes, transforms locaux propagés, collider statique actif et non sensor.
La tolérance numérique est 0,0001 m/échelle et 0,00001 quaternion. Une matrice
avec cisaillement non représentable par Rapier reste en attente (pas de
déformation physique approximative autorisée silencieusement).

Implémentation en cours de qualification, pas un verdict graphique :
- Clippy Studio WASM strict PASS sur le raccord initial (29,13 s) et final
  (25,94 s). Avertissement amont futur `proc-macro-error2` conservé.
- Tests `manual_placements` : 5 PASS, dont snapshot immuable, archive intacte,
  tête périmée refusée et réouverture exacte ; `density_inspection` : 2 PASS.
- `zone_host` : ancien test de refus devenu incompatible avec le nouveau
  contrat, remplacé par entrée/sortie avec placements, tête/fichier inchangés et
  sauvegarde explicite ensuite. Replay PASS (0,03 s).
- Tests moteur filtrés `manual_` : 14 PASS (dont 3 physiques pertinents),
  325 non rejoués. Compilation 3 min 16 s, exécution 0,06 s. Le nouveau test
  `manual_projection_rejects_preview_stale_backend_and_wrong_source` vérifie
  une racine + collider enfant : refus avant chargement, acceptation après
  synchronisation, hash d'asset différent, geste transitoire, transform local
  non propagé, pose backend en retard, ancien objet face à cible vide et
  suppression de primitive. Fixture cubique, sans rendu ni déplacement joueur.
- Au moment des tests CPU, aucun parcours graphique ; replay 77049619 ajouté
  ci-dessus ensuite, avec les limites de mouvement et contact conservées.

Reste le parcours réel Jouer → collisions
solides/passage de l'arche → retour édition → sauvegarde/réouverture, avec
rotations/échelles et erreurs. Les sections suivantes décrivent les preuves
précédentes, avant ce raccord ; leur ancien refus systématique est historique.

## Implémenté

Le Studio marque les primitives mesh sous un nœud UCX appartenant au nom du
kit vérifié (limite de parcours 128 parents). Les LOD visuels ne sont pas utilisés
comme collision et les nœuds UCX restent invisibles. Marqueur dérivé portant
l'EntityId Forge du placement, sans changement de document ou de GLB.

`ManualPlacementCollisionPlugin` construit un collider par primitive. Le
constructeur convex partagé est extrait du chemin du kit gouverné, qui le
réutilise aussi : mêmes contrôles positions Float32x3, nombre minimal, valeurs
finies et échec de convex hull. Aucune enveloppe globale de l'arche, aucun box
de remplacement implicite. Collider absent tant que le mesh manque ; échec
marqué et pas de retry silencieux à chaque frame. Suppression des entités suit
le cycle existant de la projection ; aucune seconde simulation Rapier ajoutée.

## Vérification

Complément : liste exacte des entités primitives conservée sur chaque racine.
Un observateur distingue absence de UCX, attente, échec et enregistrement ; il
vérifie le marqueur du placement, les doublons, le couple handle/contexte,
la correspondance entity2collider et la présence dans le backend Rapier.
Le résultat est recalculé, pas mémorisé définitivement après le premier succès.
Le Studio expose le nombre enregistré, l'attente et les erreurs, sans autoriser
le playtest. Ce constat d'enregistrement ne certifie ni transform synchronisé,
ni correspondance à la tête acceptée, ni passage réel d'un personnage.

Clippy Studio WASM strict PASS après ce complément (26,91 s).
Test `manual_readiness_requires_real_backend_and_exact_primitives` PASS :
Collider seul en attente, véritable enregistrement après FixedUpdate Rapier,
doublon et primitive invalide refusés, contexte disparu remis en attente,
entité supprimée refusée, liste vide non prête. Fixture cubique CPU sans rendu,
pas une preuve de collision du GLB ou du personnage. Compilation 2 min 47 s,
un test exécuté en 0,01 s (337 autres non rejoués).
L'affichage du nouveau statut n'a pas été qualifié graphiquement.

- Clippy Studio WASM strict PASS (27,83 s) après le raccord initial.
- Test CPU `manual_collision_prepares_once_and_keeps_invalid_primitives_failed`
  PASS : une forme valide, primitive invalide marquée, aucun doublon au tick
  suivant, positions source inchangées. Compilation native 3 min 53 s, test 0,01 s.
- Test existant `collider_waits_for_mesh_then_materializes_without_rewriting_source`
  PASS : attente du mesh, matérialisation et source intacte (0,01 s).
- Formatage et diff contrôlés. Aucun enregistrement Rapier ni mouvement du joueur
  déduit de ces deux tests de préparation.
- Aucun rendu ou playtest lancé par ce lot.

## Encore requis avant ouverture du playtest

- Qualifier dans le Studio les primitives attendues par placement et l'absence de UCX
  (contrôle implémenté, issu de l'instance chargée ; pas une qualification du kit).
- Relier la disponibilité à la tête exacte et aux transforms acceptés, pas à un aperçu.
- Intégrer le statut d'enregistrement Rapier à la barrière de source/transform.
- Qualifier l'affichage des erreurs et de l'attente dans le Studio.
- Étendre le snapshot et sa barrière, puis seulement remplacer le refus actuel
  des placements dans `PlaytestSnapshot::from_zone`.
- Qualifier source inchangée, entrée/sortie du jeu, collisions solides et passage
  de l'ouverture de l'arche, y compris après retouche de transform/réouverture.

Le refus actuel du playtest avec placements est conservé. Cette préparation
ne clôture pas R2-041 ou Gate 2 et ne prouve pas la physique en mouvement.
Aucun asset acquis, modifié ou publié, aucune donnée Cadle utilisée.
