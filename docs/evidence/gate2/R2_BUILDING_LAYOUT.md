# R2-032/033 — Plan géométrique d'une pièce

## État courant du raccord canonique

### Sélection directe dans la vue — candidat 4C953E54 observé

Le bouton « Sélectionner un bâtiment dans la vue » arme un clic ponctuel sur
les meshes visibles, reliés à `BuildingPartIdentity`. Le premier mesh touché
fait écran aux suivants ; un objet non bâtiment ou un clic vide retire la
sélection. L'ID alimente le même inspecteur que la liste, sans commande ni
écriture de zone. Le clic est lié au hash accepté au moment de l'armement :
changement de source, projection non concordante, aperçu bâtiment et playtest
refusent la sélection. Outils de brosse/placement et sélection bâtiment se
désarment mutuellement ; quitter la vue ou Échap dans le canvas annule.
Fmt et Clippy Web strict PASS (5,96 s), après retrait d'un import inutilisé.
Observation ciblée acquise sur `building-picking-studio-20260914-0600/forge-studio-candidate.exe`
sous `E:/OpenRPG_Forge/.gate2-evidence`, SHA-256
`4C953E54805A07FFF2F808ECB3931A1A7332B218F68DC25C4B4B4036C87E27A3`.
Trunk terminé code 0 (Rust 25,73 s), Tauri code 0 (14,44 s).
Archive B4FB1904 rouverte dans `building-picking-ui-20260914-0600` :
clic de vue d'ensemble sur Auberge, inspecteur et cadrage corrects ; réarmement
puis clic sur la façade de la copie : ID 9f568837 et inspecteur « Auberge — copie » ;
réarmement puis clic sur le ciel : sélection vide, inspecteur retiré.
Captures inspectées dans la session Computer Use. Aucun Save ni Apply ; archive
inchangée SHA-256 `B4FB1904F0CD1C37DE69668545A512B3FFBE95A84C3047996CD93C36A1542433`.
PID 13420 et sept descendants relevés puis tous absents après fermeture normale.
Limites : scénarios d'occlusion, de changement de révision entre armement/clic
et d'annulation non rejoués graphiquement ; pas de surbrillance 3D ni hiérarchie
universelle qualifiée. Aucun gain de performance ni verdict global Gate 2.

### Sélection explicite de bâtiment — candidat 82A6A72D observé

14 septembre : candidat `building-selector-studio-20260914-0520/forge-studio-candidate.exe`
sous `E:/OpenRPG_Forge/.gate2-evidence`, SHA-256
`82A6A72D4933E01D207622C7E274E1EA9D19C739E69B68B1767107F12B5F0C43`.
Trunk et packaging Tauri terminés code 0. Réouverture de l'archive B4FB1904
dans `building-selector-ui-20260914-0520` acquise : deux bâtiments rendus,
sélection de « Auberge — copie » puis « Auberge », un seul inspecteur affiché,
cadrages distincts observés et capturés. Positions affichées (30,3,20) et
(20,3,20), dimensions 8 × 6 × 3 m et retouche de porte 1,5 m conservées.
Aucune sauvegarde ni mutation durant cet essai ; SHA-256 de l'archive final
`B4FB1904F0CD1C37DE69668545A512B3FFBE95A84C3047996CD93C36A1542433` inchangé.
Fermeture normale : PID 28976 et sept descendants relevés, tous absents après
fermeture. Aucun gain FPS, franchissement joué ou hiérarchie universelle déduit.

Le panneau expose un sélecteur « Bâtiment à inspecter » : noms, verrou et ID
désambiguïsent les pièces ; un seul inspecteur est affiché, ouvert par défaut.
La sélection conserve l'ID, pas l'index ni une copie du document. Les données
de l'inspecteur proviennent de la tête courante ; un ID absent affiche l'invite
de sélection sans choisir silencieusement un autre bâtiment. Les commandes,
prévisualisations, verrous et limites de playtest restent inchangés.
Fmt et Clippy Studio Web strict PASS (6,09 s). Le candidat D29317A0
ci-dessous utilisait encore les inspecteurs empilés. R2-006 reste partiel : pas
de hiérarchie universelle terrain/placements/splines déduite de ce panneau.

### Candidat D29317A0 — renommage et duplication Studio acquis

14 septembre : `E:/OpenRPG_Forge/.gate2-evidence/building-authoring-studio-20260914-0505/forge-studio-candidate.exe`,
SHA-256 `D29317A0C2D23317D6E8CAEAC846F8D0295F9E088409C9FF971D64340FDAA0F1`.
Frontend `building-authoring-web-20260914` : compilation Rust 1 min 20 s,
Trunk terminé code 0 ; Tauri 30,32 s terminé code 0. Ancien dossier
`building-authoring-ui-20260914` déjà présent : préservé, nouveau dossier
`building-authoring-ui-20260914-0505` utilisé à partir de BA9B8BE5.

Parcours Computer Use sur écran secondaire, sans JSON manuel : ouvrir, cadrer
la pièce, saisir « Auberge », examiner résumé et Apply ; déplier duplication,
examiner la proposition « Auberge — copie » à (30,3,20), Apply ; Undo puis Redo,
Save. Captures inspectées : aperçu cyan de deux pièces, retour à une pièce après
Undo, deux volumes bâtis après Redo. Porte 1,8 × 2,2 m, correction X=1,5 m
conservées dans le résumé ; original à (20,3,20), ID 3286c3e1 ; copie ID
`9f568837-2934-5a3f-b606-993713ee5fbd` identique après Redo. Adapter 8 → 16 → 8 →
16 volumes avec collisions fixes ; pas de franchissement joué déduit.

Archive de copie isolée : 31 commandes, tête
`df48b672a6a763437e3a5c28fb98c88c05308aaffda942d4a1547f4939573c3c`, SHA-256
`B4FB1904F0CD1C37DE69668545A512B3FFBE95A84C3047996CD93C36A1542433`.
Session synchronisée après Save. Source BA9B8BE5 intacte. PID 14208 et WebView/
console 31432,24724,21080,32136,33088,12632,30136 fermés, absence vérifiée.
vctip 10756 apparenté par un ancien PID réutilisé est antérieur au lancement
(03:18:49) : préservé, pas considéré comme processus de cet essai.

Limites : pas de redémarrage/réouverture UI de cette archive, pas d'Undo UI du
renommage ni geste sur copie verrouillée. Les preuves domaine correspondantes
restent distinctes. Réutilisation de végétation présente dans ce binaire mais
non mesurée/diagnostiquée pendant ce replay. Captures inline, aucun PNG exporté.
Gate 2 ouverte ; ne coche pas R2-030/032 dans leur totalité.

### Duplication réversible des bâtiments

`BuildingEdit::Duplicate` copie les paramètres acceptés vers une nouvelle identité,
un nom et une position explicites. L'original, même verrouillé, reste intact ;
la copie est déverrouillée, avec orientation/dimensions/override de porte conservés.
Le domaine refuse ID existant/inconnu, nom invalide, emprise hors terrain et quota
dépassé via les validations communes. Pas de nouvelle archive : correction de zone
et inverse existants. Le Studio prépare un ID déterministe lié à la tête/source/
nom/position et propose « Dupliquer ce bâtiment » dans l'inspecteur, avec aperçu.
Position suggérée X + largeur + 2 m, modifiable ; aucune résolution de collision
ou de chevauchement promise, avertissement explicite avant application.

Test `duplicate_locked_building_is_independent_and_reversible` PASS : original
verrouillé intact, copie indépendante, aperçu sans mutation, retry, collision d'ID,
réouverture archive delta/Undo/Redo et hors terrain (compilation 14,90 s).
Clippy a refusé une comparaison flottante du test : remplacement par comparaison
des bits, car la copie doit être exacte. Geste Studio et rendu de deux bâtiments
encore à observer dans le prochain candidat regroupé ; 21854D33 reste antérieur.
Validation finale : les 7 tests `building_authoring` PASS (0,03 s), Clippy
domaine/test et Studio Web strict PASS (30,42 s côté Web), fmt appliqué.

### Renommage auteur des bâtiments

Commande `BuildingEdit::SetName` ajoutée au domaine et champ « Nom du bâtiment »
dans l'inspecteur avec aperçu explicite. L'ID de sélection, la pose, les dimensions,
la correction de porte et le verrou ne changent pas. Le verrou interdit le renommage
comme les autres mutations ; nom vide/blanc ou supérieur à 128 octets refusé par
la validation existante. Pas de nouvelle représentation persistante : les archives
enregistrent toujours la correction canonique et son inverse.

Test `building_name_edit_preserves_identity_geometry_and_archive_history` PASS
(0,01 s ; compilation 14,97 s) : aperçu sans mutation, retry idempotent, exactitude
des trois archives, Undo/Redo après reprise, noms invalides et verrou. Clippy domaine
bibliothèque/test PASS (3,47 s). Saisie/aperçu du nouveau champ dans le candidat
Studio encore à observer ; 21854D33 est antérieur. Aucun critère global coché.
Clippy Studio Web strict PASS (30,33 s), fmt et contrôle de diff PASS.

### Révisions Studio — réutilisation de la projection terminée implémentée

Le chemin moteur reconnaît maintenant les documents identiques hors révision,
à emprises/corridors inchangés. Il conserve les entités/meshes et actualise les
reçus terrain/population/politique. Statut explicite
`reused_settled_geometry_new_source_receipt`. Aucun format canonique changé.
La réutilisation exige des hashes précédents concordants, une qualité projetée
conforme, les bindings/meshes présents et aucune préparation native/WASM ou
étape d'upload en attente. Une source changée, un aperçu gouverné ou une erreur
interdisent ce raccourci ; les contrôles asynchrones antérieurs sont conservés.

Premier test `revision_only_room_edit_keeps_live_entities_and_acknowledges_new_sources`
PASS : révisions terrain/végétation avancées, porte élargie sans changer l'emprise,
entités conservées et nouveaux hashes reconnus ; déplacement ensuite reconstruit.
Le test a ensuite été renforcé avec le statut explicite de réutilisation.
Les cinq tests `vegetation::quality::path_tests` PASS sur cette version renforcée
(0,03 s ; compilation 55,87 s), y compris chemins/rivières et transitions de qualité.
Fmt, diff check, Clippy moteur bibliothèque/tests (20,66 s) et Studio Web
(25,46 s) PASS. Import générique du nouveau module refusé à la première passe
Clippy, remplacé par des imports explicites ; aucun lint désactivé.
Ce code est postérieur au candidat 21854D33 : aucun gain FPS/latence ou geste
Studio qualifié pour cette réutilisation. La projection du terrain elle-même
n'est pas optimisée par ce lot.

### Invalidation moteur des retouches sans changement d'emprise

Le moteur valide toujours les bâtiments reçus, mais conserve les préparations
et demandes de qualité si terrain/végétation/corridors sont identiques et si le
hash d'emprise ne change pas. Les métadonnées acceptées sont actualisées sans
annuler les uploads. Test `room_metadata_preserves_vegetation_and_pending_quality_change`
PASS (0,01 s ; compilation 55,92 s) : renommage/verrou/largeur et décalage de porte,
demande Medium conservée, porte invalide refusée, déplacement invalidant.
Fmt et Clippy moteur bibliothèque/tests strict PASS (20,60 s).

Limite importante établie par lecture de `ZoneAuthoringHead::effective_unchecked` :
le Studio reporte la révision de tête dans terrain et végétation à chaque édition.
Ce chemin moteur à documents identiques ne supprime donc pas les reconstructions
liées à ces révisions Studio. Une réutilisation distincte du contenu dérivé et
des reçus de révision reste à implémenter sans accepter un résultat asynchrone
périmé. Aucun gain FPS/latence Studio revendiqué. Le candidat 21854D33 précède
cette modification moteur.

### Candidat 21854D33 — exclusion et largeur de porte observées

14 septembre : `E:/OpenRPG_Forge/.gate2-evidence/building-clearance-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `21854D33BD893276DA2D4D8D4701CC62D050A165A052F6BE2C23CA5B6EA1FD0F`.
Frontend `building-clearance-web-20260914` : compilation Rust 1 min 15 s,
Trunk terminé code 0 ; Tauri 19,48 s, code 0. Premier lancement Trunk refusé
sur NO_COLOR=1 ; relancé avec NO_COLOR=true avant toute compilation.

Copie isolée `building-clearance-ui-20260914`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`, initialement archive E9DFB69E.
Dans le Studio : ouvrir, bâtiments, pièce 3286c3e1, cadrer porte. Capture
inspectée : sol intérieur visible dégagé, végétation extérieure conservée.
Saisie largeur de passage 1,4 → 1,8 m ; aperçu cyan et résumé 1,8 × 2,2 m,
décalage retouché X=1,5 m conservé ; Apply puis Save. UIA set_value refusé par
le fournisseur : saisie clavier après sélection visuelle du champ utilisée.

Archive sauvegardée v15 : 27 commandes, tête
`7fab24cb7212b327ce4f0a367483f68c6d47f5962db8d2564c4fc7e688614a1c`, SHA-256
`BA9B8BE52E25F226B2125CF41FA2F1171DADA3CC63DB3E295D1B1E9DBBB752AD`.
Dernière commande d8ae91d6 : bâtiment seul changé ; density, placements,
paths, material_overrides et height_deltas restent keep. Fermeture complète,
relance même EXE, Open puis Jouer devant cette porte : avatar devant ouverture
élargie et sources gelées observés. Archive identique après playtest.

Captures inspectées directement dans la conversation, pas de PNG exporté.
Écran secondaire ; sessions 13988 et 28240 et leurs descendants fermés par
Alt+F4 puis absence des PID contrôlée. Original E9DFB69E préservé.
Limites : vue ne prouve pas chaque point caché ; branches issues de l'extérieur
non découpées ; nouvelles épaisseurs/hauteur non saisies dans ce replay ; Undo UI
et franchissement clavier restent dus. Aucun verdict global R2-032/033.

### Exclusion végétale des emprises — raccord implémenté, rendu restant

`forge_world::VegetationGenerationPlan::from_documents_with_buildings` ajoute
des rectangles tournés dérivés des bâtiments validés, séparés des masques et
du quota d'exclusions authorées. Rejet des points d'implantation avant acceptation
de l'espacement. Pas de modification du document végétation, de sa seed, de ses
masques ou de ses verrous. Hash dérivé des seules emprises : sans bâtiment,
identité antérieure conservée ; déplacement change l'identité, renommage/verrou
ne la change pas. Limite 32 bâtiments héritée du domaine.

Deux tests PASS (0,10 s) : cinq rotations et points intérieurs/extérieurs ;
génération réelle avec points auparavant présents dans l'emprise, tous exclus
ensuite, population extérieure non vide, document intact et identités contrôlées.
Fmt/Clippy world bibliothèque+tests PASS (6,12 s).

Raccord ajouté aux bindings moteur, préparateurs synchrone/natif asynchrone/WASM
incrémental et barrière playtest Studio : les bâtiments acceptés participent à
l'identité de population et à l'invalidation commune. Clippy Studio Web strict
PASS (27,63 s). Première commande incorrecte ciblait le binaire desktop en WASM
et a échoué ; commande corrigée avec `--lib --no-default-features --features web`.
Test moteur `building_sources_project_and_removal_restores_population` PASS
(0,01 s, compilation 1 min) : projection/hash attendus, répétition sans changement,
refus atomique d'une rotation NaN et retrait restaurant l'identité initiale.
Clippy moteur bibliothèque+tests strict PASS (19,15 s), après extraction de la
fixture de pièce : première passe refusée pour longueur du nouveau test, sans
désactivation du lint. Fmt appliqué. Aucun processus graphique ouvert pour ce lot.
Ne pas déclarer le défaut visuel corrigé dans le candidat 707BA511, antérieur
à ce raccord. Packaging et observation graphique regroupée encore dus.
La règle porte sur les racines dans l'emprise des pièces de plain-pied ; elle
ne découpe pas les couronnes/branches débordant depuis l'extérieur.

### Dimensions complètes accessibles à l'auteur — code du 14 septembre

L'inspecteur expose maintenant largeur/hauteur du passage de porte et épaisseurs
des murs et du plancher/toit, en plus des dimensions extérieures déjà présentes.
Même `SetDimensions` typé, parse fini, contrôle métier d'ouverture, verrou,
aperçu avant Apply et sauvegarde explicite. Le résumé de proposition affiche
également ces quatre paramètres ; aucune modification silencieuse du décalage
de porte retouché. Aucun nouveau format, aucune nouvelle règle de géométrie.

Fmt et Clippy Studio WASM strict PASS (25,69 s ; avertissement amont futur
inchangé). Test de conservation étendu aux quatre paramètres : régénération,
archives normale/compacte/delta, Undo rétablissant toutes les dimensions, Redo
exact et décalage manuel 1,5 m conservé, PASS (0,01 s). Ce test porte sur le
contrat de commande ; saisie/aperçu/appliquer des nouveaux champs dans le Studio
encore non observés. Le candidat 707BA511 ne contient pas ce complément UI.

### Franchissement physique natif — contrôle ciblé du 14 septembre

Nouveau test `authoring_buildings::passage` : projection de production des huit
volumes, Rapier et `PlayerControllerPlugin` avec le profil utilisé par le Studio.
Huit scénarios indépendants (rotations 0/90/180/-90°, voie porte ou mur voisin),
sol synthétique plat au niveau du plancher, capsule 1,8 m, ouverture 1,4×2,2 m
retouchée à X local 1,5 m. Pas de substitut géométrique aux murs de production.

Une seconde de commande avant : capsule à l'intérieur par l'ouverture,
maintenue à l'extérieur par le mur voisin. Retour de 1,5 s par la porte :
capsule au-delà du repère extérieur Z < -4 m. Contrôle de dérive latérale,
contact sol à l'entrée, un tick physique par pas 1/60 s et hash source inchangé.
Premier essai avec retour limité à 1 s : assertion de distance échouée à
Z=-3,563923 (déjà extérieur), temps de freinage non inclus dans le scénario.
Retour porté à 1,5 s, assertion de distance conservée. Replay PASS : 1 test,
8 scénarios, 0,18 s d'exécution après compilation 55,73 s. Fmt appliqué et
Clippy moteur `--lib --tests --locked -- -D warnings` PASS (29,54 s).

Preuve CPU native avec commandes rejouées, pas une capture de franchissement
au clavier Studio. Aucun terrain utilisateur modifié, aucune fenêtre/GPU lancé.
R2-033 reste ouvert pour le parcours produit ; ne pas répéter ce test inchangé
comme remplacement de cette preuve UI manquante.

### Candidat regroupé 707BA511 — raccord observé le 14 septembre

Trunk code 0 (Rust 1 min 19 s), Tauri code 0. Exécutable
`E:/OpenRPG_Forge/.gate2-evidence/building-facing-studio-20260914/forge-studio-candidate.exe`,
SHA `707BA5115CEC78D4FB778E7CD3CF1BEEB22FBF03330BB32C6AF87F5A1BFF17B7`.
Copie isolée `building-facing-ui-20260914`, fenêtre secondaire 1602×932.
Captures inspectées : l'éclairage ponctuel 100 000 lm distingue maintenant
l'ouverture entière, son encadrement et le mur intérieur du plan de façade.
« Jouer devant cette porte » place l'avatar devant l'entrée avec la vue dans
son axe, caméra à l'extérieur (contrairement aux précédents candidats).
Désactivation de l'éclairage en playtest observée : retour à la façade sombre.
Ce résultat qualifie le raccord d'inspection et le départ pour cette pièce,
pas l'éclairage final, les bâtiments tournés en UI ni le franchissement joué.

Aucun mouvement ni Apply/Save ; archive avant/après E9DFB69E inchangée.
Fermeture normale, PID 19052 et descendants 28840,27940,9188,28824,30868,20204,28700
vérifiés absents. Ne pas reconstruire/rejouer ce candidat inchangé pour ces
mêmes contrôles. Suite R2-033 : passage physique et parcours d'édition agrégé.

### Départ orienté vers la porte — 14 septembre, code et tests ciblés

Le bouton de départ calcule désormais le cap depuis la rotation du bâtiment
accepté (local +Z, donc yaw + 180°). `PlaytestStart.facing_degrees` est optionnel,
validé fini dans le domaine et transmis par le même hôte natif. Les anciens
messages X/Z restent lisibles et conservent leur représentation quand le cap
est absent. Le cap ne fait pas partie de la recette, du hash de zone ou d'un
spawn sauvegardé. Le viewport réutilise `with_player_camera_target` et le rig
existant, sans autre contrôleur ou mutation ECS du document.

Tests ciblés PASS : départ temporaire/identité inchangée + JSON ancien/nouveau
et cap NaN refusé ; hôte natif/Inspect sérialisé conservant le cap sans écriture
du fichier ; calcul départ/cadrage/cap pour rotations 0/90/180/-90°. Trois tests,
aucun échec. Fmt et Clippy Studio WASM strict PASS (30,56 s ; avertissement amont
de future incompatibilité proc-macro-error2 inchangé). Qualification workspace
du contrat partagé encore due avant clôture de gate. Pas encore packagé ni
essayé en mouvement : ne pas confondre
orientation calculée et porte réellement franchie. Regrouper l'essai avec le
dernier éclairage compilé, sans rejouer les candidats antérieurs inchangés.

### Lumière d'inspection à distance — code révisé après essai

`authoring_lighting.rs` ajoute une lumière ponctuelle enfant de la caméra active,
supprimée lors de la désactivation/changement de caméra ; pas de shadow map.
L'ambient de travail descend à 250. Aucun document, matériau ou collider modifié.
Le candidat `building-depth-light-studio-20260914/forge-studio-candidate.exe`,
SHA `9F38E462CB029AA11E6061E6A39D34C85F8D39D225B6DE94CF2F82B6EFBEBFE2`,
a été construit (Trunk Rust 26,03 s, Tauri code 0) et essayé avec 2 000 lm.
Capture réelle du cadrage porte : profondeur encore insuffisamment lisible,
donc qualification refusée. Pas de mouvement joué. Archive E9DFB69E inchangée ;
PID 33548 et descendants 33088,13032,32484,18776,30984,27920,33436 vérifiés absents.

Inspection des sources épinglées Bevy 0.19.1 : intensity exprimée en lumens,
absence de visibilité parent traitée comme visible. Le code passe à 100 000 lm
pour l'exposition extérieure : ordre de grandeur isotrope 500 lux à 4 m contre
80 lux à 10 m avant atténuation de portée (24 m). Ce dernier réglage est **compilé
mais pas encore packagé/observé** ; ne pas attribuer au candidat 9F38E462.
Fmt et Clippy Studio WASM strict PASS (6,24 s), avertissement amont futur inchangé.
Ne pas déduire une qualification artistique ou physique de ces calculs.

### Essai ciblé 4EFAFB4C — éclairage de travail, 14 septembre

Trunk terminé code 0 (compilation Rust 25,55 s), construction Tauri terminée
code 0. Candidat `E:/OpenRPG_Forge/.gate2-evidence/building-work-light-studio-20260914/forge-studio-candidate.exe`,
SHA `4EFAFB4C32BFDC1F53A9A9527D5102932DB639BC39F2976FBA3CA9A9AE921978`.
Copie isolée `building-work-light-ui-20260914`, fenêtre 1602×932 secondaire.
Ouverture, cadrage porte et captures avec éclairage actif/inactif/réactivé
effectués. Façade grise en mode travail, presque noire en mode normal :
bascule et restauration visuelles observées. Aucun mouvement/playtest dans cet essai.

Limite révélée : l'ambient uniforme ne sépare pas clairement le mur intérieur
de la façade dans l'ouverture. Ne pas déclarer la lisibilité de la porte résolue,
ni déduire un défaut de géométrie de cette seule image. Prochain correctif :
éclairage d'inspection donnant un relief/profondeur lisibles, sans mutation de
la recette ni changement des dimensions de la pièce de preuve.

Aucun Apply/Save ; archive avant/après SHA
`E9DFB69E05210FBD7AB339D1B1336010169C83DB0962E111E93B1ABC163B2788`.
Fermeture normale ; PID 22212 et descendants 25748,6824,33180,19068,708,20000,26280
vérifiés absents. Pas de nouvelle campagne performance, pas de qualification
Gate 2 et aucune publication. Les réserves de la section suivante sont historiques
pour la compilation initiale ; le présent essai qualifie uniquement la bascule.

### Éclairage de travail — implémenté, qualification visuelle en attente

Le viewport d'auteur propose maintenant une case « Éclairage de travail ».
L'adapter applique temporairement un ambient de 3 000 aux caméras 3D, conserve
leur composant initial et le restaure à la désactivation (ou retire le composant
s'il était absent). Les nouvelles caméras de playtest sont également couvertes,
ce qui est explicitement indiqué dans le libellé. Aucun changement du document,
des matériaux, du kit ou du runtime final ; aucune écriture répétée lorsque le
mode reste identique. Ce réglage d'inspection ne remplace pas un environnement
de jeu authoré et ne qualifie pas son éclairage.

`cargo fmt --package forge_studio` et Clippy Studio WASM avec warnings projet
en erreurs : PASS le 14 septembre (5,93 s pour Clippy). Avertissement de future
incompatibilité amont `proc-macro-error2 2.0.1` toujours présent. Le candidat
7DD26394 ci-dessous ne contient pas ce changement. Construction regroupée,
capture et contrôle de la restauration encore à faire ; lisibilité non validée.

### Candidat regroupé 7DD26394 — essai réel du 14 septembre

Trunk PASS (compilation 1 min 19 s), Tauri PASS (30,25 s). Exécutable :
`E:/OpenRPG_Forge/.gate2-evidence/building-door-access-studio-20260914/forge-studio-candidate.exe`,
SHA `7DD263942AF6D369F00B3F111C2837F8E6A87FF1479D0B170788C75B9969E36E`.
WASM `E8DD5F35FE012DC6C7D36B401594A45D49326EB9C15068165187D5D397E8127F`.
Kit gouverné précédent réutilisé, aucune acquisition. Copie d'essai
`building-door-access-ui-20260914`, archive initiale E9DFB69E, identique à la
source vérifiée avant copie. Fenêtre 1602×932 sur écran secondaire.

Acquis par gestes et captures inspectées : Open retrouve position (20,3,20)
et porte retouchée à 1,5 m ; « Cadrer la porte » et « Cadrer ce bâtiment »
changent la vue côté façade ; « Jouer devant cette porte » démarre le mode
playtest avec avatar devant l'ouverture. L'ouverture rectangulaire est visible
depuis la caméra à l'intérieur, derrière le joueur extérieur. Aucun mouvement
ni franchissement joué attesté. Aucun Apply/Save, archive disque inchangée.

Défaut observé : façade à l'ombre et intérieur presque noirs, ouverture difficile
à distinguer dans le cadrage d'édition. Ne pas qualifier la lisibilité/qualité
du bâtiment ; corriger l'éclairage de l'environnement d'édition avant nouvelle
qualification. Conservation des handles sur retouche d'une autre couche non
mesurée pendant cet essai (preuve moteur séparée). Aucun verdict R2-032/033.
Fin d'essai : PID 30136 et descendants 3636,7604,3028,30428,31804,3088,25560
vérifiés absents. SHA disque final E9DFB69E inchangé. Aucune publication Git.

### Accès au playtest devant la porte — code raccordé

« Jouer devant cette porte » relit le bâtiment par ID dans l'état accepté,
calcule un départ à deux mètres devant l'ouverture effective puis utilise
le même préparateur StartPlaytestAt que la saisie X/Z. Ni position de caméra,
ni brouillon de formulaire, ni coordonnée de carte de preuve codée en dur.
Départ hors terrain refusé par la validation existante ; absence de bâtiment
ou playtest déjà actif refusés. Hash courant et barrières de projection conservés.
Pas de modification des sources ni de sauvegarde implicite.

Le départ reste sur le terrain : pas de garantie d'accès à une pièce surélevée,
pas d'évitement automatique d'autres objets et orientation de vue inchangée.
Ces limites sont affichées ; le bouton ne prouve pas le franchissement.
Clippy Studio WASM strict PASS (30,74 s, avertissement futur inchangé).
Test `authoring_building_view` PASS : décalage local de porte et recul de 2 m
conformes à 0/90/180/-90°, en plus des assertions de cadrage ; 22,59 s de build,
exécution <0,01 s. Aucun nouveau candidat lancé. Vérification graphique à
regrouper avec les corrections de cadrage et d'invalidation ci-dessous.

### Porte verrouillée et clairière — reprise fichier combinée

Le diagnostic de régénération ajoute `BuildingConstraintChanged` avec ID, état
du verrou et raison issue du validateur de bâtiments. Il conserve aussi le
diagnostic général du document invalide ; aucune correction automatique de pose.
Le refus est appliqué même si l'appelant envoie directement Regenerate.

`cargo test -p forge_project --test building_authoring --locked` : 5 PASS,
compilation 14,96 s, exécution 0,03 s. Nouveau scénario synthétique combiné :
masque de densité corrigé/verrouillé, porte décalée à 1,5 m puis bâtiment verrouillé,
nouvelle seed terrain/végétation et ordre de couches inversé ; corrections
strictement identiques. Emprise incompatible : conflit avec ID/verrou, aucun
changement de tête. Archive delta, destruction de la session en mémoire, stockage
DocumentStore, ouverture FileZoneSession, Save, fermeture/réouverture : tête exacte.
Fixture temporaire créée sous TEMP sur E: et supprimée après réussite.
Clippy forge_project tous targets strict PASS (4,23 s), format exécuté.
Non-régression `zone_regeneration` : 3 PASS, compilation 0,90 s, exécution
0,01 s. Qualification transversale workspace/WASM de clôture non rejouée.

Limites : fermeture de la session, pas effacement/rotation du journal ; verrou
du bâtiment entier, pas verrou de propriété indépendant pour la seule porte ;
masque synthétique via commande, pas geste de pinceau. Aucun parcours Studio,
franchissement, comparaison GPU ou verdict global R2-045 acquis par ce test.

### Invalidation du lot de bâtiments — conservation vérifiée côté moteur

`BuildingProjection::replace` compare les parties dérivées avec le lot précédent
après validation de la nouvelle tête. Si la géométrie est identique et les
composants/handles/backend Rapier concordants, il conserve les entités, meshes,
matériaux et colliders enregistrés, restaure la visibilité et rattache le lot au
nouveau hash. Le masquage temporaire d'aperçu n'empêche pas cette conservation ;
la barrière publique du playtest continue de refuser une partie masquée.

Un changement géométrique ou un lot endommagé suit la reconstruction existante ;
aucun acquittement physique anticipé. Pas de calcul supplémentaire par frame,
pas de cache persistant ni de seconde source canonique. Cette étape évite le
remplacement du lot inchangé, mais recalcule encore les parties pour comparer
et reconstruit tout le lot lorsqu'une partie change. R2-050 reste partiel.
Le test ciblé mesure les identités/handles conservés, pas un gain de FPS ou
un budget de latence ; aucune amélioration chiffrée de performance annoncée.

`cargo test -p forge_engine --lib authoring_buildings --locked` : 3 PASS,
347 filtrés, compilation 1 min 12 s, exécution 0,02 s. Le nouveau cas vérifie
identités ECS, handle mesh et handles Rapier inchangés après renommage/altitude
terrain, hash ancien refusé et hash nouveau accepté sans resynchronisation ;
collider supprimé puis réparé, porte changée puis attente de reconstruction,
nettoyage final. Les deux tests antérieurs de disponibilité et de cycle de vie
passent également. Aucun GPU ni parcours Studio exécuté pour cette modification.
Clippy Studio WASM strict (incluant forge_engine) PASS, 25,61 s ; avertissement
futur proc-macro-error2 inchangé. Format exécuté. Contrôles workspace de clôture
non rejoués : ce résultat ciblé ne vaut pas qualification de Gate 2.

### Correction du cadrage après l'essai 168F6C25

Le cadrage générique fixe vers +Z montrait le mur arrière, l'entrée étant -Z.
Le Studio calcule maintenant une vue de façade dans le repère tourné de la
pièce et une vue rapprochée « Cadrer la porte » sur le décalage effectif
(override si présent, sinon recette). Les placements d'assets conservent leur
cadrage existant ; la caméra de playtest n'est pas modifiée. Aucun changement
de domaine, d'archive, de géométrie ou de données de la carte.

`cargo test -p forge_studio --lib --no-default-features authoring_building_view --locked` :
1 PASS, 58 filtrés, 22,61 s de compilation, exécution <0,01 s. Vérifie la position
de caméra hors façade pour 0/90/180/-90°, le ciblage de l'override et le retour
à la valeur de recette, sans mutation de l'objet. Clippy Studio WASM strict PASS
(6,19 s), avertissement futur proc-macro-error2 inchangé. Format exécuté.
Pas encore de packaging ni d'observation graphique de ces deux cadrages ;
regrouper cette vérification avec le prochain candidat pertinent.

### Essai ciblé du candidat 168F6C25 — 14 septembre

Candidat déjà construit réutilisé, aucune recompilation ni campagne performance.
Fenêtre sur écran secondaire. Copie isolée `building-playtest-ui-20260914`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06` ; aucune édition directe de l'archive.

Acquis par gestes Studio et captures inspectées dans la session :
- cadrage proche de la pièce existante ;
- saisie X/Z=20, aperçu de position (20,3,20), Apply puis cadrage du bâtiment déplacé ;
- correction de porte X local=1,5, aperçu cyan et résumé distinct de l'état accepté ;
- Apply : inspecteur « porte retouchée à 1.5 m », puis Save et « Session synchronisée » ;
- Jouer : avatar rendu, mode « Playtest · sources gelées, aucune sauvegarde implicite ».

Archive disque v15, 26 commandes, SHA-256
`E9DFB69E05210FBD7AB339D1B1336010169C83DB0962E111E93B1ABC163B2788` ; tête
`7470126a49d06f3891ad398672ce7c54872d162b1c1aa0d355062b5187ea3006`.
Dernière commande `386d5254-5d9e-5b99-a5b7-77e019bf0b70`, révision attendue 35,
bâtiment `3286c3e1-c399-54c6-b943-6e3598d17526`, origine (20,3,20), yaw=0,
override de porte=1,5 ; autres couches en keep.

Limites : cadrage depuis l'arrière, ouverture de porte non inspectée visuellement ;
aucun déplacement joueur/franchissement qualifié, ni réouverture ou Undo/Redo UI
sur ces deux nouvelles commandes. Le mode playtest actif seul ne prouve pas la
disponibilité physique complète. Aucun critère R2-032/033 clôturé sur cette base.
Les mentions « non lancé » suivantes décrivent les étapes précédentes.

Candidat fermé en fin d'essai ; PID 13388 et descendants 10344,26512,19180,
20000,22976,32800,5292 vérifiés absents. Archive source 2B718781 inchangée ;
archive d'essai E9DFB69E inchangée après le playtest. Pas de publication Git.

Position/orientation désormais éditables au code via `BuildingEdit::SetPose`
et les champs X/Y/Z/rotation Y de l'inspecteur. Aperçu et journal habituels,
contrôle des empreintes tournées et des verrous ; décalage local de porte conservé.
Quatre tests `building_authoring` PASS (0,01 s), dont déplacement/Undo/Redo,
refus aux limites et refus d'une pièce verrouillée. Aucun nouveau format persistant.
Geste de déplacement Studio pas encore observé dans un candidat.
Clippy Studio WASM PASS (30,73 s). Candidat regroupé construit ensuite, non lancé :
`E:/OpenRPG_Forge/.gate2-evidence/building-playtest-studio-20260914/forge-studio-candidate.exe`,
SHA `168F6C259B3244C6FE78C9F509CB8127340586EB289D7772111181A41FF2517C`.
WASM `96AD0B59F79176AD97B07638FF6620418CD877C03A9BF901284C85A8DA22F71B`.
Trunk et Tauri PASS. Inclut cadrage, pose et barrière Rapier de playtest.
Copie isolée prête : `.gate2-evidence/building-playtest-ui-20260914`, archive
initiale `2B718781BDEAB8E1B39DFBF7353AAE58DAFEE98807469F3A43EE8C9EB0F45E71`.
Essai à poursuivre sur cette copie ; ne pas reconstruire sans changement pertinent.

### Raccord playtest — implémenté, passage joué non qualifié

Le refus général des bâtiments est remplacé au code par une barrière de
disponibilité réelle. Le snapshot immutable transporte les recettes/corrections
de bâtiments et reste lié au hash de zone. Le Studio attend le lot accepté,
le nombre exact de parties, leurs identités, transformations et dimensions,
les composants mesh/matériau et les colliders enregistrés dans le contexte Rapier
attendu. Pose, échelle et shape doivent correspondre au backend actif, non sensor.
Un aperçu actif, une partie masquée ou déplacée hors commande empêche le démarrage.
Colliders statiques autonomes, sans RigidBody explicite, comme les primitives
d'assets. Le contrôle backend existant est partagé sans seconde physique.

Deux tests moteur ciblés PASS (compilation 2 min 32 s, exécution 0,01 s) :
cycle de vie ; attente avant enregistrement puis disponibilité après synchronisation
Rapier, rejet du compte incorrect, du masquage et du déplacement périmé.
Test existant `manual_readiness_requires_real_backend_and_exact_primitives`
PASS en réutilisant le même binaire, sans nouvelle compilation. Trois tests
`building_authoring` PASS après ajout du snapshot bâtiments, dont conservation
des valeurs gelées et refus d'un hash périmé (0,01 s).
Pas de candidat graphique reconstruit ni franchissement joué pour ce raccord.
Clippy Studio WASM strict PASS sur ce raccord (44,81 s), avertissement futur
proc-macro-error2 inchangé. Aucun processus graphique lancé.
Les refus de playtest décrits ci-dessous concernent les versions antérieures.

### Essai Studio du 14 septembre — acquis et limites

Candidat `E:/OpenRPG_Forge/.gate2-evidence/building-preview-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `974CAD8209E27FE4236FBA0706D8984774EEDB4DB306941FD274CEDF2F49422C`.
Trunk et packaging Tauri PASS. Fenêtre observée à X=-1833/Y=90, 1602×932,
sur écran secondaire. Copie isolée sous `.gate2-evidence/building-authoring-ui-20260914`.

Parcours réellement exécuté : ouverture de la zone précédente, panneau bâtiments,
origine (0,3,0), aperçu d'une pièce 8×6×3, affichage cyan visible en vue lointaine,
Apply puis rendu accepté beige, Save, fermeture de zone, Open et rendu retrouvé.
Pas de retouche de porte, Undo/Redo UI, abandon UI ou passage joué dans cet essai.
Le cadrage lointain ne qualifie ni les détails de porte ni la qualité architecturale.

Archive finale : version 15, 24 commandes, tête
`480c86a4347d3966a4d83afd79e3e1eb12a02dc6afe3187b80ce34a96b2efa05`.
SHA-256 disque `2B718781BDEAB8E1B39DFBF7353AAE58DAFEE98807469F3A43EE8C9EB0F45E71`.
Bâtiment `3286c3e1-c399-54c6-b943-6e3598d17526`, aucune correction de porte.
La dernière commande conserve les autres couches (keep) ; source originale
`1E48B2F6D8E61118D5D8CD1677ADC4C9848419FE3FEE7105963BEB20A62EB500` inchangée.

Candidat fermé : PID 32276 et descendants 28972,11960,5604,32968,11416,32704,31448
vérifiés absents. Aucun processus de cet essai laissé actif.

Suite au défaut de cadrage constaté, bouton « Cadrer ce bâtiment » ajouté au code
de l'inspecteur ; utilise la caméra existante, sans mutation ni déplacement de
carte. Clippy Studio WASM PASS (6,20 s). Ce complément n'est pas dans le candidat
974CAD82 et son geste réel reste à vérifier. Gate 2 toujours ouverte.

Complément aperçu : `ZoneAuthoringHead::building_preview` contrôle le reçu,
la révision/hash de départ et d'arrivée, l'inverse, les verrous et l'absence
de changement d'un autre domaine. Trois tests `building_authoring` PASS
(0,01 s), dont refus d'un reçu modifié et d'une autorité périmée.
Le Studio possède désormais un lot cyan séparé, sans colliders ; le lot accepté
garde ses collisions et son identité. Son rendu est temporairement masqué pour
éviter les surfaces coplanaires. Abandon, nouvelle observation ou disparition
de la source retirent la proposition ; le rendu accepté est restauré.
Ce comportement est implémenté, pas encore observé graphiquement. La suppression
prévisualisée apparaît par l'absence du bâtiment dans le lot cyan, avec son
identité nommée dans le résumé. Le playtest demeure explicitement bloqué.
Clippy Studio WASM strict PASS sur le raccord final (23,48 s) ; avertissement
futur proc-macro-error2 inchangé. Pas de lancement GPU dans ce complément.

Projection initiale ensuite implémentée dans `forge_engine::authoring_buildings`
et raccordée aux sources acceptées du Studio. Huit volumes dérivés par pièce,
un cube unitaire et un matériau partagés par lot ; mêmes transformations pour
meshes et colliders fixes. Pose X/Y/Z et rotation Y appliquées aux bornes locales.
Le lot possède ses entités/handles et les retire explicitement au remplacement
ou à la fermeture. L'aperçu moteur sans collision existe, mais le Studio utilise
encore l'aperçu numérique : seul l'état accepté est actuellement projeté.
Le contrôle de source en boucle lit un hash mémorisé, sans rehash/clonage de tête
à chaque image. Playtest toujours refusé : synchronisation Rapier et passage
joué restent à qualifier. Aucun rendu réel ni candidat de cette version observé.

Validation de ce raccord : Clippy moteur natif bibliothèque PASS (40,97 s).
Test CPU `authoring_buildings::tests::projection_shares_geometry_and_releases_owned_resources`
PASS (compilation 2 min 04 s, exécution <0,01 s) : huit identités/transformations,
colliders fixes acceptés, absence de collider aperçu, remplacement des entités,
un mesh partagé, nettoyage des meshes et matériaux. Ce test ne fait pas tourner
Rapier et ne prouve donc pas sa synchronisation ni un passage physique.
Clippy Studio WASM strict PASS (5,38 s) après ajout de l'alias ECS requis par
la macro Resource ; avertissement futur proc-macro-error2 inchangé.
Aucune campagne GPU, aucun candidat graphique ni publication Git dans ce lot.

Panneau `authoring_buildings.rs` ajouté au Studio : création de pièce à X/Y/Z,
redimensionnement, retouche absolue de porte, retour à la recette, verrouillage
et suppression. PreviewZone réutilise le bus existant ; ni Apply ni Save implicite.
Le résumé donne les dimensions, décalage et verrou proposés, distincts de l'état
accepté. L'absence de projection et le blocage actuel du playtest sont affichés.
Clippy Studio WASM strict PASS (5,70 s), après extraction du libellé bâtiment
pour conserver la limite de taille de `preview_label`, sans suppression de lint.
Avertissement futur préexistant `proc-macro-error2` signalé par Cargo.
Ce panneau n'a pas encore été construit en candidat ni observé réellement.

Le raccord initialement prévu ci-dessous est maintenant implémenté dans
`zone_authoring/buildings.rs` : corrections v5 et archives complète 13,
compacte 14, delta 15. Insertion, dimensions, correction absolue de porte,
verrou et suppression passent par Preview/Apply de zone, sans nouveau document.
La correction de porte est séparée de la recette et conservée au redimensionnement.

`cargo test -p forge_project --test building_authoring --locked` : 2 PASS
(0,01 s). Relecture en mémoire des trois formats, Undo/Redo après relecture,
régénération avec seed différente, aperçu sans mutation, réduction incompatible
et tentative de suppression contournant le verrou contrôlés. Playtest refusé
explicitement en l'absence de projection. Clippy forge_project tous targets
strict PASS (0,30 s). Le premier test utilisait une régénération identique,
correctement refusée sans effet ; fixture corrigée pour changer la seed.
Deux lints de midpoint et comparaisons flottantes corrigés, sans suppression de contrôle.

Ce résultat ne prouve pas l'UI, l'écriture atomique sur disque, la qualification
complète des archives historiques ni le rendu/collision. Ceux-ci restent dus.
Les sections suivantes conservent les preuves des étapes géométriques antérieures.

## Implémenté le 14 septembre 2026

`forge_world::building_layout` calcule une pièce rectangulaire de plain-pied :
sol, toit plat, trois murs complets et façade divisée en deux montants/linteau.
Les dimensions métriques sont des entrées validées et bornées ; aucune carte,
aucun asset ou style de module n'est codé en dur. +Y vertical, sol fini Y=0,
entrée vers -Z. Huit volumes solides et deux volumes vides explicites (pièce,
passage de porte). Les futures représentations visuelle et physique doivent
consommer ces mêmes bornes, sans recréer une deuxième géométrie indépendante.

Les IDs sont dérivés du parent bâtiment et d'une clé sémantique de partie,
jamais de la dimension ni de l'ordre de parcours. Un redimensionnement conserve
donc les cibles des futures corrections ; cela ne prouve pas encore leur fusion.

Les paramètres `RoomDimensions` résident désormais dans `forge_project::building`,
avec sérialisation stricte et validation ; `RoomLayout::generate` dans forge_world
les consomme. Ce payload n'est pas encore accepté par ZoneCorrections : pas de
nouveau document canonique ni de commande accessible dans le Studio. Aucun format
d'archive modifié, aucune dépendance nouvelle, aucun asset importé.

La porte possède un ID sémantique propre et un décalage X local absolu en mètres.
Montants/linteau et volume vide suivent ce décalage. Un agrandissement en largeur
ne la recentre pas ; un rétrécissement incompatible est refusé, sans clamp.
Cela ne prouve pas encore la conservation d'un override à Regenerate : ce cas
nécessite le futur raccord à la tête, aux corrections et aux commandes.

## Vérifié

`cargo test -p forge_world building_layout --lib --locked` : 3 tests PASS,
6,29 s de compilation, exécution <0,01 s. Dimensions invalides/non finies et
porte sans montants/linteau refusées ; parties strictement positives, sans
chevauchement entre solides ni intrusion dans la pièce/le passage ; résultat
déterministe, identités stables après redimensionnement et distinctes par parent.
Format exécuté. Clippy `--all-targets` échoue sur une fonction préexistante de
122 lignes dans `tests/placement_transform_authoring.rs:35`, hors de ce lot.
Ne pas déclarer le contrôle tous targets vert. Clippy bibliothèque séparé
(`--lib --locked -- -D warnings`) PASS, 0,23 s.

Suite du 14 septembre : extraction dans ce test des helpers de session fixture
et de comparaison geste/numérique, sans retrait d'assertion. Les deux tests
`placement_transform_authoring` PASS (0,01 s) et Clippy forge_world
`--all-targets --locked -- -D warnings` PASS (0,36 s). Le défaut de lint est
résolu ; cela ne constitue pas une campagne de tests complète du workspace.

Le raccord persistant a été inspecté et préparé dans
`R2_BUILDING_PERSISTENCE_PLAN.md` : plan uniquement, aucun changement de schéma
ou d'archive implémenté à cette étape.

## Reste à implémenter et qualifier

Dernier contrôle : 2 tests de paramètres projet PASS (round-trip strict,
incompatibilité et valeurs invalides), 4 tests de génération PASS (dont porte
décalée et agrandissement à identité/position préservées). Clippy projet/world
tous targets strict PASS (5,95 s), après correction d'un commentaire rustdoc.
Pas de tests workspace complets ni de parcours Studio exécutés pour ce complément.

- Paramètres et corrections persistants dans la tête de zone, migration explicite,
  commandes Preview/Apply/Undo/Redo, provenance, sauvegarde et régénération.
- Panneau et manipulation Studio, matériaux/pack de style, rendu et collisions dérivés.
- Porte éditable, franchissement joué, conservation d'une retouche de porte.
- Selon le périmètre architectural complet : fenêtres, toiture inclinée, pièces
  multiples, niveaux/escaliers, navigation et sockets de décoration.

Pas de preuve visuelle, de collision jouée ni de bâtiment sauvegardable.
R2-032, R2-033 et Gate 2 restent ouverts. CI distante NOT RUN — infrastructure billing.

## Qualification native R2-032/R2-033 — 22 septembre 2026

Verdict local : **R2-032 et R2-033 PASS par automatisation native CDP** sur le
candidat figé SHA-256
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`.
Le harnais exécuté avait le SHA-256
`AF5C58E10DEF26779602DF488B8AA84CD38E10F2C26D0E427D9DAD62D7D769FA`.

R2-032 passe 3/3 sous-cas : les treize champs requis sont présents ; la
projection annonce exactement huit volumes préparés ; une largeur nulle reçoit
le refus attendu sans Preview/Apply/Save ; nom, pose, dimensions, épaisseurs et
retouche de porte sont prévisualisés et appliqués par les contrôles réels. Le
bâtiment est ensuite verrouillé, Undo le rend modifiable, Redo restaure le verrou,
puis Save/Close/catalogue/Open relit les valeurs exactes. L'option unique conserve
« (verrouillé) », le champ largeur reste désactivé et l'archive rouverte est
strictement égale au snapshot sauvegardé.

R2-033 passe 1/1 sous-cas. Le bouton « Jouer devant cette porte » démarre une
projection fraîche ; le harnais restaure l'audit après ce rechargement et attend
la simulation réellement prête. Coordonnées locales observées : départ
`X=1,6044, Z=-5,2538`, intérieur `X=1,6002, Z=-2,2629`, mur arrière
`X=1,6027, Z=2,6346` avec deux contacts, retour extérieur
`X=1,6028, Z=-3,8566`. Le centre reste dans la largeur de passage, progresse
jusqu'à la bande bornée du mur arrière et ressort par S. Pointer-lock acquis puis
libéré par Échap ; retour édition vérifié après le second rechargement. L'archive
sauvegardée reste inchangée autour du playtest.

Preuves :
`artifacts/forge-runtime/gate2-resume-20260919/r2-032-033-building-playtest-v7-20260922-v3/`.
SHA-256 du ledger agrégé :
`57F3A04DC27C09F14C45EA27A0ABC69A4DD58CABEDACD946B82E0E71004B3354` ;
rapport :
`3E2BC46F37F7A0203B289DA50A201E982A926971D36E5F2EFE6EA8FD8F40A815`.
Source et candidat sont intacts ; audit fetch restauré, processus possédé fermé,
environnement restauré et listener CDP absent.

Luna max a inspecté les quatre PNG 1920×1080 : captures valides et cohérentes,
avec bâtiment, ouverture et personnage lisibles pendant le playtest. Les vues
R2-032 restent éloignées et ne montrent pas tous les champs ni les huit volumes
en gros plan simultanément ; ces assertions reposent donc sur le ledger/CDP.
Inspection visuelle par agent uniquement, pas une revue humaine.
`human_review_performed=false`, `gate_qualified=false`, CI
`NOT RUN — infrastructure billing`. Clôture locale des deux critères, sans
scellement formel de Gate 2 et sans ouverture de Gate 3.
