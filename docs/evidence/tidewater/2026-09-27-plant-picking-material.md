# Materiau d'identifiants des plantes — preparation

Le moteur expose `tidewater_plants::picking`, un `ExtendedMaterial` du materiau
source. Le shader derive conserve les fonctions de deformation et de surface,
notamment leurs rejets alpha/LOD. Le vertex transmet un identifiant entier plat
egal a la base du groupe plus son MeshTag. Le fragment encode cet identifiant
sur les trois canaux RGB apres l'appel de surface. Zero reste reserve au fond
et aux occluders. Les plages sont bornees a 24 bits et le decodeur refuse une
valeur alpha incomplete.

La derivation attend les quatre adaptateurs connus et refuse les signatures
incompatibles ou un shader deja transforme. La source acquise n'est pas modifiee.
Le probe `tidewater_plant_picking_probe` compare les fonctions source avant/apres
et controle les bornes, le decodage et plusieurs alterations refusees.

Ce module n'est pas encore installe dans Studio. Il faut encore une cible RGBA8
lineaire sans MSAA/post-traitement, une camera et des uniformes apparies, les
occluders, la table ID vers identite canonique, une lecture GPU et le rejet des
reponses perimees. Aucun clic, shader GPU ou parcours GUI n'est qualifie ici.

Validation en cours : compilation native et Clippy cible. Le premier Clippy
avec dependances a retrouve les deux alertes preexistantes de
`forge_project/zone_authoring/session.rs:349-350`. La passe cible suivante a
signale deux annotations locales (doc Markdown et must_use), corrigees.
Clippy cible sans dependances passe ensuite (`plant-picking-material-clippy-003.log`).
La premiere compilation native passe en 5 min 42 s. L'execution detecte un
melange CRLF/LF dans la source acquise et refuse la derivation. Correction :
normalisation des fins de ligne avant transformation, et comparaison du probe
sur la meme normalisation. Nouvelle compilation/execution lancees, resultat
obtenu : PASS en 5 min 39 s (`plant-picking-material-build-002.log`).
`run-002.log` : `PLANT_PICKING_ADAPTER_PASS kinds=4 source_functions=unchanged
id_boundaries=pass GPU=NOT_RUN`. Executable SHA-256 :
436a1225d082f395292fee552955748dbfb81e8183febf9ff7a295d68ba9fe7b.

## Correspondance canonique en cours

`Resident::picking_snapshot` capture les lots actuellement selectionnes sans
refaire les choix LOD ni allouer de ressources GPU. Chaque lot conserve mesh,
buffers, uniformes et ordre des MeshTag ; les deux niveaux d'un groupe partagent
la meme plage d'identifiants. La table refuse les doublons canoniques, les
entites disparues, les associations mesh/materiau/identite incoherentes et les
IDs inconnus. La resolution exige le hash courant du document. Le futur ticket
de lecture GPU devra aussi controler camera, viewport et generation de bibliotheque.

Le probe de population est etendu pour verifier cette correspondance lors de
A/B/A et le refus d'un MeshTag corrompu temporairement. Clippy cible strict
sans dependances PASS (`plant-picking-snapshot-clippy-001.log`), incluant les
deux probes et le moteur. Compilation/execution du probe de population en cours
(`plant-picking-snapshot-build-001.log`, `run-001.log`) : PASS, compilation
5 min 10 s, execution sur les 65383 identites. Correspondance exacte pour
18825 draws / 8 lots, 18576 / 2, puis 18825 / 8 ; revision perimee et tag
corrompu refuses. Ressources constantes (8 meshes, 8 materiaux, 12 buffers),
37098 entites conservees au cumul. Etat du workspace inchange.
Executable SHA-256 :
0240d8b07a1444d97488fa600761f33068967d491c73d5df6b8774e43c47e11c.
Cette extension ne constitue pas une preuve GPU.

## Passe hors ecran

Le module `picking::render` ajoute une cible RGBA8 lineaire, une camera de
preview exclue des outils Studio et des copies de draws sur le layer 30.
MSAA, tonemapping et dithering sont desactives. Les meshes, textures et buffers
restent partages. Le proprietaire de la passe doit fournir les occluders,
l'attente des pipelines, la lecture GPU et les controles de fraicheur.

Le mode natif `--resident-plant-navigation` prepare desormais cette passe
plantes seules. Apres readiness des pipelines, il demande une lecture,
controle chaque ID non nul et ecrit `plant-ids.bin` et `plant-ids.json` a cote
de la capture. La sortie reussie attend ce controle. Cette nouvelle preuve GPU
n'est pas encore executee ; aucun verdict visuel ou d'occlusion a ce stade.
Clippy cible strict PASS (`plant-id-gpu-clippy-002.log`) apres correction d'un
parametre injecte dans setup au lieu de capture. Build et execution GPU natifs
lances : `plant-id-gpu-build-001.log`, `native-plant-id-001.log`. Resultat attendu.

Resultat obtenu : compilation PASS 6 min 40 s ; execution GPU FAIL. Le graphe
de scene demande COPY_SRC sur la profondeur de la nouvelle camera, dont les
usages etaient limites a RENDER_ATTACHMENT. Usages corriges dans la passe ;
nouveau test GPU de selection encore requis. Aucun ID GPU valide a ce stade.
Les materiaux d'occlusion terrain/village opaque sont prepares et passent le
Clippy cible (`plant-occluders-clippy-001.log`), mais ne sont pas raccordes a la
preuve. La priorite passe au parcours jouable selon la demande du proprietaire.

## Reprise GPU pendant la validation du vent

Le mode `--resident-plant-navigation` a ete rejoue sur le binaire
`a3bfd120d0b0d110b2f1d5f2f8fca200a8f57f398503a59eae2ad6bf9407fc30`.
`native-wind-bootstrap-001` termine exit 0 en 8,89 s et produit
`PLANT_ID_GPU_PASS unique=47 canonical_resolution=pass occluders=NOT_IMPLEMENTED`.
Le fichier `plant-ids.json` resout 47 IDs GPU en 47 IDs canoniques du head
`f05a68acc294b586940237565c77fe2f7679ae9d2b77f4596f0380680665d50f`.
La capture visible et les deux sorties d'ombres restent identiques a la baseline.
Preuves sous `E:/OpenRPG_Forge/.tidewater-integration/`. La correction COPY_SRC
est donc validee sur GPU natif ; les occluders et les gestes Studio restent ouverts.
