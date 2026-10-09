# Tidewater — exposition du Playtest Studio, vue village

Suite du 2 octobre : [lecteur Rust des shaders du bateau](2026-10-02-boat-shader-loader.md).
Vingt programmes et leurs uniformes passent les contrôles CPU ; le dessin
Rust/Studio et la parité visuelle restent ouverts.

Suite du 2 octobre : [shaders source du bateau](2026-10-02-boat-source-shaders.md).
Sept matériaux et vingt variantes compilent dans le WebGPU de la scène source ;
leur raccordement Rust et la parité visuelle restent ouverts.

Suite du 2 octobre : [projection du bateau dans Engine](2026-10-02-boat-engine-instance.md).
Géométrie et collisions du paquet réel passent l’essai CPU ; le raccordement
des matériaux et l’édition dans Studio restent à qualifier séparément.

Suite du 2 octobre : [lecteur Rust du bateau](2026-10-02-boat-rust-loader.md).
Le paquet réel passe ses contrôles CPU natifs/WASM ; matériaux, instance
éditable Studio et parité de rendu restent à raccorder et qualifier.

Suite du 2 octobre : [acquisition du bateau canonique](2026-10-02-boat-source-acquisition.md).
Les 12 pièces visibles et le proxy de coque source sont acquis et contrôlés ;
les matériaux procéduraux et le placement Studio restent à raccorder.

Suite du 2 octobre : [masque de coque dans l’eau réelle](2026-10-02-real-water-hull-probe.md).
Le matériau actuel passe huit phases GPU natives ; le bateau canonique
reste à importer et à raccorder à Studio. Cette preuve ne requalifie pas
la calibration ni la parité visuelle exposées ci-dessous.

Suite validée : [ombres animées et récepteurs](2026-09-30-studio-live-shadow-receivers.md),
puis [optimisation des matériaux Studio](2026-09-30-studio-cloud-material-churn.md).
Les chiffres ci-dessous restent ceux du candidat 096 ; les rapports de suite
conservent les résultats de chaque nouveau candidat.

Référence : Tidewater figé à `1438b1abfcaee3267092b75573014f4d9b4a983c`,
caméra Village, PNG 1920 × 1080 à 2 s. Le candidat Studio 096 est photographié
à la même caméra de diagnostic avec son profil d'effets et les nouveaux émetteurs
d'ombres végétales. Le [reçu de la série](../../../../.tidewater-integration/studio-tidewater-exposure-sweep-098/tidewater-studio-village-reference.json)
confirme les valeurs envoyées au curseur ; les captures restent à des temps
différents de la source.

| Exposition Studio | Moyenne Studio | Erreur absolue moyenne, image entière | Maisons | Premier plan |
|---|---:|---:|---:|---:|
| +0,40 EV | 118,41 | 13,40 | 17,37 | 13,82 |
| +0,10 EV, début | 108,49 | **9,50** | 10,77 | **10,81** |
| +0,05 EV | 106,86 | 9,73 | **10,51** | 11,68 |
| 0,00 EV | 105,23 | 10,25 | 10,58 | 12,91 |
| −0,10 EV | 102,00 | 11,94 | 11,60 | 16,07 |
| −0,15 EV | 100,41 | 13,02 | 12,48 | 17,93 |
| +0,10 EV, répétition | 108,49 | **9,50** | 10,77 | **10,81** |

La source a une luminance moyenne de 109,23 sur les 32 400 pixels échantillonnés
(un sur huit par axe). Les [comparaisons](../../../../.tidewater-integration/studio-tidewater-exposure-sweep-098/comparison-4.json)
utilisent toutes `Compare-PairedCapture.ps1 -Preset Village`. La répétition à
+0,10 EV retrouve les mêmes métriques à 0,01 niveau près après l'aller-retour
du curseur, ce qui appuie le choix de ce réglage pour cette vue. La
[capture +0,10 EV](../../../../.tidewater-integration/studio-tidewater-exposure-sweep-098/village-exposure-010-00.png)
reste visiblement différente de la source ; le nombre ne prouve pas la parité.

La source appelle son réglage manuel `exposure = 0.55`, multiplicateur linéaire
divisé par 0,6 dans `src/post/PostFX.js`. Bevy utilise des stops : la valeur
Studio `exposure_ev = 0.4` multipliait la lumière par `2^0.4` avant son ACES.
La conversion algébrique du seul réglage source donnerait environ −0,13 EV,
mais la chaîne d'éclairage et l'adaptation automatique diffèrent : l'essai à
−0,15 EV est nettement trop sombre. Le profil Studio par défaut passe donc à
+0,10 EV sur la base de l'image complète, sans retoucher la courbe d'auto-
exposition ni la source.

Limites : la capture source et les captures Studio ne partagent pas encore la
même horloge d'animation ; ce réglage est une calibration de la vue Village,
pas une preuve de stabilité sur toutes les heures, météos et vues de l'île.

Suite : [ombres animées et récepteurs du terrain Studio](2026-09-30-studio-live-shadow-receivers.md).
Les chiffres ci-dessus concernent le candidat 096 ; ils ne qualifient pas
automatiquement un changement ultérieur des passes d'ombres.

Reprise du 1er octobre : [rayons sous-marins](2026-10-01-studio-caustic-shafts.md),
puis [cartes d'éclairage et récepteurs immergés](2026-10-01-underwater-light-maps.md).
L'exposition +0,10 EV ci-dessus reste une calibration de la vue Village ;
ces nouveaux contrôles possèdent leurs propres preuves et limites.

Suite de la reprise : [point d'entrée des ombres sous l'eau](2026-10-01-refracted-underwater-shadows.md).
Le déplacement est vérifié en natif et dans Studio sur les piles du ponton.
La reprise corrige aussi les casters manquants du ponton, des passerelles et
des accessoires de place. La preuve terrain séparée passe dans la suite Studio
166 ; la parité complète reste ouverte et l'objectif complet reste actif.

Suite : [capture séparée du fond à travers l'eau](2026-10-01-studio-refraction-capture.md).
Le nouveau réglage et son effet local possèdent leurs propres comparaisons
figées et lectures de textures GPU ; ils ne requalifient pas la calibration 096.

Suite : [réfraction en mouvement et profil CPU](2026-10-01-studio-refraction-motion.md).
La preuve 172 passe ; le diagnostic 173 conserve son échec de témoin de sable
recouvert par une vague. Une optimisation de sélection végétale est en cours
de qualification et ne requalifie pas ces anciens candidats.

La suite Studio 181 qualifie la réutilisation des meshes terrain/océan :
régression complète et audits passent, avec 77,51 % de réduction du débit
des grandes écritures GPU sur ces essais. La saccade initiale persiste ;
la capture native et la parité complète restent ouvertes. Les preuves et
limites sont dans le rapport de mouvement ci-dessus.

La suite native qualifie maintenant la capture de réfraction sur le candidat
215 : triplet de surface 216 avec lectures HDR/profondeur et répétition exacte,
puis parcours de nage et d'ombres 217 sur le même binaire. Les audits indépendants
passent ; l'échec intermédiaire 203 reste conservé. Cette preuve locale ne
requalifie pas l'exposition 096 ni la parité de l'île.
[Raccordement natif, preuves et limites](2026-10-01-native-refraction-capture.md).
La prochaine tranche porte sur les
[entrées GPU du TAAU source](2026-10-01-temporal-auxiliary-design.md), encore absentes.

Le [raccordement temporel suivant](2026-10-01-temporal-auxiliary-runtime.md)
installe maintenant les vraies passes auxiliaires dans le moteur. Le premier
essai GPU 220 échoue et conserve ses diagnostics ; les corrections et la branche
HDR du resolve source sont en qualification. Ce nouvel état ne transforme pas
les contrôles CPU en preuve de parité visuelle.

La suite du 2 octobre acquiert trois snapshots HDR du resolve source (236),
puis qualifie la vitesse d'eau avant transparences sur deux frames natives
complètes (240/244). Les écarts du modèle eau final sont tous contenus dans les
pixels modifiés par les transparences ; leur mélange et les écarts couleur du
resolve restent à vérifier. Les seuils et échecs précédents sont conservés.
Le [rapport temporel](2026-10-01-temporal-auxiliary-runtime.md) contient les
captures, les verdicts séparés et le candidat figé245. L'objectif reste actif.

La suite 253/249 reçoit maintenant les trois couples de lectures source et de
sonde instrumentée. Les 21 textures originales sont identiques à 236 ; la sonde
diffère et ne permet pas d'attribuer ses valeurs internes au shader original.
Deux défauts de raccordement (visibilité Rust et identité MainWorld/RenderWorld)
sont corrigés et testés ; les anciens FAIL sont conservés. Le rapport temporel
documente ces preuves et le prochain raccord au traitement temporel jouable et
à Studio. La calibration096 et la parité complète restent distinctes.

Le [raccord temporel dans RenderWorld](2026-10-02-render-owned-temporal.md)
passe maintenant son essai natif270 avec rendu pipeliné :32 phases, déplacement,
reset, redimensionnement impair et reprise après caméra inactive. Le FAIL262 de
profondeur eau fixe reste conservé ; le correctif264 suit la taille réelle et
garde les contrôles stricts. Les89 tests temporels, l'import shader et3 tests du
probe passent. Ce résultat porte sur l'encodage et ses jointures ; le mode Studio,
les nouvelles lectures Source7 et la parité complète restent ouverts.

Suite : [choix temporel du playtest Studio](2026-10-02-studio-temporal-controls.md).
Les nouveaux tests de caméra/réfraction et de cycle de playtest passent en CPU;
le mode Tidewater conserve un refus explicite tant que son contrat de scène
éditable n’est pas complet. La qualification Studio/WebGPU suit séparément.

Suite : [passe de distance de coque](2026-10-02-hull-distance-pass.md).
Le moteur ajoute la cible HULL R16Float séparée et le discard commun aux
fragments eau. Le proxy source est conservé sans combler ses huit arêtes
ouvertes ; son exception exige une empreinte vérifiée. Le bateau éditable,
l’inventaire Studio et le fragment eau réel restent à raccorder. Les 11 tests
CPU et les 11 états GPU hors écran passent, avec 384 contrôles de distance ;
ces preuves ne qualifient pas encore le rendu complet de Studio.
Le contrôle de métadonnées de tout l’Engine courant termine également
avec un retour compilateur 0 contre les dépendances natives reçues. Son
reçu et l’audit séparé de la normalisation des chemins Windows sont
documentés dans le rapport de coque ; le raccord GPU au matériau optique
et l’inventaire éditable restent ouverts.

Suite : [programmes du bateau sur le GPU Bevy](2026-10-02-boat-bevy-pipelines.md).
La création native des vingt programmes existants passe avec un reçu de politique
dérivé du SDK. L'inventaire App est incomplet et aucun dessin du bateau n'est
encore qualifié ; cette preuve ne requalifie pas l'exposition ni la parité de l'île.

Suite : [éclairages locaux source](2026-10-02-source-local-lights.md).
Le collecteur Rust et son adaptateur ECS sont en validation. Les vraies lampes,
l'état jour/nuit et le raccord au dessin de scène restent ouverts ; la calibration
096 et la parité complète gardent leurs limites.

Suite : [chargement et raccordement des lampes dans Studio](2026-10-03-studio-light-package-loader.md)
et [transport des lampes sur le GPU Bevy](2026-10-03-source-light-material-upload.md).
Les vérifications isolées couvrent désormais les 23 identités bâtiments et le
buffer lumineux avec et sans packet temporel. La collecte du produit est en
validation ; l'activation du paquet, le dessin réel et les comparaisons visibles
restent ouverts. Ces résultats ne requalifient pas l'exposition ni la parité.

Suite : [raccord solaire, atmosphère et éclairage partagé dans Studio](2026-10-03-solar-environment.md).
Le nouveau raccord utilise les retours atmosphériques GPU terminés pour alimenter
nuages, environnement et optique d'eau, avec des diagnostics distinguant l'attente
de la lecture effective. Les contrôles WASM passent ; le nouveau candidat solaire
est en compilation et sa copie runtime isolée est prête. La cadence GPU, les ombres
visibles et la parité avec la référence restent à vérifier sur ce candidat.

Suite : [surface complète de l'étal et raccord poisson natif](2026-10-06-stall-surface-parity.md).
Les validations isolées de surface/contact et le check Web de Studio sont
documentés séparément du dessin de scène. Le parcours natif prépare désormais
les cinq poissons affichés et les 78 décoratifs avec leurs surfaces et masques
source ; sa compilation et ses comparaisons visibles restent ouvertes. Les
contrôles de distribution exigent ce nouvel inventaire et les preuves du binaire
exact. Les tests de commande du launcher courant passent dans un harnais sans
Tauri ; son contrôle Tauri complet passe ensuite et un candidat launcher dev
avec l'interface Aube embarquée est construit et vérifié par marqueurs. Aucun nouveau paquet portable
ni parité complète n'est qualifié par ces résultats.
