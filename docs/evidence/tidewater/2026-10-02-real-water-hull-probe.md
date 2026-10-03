# Tidewater — masque de coque dans le matériau d’eau réel

Suite du [pass de distance HULL](2026-10-02-hull-distance-pass.md), reprise du
[rapport d’exposition Studio](2026-09-30-studio-exposure-sweep.md).

La sonde native **304 build.2/run.0 passe ses huit phases** avec le véritable
`PreparedOptics::new/stage` de l’Engine courant compilé en 300. Sur les
272 pixels couverts par la coque qui affichent de l’eau dans le témoin,
267 retrouvent exactement le fond quand la coque est activée ; les cinq
autres gardent le témoin eau. Aucun pixel de cette région ne prend une
troisième couleur. Le seuil de distance du shader et sa condition de face
restent ceux de la source ; le test n’exige pas de supprimer toute l’eau
projetée dans la silhouette, indépendamment de sa profondeur.

La répétition sans coque retrouve tous les pixels HDR logiques à l’identique.
Le contrôle caméra sous la surface retrouve aussi une image identique avec
et sans coque, avec 10 095 pixels d’eau distincts du fond. Ce contrôle ne
fournit pas une mesure indépendante du builtin `front_facing` ; cette limite
reste inscrite dans le résultat.

## Construction et entrées

Le binaire de sonde mesure **110 289 408 octets**, SHA-256
`4af90d538af141f0daa92b74c092aa97d7bf9a72b096c64213593b67f9ed347c`.
Il est lié au rlib complet Engine300
`d494ac20ab8305b69a70056c8d13f9741c54ef932d5a1fc9706b63ae045eb57d`,
construit depuis les sources E: vérifiées avant/après. Les dépendances restent
les binaires natifs historiques explicitement reçus de Library247.6 ; ceci
ne remplace pas une reconstruction Cargo de toutes les crates actuelles.

Les cinq paquets physiques vérifiés sont ceux du staging Studio284 :

| Paquet | Identité du manifeste |
|---|---|
| Surface d’eau | `307f7af005da3ba4c4b35e3268880ce4443ae19c363daa73a54beb0057abcd93` |
| Terrain | `5e5e88f902df94fec5e40e3ce0f0e279245d5dca1aebfe1be224bae7bb4ee258` |
| Environnement | `dddf01a25b409c01992bf372117646583b1f72f1f173046151ca0b33753bd097` |
| Océan FFT | `ba9da93d9012341d180712e1ddcf8143f7878770e19ce7ba477269aa24e9b879` |
| Ressac | `2dda3c022af1f62a3148e811155ecafc83b825d4e7e0c4069124ff69e7fca754` |

Les chargeurs contrôlent schémas, noms autorisés, tailles et empreintes de
chaque fichier. Le runner conserve aussi l’inventaire des **345 fichiers**
de ces paquets et vérifie leurs empreintes avant/après. Le shader optique
source possède l’empreinte
`c80a0a521379acea2f8f72a61eb78d9fa0eb8c9c900946903d67c2ad51540c59`
avant les adaptations du moteur ; les paquets ne sont pas modifiés.

Le document réellement importé est `terrain-v4-001.json`, SHA-256 des octets
`f385a0e90d7c74dea6b3dec4c78901d63d3f7e42840e71247f7ceb2499dd4e3b`.
Le raster float32 2048² de 16 777 216 octets possède l’empreinte
`be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3` ;
il est lu dans le `source-blobs` de la zone isolée Studio du 27 septembre.
`SampledTerrainPlan` valide le couple et fournit les mêmes octets aux
constructeurs terrain, géométrie d’océan, relief et ressac. Cette sonde
utilise ce document source, pas un `ZoneAuthoringHead` effectif ni une preuve
d’ouverture de son snapshot dans Studio. Les chemins, hashes et limites
d’autorité sont conservés dans la note 301 et `304/inputs.json`.

La coque est la fixture source 290 : 952 sommets, 5 436 indices, empreinte
canonique `52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e`,
huit arêtes ouvertes auditées, pose source `(64.5, 0, 36.5)` sans rotation.
Les tailles et hashes des buffers bruts sont refusés s’ils diffèrent de la
fixture reçue. Aucun mesh de remplacement n’est utilisé.

## Exécution GPU

La caméra hors écran rend en `Rgba16Float`, HDR sans tonemapping, MSAA désactivé.
La préparation passe par les stages publics réels d’environnement, FFT,
relief, ombres, ressac, terrain, géométrie et optique. Elle ne fabrique pas
de ressource `ResidentEnvironment/Terrain/Geometry` pour satisfaire les
préconditions du matériau. Il n’y a aucun bateau opaque, végétation,
éclairage caustique ni ciel visible dans cette scène réduite ; le fond est
le terrain réel et un clear constant. La passe d’ombres sans caster et les
entrées caustiques désactivées utilisent les chemins neutres du produit.

L’horloge FFT est pausée immédiatement après son stage. Chaque capture
attend océan, ressac et nuages à l’étape 120, nuages à 120 soumissions,
les ressources prêtes et les pipelines compilés. Le snapshot réel reste
`paused=true`, `quiescent=true`, `next_step=121`, `simulation_time=2.0`,
`pending=None`, `completed=None`. Le bootstrap n’est pas un `LiveStep`
publié ; le temps 2 s vient de l’acquisition vérifiée et de cette horloge.

| Phase | Taille | Pixels HULL > 0,01 | Activation GPU |
|---|---|---:|---:|
| Vue supérieure, coque active | 128 × 96 | 272 | 1 |
| Même vue, coque retirée | 128 × 96 | 0 | 0 |
| Fond, océan masqué | 128 × 96 | 0 | 0 |
| Sans coque, répétition | 128 × 96 | 0 | 0 |
| Vue inférieure, sans coque | 128 × 96 | 0 | 0 |
| Vue inférieure, coque active | 128 × 96 | 302 | 1 |
| Fond de la vue inférieure | 128 × 96 | 0 | 0 |
| Vue supérieure, taille impaire | 129 × 97 | 279 | 1 |

Les **24 lectures GPU** sont les huit triplets couleur, distance `R16Float`
et buffer d’activation 16 octets. Elles contrôlent formats, valeurs finies,
activation et clear des états sans coque. Les pas de ligne sont 1024/256
octets à 128 × 96, puis 1280/512 à 129 × 97 ; le padding des textures est
conservé et exclu de la comparaison de pixels logiques. Le buffer
d’activation vérifie aussi ses douze octets de queue nuls. Les encodages
HULL complets correspondent à la caméra, à la taille et aux comptes attendus.
Chaque phase demeure immobile jusqu’aux trois callbacks.

Les phases avec eau attestent le pipeline forward de l’entité d’océan et
son shader réel ; les contrôles de fond cachent volontairement cette entité.
Les observations comptent 45 pipelines, aucun pending ni erreur au moment
des captures. Adaptateur enregistré : **AMD Radeon RX 7800 XT**, Vulkan,
pilote AMD `26.8.1 (LLPC)`.

Les reçus build et run terminent avec exit 0 et zéro divergence de source,
dépendance ou paquet. Le log stderr conserve un avertissement Bevy
`gpu_readback` à la fermeture : envoi vers un canal fermé. Les 24 lectures
et le résultat sont déjà reçus avant `AppExit` ; cet avertissement n’est
pas effacé ni présenté comme un log vide. Aucun échec de pipeline n’est
relevé. Le premier build 304.0 échoue sur l’environnement de macros Cargo
absent et le formatage de l’adaptateur ; ses logs et sources restent conservés.
Le build 304.1 passe ; 304.2 ajoute les contrôles de fixture, de fond et
d’horloge avant le seul run GPU de cette série.

## Reçus et limites

Preuves brutes : `F:/OpenRPG_Forge/.build/hull-water-probe-304`.

| Reçu | SHA-256 |
|---|---|
| Engine300 library.0 | `4cc8dc40881881949005f2461f022410c1b106243408ee0e305b52d4903c123c` |
| Probe304 build.2 | `cb9f2d301d3f1412db5fe4b1b8d176b1a4360afbb021f370c27c2e3572aadf4b` |
| Probe304 run.0 | `93691ed1fb43fa11f0dd8ed0f860eb6193f63e93905eb65bcff19690e123eb71` |
| Résultat GPU | `976c957ce8ea2b229478b8e11f2561cf6f0050a3267284c477cb7821ed5f2b9d` |

Les revues statiques 302/305/307 conservent leurs états intermédiaires et
limites. L’audit post-run 307 recalcule les hashes des **24/24 buffers**,
confirme les tailles, pitches, flags, couvertures et l’oracle, puis vérifie
le reçu terminal exit 0 et ses divergences nulles. Il consigne également
l’avertissement de fermeture et l’identité de l’adaptateur. Le verdict
porte sur le `discard` de l’eau courante et le contrôle
de vue inférieure. Il ne qualifie pas la couleur source, le traitement
temporel complet, le builtin de face indépendamment, une jointure GPU
par `FrameTag`, Studio, WebGPU ou le jeu complet. Le redimensionnement
contrôle l’encodage et les tailles des lectures ; il ne possède pas de
témoin couleur sans coque à la même taille.

La cartographie 306 confirme que le **bateau principal canonique n’est pas
encore importé ni placé dans Rust/Studio**. Le proxy de test ne le remplace
pas. La suite doit acquérir son asset et son proxy gouverné, les attacher
au point de stage générique du module, puis établir l’inventaire des parts
effectivement soumises et son invalidation pendant les éditions. Le
`HullMaskAdapter` Studio et les nouvelles constructions native/WASM restent
à raccorder. La calibration d’exposition 096 et la parité complète de l’île
ne sont pas requalifiées par cette sonde. L’objectif complet reste actif.
