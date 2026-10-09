# FishProps — raccord bibliothèque et inventaire Studio

Le candidat de bibliothèque accepte désormais un `fish_props_sha256` optionnel.
Le lecteur vérifie le manifest et ses 250 payloads, avec contrôle de génération
pendant le chargement. La publication du paquet et des métadonnées se fait avec
celle des autres ressources après validation complète ; une bibliothèque sans
paquet poissons publie un inventaire vide, et son retrait nettoie les caches.

`PreparedFishProps::templates()` expose les 78 identités/transformations source
sans maillage ni handle GPU. `source_objects::source_placements` les intègre via
la même liaison au terrain et `select_source` que les autres imports : identité
stable, retouches et tombstones Removed restent dans les placements communs.
Ce raccord ne prouve pas encore leur dessin, leur sélection GPU dans Studio,
ni leur sauvegarde réelle. La projection viewport dédiée reste à écrire.

## Preuves

- Native isolé moteur : `fish-material-native-validation-20261004-v15`,
  compilation, 18 tests et Clippy ciblé PASS. Cette preuve ne teste pas l'UI.
- Cargo WASM Studio : `studio-fish-library-check-20261004-v1/result.json`,
  code 0, 1 347 sources figées inchangées. Pas de lien d'application ni de GPU.
- Bibliothèque indépendante : `studio-fish-library-check-20261004-v1/library-stage.json`,
  2 760 fichiers vérifiés, nouveau paquet ajouté avec tous les champs précédents
  du candidat conservés, source intacte, aucune copie déployée dans le produit.
- Sauvegardes avant modifications et empreintes :
  `fish-studio-library-wiring-20261004-v1`. L'écriture initiale a utilisé le mauvais
  encodage Windows ; UTF-8 et fins de ligne ont été réparés avant le gel et le
  contrôle Cargo, sans suppression des sauvegardes originales.

## Suite

Raccorder le catalogue/projection des poissons au matériau Bevy dédié et à la
sélection commune, animer les uniforms de distance/frame, utiliser le proxy
avec son masque de nageoires dans la passe d'ombre. Construire un frontend
exécutable, vérifier les 78 instances et les retouches en scène, puis comparer
les mêmes vues à la référence. Les poissons StandardMaterial du composite
FishStand précédent nécessitent une vérification de chevauchement source avant
remplacement ; aucun retrait arbitraire n'est réalisé ici.

Le parcours Save du candidat corrigé reste indépendant : session 67213 active,
observation réelle à 59 Apply et 60 Preview, aucune commande pending ni erreur
persistante. Aucun Save encore observé à cet instant ; aucun PASS anticipé.

## Projection viewport FishProps — candidat v1

Le module `fish_props_scene.rs` installe le MaterialPlugin dédié, la table peau
et le shader source en mode ThreeReference. Les racines et parties utilisent
les marqueurs communs ; l'inventaire poissons est retiré du fallback GLB.
Focus et contrôles de synchronisation des propositions intègrent cette projection.
La visibilité s'appuie sur le frustum de la caméra active et les LOD/fondus source.
Les changements de pose conservent meshes et matériaux résidents quand l'identité
de l'inventaire reste identique ; ajouts/retraits reconstruisent actuellement la
projection. Le proxy d'ombre reste réservé à une future passe masquée.

Le snapshot d'éclairage préparé par la scène commune est exposé aux matériaux
procéduraux, sans document de rendu persistant supplémentaire. Les références
de textures et les paramètres source sont repris ; chaque copie conserve ses
uniforms indépendants de LOD/time/frame.

Cargo WASM Studio v1 termine code 0 sur 1 348 sources figées intactes :
`studio-fish-scene-check-20261004-v1/result.json`. Aucun dessin GPU ni lien
exécutable ne figure dans ce contrôle. Construction frontend lancée ensuite,
session 97954, sources de ce même candidat figées ; ne pas annoncer son succès
avant `frontend-result.json` et sortie terminale.

Une correction suivant ce snapshot relit les matrices du Receiver courant et
les paramètres d'environnement/nuages à chaque frame, car leur handle peut rester
stable. Elle est figée dans `studio-fish-scene-check-20261004-v2`, non validée à
ce point et exclue du build v1. Son contrôle doit attendre que le cache WASM du
build 97954 soit libre. Il faut ensuite tester le vrai dessin, les bindgroups,
le picking/édition, l'absence de doublons avec le composite et la passe d'ombre
masquée. Aucun verdict de parité ni de disponibilité finale dans Studio.

## Résultats terminaux suivants

Le build frontend v1 (session 97954) termine code 0, sources figées intactes,
application liée. WASM 125 617 978 octets, SHA256
`0d32804ccd70ea24e740cdc33c16c792c5b6da85b73db384487fe72b9df75edb`.
Il n'est pas exécuté dans un navigateur dans cette preuve.

Le contrôle Cargo WASM v2 (session 33842) termine code 0 avec sources intactes.
La correction de rafraîchissement des matrices/nuages est donc contrôlée mais
pas liée ni exécutée dans ce résultat. La passe d'ombre masquée et les preuves
de dessin, picking, édition et absence de doublons restent à réaliser.

Construction frontend v2 lancée ensuite, session 70658, mêmes sources figées
que son contrôle Cargo. Le candidat v1 est préservé ; aucun succès du build v2
ni aucune exécution GPU n'est anticipé.

Le frontend v2 termine ensuite code 0, sources figées intactes. WASM brut
125 621 226 octets, SHA256
`f66c8eb23bab30ed575567523ce4de6481e79179361c5d7095121273eed86133`.
Bindings et bibliothèque complète sont assemblés dans son dossier `frontend`.
Le worker navigation du candidat plage est réutilisé seulement après vérification
de toutes ses sources et artefacts : 2 771 fichiers finaux épinglés dans
`frontend-ready.json`. Le WASM après wasm-bindgen a une empreinte distincte,
`3eec9624d751eb10542d4d1f970a2ac9e4a9a82b0ffe8d470d7a2aa79ed7cf3c`.
Aucun dessin GPU ni picking n'est prouvé par cet assemblage. Le candidat natif
v3 conserve ce frontend et ajoute les corrections domaine d'ouverture à froid
testées séparément ; voir le rapport de Save pour la dérivation et ses limites.

## Inventaire réel et contrôle de cadrage

Le candidat natif v3 est construit et sa réouverture contrôlée. Le test de
sélection observe 78 entrées `fish-prop` dans le dropdown commun, sélectionne
`fish-prop:0` depuis la valeur effectivement observée puis actionne le cadrage
public. `fish-focused-view-v3` termine son parcours UI mais sa capture montre
toujours la plage : ce reçu n'est pas une preuve de cadrage ou de dessin poisson.

Le contrôle v4 attend ensuite un changement réel de la boussole caméra.
Session 15846 termine en échec après 120 s, vue toujours à 180 degrés, source et
frontend préservés. Le code FishProps consommait son focus même quand la projection
n'était pas prête ; la demande est désormais conservée jusqu'au placement prêt,
et annulée si l'objet disparaît de l'inventaire désiré. Ce correctif nécessite
encore validation runtime ; le timeout seul ne prouve pas qu'il est l'unique cause.

Le snapshot `studio-fish-scene-check-20261004-v4` dérive de v3 et remplace seulement
le module de scène poissons, l'entrée moteur et le nouveau module d'ombre.
Il conserve les correctifs domaine testés de v3. Contrôle Cargo WASM lancé,
session 33559 ; aucun verdict runtime anticipé.

Le contrôle Cargo v4 termine code 0, puis son frontend est construit (55147) :
125 502 859 octets bruts, SHA256
`db49e5ecd6f834a89f60c447d4731600657c3bcf17ce6537b5a13e64da3fde2d`.
Bindings et worker vérifié sont assemblés sans modifier le candidat v3.
Construction native v4 termine code 0 (50016), sources/frontend intacts : EXE
`79ea24bc5f0074351a7f24e97d202ddb81816127871afd021d8882d41c6f46aa`.
Le contrôle caméra suivant doit utiliser ce nouveau candidat et attendre le
changement réellement observé ; aucune amélioration de cadrage n'est anticipée.
Le raccord des ombres appartient au snapshot v6 suivant, distinct de ce v4.

Contrôle caméra suivant `CaptureFishFocusV5.ps1` sur l'EXE v4 : session 12981
termine code 0. Les 78 entrées sont observées dans le sélecteur réel, `fish-prop:0`
est sélectionné par sa valeur affichée, puis le cadrage public fait passer la
boussole de 180 à 329 degrés. Source, frontend et fermeture du listener passent.
Capture `fish-focused-view-v5/fish-focused.png` examinée : vue rapprochée du quai
et de ses accessoires, avec le village au fond. Cette capture confirme le
changement de caméra ; elle ne suffit pas à attribuer chaque pixel au matériau
procédural FishProps ni à prouver l'absence de doublons avec les composites.
Le shader masqué du graphe appartient au candidat v6, encore sans runtime.

## Cycle commun poisson — contrôles suivants

Le parcours `fish-common-lifecycle-v6` sur le candidat natif v6 exécute
translation, rotation, échelle non uniforme, verrouillage, duplication et
déverrouillage via les commandes publiques, puis sauvegarde/réouverture.
Il termine toutefois en failed_closed (73640) sur la comparaison JSON textuelle
du harnais entre proposition et tête décodée. Source et frontend sont préservés,
listener fermé. Le checkpoint sauvegardé est décodé par le lecteur Rust épinglé :
archive 6195ed9dd88721807d9e11375d3ac572d4c6cf0613396f4c35ee0eb30b1bc180,
tête b02367a829e4d73f0479d84722d309db6c496c438a96d4cef717acdd2c7ef0f5.
Le poisson source et sa copie conservent le pin du package, leurs corrections
et échelles 1,2 / 0,9 / 1,1 sous représentation float32.

Le harnais v7 compare séparément identité/nom/pins/verrouillage et chaque
composante de transformation après conversion float32, puis lit les contrôles
réels de l’inspecteur rouvert. Il sauvegarde aussi le checkpoint accepté avant
la comparaison. Parcours relancé (77859), sans verdict anticipé. Suppression,
Undo/Redo et réouverture finale restent à confirmer. Aucun picking GPU poisson
n’est revendiqué par ces parcours.
Le parcours v7 termine failed_closed (77859) sur une lecture d’échelle du harnais
adressée au mauvais details : Position précise et duplication, au lieu de
Rotation et échelle précises. Le checkpoint accepté est conservé dans
fish-edit-checkpoint.json ; le contrôle float32 de l’original a passé avant cet
échec. Source et frontend préservés, listener fermé. Aucun défaut de persistance
produit n’est inféré de cette erreur de sélecteur.

La reprise v8 clone ce checkpoint sauvegardé sans rejouer ses transformations,
vérifie son hash, retrouve l’original par une valeur réellement disponible dans
le sélecteur, et attend 79 entrées (78 sources plus sa copie). Les échelles sont
lues dans leur panneau public correct. Elle poursuit ensuite suppression,
Undo/Redo et sauvegarde/réouverture finale. Session 70760 lancée, verdict en attente.
Le lancement v8 termine failed_closed (70760), avant nouvelle mutation :
l’inventaire était celui de la fixture initiale (78). Inspection du script généré
confirme que le remplacement du chemin source n’avait pas eu lieu, à cause de
l’échappement Python du séparateur avant authoring-root. Le checkpoint v7 et ses
copies restent intacts ; ce résultat ne prouve aucun défaut de copie du produit.
Le script v9 vérifie la présence du chemin exact avant remplacement et clone
réellement fish-common-lifecycle-v7/authoring-root. Reprise lancée (10277).
## Cycle commun poisson terminé sur le checkpoint v7

La reprise v9 termine code 0 (10277). `reopen-result.json` est passed, aucune
erreur, source et frontend préservés, listener fermé. Les vérifications de
l’original et de sa copie conservent id/nom/asset_id/asset_hash/verrouillage et
chaque composante de transformation float32 ; leurs échelles sont également
lues dans l’inspecteur après réouverture. Le checkpoint d’édition v7 est réutilisé
avec hash cc245e2f5c5d11719336a9777994723409ba17043fec3a850da84853aae155c2.

La suppression de l’original puis de la copie passe, Undo restaure uniquement
la copie, Redo la retire. Après sauvegarde et réouverture les deux restent
absents du sélecteur et le tombstone de l’original est vérifié. Archive finale :
34a6f98d84f40a1c0614108b112c8941498be20394e88376d1b447e2d161b861.
`fish-lifecycle.json` conserve les transformations acceptées, demandes et
checkpoints. Cette preuve couvre une instance réelle fish-prop:0 et sa copie,
avec sélection dropdown ; picking GPU, totalité des 78 variantes et parité
visuelle restent distincts et non prouvés. Les essais v6/v7/v8 restent en échec
historique, sans réécriture de leurs reçus.