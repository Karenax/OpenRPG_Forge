# Transport des lampes vers le matériau bâtiment

Suite de [la collecte et de l'horloge source](2026-10-02-source-local-lights.md).
La parité, l'installation Studio/Runtime et le dessin des bâtiments restent ouverts.
La lecture GPU du transport passe sur les sondes décrites ci-dessous.

L'API publique de `MaterialSlab` Bevy 0.19.1 expose le bind group, pas les
buffers d'un uniforme préparé. Le récepteur bâtiment utilise donc maintenant
un `ShaderBuffer` partagé à la liaison 54, readonly en WGSL, géré par le renderer
Bevy existant. Les champs, l'ordre, les huit slots et les 448 octets du paquet
source restent inchangés. Le test encase compare désormais les écritures
uniforme et storage avec les mêmes octets source. Le constructeur commun des
matériaux fournit le handle ; `stage_shader` installe son asset neutre.

`local_lights::gpu::register` ajoute l'extraction de Collection et une mise à
jour Render après PrepareAssets et avant Queue. Il ne démarre pas de renderer
et n'active pas de collecte auteur. L'update lit les ViewPacket et MainEntity
de la caméra extraite, exclut les captures, refuse les correspondances absentes
ou ambiguës et les serials déjà consommés pour cette caméra. Le buffer préparé
doit faire 448 octets et autoriser STORAGE/COPY_DST. Les paquets valides sont
écrits avec la queue existante ; les refus de collecte/frame écrivent le paquet
neutre pour retirer les anciennes lampes. Les floors de serial restent conservés.

`Status` indique le tag et les lampes du dernier write queued, sans prétendre
une complétion GPU ni un dessin. Un buffer absent/incompatible n'est pas écrit.
Le contrôle d'identité de l'asset neutre refuse une occupation différente et
ne la remplace pas. Son test vérifie aussi le refus d'enregistrement sans
RenderApp. Ce test ne crée aucun appareil GPU.

Material-upload-v1 fige 936 fichiers et vingt-deux overlays, garde initiale PASS.
Les 19 tests ciblés passent (602 autres filtrés). Clippy termine avec retour 0,
mais relève une complexité de type Query et un match réécrivable en let-else,
en plus des 29 diagnostics préexistants hors tranche. V1 et ses diagnostics
sont conservés. V2 introduit un alias de Query et récupère le guard de télémétrie
en cas de mutex empoisonné : une erreur de télémétrie ne doit pas empêcher la
remise à zéro/mise à jour du buffer. Ce dernier changement est fonctionnel ;
les tests, Clippy et WASM de v2 seront donc rejoués sur un nouveau snapshot.

Résultat terminal v1 : check WASM retour 0. Résultat terminal v2 : les 19 tests
ciblés sont rejoués et passent ; Clippy natif et check WASM terminent avec retour
0, sans diagnostic possédé (29 diagnostics préexistants hors tranche). La garde
finale des 936 fichiers figés et vingt-deux fichiers root possédés passe.
Résultats conservés dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-material-upload-v2`.
Ces contrôles n'exécutent pas le système upload sur un appareil GPU. Le nouveau
transport et son ordre sont compilés ; sa lecture GPU et son vrai consommateur
matériau doivent encore être prouvés par un essai rendu distinct.

Le plugin n'est pas encore appelé par Studio ou Runtime. Il reste à installer
le chargement du paquet et la projection/reconciliation des owners, le publisher
solaire et la collecte après propagation, puis à prouver la vraie extraction,
les octets GPU et le dessin des bâtiments. L'ordre déclaré et les tests CPU
ne constituent pas cette exécution. Aucune capture, parité ou livraison portable
n'est qualifiée par cette tranche.

## Exécution du transport sur le GPU existant

`verify_building_light_upload` crée une caméra Bevy vers une petite texture,
sans fenêtre, et utilise le RenderApp, RenderDevice et RenderQueue du même
renderer. La collecte a lieu après propagation des transformations ; son paquet
est extrait et écrit dans le ShaderBuffer partagé des matériaux. Trois copies
GPU vers un buffer MAP_READ vérifient les 448 octets : paquet valide, paquet
neutre après inventaire invalide, puis récupération. Le tag observé correspond
à celui de la caméra vivante. Aucun mesh bâtiment n'est créé dans cette sonde.

La première sonde synthétique passe avec retour 0, dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-material-upload-proof-v1`.
Le binaire a le SHA-256
`ccfc244f29141c303af0a689b1293734c50f69540ac918a7e4d0f0a99c7682ef`.
Clippy termine sans diagnostic possédé, avec 29 diagnostics préexistants.

Le mode supplémentaire accepte les racines et hashes des deux paquets fermés.
Il remplace la lampe synthétique par les projections canoniques des bâtiments,
avec leurs transformations source et les enfants lumineux dérivés. Il exige
19 racines vérifiées et 32 ordres distincts ; les quatre bâtiments sans identité
de lampe explicite restent listés et refusés, sans attester leur absence de lampes.
Après propagation, la caméra est placée près de la première lampe. La sélection
source produit huit lampes ; les trois lectures GPU passent, avec effacement
et récupération, dans le candidat building-upload-proof-v1 (938 fichiers figés,
24 fichiers root possédés, garde initiale PASS).

Paquets lus et vérifiés : bâtiments
`a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`,
lampes `6a466956e0327b6c9fd337b9a230d3219be868255cbb2ef950f6191b9f35d5d9`,
source `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le rapport `gpu-proof.json` conserve la couverture, les quatre identités
non résolues et le SHA du paquet GPU
`60107ade0859b5d96f14b664fb85db6aeb0098b5cef17fedf7fe02258c486a61`.
Résultats :
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-building-upload-proof-v1`.

Clippy v1 retourne 0 mais signale deux avertissements possédés : clone assigné
et longueur du main. V2 emploie clone_from et extrait la configuration du mode
source dans une fonction ; une nouvelle validation est requise. Les modules
de production restent ceux de material-upload-v2, déjà couverts par les 19 tests
et le check WASM ; ces résultats ne sont pas présentés comme rejoués ici.

Résultat terminal v2 : Clippy natif et construction de la sonde retournent 0,
sans diagnostic possédé (29 préexistants hors tranche). Les trois lectures GPU
sur les paquets réels repassent, avec les mêmes octets que v1. Le binaire v2
a le SHA-256 `207aa2e89b134bb67211bd1b48a356820699ca6d24258a50491687600a8434e4`.
Le formatage de la sonde et de son module passe ; la garde finale des 938 fichiers
figés et 24 fichiers root possédés passe. Résultats conservés dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-building-upload-proof-v2`.

La preuve établit le trajet paquets fermés → racines canoniques vivantes →
collecte caméra → extraction → buffer GPU partagé. Elle ne prouve ni le bind
group consommé par un dessin VillageMaterial, ni l'image des lampes, ni la
projection Studio et ses retouches, ni les autres familles d'owners. L'horloge
de la sonde est figée à minuit ; ce n'est pas un publisher solaire du produit.

## Condition à résoudre avant le raccordement de tous les modes Studio

L'inspection du contrôleur authoring_temporal et de prepare_main_view révèle
que la caméra du playtest n'a pas systématiquement un ViewPacket : le mode
source sélectionne explicitement le propriétaire RenderWorld, tandis que les
modes désactivé et TAA Bevy ne sélectionnent pas cette séquence. La préparation
Main retire aussi les anciens packets quand aucune vue n'est sélectionnée.
La sonde d'upload activait explicitement Frames ; elle ne prouve donc pas le
transport dans tous les modes graphiques du Studio.

Installer seulement le plugin actuel laisserait des lampes neutres hors de ce
chemin. Le raccordement doit vérifier la collecte contre la vraie caméra
extraite également quand aucun packet temporel n'existe, en conservant l'identité
MainEntity, la pose et la remise à zéro des inventaires invalides. Il doit garder
les tags/serials de la séquence source quand celle-ci est active et distinguer
une observation d'upload sans séquence temporelle. Activer ou remplacer le
resolve expérimental pour afficher les lampes ne satisfait pas cette condition.
Ce complément n'est pas encore implémenté ni qualifié ; le dessin matériau et
les comparaisons visibles restent ouverts.

## Candidat caméra indépendant du resolve temporel

Le candidat camera-upload-v1 ajoute Collection::for_camera : identité Main de
la caméra, pose extraite vérifiée avec la même tolérance f32/f64, révision de
collecte réussie et invalidation après inventaire refusé ou changement de torche.
L'upload utilise maintenant ExtractedView même en l'absence de ViewPacket.
Il refuse une révision déjà publiée. Si un ViewPacket existe, ses vérifications
supplémentaires d'identité, de pose et de serial restent obligatoires : une
erreur temporelle ne bascule pas silencieusement vers le chemin sans packet.
L'observation distingue caméra/révision de collecte et tag temporel optionnel.
La séquence de resolve n'est ni activée ni remplacée par cet adaptateur.

La sonde est prolongée de trois lectures GPU après Frames::activate(None) :
buffer éclairé identique, remise à zéro d'un inventaire invalide puis récupération.
Elle exige aussi la disparition du ViewPacket et un upload sans tag temporel.
Le test de collection vérifie caméra étrangère, pose différente, matrice NaN,
invalidation et progression de révision. Ces checks ne prouvent pas encore
le dessin du matériau ni les modes graphiques exécutés dans le produit.

Snapshot figé : 939 fichiers et 26 fichiers possédés, garde initiale PASS.
Validation native, Clippy et sonde GPU lancés dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-camera-upload-v1`.
Aucun résultat d'exécution de ce candidat n'est annoncé avant son retour terminal.

Résultat terminal camera-upload-v1 : les 20 tests ciblés passent ; Clippy et la
construction de la sonde retournent 0. Clippy signale cependant un nouveau
too_many_lines dans main de la sonde. Le worktree extrait ensuite la séquence
sans temporal dans une fonction dédiée ; cette retouche n'est pas couverte par
le binaire v1 ni encore requalifiée en natif.

Les six lectures GPU v1 passent réellement, avec et sans ViewPacket, y compris
invalidation et récupération. Elles utilisent le même RenderApp/device Bevy
et le paquet enrichi aecf137b, avec 32 lampes sur 19 racines. Huit lampes sont
sélectionnées et les 448 octets ont le SHA
`60107ade0859b5d96f14b664fb85db6aeb0098b5cef17fedf7fe02258c486a61`.
Le binaire exécuté a le SHA
`04502df1cb89ab6fe897c451d336dee6dec4c13b5152db863bd96d2f7fded26b`.
Le helper GPU ne reconnaît pas encore les quatre attestations vides : il les
liste toujours comme unresolved ; la sonde ECS séparée les couvre. Aucun dessin
VillageMaterial, Studio ou verdict de parité visuelle n'est acquis ici.

La garde finale v1 conserve les 939 fichiers figés, mais signale une différence
dans le worktree sur la seule sonde verify_building_light_upload.rs, due à
l'extraction du helper après exécution. Le PASS du binaire v1 ne qualifie donc
pas cette version retouchée. Les autres fichiers possédés restent identiques.

## Protection supplémentaire du mode source sélectionné

Le résultat natif v6 est revérifié dans
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v6/native` :
retour 0 et six readbacks, avec et sans packet temporel. Il couvre désormais
23 racines canoniques, dont 19 lumineuses et quatre attestées sans lampe,
32 lampes source et aucune identité de bâtiment non résolue. Huit lampes sont
sélectionnées. Le SHA du buffer de 448 octets reste
`60107ade0859b5d96f14b664fb85db6aeb0098b5cef17fedf7fe02258c486a61` ;
celui du binaire exécuté est
`d05d6111c45f381f40dc3c1ca25060f3a514a3324ec99af34d42ac132233b30d`.
Les champs geometry_draw_executed, village_material_draw_executed,
studio_executed et visual_parity restent faux. Ce résultat ne couvre pas le
contrôle d'intention ajouté ensuite.

L'inspection du contrôle RenderWorld met en évidence un cas non couvert par
les six readbacks : un Control sélectionné peut retirer son ViewPacket lorsque
la préparation échoue. L'option de packet seule ne distingue pas ce refus d'un
mode Off/TAA. Le worktree ajoute donc le contrôle d'intention avant upload :
si une caméra source est sélectionnée, elle doit correspondre à la caméra
collectée et posséder son packet. Une frame manquante ou une autre sélection
refuse la publication et suit la remise à zéro existante. Aucun mode source
n'est activé pour satisfaire cette condition. Les validations de tag, pose et
serial sont extraites dans temporal_tag sans modifier leur comportement.

Un test couvre absence de sélection, packet facultatif en Off/TAA, sélection
correcte avec packet, packet manquant et caméra étrangère avec/sans packet.
La sonde extrait aussi l'écriture de son rapport pour traiter l'avertissement
de taille de main conservé en v6. Ces retouches sont préparées dans le snapshot
Studio v8 pour validation native après le build WASM v7 ; elles ne sont pas
contenues dans le WASM v7 en cours et ne sont pas encore exécutées.

## Relecture des protections de projection avant V8

Les anciennes revues sont relues contre le worktree courant, sans réouvrir
leurs défauts déjà corrigés. projection.rs valide désormais chaque ancien
émetteur avant un remplacement : identité complète, ordre, paramètres source,
parent et Transform local attendu. Le changement de plage d'enregistrement
ne permet pas de contourner ce refus. is_synchronized compare aussi les
paramètres acquis, et le staging vérifie le GlobalTransform contre la
propagation attendue depuis les Transform courants.

Les tests nommés
source_local_lights_projection_refuses_compromised_old_emitter_even_with_a_new_order_range
et source_local_lights_projection_refuses_snapshot_mutation_and_stale_owner_transform
sont présents dans projection/tests.rs. Cette relecture est une inspection
du code et de la couverture ; elle n'ajoute pas un verdict d'exécution V8.
La validation native V8 reste active dans forge_engine au moment de ce relevé.
La readiness géométrie/collider et le dessin Studio restent des preuves séparées.

Le contrôle native local-light-tests V8 termine ensuite avec retour 0 :
21 tests passés, zéro échec, 602 tests filtrés. Le nouveau test
local_lights_gpu_source_selection_requires_its_camera_and_packet passe
effectivement, ainsi que les refus de projection compromise et de mutation
des paramètres. Clippy, construction de la sonde et lectures GPU V8 restent
en cours ; ce résultat ciblé ne qualifie pas encore leur exécution.

Clippy natif V8 termine avec retour 0. Le reçu scoped-clippy-diagnostics
compte 29 diagnostics, aucun sur les 16 fichiers possédés, et aucun diagnostic
sans span primaire. L'avertissement de taille de main constaté en V6 ne
réapparaît donc pas sur la sonde V8. La construction de la sonde démarre ensuite.

L'inventaire d'acquisition est aussi vérifié séparément contre le manifeste
aecf137b et les pins descriptor/acquisition : les 60 IDs lumineux sont uniques
et chacun possède un owner source observé. Les 32 entrées Buildings.js sont
30 buildHouse, une buildBoathouse et une buildMarketStall. Les 28 autres sont
11 lanternes Village/Pier, trois Boardwalk, une lampPost, trois pathLight,
huit BoatModel, une FishStand et une Chandlery. Le reçu
source-light-owner-inventory.json conserve leurs références source exactes.
L'absence d'owner non résolu dans l'acquisition ne constitue pas une jointure
catalogue, une projection Studio ou un raccord GPU pour ces 28 autres lampes.

La validation native V8 termine intégralement avec retour 0 à
2026-10-03T01:30:37Z : 21 tests, Clippy, construction et exécution de la sonde.
Les six lectures GPU passent sur le même RenderApp/device Bevy, avec et sans
ViewPacket, invalidation et reprise. Le buffer de 448 octets conserve le SHA
60107ade0859b5d96f14b664fb85db6aeb0098b5cef17fedf7fe02258c486a61.
Le binaire exécuté a le SHA
08d2e23c537a5c3974e5f1748027e68c904b1fca9dabb81f9585e2812227f7e4.
Il couvre 23 racines canoniques (19 lumineuses, quatre attestées sans lampe),
32 lampes source, huit sélectionnées, sans identité de bâtiment non résolue.
La garde finale vérifie 1342 fichiers figés et 16 possédés, sans différence.

Ces six readbacks vérifient le transport du buffer et ses refus d'inventaire ;
le refus d'intention temporelle manquante est couvert par le nouveau test
unitaire ciblé. Aucun dessin VillageMaterial, Studio ou verdict de parité
n'est acquis. La construction WASM release V8 prend ensuite le relais après
une nouvelle garde initiale PASS à 2026-10-03T01:30:43Z.
## Précontrôle des ancres passerelle/place — 3 octobre

`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v8/Verify-SideLightAnchors.py`
vérifie la fermeture exacte, les tailles et SHA-256 de trois paquets : lampes
AEC, passerelles `2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6`
et place `c0b3ce259e9de735204e5ea03e2cce5b6bdf0a332c3ffaca9fab59052712a23a`.
La jointure emploie uniquement le `source_object_id` explicite de l'acquisition,
avec contrôle d'unicité et de commit ; ni distance ni nom d'affichage ne servent
à choisir un objet.

Le reçu `side-light-anchor-preflight.json` passe pour quatre lampes : les trois
de `boardwalk-0` et celle de `plaza-prop-2`. Leurs matrices capturées correspondent
exactement aux repères TRS des objets géométriques convertis (écart maximal0).
Les lignes absentes n'attestent pas qu'un objet est dépourvu de lumière.
Cette vérification prépare le raccordement aux racines éditables ; elle
n'installe aucun binding produit et n'exécute aucune projection Studio ni aucun
dessin GPU. Les28 lampes hors bâtiments restent donc non raccordées au produit.
