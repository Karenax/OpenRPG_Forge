# Tidewater — suivi d'intégration complète

Objectif propriétaire du 24 septembre 2026 : même carte et mêmes effets visuels,
avec assets réutilisables, édition Studio et fonctions du plan approuvé.
Décision : [ADR-0017](../ADR/ADR-0017-tidewater-fidelity-integration.md).
Ce fichier suit ce chantier ; `PROJECT_CONTEXT.md` reste le contexte canonique.

## Référence

- Source `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
- Données locales : `E:/OpenRPG_Forge/.tidewater-integration/`.
- `upstream/` : clone intact ; `reference/` : clone de travail de référence.
- Patch suivi préexistant et inventaire de départ conservés hors dépôt.
- Forge : branche Rust existante, travail modifié conservé, aucune publication.

**Erratum d'acquisition :** les six PNG initiaux de `baseline/` répètent
exactement leur contenu à 1280 px horizontalement et 720 px verticalement.
Ils sont conservés comme traces mais exclus des comparaisons finales de scène.
Une nouvelle acquisition PNG sans perte `baseline-003/` est inspectée et indexée ;
le contrôle d'indexation rejette désormais ce défaut. `baseline-002/` est aussi
exclu : JPEG renvoyés par l'API générique malgré les extensions PNG.
[Procédure et preuves](../evidence/tidewater/2026-09-24-baseline-reacquisition.md).
Les oracles directs terrain/végétation/village sont séparés.

## Critères et état (aucune parité acquise à ce stade)

| ID | Livraison | Critère décisif | État |
|---|---|---|---|
| TW-001 | Acquisition et licences | source exacte, inventaire/hash par fichier, notices | en cours |
| TW-002 | Baseline visuelle | vues et parcours appariés, paramètres enregistrés | en cours |
| TW-003 | Export complet | terrain après village, masques, instances, prototypes, matériaux | en cours |
| TW-004 | Prototype arbre | même silhouette, matériaux/vent/LOD, pose Studio, save/open | en cours — palmier/feuillu/arbuste Bevy isolés |
| TW-005 | Sources de carte | blocs de hauteur et instances stables, migrations, commandes | en cours — terrain v4, végétation v2 (65383 instances), exclusions/densité et magasin des sources workspace testés ; raccordement Studio, autres corrections et export portable ouverts |
| TW-006 | Île fidèle | terrain, village, récif, décors, positions et collisions | en cours — terrain adaptatif, village et six groupes de végétation ; éclairage/effets/décors incomplets |
| TW-007 | Océan | FFT, houle, écume, vagues, ressac, embruns, sillages | en cours — FFT, ShoreWaves, ShoreSim/SurfFoam et sable humide comparés ; écarts locaux, embruns/sillages/dynamique ouverts |
| TW-008 | Optique eau | réfraction, réflexion, caustiques, vue mixte, immersion, gouttes | en cours — matériau optique natif comparé sur deux vues ; côte/immersion/chaîne finale ouvertes |
| TW-009 | Atmosphère | soleil/lune/étoiles, nuages/ombres, brume, rayons | en cours — ciel diurne, volume nuageux, reflets et ombres reçues comparés ; contrôleur Rust et historique GPU persistant sur 120 pas ; nuit, brume/rayons et interaction continue ouverts |
| TW-010 | Rendu final | ombres, lumière indirecte, AO, temporel, bloom, exposition, flou | en cours — ombres relief/objets et réception PCSS/PCF fixes ; temporel et rendu final ouverts |
| TW-011 | Vivant et sons | végétation animée, poissons/oiseaux/crabes/baleine, audio | à faire |
| TW-012 | Jeu | déplacement/nage, bateau, pêche, commerce, progression, sauvegarde | à faire |
| TW-013 | Studio | catalogue complet, objets plaçables, retouches conservées, Undo/Redo | à faire |
| TW-014 | Qualification | natif/WebGPU, métriques, parité fixe et animée, revue propriétaire | à faire |
| TW-015 | Autonomie | package local sans clone upstream ni Node, licences agrégées | à faire |

## Ordre d'exécution

Nuages : acquisition du système `SkyProClouds` réellement activé par défaut,
560 fichiers reproductibles, 120 blocs temporels et sorties à 1/120 ; les 37
sorties d'éclairage préexistantes restent identiques. Calcul Bevy de couverture
1024² : cinq texels diffèrent d'un niveau/255 ; bornes 64² identiques et
conservativité vérifiée indépendamment. Volume, temporalité et raccordement
scène restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-weather.md).

Complément : calcul natif des ombres de nuages 256² sur 120 pas d'uniformes
acquis, avec bruit 3D original et couverture calculée dans Bevy. Erreur moyenne
de transmission 2,74e-7, maximale 0,001041 ; répétition exacte. Les nouvelles
entrées atmosphériques nécessaires au volume sont acquises et vérifiées.
[Preuves ombres](../evidence/tidewater/2026-09-25-cloud-shadows.md).

Volume : première passe de marching originale exécutée dans Bevy à l'image
120, avec météo/bruit natifs et tables atmosphériques source appariées.
Résultats reproductibles mais écarts locaux : opacité max 0,021485,
profondeur max 125 m. Reprojection, panorama, atmosphère entièrement native et
composition scène restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-volume-trace.md).

Historique : 120 passes marching/reprojection natives depuis des textures vides,
deux paires 960×540 et répétition exacte. L'atmosphère reste appariée à la source.
Écarts finaux maximum : RGB 0,041748, opacité 0,051758, profondeur historique
0,875 km. Panorama, contrôleur interactif, diagnostic des écarts et raccordement
scène restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-history.md).

Panorama : la passe originale 512×160 est calculée nativement depuis zéro sur
120 étapes, avec la grille partielle 4×4 originale. Répétition exacte, historique
de vue inchangé ; transmission max 0,004395 d'écart. Les consommateurs ciel,
environnement et eau restent à raccorder, ainsi que l'atmosphère native.
[Preuves](../evidence/tidewater/2026-09-25-cloud-panorama.md).

Atmosphère raccordée : les six passes de nuages consomment désormais les LUTs
produites par le graphe atmosphérique Bevy, sur le même appareil. Deux replays
réussissent avec des packs sans aucun résultat calculé source. Paramètres
temporels/soleil/irradiance encore acquis ; contrôleur et composition scène ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-native-atmosphere.md).

Nuages visibles : graphe partagé raccordé au ciel de la scène Bevy, comparaison
FHD sur la vue île. Région d'effet 91 038 pixels : erreur moyenne 0,1343/255,
P99 1/255, maximum 4/255 ; capture native répétée identique. Eau, réception des
ombres, dynamique et Studio restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-visible-sky.md).

Reflets nuageux raccordés : ciel, océan et lèvre des déferlantes partagent le
panorama natif. Vue côtière avec embruns : répétitions identiques, moyenne
0,167551/255 dans la région d'effet, P99 3/255, maximum local 111/255.
Les 21 buffers FFT et les particules actives par identité sont conservés ;
le rendu sans nuages reste identique. Ombres reçues, environnement dynamique,
animation longue et Studio restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-water-reflections.md).

Ombres nuageuses reçues : champ natif partagé avec terrain/CDLOD, village,
végétation/herbe, eau, déferlantes et embruns. Cas couvert original à 0,85 :
1 300 520 pixels dans la région d'effet, moyenne 0,196812/255, P99 4/255,
maximum 109/255 ; répétitions identiques. La pose par défaut est claire au sol
et ne suffit pas à qualifier cet effet. Environnement dynamique, longues
séquences, performance et édition restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-shadow-receivers.md).

Contrôleur nuageux natif : Frame/Settings calculés en Rust, 360 blocs exacts
sur chacune des vues côtière normale, couverte et île. Séquence CPU originale
de 32 transitions comparée ; léger résidu numérique du chemin JSON consigné.
Le graphe fonctionne sans fichiers locaux de paramètres enregistrés : dix
textures identiques au replay et image couverte inchangée. Frame global et
atmosphère restent acquis ; boucle GPU interactive et Studio encore ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-native-controller.md).

Historique GPU persistant : 120 pas sur 120 invocations du graphe, ressources
conservées et dix textures identiques au calcul en lot sur deux essais.
La scène assemblée retrouve l'image précédente, avec une variation de 14 pixels
(maximum 2/255) sur l'une des répétitions. Cette préparation bornée ne qualifie
pas encore l'animation interactive illimitée ni les performances.
[Preuves](../evidence/tidewater/2026-09-25-cloud-stepped-gpu.md).

1. Référence intacte, baseline et export reproductible.
2. Palmier et arbre feuillu complets dans un prototype Forge puis Studio.
3. Contrats d'import et île complète ; fidélité des données avant approximation.
4. Océan/côte puis ambiance complète ; comparaison à chaque effet.
5. Faune et jeu ; catalogue et édition sur les mêmes contrats.
6. Régressions, performances, packaging local et revue finale.

## Dernier incrément — budget autonome sur retours GPU natifs

Le contrôleur est raccordé au compteur GPU, sans calendrier enregistré dans
les entrées. Deux calculs isolés et deux captures de scène répètent budgets,
particules et écume. Les 119 retours utilisés pour 120 pas sont vérifiés.
La capture native reste identique au mode rejoué sur la pose à 2 s ; les écarts
avec Tidewater persistent (7 014 particules actives contre 7 013 notamment).
Le protocole attend chaque compteur pour reproduire la latence de la référence ;
la performance en temps réel et la parité animée restent ouvertes.
[Preuves et limites](../evidence/tidewater/2026-09-25-native-spray-budget.md).

## Incrément précédent — contrôleur Rust du budget d'émission

Le calcul adaptatif original est porté en Rust : historique, wrap du compteur,
lissage et bornes. Six tests passent. Sur deux acquisitions de 540 pas, chaque
budget f32 d'émission et d'après émission est identique à la source. Les
compteurs restent des entrées source appariées pour cette preuve ; le
raccordement aux readbacks GPU natifs reste à faire et la scène rejoue encore
le calendrier enregistré.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-budget-controller.md).

## Incrément précédent — embruns rendus dans la scène native

Les sprites originaux lisent les buffers de simulation natifs ; le ruban,
l'eau et le sable partagent les crêtes et l'état couplé du rivage. Deux captures
FHD à 2 s sont exactement reproductibles. Dans la région affectée, l'erreur
moyenne source/natif vaut 0,150027/255, avec un maximum local de 41/255.
La scène sans embruns et les états numériques antérieurs restent inchangés.
La parité animée, le budget adaptatif natif et les performances restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-sprites-native.md).

## Incrément précédent — référence visuelle des embruns

Les shaders, hooks et deux textures avec leurs 15 mipmaps sont acquis ; 23
fichiers de rendu se répètent exactement. La capture source avec embruns est
identique sur trois essais. Le vent global du probe numérique et celui de la
scène étaient différents ; une acquisition appariée prouve désormais les mêmes
7 013 particules source entre calcul et image. Le calcul natif apparié reproduit
le dépôt final de 282 unités, mais conserve des divergences de trajectoires et
d'écume accumulée. Le rendu natif des sprites reste à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-sprite-reference.md).

## Incrément précédent — retombées des embruns dans l'écume

La boucle GPU respecte maintenant FFT → rivage → émission → particules sur
120 pas. ShoreSim consomme les dépôts du pas précédent dans le buffer partagé.
L'effet modifie 5 478 texels d'écume natifs contre 5 504 source ; les deux essais
natifs se répètent exactement. Les particules, crêtes, compteurs et FFT restent
inchangés, comme les effets sur l'humidité et le flux (nuls dans cette pose).
L'écart local maximal d'écume vaut encore 0,080811 : la parité n'est pas acquise.
Rendu des sprites, état couplé dans la scène, budget adaptatif natif et écarts
numériques restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-shore-coupling.md).

## Incrément précédent — historique natif des embruns

Le FFT, l'émission et le déplacement s'enchaînent pendant 120 pas, depuis des
buffers vides et sans importer de résultats FFT ou de particules. Deux essais
natifs se répètent exactement modulo emplacements atomiques. Les 6 886
particules actives ne prouvent cependant pas la parité : 13 identités diffèrent,
un écart de trajectoire atteint 269,516 mm et un texel reçoit quatre unités de
dépôt supplémentaires. Le budget source est encore rejoué ; sa prise en compte
un pas trop tôt a été corrigée. Consommation des dépôts par ShoreSim, budget
adaptatif natif et rendu des sprites restent ouverts. Les 21 buffers FFT et
les décisions du test isolé de retombées restent inchangés.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-history.md).

## Incrément précédent — progression temporelle du FFT

Le FFT natif peut avancer d'un pas par soumission en conservant ses ressources
et son temps. Ses 21 sorties restent exactement identiques au calcul groupé
au premier pas et à 120 pas, ainsi qu'en mer agitée à 120 pas. La capture de
scène et les états rivage/crêtes sont inchangés. Le raccordement temporel
des embruns et du rivage reste à effectuer.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-stepped.md).

## Incrément précédent — émission et premier déplacement natifs des embruns

Le kernel d'émission original tourne sur le GPU Bevy depuis des buffers vides.
Au premier pas, les 47 particules ont les mêmes types et identifiants ; l'écart
maximal par coordonnée vaut 0,729 mm. Répétition native identique modulo ordre
atomique. L'acquisition conserve désormais la graine GPU effective sans
introduire de soumission intermédiaire. Les entrées FFT sont encore appariées
source. Le kernel de déplacement s'exécute ensuite sur les mêmes buffers :
47 particules concordantes, maximum de position 0,727 mm, répétition identique.
Le dépôt est nul à ce premier instant. Des mises à jour natives isolées à 2 s
et 9 s, depuis des états initiaux source, reproduisent exactement les décisions
de vie/mort et les dépôts pondérés de 349 et 448 gouttes dans les mêmes texels.
Historique natif couplé, consommation par ShoreSim et rendu restent à qualifier.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-emission.md).

## Incrément précédent — référence couplée des embruns

La simulation originale FFT → rivage → émission → particules est acquise sur
neuf secondes, avec budget d'émission et dépôts d'écume enregistrés. Deux
exécutions donnent les mêmes particules actives modulo permutation atomique,
6 886 à 2 s et 12 462 à 9 s. Les effets sur l'écume et son résidu sont mesurés.
Simulation et rendu natifs des embruns restent à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-reference.md).

## Incrément précédent — ruban visible des déferlantes

Le matériau original du ruban est rendu dans Bevy avec les crêtes GPU natives,
alpha prémultiplié et éclairage du ciel partagé. Sur la pose côtière à 2 s,
1 484 pixels modifiés concordent sur 1 485 ; erreur moyenne de canal dans la
région comparée 0,115/255. Capture répétée identique et régressions réussies.
Autres poses/temps, embruns, animation et qualification globale restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-lip.md).

## Incrément précédent — crêtes dans la scène et référence du ruban

Le calcul partagé est installé dans la scène côtière ; capture, FFT et ShoreSim
restent identiques et les crêtes concordent exactement avec le test natif isolé.
Le rendu source avec le ruban original est acquis et reproduit exactement.
Le matériau natif du ruban reste à raccorder ; aucune parité visuelle nouvelle
n'est déclarée et tous les critères TW complets restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-scene-inputs.md).

## Incrément précédent — FFT natif partagé vers les crêtes

Les crêtes consomment directement la texture GPU du FFT natif à 2 s.
L'exécution réussit sans fichiers de résultats FFT/crêtes dans les entrées :
690 crêtes actives, identifiants et segments dessinables concordants ; erreur
maximale par coordonnée de racine 1,431 mm. Les régressions FFT, ShoreSim,
calcul isolé et capture côtière restent identiques. Le ruban visible et
l'animation restent ouverts ; aucun critère TW complet n'est fermé.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-shared-fft.md).

## Incrément précédent — calcul GPU isolé des crêtes

Le kernel des crêtes tourne sur le GPU de Bevy, avec stations générées en Rust
et entrées FFT appariées de la référence. Aux trois instants contrôlés, mêmes
crêtes actives, identifiants et segments dessinables ; erreurs maximales par
coordonnée de racine de 1,20 à 2,15 mm. Répétition, build et Clippy réussis.
La chaîne FFT natif partagé → crêtes → ruban visible reste à raccorder ; ce
contrôle numérique isolé ne vaut pas qualification visuelle.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-compute.md).

## Incrément précédent — géométrie des vagues plongeantes

Les 577 stations de plage et le maillage de rubans sont calculés en Rust et
identiques octet pour octet à la référence (46 080 sommets, 131 328 indices).
Le kernel original des crêtes et ses entrées/sorties sont acquis et reproduits
sur trois instants jusqu'à 9 s. Le calcul GPU natif des crêtes, leur matériau
visible et les embruns restent à raccorder et à comparer.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-geometry.md).

## Incrément précédent — variations de surface SeaDetail

Les fonctions originales de rafales et de bandes lisses modulent maintenant
le matériau d'eau natif. Bruit et paramètres acquis et vérifiés ; deux vues
appariées à 2 s comparées et inspectées. Répétitions, anciens modes, états FFT
et ShoreSim, compilation et Clippy réussis. Dérive interactive et lignes
d'écume par vent fort restent à qualifier, ainsi que les effets encore absents.
[Preuves et limites](../evidence/tidewater/2026-09-24-sea-detail.md).

## Incrément précédent — sable humide raccordé au terrain CDLOD affiché

La copie du matériau utilisée par le terrain adaptatif reçoit désormais la
simulation du rivage. Le diagnostic direct d'humidité confirme la correction ;
l'erreur moyenne sur la région d'effet diminue de 1,276578 à 0,259991 /255
sur la plage et de 0,474857 à 0,124949 sur l'île. Deux vues inspectées,
répétition, anciens modes, FFT et ShoreSim vérifiés ; build et Clippy réussis.
Les écarts locaux, la dynamique et les effets encore absents restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-lod-material.md).

## Incrément précédent — rivage simulé visible dans Forge

ShoreSim utilise le graphe GPU de la scène et fournit une Image Bevy partagée
par l'eau et le terrain. SurfFoam et le hook original de sable mouillé sont
raccordés ; deux vues natives sont comparées et inspectées. Répétition, états
simulés, FFT et anciens modes sont vérifiés. Les écarts locaux d'écume et la
qualification animée restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-material-native.md).

## Incrément précédent — référence des matériaux du rivage simulé

Acquisition reproductible de 64 fichiers : WaterMaterial, ShoreSim, SurfFoam,
dentelle et hook de sable mouillé original. Deux vues source sont capturées et
inspectées ; leur état GPU est identique au pas 120 de l'oracle numérique.
Le rendu natif doit maintenant consommer sa propre texture ShoreSim dans les
matériaux de l'eau et du terrain, puis être comparé à ces vues.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-material-reference.md).

## Incrément précédent — arrondi du rivage corrigé

La contraction arithmétique du hash des vagues est corrigée dans le calcul
ShoreSim et le matériau visible. Les 73 728 valeurs contrôlées sont identiques
à la référence en Vulkan et DX12 sur le GPU testé. La simulation de 18 s est
reproductible et ses écarts sont fortement réduits ; deux vues de carte sont
recalculées et inspectées. Le raccordement de l'état persistant au sable et
à SurfFoam reste la prochaine étape, puis la comparaison animée.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-rounding.md).

## Incrément précédent — simulation persistante du rivage

ShoreSim s'exécute sur le GPU Bevy pendant 18 secondes depuis un état vide,
avec conservation de l'écume, de l'humidité et des résidus. Acquisitions et
répétition native reproductibles, compilation et Clippy réussis. La comparaison
révèle une divergence du hash trigonométrique qui module les vagues ; la parité
numérique et le raccordement aux matériaux restent ouverts.
[Preuves et diagnostic](../evidence/tidewater/2026-09-24-shore-simulation.md).

## Incrément précédent — vagues de rivage et nappe de ressac

ShoreWaves est raccordé au relief final et au matériau natif : déferlement,
écume immédiate et remontée sur le sable. Deux vues appariées sont comparées,
avec mesure spécifique des zones modifiées par l'effet. Les acquisitions,
la répétition de plage et la régression sans rivage sont exactes. Le champ de
propagation reste acquis ; sa régénération Rust, ShoreSim et les embruns sont ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-waves.md).

## Incrément précédent — matériau optique de l'eau visible

Les fonctions WaterMaterial originales sont exécutées dans Bevy : normales,
Fresnel, reflets ciel/écran, réfraction, absorption/diffusion et mélange d'écume.
Deux vues appariées donnent un écart RGB moyen d'image de 0,177 et 0,147 /255.
La répétition de la vue mer, la géométrie et les sorties FFT restent exactes.
La parité complète, les vagues de côte, l'immersion et le rendu final restent
ouverts ; les paramètres ambiants sont encore figés pour la comparaison.
[Preuves et limites](../evidence/tidewater/2026-09-24-water-optics.md).

## Incrément précédent — entrées de scène pour l'optique de l'eau

La surface lit maintenant couleur et profondeur opaques avant la passe d'eau,
sur le device Bevy. Comparaison indépendante sur la vue vers la mer ; géométrie
et sorties FFT inchangées. Les modules du matériau original et les onze mips
de sa texture d'écume sont acquis de manière reproductible. Le rendu optique
lui-même reste à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-water-scene-inputs.md).

## Incrément précédent — surface océanique déplacée visible

La surface utilise les déplacements FFT natifs dans la scène Bevy, avec le
relief et les fonctions WaterSurface d'origine. Deux vues comparent 61 et 132
tuiles exactes ; les silhouettes blanches présentent 1 et 14 pixels d'écart.
Les 21 sorties FFT et la régression du ciel sont identiques aux preuves
précédentes. Le blanc est un diagnostic géométrique ; le matériau optique,
le rivage et la boucle interactive restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-geometry.md).

## Incrément précédent — maillage et sélection océanique exacts

La grille native réutilisable correspond aux 1 089 sommets et 6 144 indices
source. Sélection des racines mobiles et transitions exactes sur 70 caméras,
8 695 tuiles ordonnées, douze lignes de morphing. Les fonctions WaterSurface
d'origine sont acquises pour le raccordement des déplacements ; elles ne sont
pas encore exécutées dans un rendu de surface native.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-lod.md).

## Incrément précédent — graphe océan partagé avec la scène

Le calcul FFT s'installe sur le device de la scène Bevy et expose ses deux
textures à quatre couches/neuf mips. Les 21 sorties sont identiques au test
isolé, l'image de l'île est inchangée et la régression tempête passe.
La capture attend aussi la fin des lectures GPU. Ce raccordement ne dessine
pas encore de surface d'eau et conserve un historique fixe de 120 pas.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-shared-graph.md).

## Incrément précédent — FFT, écume et mips natifs

Neuf kernels d'océan exécutés sur le device Bevy : initialisation, transformées
inverses lignes/colonnes, accumulation d'écume et neuf niveaux mipmap. Six
snapshots appariés (1/60/120 pas, défaut/tempête), tous finis. Écart maximum
de déplacement par cascade : 1,95 mm / 3,91 mm ; répétition finale exacte.
La boucle de scène et le matériau d'eau restent à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-fft.md).

## Incrément précédent — spectre océanique natif

Les trois kernels d'initialisation des quatre cascades océaniques s'exécutent
sur le device Bevy avec paramètres Rust. Douze blocs uniformes sont exacts
dans chacun des deux cas (défaut/tempête). Écarts GPU maximaux de 3,93e-6 et
5,78e-6 sur le spectre. Références temporelles complètes acquises pour la suite,
y compris écume de tempête ; pas encore de FFT animée ni de surface d'eau native.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-spectrum.md).

## Incrément précédent — ciel clair visible et soleil natifs

Les LUTs calculées sur le même device Bevy alimentent le fond de ciel et le
disque solaire. Deux vues fixes 1920×1080 comparées à la source : erreur RGB
moyenne de 0,1154/255 sur l'île et 0,0315/255 vers le soleil. Les différences
résiduelles et une variation d'un pixel entre deux captures sont conservées.
Le village sans ciel et les quatre sorties atmosphériques restent identiques.
Le fond bleu sous l'horizon n'est pas un océan ; eau, nuages et nuit restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-visible-sky.md).

## Incrément précédent — frontière atmosphérique Vulkan stabilisée

Diagnostic GPU du choix de racine à la frontière puis correction conditionnelle
de la distance. Erreur maximale de transmission réduite de 1 à 0,0122 ; 94
pixels fortement différents deviennent un seul. Sorties Dx12 inchangées ;
écarts résiduels conservés. Le ciel visible reste à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-atmosphere-boundary.md).

## Incrément précédent — kernels atmosphériques natifs

Transmission, diffusion multiple, luminance du ciel et irradiance calculées
par Bevy ; paramètres Rust exacts à deux hauteurs. Acquisitions reproductibles,
build et Clippy réussis. Une anomalie de transmission est localisée à la
dernière ligne sous Vulkan ; le même binaire Dx12 réduit fortement l'écart.
Cause numérique précise, raccordement au ciel visible et nuages encore ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-atmosphere-compute.md).

## Incrément précédent — réception PCSS/PCF des ombres natives

Terrain, village, végétation et herbe reçoivent les ombres des objets calculées
par Bevy, avec le filtre source et les paramètres de cascades Rust. Village
et forêt comparés et inspectés : erreurs moyennes 0,427 et 0,778 /255 par
canal ; des écarts localisés subsistent. Captures reproductibles, régression
sans filtre inchangée et refus des modes incohérents vérifiés. Animation,
rendu final et parité complète restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-object-shadow-filter.md).

## Incrément précédent — passes d'ombre partagées avec la scène couleur

Le prototype visuel installe les mêmes passes natives que l'outil headless,
sur son device Bevy. Profondeurs village et forêt identiques aux captures
natives précédentes ; couleur et ombre du relief village inchangées.
La texture est disponible dans la scène, mais le filtre des matériaux reste
à raccorder. [Preuves et limites](../evidence/tidewater/2026-09-24-shared-shadow-graph.md).

## Incrément précédent — profondeur des ombres végétales natives

Les cinq groupes de plantes occulteurs rejoignent terrain et village dans
les trois cascades Bevy. Deux vues comparées, avec identifiants et ordre des
instances exacts. Les captures natives sont reproductibles et la régression
terrain/village reste identique. Des écarts ponctuels de masque/profondeur
subsistent ; le filtre et la réception dans la scène couleur restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-plant-shadow-depth.md).

## Incrément précédent — terrain dans les cartes d'ombre natives

Grille CDLOD instanciée et morphée depuis la caméra principale dans les trois
cascades, avec hauteurs et programmes source. Deux vues comparées : couverture
exacte ; profondeur exacte sur la vue île, 1 996 texels différents sur la vue
village (maximum 4,53e-6 normalisé). Plantes et filtrage restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-terrain-shadow-depth.md).

## Incrément précédent — profondeur native des ombres du village

Les trois couches d'ombre sont générées par le GPU Bevy pour les bâtiments
opaques et l'enseigne. Deux caméras comparées : profondeurs identiques à
l'octet près aux six couches source (dont deux vides sur la vue éloignée).
Terrain, plantes, filtrage et réception dans la scène assemblée restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-village-shadow-depth.md).

## Incrément précédent — préparation des ombres portées

Calcul Rust des trois caméras d'ombre et de leur actualisation vérifié contre
89 états source : 20 025 scalaires, erreur maximale 2,85e-13. Oracle GPU source
des objets acquis et répété. Les passes de profondeur et le filtre des ombres
portées restent à raccorder au rendu natif.
[Preuves et limites](../evidence/tidewater/2026-09-24-sun-cascades.md).

L'herbe est également raccordée au prototype : trois niveaux de détail,
masque et sélection source, contrôles géométriques et scène assemblée.
[Preuves herbe](../evidence/tidewater/2026-09-24-grass.md).

## Incrément précédent — ombres du relief calculées sur le GPU natif

Rust reconstruit exactement la pyramide de hauteurs source ; Bevy exécute
le kernel d'ombre et partage sa sortie entre terrain, village et végétation.
Lecture GPU comparée au source : 22 texels sur 262 144 diffèrent dans la version
à soleil paramétrable, hauteur maximale 0,03125 m ; distance d'occulteur exacte.
Vues village/île comparées, régression sans ombres inchangée. Ombres des objets,
nuages et parcours de modification interactive restent à qualifier.
[Preuves et limites](../evidence/tidewater/2026-09-24-hill-shadows.md).

## Incrément précédent — éclairage commun terrain, village et végétation

Les trois familles consomment le même soleil et le même environnement acquis,
avec une texture de ciel partagée. Deux vues assemblées comparées à l'oracle
source sans ombres : erreur moyenne par canal 0,464934/255 au village et
0,166189/255 depuis l'île. Albédo village et végétation isolée inchangés.
Les ombres, l'océan, le ciel dynamique et le rendu final restent à raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-scene-lighting.md).

## Incrément précédent — éclairage du feuillage par le ciel acquis

Le natif consomme les SH diffus et le cube spéculaire calculés par les
programmes originaux d'atmosphère/nuages, avec le soleil réel et la compensation
de diffusion source. Comparaisons village/forêt : erreurs moyennes par canal
0,056246/255 et 1,011835/255. Écarts persistants, environnement figé issu d'un
état froid ; ni port du ciel dynamique ni fidélité finale ne sont acquis.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-environment.md).

## Incrément précédent — lumière directe du feuillage

Le programme natif reprend les termes directs source, intensité spéculaire et
transmission incluses. Sous soleil blanc unitaire, erreurs moyennes par canal
de 0,015624/255 au village et 0,339026/255 en forêt ; silhouettes inchangées.
Ce contrôle exclut ciel, ombres et traitements finaux. Il ne qualifie pas
l'éclairage complet et conserve des écarts dans la forêt.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-direct-light.md).

## Incrément précédent — végétation proche et lointaine

Les 65 383 enregistrements des six groupes sont raccordés au prototype natif,
avec huit niveaux de géométrie et leurs matériaux source. Sélection initiale
Rust, matrices exactes, maillages partagés et fenêtres de distance GPU.
Trois vues comparées au source indépendant : IoU des silhouettes de 0,997886
à 0,999740 ; écarts résiduels documentés. Palmiers et sous-bois apparaissent
autour du village. Éclairage/ciel/ombres/temporalité restent non fidèles,
herbe absente, sélection en mouvement et Studio non raccordés.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-scene.md).

## Incrément précédent — références reprises et canopée partagée

Les six références upstream sont désormais de vrais PNG Full HD sans mosaïque,
dans `baseline-003/`. Les acquisitions précédentes restent exclues et conservées.
Les 18 343 arbres/arbustes lointains du prototype natif partagent un maillage et
un matériau ; leurs données source sont adressées par instance. Masque isolé
et image combinée avec village/terrain strictement identiques au prototype
précédent, sans qualification de performances ni parité de scène entière.
[Preuves et limites](../evidence/tidewater/2026-09-24-canopy-instancing.md).

## Premier incrément vérifié — 24 septembre 2026

- Source exacte conservée ; build upstream et tests de logique réussis.
- Six captures upstream FHD inspectées, caméras/heures/métadonnées et hashes dans
  `.tidewater-integration/baseline/index.json` (hors dépôt). Ce sont des vues fixes,
  pas encore des parcours ni une preuve de déterminisme temporel.
- Exports indépendants `source-006` et `source-007` identiques : 105 fichiers de
  données, terrain après fondations, masques, 65 383 instances, huit prototypes,
  modules WGSL végétation et atlas de feuilles avec tous ses mipmaps.
- Contrat raster générique + lecteur Rust : 1 030 sondes réelles, erreur maximale
  2,842170943040401e-14 m. Pas encore branché au document de zone ni aux commandes.
- `cargo test -p forge_world -p forge_project --locked -j1` : 416 tests réussis.
- Clippy strict sur toutes les cibles de ces deux crates : réussi.
- Compilation WASM de ces deux crates : réussie, sans qualification du rendu Web.
- Réparation de compatibilité d'un test existant : champ `AuditStamp::legacy()`
  ajouté au reçu de `tests/path_surface.rs`, conforme au reçu preview courant.
- Prototype `tidewater_plant_probe` compilé et exécuté : capture native FHD inspectée,
  deux exécutions avec PNG identiques ; Clippy strict réussi. Géométrie et surface
  originales, éclairage Bevy provisoire, ombres/translucence/LOD temporel et Studio
  non qualifiés. [Preuves et limites](../evidence/tidewater/2026-09-24-first-increment.md).

Les journaux et données volumineuses restent sur E:, sous le répertoire du chantier.
Les serveurs de référence ont été arrêtés et l'onglet temporaire fermé après capture.
Toutes les lignes TW restent ouvertes tant que leur critère complet n'est pas satisfait.

Deuxième incrément : exports `source-008/009` identiques (207 fichiers), paramètres
runtime des 65 383 plantes, atlas et shaders lointains conservés. Prototype Bevy
de canopée avec les 18 343 records arbres/arbustes, capture inspectée. Mesures de
silhouettes : proche 98,99–99,42 %, feuillu lointain 100 % sur une pose, canopée
lointaine 99,988457 % (16 pixels différents). Clippy strict et régression proche
réussis. Éclairage, animation, production/Studio et scène complète non qualifiés.
Voir les preuves ci-dessus ; aucun de ces chiffres ne ferme la parité visuelle.

Troisième incrément : terrain canonique v4 opt-in avec source raster hashée,
retouches sur la grille auteur existante, Undo/Redo et archive de session testés.
419 tests projet/monde réussis, Clippy et compilation WASM de ces crates réussis.
Assemblage natif terrain 2048 m + canopée exécuté et capture inspectée ; matériau
gris et grille fixe encore diagnostiques. Export des shaders et cartes de terrain
originales (`source-010/011`, 226 fichiers identiques). La résolution des blobs
dans la zone, Studio, Rapier, CDLOD et matériaux source restent ouverts.
[Preuves et limites du terrain v4](../evidence/tidewater/2026-09-24-raster-document.md).

Quatrième incrément : matériaux de terrain source rendus dans Bevy avec leurs
mipmaps originaux (exports `source-012/013`, 260 fichiers identiques). Vues île et
côte inspectées ; caméra côtière reprise de la référence. Clippy strict réussi,
capture côtière répétée identique et régression silhouette proche inchangée.
Éclairage Bevy provisoire, humidité/ombre solaire/CDLOD/océan/contenu complet et
Studio restent ouverts. [Preuves matériau](../evidence/tidewater/2026-09-24-terrain-surface.md).

Cinquième incrément : sélection CDLOD Rust identique à la source pour 47 vues,
2 845 tuiles ordonnées, 405 bornes et neuf paramètres de transition. Déplacement
GPU original raccordé au prototype Bevy : masques côtiers et île identiques
pixel pour pixel en FHD, sur deux poses fixes. La côte utilise 62 tuiles et
198 400 triangles ; l'île 18 tuiles et 57 600 triangles. Ces nombres ne valent
pas benchmark ni parité des matériaux, ombres ou scènes complètes. Instanciation
produit, caméra mobile, retouches GPU et Studio restent ouverts.
[Preuves CDLOD](../evidence/tidewater/2026-09-24-terrain-cdlod.md).

Sixième incrément : village rendu dans la scène native avec sept matériaux source,
14 textures et leurs mipmaps, 31 éléments de rendu (bâtiments/accessoires regroupés,
enseigne et lanternes). Exports indépendants `source-016/017` identiques, 503
fichiers de données vérifiés. Une passe d'albédo directe donne un pixel de contour
différent sur 351 564 pixels occupés et une erreur moyenne de canal de 1,1985/255 ;
les couleurs ne sont donc pas déclarées identiques. Éclairage, poissons du marché,
animation des groupes et séparation en objets Studio restent ouverts.
[Preuves village](../evidence/tidewater/2026-09-24-village.md).

## Vérifications obligatoires

Adaptateur Bevy du paquet : maillages locaux/de référence et bibliothèque de
textures/matériaux préparables par le moteur partagé. Check et Clippy ciblé
passent ; exécution réussie sur 23 bâtiments et 111 morceaux. Le prototype natif
charge maintenant le paquet sans les 598 anciens fichiers de bâtiments : image
et ombres identiques au rendu précédent, en pose initiale et après déplacement
enregistré d'une maison. Raccordement au viewport Studio encore ouvert.
[État de l'adaptateur](../evidence/tidewater/2026-09-26-package-bevy-assets.md).

Racines de bâtiments importés : création depuis le placement canonique,
maillages partagés et identité de sélection commune avec Studio. Contrôle ECS
de duplication/suppression réussi et Studio compilé en WASM ; résolution du
paquet dans le contrôleur Studio et qualification visuelle encore ouvertes.
[Entités de placement](../evidence/tidewater/2026-09-26-placement-entities.md).

La construction des matériaux du village est maintenant partagée avec le moteur
et utilisée par le prototype. Capture native depuis le paquet : pixels et deux
buffers d'ombres identiques à la capture précédente.
[Construction des matériaux](../evidence/tidewater/2026-09-26-material-construction.md).

Éclairage du village paramétrable : soleil et coefficients diffus SH ne sont
plus figés dans le shader du paquet 003. Capture aux valeurs acquises : deux
pixels à un niveau d'écart maximal, ombres identiques. Variations indépendantes
du soleil et de l'ambiance diffuse observées et mesurées ; raccordement aux
lumières Studio et à l'horloge de scène encore ouvert.
[Éclairage du village](../evidence/tidewater/2026-09-26-village-scene-lighting.md).

Cube HDR : chargeur partagé natif/WebGPU, validation des dimensions, mipmaps et
valeurs finies. Bytes acquis conservés exactement ; nouvelle capture native
identique à la précédente, ombres comprises. Le calcul GPU du ciel reste à
sortir du module d'exemple pour le raccordement Studio.
[Cube d'environnement partagé](../evidence/tidewater/2026-09-26-shared-environment-cube.md).

Les quatre kernels atmosphériques sont désormais exécutés par le module moteur
partagé, sans I/O ni nouveau périphérique GPU. Les quatre readbacks sont identiques
à la preuve native stabilisée ; compilation WASM réussie. Le calcul reste fixe :
horloge/caméra, adoption Studio et exécution WebGPU restent à qualifier.
[Atmosphère partagée](../evidence/tidewater/2026-09-26-shared-atmosphere.md).

Le matériau et la géométrie du ciel sont également partagés. Le shader vérifié
est installé en mémoire, sans chemin AssetServer vers la fixture. Capture native
identique à la précédente (zéro pixel différent, deux ombres identiques), Clippy
et compilation WASM réussis. Nuages GPU et raccordement Studio restent ouverts.
[Ciel partagé](../evidence/tidewater/2026-09-26-shared-sky.md).

La chaîne GPU nuageuse est extraite dans le moteur partagé : météo/bornes,
ombres, volume, panorama et historique. Dix textures identiques à l'octet sur
120 pas/120 passages ; capture assemblée identique, ombres inchangées.
Check natif/WASM et Clippy ciblés passent. Séquence encore bornée et paramètres
globaux acquis : boucle interactive et raccordement Studio restent ouverts.
[Nuages partagés](../evidence/tidewater/2026-09-26-shared-clouds.md).

Paquet d'environnement séparé et reproductible : 266 fichiers, 2 677 437 octets,
chargement sync/async borné et épinglé. La candidate Studio référence désormais
ce paquet avec les bâtiments, adoption en mémoire après vérification des deux.
Check WASM réussi ; nouveau parcours navigateur et raccordement GPU restent
à vérifier. Les ombres terrain/objets ne sont pas incluses dans ce paquet.
[Paquet d'environnement](../evidence/tidewater/2026-09-26-environment-package.md).

Le nouveau chargement conjoint est maintenant exécuté dans le navigateur :
1090 réponses attendues et statut de succès. Un shader altéré est refusé,
les requêtes suivantes cessent et le réessai après restauration réussit.
Qualification du transport uniquement, sans backend Tauri ni scène rendue.
[Preuve navigateur environnement](../evidence/tidewater/2026-09-27-studio-environment-transport.md).

La préparation moteur depuis les bytes du paquet est raccordée au chargement
Studio avant adoption. Mode natif `--package` : dix textures identiques après
120 pas, cube HDR conservé ; données d'éclairage/cube invalides rejetées même
avec des hashes cohérents. Check Studio WASM réussi. Les caches préparés ne
sont pas encore installés dans le renderer Studio.
[Préparation moteur du paquet](../evidence/tidewater/2026-09-27-package-environment-engine.md).

Atmosphere chargeable apres demarrage : extraction main/render enregistree une
seule fois, remplacement des cibles depuis le monde principal. Deux generations
natives successives qualifiees, quatre sorties identiques a la reference ;
parcours initial egalement inchange. Consommation
du cache Studio encore ouverte.
[Chargement tardif de l'atmosphere](../evidence/tidewater/2026-09-27-late-atmosphere.md).

Nuages chargeables apres demarrage : entrees et atmosphere extraites ensemble,
historique et caches GPU remis a zero au remplacement. Deux generations de
120 pas qualifiees ; dix sorties identiques a la reference. Entrees invalides
refusees sans modifier les cibles actives. Consommation du cache Studio raccordee ci-dessous.
[Chargement tardif des nuages](../evidence/tidewater/2026-09-27-late-clouds.md).

Studio consomme maintenant la preparation de l'environnement dans son Update,
apres enregistrement des graphes avec les plugins de rendu. Cube, shader, LUTs,
nuages et uniforms sont conserves ensemble pour les consommateurs de scene.
Le chemin partage est qualifie nativement (dix sorties identiques), le check
WASM passe. Verification navigateur avec zone chargee et affichage encore ouverts.
[Ressources residentes Studio](../evidence/tidewater/2026-09-27-studio-resident-environment.md).

Apercu explicite du ciel de reference : camera hors ecran dans le renderer
existant, pixels affichables dans la bibliotheque sans changer la carte. Preuve
native inspectee ; les 200 premieres lignes correspondent pixel par pixel au
ciel de la scene assemblee. La partie sous l'horizon ne contient pas de terrain
ni d'eau. Action navigateur avec zone active encore a qualifier.
[Apercu du ciel](../evidence/tidewater/2026-09-27-sky-preview.md).





Lecteur Rust du paquet : 23 bâtiments résolus depuis la seule racine du paquet ;
corruption, dépendance absente et mauvais hash de manifeste refusés. Consommation
par le prototype natif validée ; intégration au viewport Studio encore ouverte.
[Preuves du lecteur](../evidence/tidewater/2026-09-26-package-reader.md).

Lecteur asynchrone ajouté pour l'adaptateur navigateur : mêmes validations que
la voie native, suspensions et arrêts sur corruption vérifiés sur le paquet
complet. Compilation WASM réussie ; transport HTTP et viewport à raccorder.
[Acquisition asynchrone](../evidence/tidewater/2026-09-26-package-async.md).

Studio propose désormais la vérification/lecture du paquet local dans la fiche
de bibliothèque et conserve les bytes vérifiés. Check WASM et relecture de la
copie de déploiement réussis. Parcours HTTP réel validé dans Studio navigateur :
823 requêtes réussies et statut des 23 bâtiments vérifiés affiché. Projection
visuelle encore à raccorder ; Clippy Studio arrêté sur la longueur de `atelier_ui::bind_main`.
[Transport Studio](../evidence/tidewater/2026-09-26-studio-package-transport.md).

Paquet local des 23 bâtiments : géométries, matériaux, textures, shader adapté et
notice MIT rassemblés ; hashes et fermeture des références vérifiés. Chargement
produit et ressources d'éclairage dynamiques restent ouverts.
[Preuves du paquet](../evidence/tidewater/2026-09-26-building-package.md).

Matériau village et paramètres d'ombres partagés dans `forge_engine` ; les deux
prototypes compilent avec ces types. Capture et ombres identiques après une
première exécution échouée ; raccordement Studio et stabilité globale ouverts.
[État du matériau partagé](../evidence/tidewater/2026-09-26-shared-village-material.md).

Le chargeur CPU des maillages village est partagé dans `forge_engine` et consommé
par le prototype. Test ciblé et Clippy strict passent ; capture native et buffers
d'ombres identiques après refactoring. Matériaux partagés et Studio ouverts.
[État du chargeur partagé](../evidence/tidewater/2026-09-26-shared-village-mesh.md).

Suppression native d'une maison et de son ombre vérifiée depuis un workspace
sauvegardé ; Undo rétablit exactement image et buffers d'ombres. Studio ouvert.
[Preuves suppression/Undo](../evidence/tidewater/2026-09-26-building-removal-render.md).

Placements rendus : le village consomme les transformations sauvegardées et les
partage avec les ombres. Pose initiale pixel-identique ; maison au premier plan
déplacée de 12 m et capture inspectée. Studio et autres transformations ouverts.
[Preuves du raccordement natif](../evidence/tidewater/2026-09-26-building-placement-render.md).

Placements canoniques : 23 bâtiments insérés par commande, déplacement de 12 m,
suppression, sauvegarde/relecture et annulations exactes des placements vérifiés.
Rendu et Studio ne consomment pas encore ces références.
[Preuves des placements](../evidence/tidewater/2026-09-26-building-placements.md).

Catalogue importé : 23 descripteurs de bâtiments avec identité stable, hash et
provenance externe MIT ; résolution après sérialisation, 30 tests assets et
Clippy réussis. Il s'agit de métadonnées ; UI et dépendances portables ouvertes.
[Preuves catalogue](../evidence/tidewater/2026-09-26-imported-catalog.md).

Repères locaux des 23 bâtiments préparés : positions et normales réversibles
à moins de 1e-6 m/composante, attributs source conservés, deux sorties identiques.
Provenance MIT à représenter dans le catalogue ; placements et Studio ouverts.
[Preuves des repères](../evidence/tidewater/2026-09-26-building-frames.md).

Village séparé rendu nativement : 145 nœuds, attributs indexés conservés,
ombres exactes. Capture identique hors 14 pixels d'embruns (maximum 2/255),
avec particules identiques modulo slots. Pivots, autorité d'instances et Studio
restent à raccorder ; performances non qualifiées.
[Preuves du rendu séparé](../evidence/tidewater/2026-09-25-split-village-render.md).

Partition statique du village : 137656 triangles conservés, attribution exacte
aux 23 bâtiments ou au décor restant par décalages de construction. 121 plages
reconstituent les flux d'indices originaux ; rendu natif séparé à qualifier.
[Preuves de partition](../evidence/tidewater/2026-09-25-village-partition.md).

Matériaux des bâtiments séparés : remappages source appliqués avant conversion
f32 ; 65363 triangles retrouvent exactement tous leurs attributs dans les lots
acquis. Deux préparations donnent 560 fichiers identiques. Partition du décor
restant et rendu séparé encore à réaliser.
[Preuves des matériaux](../evidence/tidewater/2026-09-25-building-materials.md).

Village éditable : 23 bâtiments séparés avant fusion en 111 portions ; 667
fichiers identiques entre deux acquisitions. Géométrie finale et terrain
inchangés par l'observation. Matériaux remappés, catalogue et instances Studio
restent à raccorder.
[Preuves de séparation](../evidence/tidewater/2026-09-25-building-parts.md).

Audit de direction côtière : la référence utilise `dirOn=0` et le champ direct.
La texture filtrée optionnelle n'est donc pas un raccordement manquant pour
cette route ; les entrées qui l'activent sont désormais refusées explicitement.
[Preuves du parcours source](../evidence/tidewater/2026-09-25-shore-direction-route.md).

Stations de déferlement : reconstruites depuis le terrain effectif, coordonnées
exactes avec la source pour 577, 555 et zéro point. Buffers, rubans et ombres
d'embruns adaptés ; cas vide sans émission et Undo pixel-identique vérifiés.
[Preuves des stations retouchées](../evidence/tidewater/2026-09-25-authored-breaker-stations.md).

Simulations côtières couplées : champ et corrections de hauteur canoniques
transmis au ressac, aux crêtes et aux embruns. Captures locales/globales sans
erreur GPU ; Undo restitue exactement image, ressac, crêtes, dépôts et ensemble
des particules. Stations de déferlement encore à reconstruire sur le terrain effectif.
[Preuves des simulations retouchées](../evidence/tidewater/2026-09-25-authored-coupled-terrain.md).

Propagation côtière : calcul Rust identique à la source sur 1048576 valeurs,
également après relief local et élévation globale ; champ dérivé raccordé au
matériau optique et Undo pixel-identique. Les simulations couplées doivent encore
consommer ces données effectives.
[Preuves du champ côtier](../evidence/tidewater/2026-09-25-authored-shore-field.md).

Rendu de l'eau après retouche : hauteurs canoniques raccordées à la géométrie
et à l'optique ; essai local avec 203 plantes déplacées, Undo pixel-identique
après conservation du shader source sans correction. Le recalcul du champ
côtier, du ressac et des déferlements reste ouvert.
[Preuves eau/terrain](../evidence/tidewater/2026-09-25-authored-water-terrain.md).

AO du relief : formule Rust identique sur les 4194304 texels source, et oracle
JavaScript exact pour bosse locale/élévation globale. Alpha raccordé aux mipmaps
GPU ; Undo local pixel-identique. Roche, côte, eau et autres consommateurs restent
ouverts. [Preuves AO](../evidence/tidewater/2026-09-25-authored-terrain-ao.md).

Normales macro corrigées : 11462 texels validés indépendamment, onze passes de
mipmaps GPU, texture partagée terrain/optique de l'eau, sorties d'ombre inchangées
et Undo pixel-identique. AO, roche, autres masques et qualification des mipmaps
restent ouverts. [Preuves normales](../evidence/tidewater/2026-09-25-authored-terrain-normals.md).

Ombres lointaines du relief édité : pyramide corrigée raccordée au bake GPU,
hash contrôlé indépendamment, 885 valeurs de sortie modifiées et annulation
exacte de la texture et de l'image. Normales/mipmaps et autres dérivés restent ouverts.
[Preuves ombres de relief](../evidence/tidewater/2026-09-25-authored-hill-shadows.md).

Relief auteur GPU : grille de deltas séparée raccordée au terrain CDLOD et aux
ombres proches ; reprojection des plantes consommée par les deux passes.
Essai workspace : bosse locale de12m, 203 plantes déplacées, Undo pixel-identique.
Normales, masques, ombres lointaines, eau, collisions et Studio restent ouverts.
[Preuves relief GPU](../evidence/tidewater/2026-09-25-authored-relief-gpu.md).

Terrain workspace : bloc source retiré de la fixture, rendu identique depuis
l'autorité sauvegardée et entrée partagée avec les ombres. Projection monde des
65383 plantes sur élévation éditée testée avec sauvegarde/relecture et Undo exact ;
relief édité GPU et consommation de cette projection restent ouverts.
[Preuves terrain/reprojection](../evidence/tidewater/2026-09-25-workspace-terrain-and-plant-projection.md).

Chargement workspace du prototype : végétation et retouches proviennent de la
référence de zone sauvegardée et des blobs de l'autorité. Fixture sans population
de secours, capture identique au rendu de l'archive, refus zone absente/blob
corrompu, autorité source inchangée. Les autres ressources et corrections ne
sont pas encore raccordées au workspace.
[Preuves du chargement workspace](../evidence/tidewater/2026-09-25-workspace-vegetation-render.md).

Retouches végétales dans le prototype natif : archives de commandes relues,
exclusion locale de 515 plantes confirmée indépendamment, suppression visible
limitée à la pente gauche, annulation pixel-identique à la référence ; densité
nulle sur 65383 plantes rendue sans erreur GPU. Raccordement Studio interactif,
placements manuels et autres corrections restent ouverts.
[Preuves des retouches rendues](../evidence/tidewater/2026-09-25-authored-vegetation-render.md).

Complément herbe : trois géométries et masque source raccordés au prototype
natif ; sélection village identique (9/28/90 cellules), silhouette IoU
0,9995915 à caméra/temps fixes. La scène assemblée inclut désormais l'herbe,
sans qualification temporelle ou Studio.
[Preuves herbe](../evidence/tidewater/2026-09-24-grass.md).

- Export répété : mêmes hashes hors métadonnées temporelles ; aucune source modifiée.
- Inventaire complet avec statut explicite des éléments non encore extraits.
- Tests invalides : taille, chemins, hash, NaN, doublons, version, quotas.
- Promotion/modification/suppression d'une instance : pas de réapparition après
  régénération, sauvegarde, fermeture et réouverture.
- Terrain, eau, collisions et végétation concordent après une retouche.
- Même objet trouvable et plaçable dans une autre carte depuis Studio.
- Régression des cartes existantes et contrôles workspace pour contrats partagés.
- Captures et métriques identifient renderer, source, build, résolution et paramètres.
- Un export technique ne vaut ni asset qualifié, ni carte Forge chargée, ni parité.

## Limites connues au lancement

Végétation limitée à 512 m ; placements manuels plafonnés à 4096 ; source de
hauteur actuelle procédurale. La carte upstream couvre 2048 m avec heightmap
2048². Les silhouettes végétales et textures village dépendent de shaders.
La consommation séquentielle du RNG du village participe à sa composition.
Ne pas reconstruire avec des seeds arbitraires ni exporter avant les fondations.


Regroupement des entrees du prototype natif : sept groupes, 1539 fichiers,
291477343 octets controles. Rendu depuis le nouveau dossier identique au rejeu
contemporain depuis les anciennes racines ; ombres identiques. Un ecart de 14
pixels (2/255 maximum) subsiste par rapport a la capture historique et reste
documente. Le paquet n'est pas un module ni la carte Studio finalisee.
[Paquet du prototype](../evidence/tidewater/2026-09-27-preview-scene-bundle.md).


Preparation des 23 batiments dans les assets du viewport Studio : geometries et
textures validees avant adoption, partage des handles qualifie par le probe natif.
Affichage/placement Studio encore ouvert.
[Residences batiments](../evidence/tidewater/2026-09-27-studio-building-residency.md).

Ombres de relief extraites dans le moteur partage : chargement tardif et remplacement
GPU qualifies, deux generations distinctes puis retour exact aux ombres de reference.
Scene native identique a une capture Forge anterieure ; edition Studio non qualifiee.
[Ombres partagees](../evidence/tidewater/2026-09-27-shared-hill-shadow.md).

Preparation GPU des casters opaques du village partagee dans le moteur :
rejeu natif sans difference de pixel ni d'ombres. Extraction completee ensuite
par le graphe terrain/feuillages ci-dessous ; adoption Studio encore ouverte.
[Ombres opaques partagees](../evidence/tidewater/2026-09-27-shared-opaque-shadow.md).

Graphe complet des trois cascades partage : opaque, terrain et feuillages.
Adoption tardive et remplacement qualifies sur GPU natif ; rendu de scene et deux
buffers d'ombres strictement identiques a la baseline Forge precedente.
Compilation Studio WASM reussie, execution WebGPU et edition Studio non qualifiees.
[Graphe d'ombres partage](../evidence/tidewater/2026-09-27-shared-object-shadow-graph.md).

Preparation des ombres du terrain depuis le document Forge extraite dans le moteur,
avec corrections de hauteur partagees par terrain/ocean/rivage. Clippy moteur et
verification des examples passes ; test CPU des retouches passe, scene native
et ombres identiques a la baseline precedente, compilation Studio WASM reussie.
[Terrain edite et ombres](../evidence/tidewater/2026-09-27-authored-terrain-shadow.md).

Alignement des placements sur raster et retouches partage dans forge_world :
6 tests de terrain importe et 4 regressions de placement procedural passes,
Clippy strict passe. Raccordement des controles Studio au raster encore ouvert.
[Alignement sur relief importe](../evidence/tidewater/2026-09-27-sampled-placement-alignment.md).

Transport du raster de la zone ouverte ajoute au protocole natif et au client
Studio : blocs lies au document, SHA-256 complet, source workspace geree verifiee.
Insertion, position precise, glissement et poignees utilisent le sampler importe.
Tests du host et compilations natif/WASM passes ; parcours interactif non qualifie.
[Transport terrain Studio](../evidence/tidewater/2026-09-27-studio-terrain-transport.md).

Materiau et geometrie CDLOD de terrain transferes dans le moteur partage,
prototype raccorde aux nouveaux types. Scene native et deux buffers d'ombres
identiques a la baseline precedente ; compilation Studio WASM reussie.
Viewport Studio importe non qualifie.
[Terrain partage](../evidence/tidewater/2026-09-27-shared-terrain-renderer.md).

Paquet des textures/shaders de terrain cree sans dupliquer le raster canonique :
6 payloads / 46209937 octets, empreintes et refus de corruption qualifies.
Lecteur Rust synchrone/asynchrone verifie sur le paquet reel, refus de corruption
et controles stricts Clippy/WASM passes. Transport et adoption Studio encore a realiser.
[Paquet terrain](../evidence/tidewater/2026-09-27-terrain-package.md).


Preparation partagee des textures de terrain et acquisition optionnelle depuis
la bibliotheque Studio raccordees. Paquet reel, Clippy, compilation Studio WASM
et regression native passes : PNG et deux buffers d'ombres inchanges.
Aucun terrain visible/edition Studio importe n'est encore qualifie.
[Textures et acquisition terrain](../evidence/tidewater/2026-09-27-shared-terrain-textures.md).


Recalcul normal/AO et graphe des mips transferes au moteur partage, avec invalidation
des anciennes generations lors d'une retouche. Clippy strict, compilation native,
retouche/retour CPU et remplacement GPU tardif passent. Rejeu natif identique
a la baseline precedente. Raccordement au terrain visible Studio restant.
[Normales partagees](../evidence/tidewater/2026-09-27-shared-terrain-normals.md).


Assemblage prepare du terrain importe ajoute (geometrie, textures, shaders,
normales, materiau, entite et empreinte documentaire). Clippy, preuve CPU de
remplacement et compilation Studio WASM passes. Prototype natif raccorde : PNG
et deux sorties d'ombres identiques. Raccordement visible Studio encore ouvert.
[Assemblage terrain](../evidence/tidewater/2026-09-27-terrain-scene-assembly.md).


Paquet terrain v2 avec shader d'ombres du relief prepare ; v1 reste lisible.
Empreintes, inventaires par version, lecteurs synchrone/asynchrone et Studio WASM
verifies. Ressources de bibliotheque reconstruites ; pas de nouveau verdict GUI.
Le pinceau exige encore le raccordement des collisions du terrain importe.
[Paquet terrain v2](../evidence/tidewater/2026-09-27-terrain-package-v2.md).


Projection des collisions du terrain importe ajoutee : 256 tuiles metriques,
remplacement par empreinte documentaire et curseur compatible avec le sampler.
Clippy, tests natifs de formes/jonctions et de synchronisation Rapier passes ;
256 colliders, retouche +12 m et restauration exacte verifies. Compilation
Studio WASM passee. Raccordement du viewport et geste Studio encore ouverts.
[Collisions importees](../evidence/tidewater/2026-09-27-sampled-terrain-collisions.md).

Controle de synchronisation physique importe disponible et verifie : refus
avant synchronisation, pour une ancienne empreinte et une pose locale non
appliquee ; acceptation de 256 tuiles courantes. Tests natifs et Studio WASM
passes. Ce controle reste a raccorder au cycle de projection du viewport.

Branchement du viewport importe en cours : ouverture conditionnee au raster
verifie et aux paquets, projection des revisions, ciel/relief/collisions, picking
apres synchronisation, CDLOD sur bornes cachees et navigation a la taille de
l'ile. L'eau, les objets, le playtest et le geste Studio restent ouverts.
[Viewport importe](../evidence/tidewater/2026-09-27-studio-imported-viewport.md).

Verification de ce branchement : compilation WASM, tests natifs de physique et
de selection camera passes ; capture native et deux sorties d'ombres identiques
a la precedente reference Forge. La nouvelle vue Studio reste NON QUALIFIEE
visuellement, avec eau/village/vegetation encore a raccorder. Clippy Studio
strict reste limite par bind_main preexistant (atelier_ui.rs:122).

Projection des placements de batiments importes raccordee dans le code Studio :
revisions exactes, meshes partages, materiaux lies au terrain, selection/gizmos
et cadrage. Probe natif PASS sur 23 batiments / 111 colliders, transformations,
duplication, suppression, restauration et raycasts ; Studio WASM compile.
Rendu et gestes GUI non qualifies ; ombres d'objets et decors residuels ouverts.
[Projection batiments](../evidence/tidewater/2026-09-27-studio-building-projection.md).

Raccordement des ombres des batiments editables : cascades partagees, poses
courantes des racines, publication des generations terminees et conservation
des racines/collisions lors des changements lumineux. Probe GPU natif PASS sur
les 107 parties opaques des 23 batiments : ombres initiales, transformation,
suppression complete et readback des trois couches. Aucun verdict GUI Studio,
ni performance interactive, ni parite de scene complete. Vegetation/eau restent
ouvertes. [Ombres editables](../evidence/tidewater/2026-09-27-studio-live-shadows.md).

Cache des bornes/corrections terrain pour les ombres de la camera raccorde au
Studio. Huit vues, retouche isolee et retours sources verifies ; mesure CPU locale
242,152 ms sans cache contre 12,607 ms avec cache pour huit preparations (pas un
benchmark FPS). Capture native et deux cartes d'ombres identiques a la baseline
Forge precedente. Compilation WASM PASS ; qualification interactive/GPU dynamique
et reutilisation des ressources GPU toujours ouvertes.
[Cache ombres terrain](../evidence/tidewater/2026-09-27-shadow-terrain-cache.md).

Materiau plantes, attributs, selection near/far et conversion canonique deplaces
dans tidewater_plants (moteur partage). La scene native utilise ce chemin :
capture et sorties d'ombres identiques a la precedente baseline, WASM compile.
Paquet vegetal portable produit et verifie (83 fichiers, 46 590 597 octets),
sans dupliquer les buffers de placements. Lecteur Rust, transport/catalogue
Studio, identites de picking et cycle de projection vegetal restent ouverts.
[Plantes partagees](../evidence/tidewater/2026-09-27-shared-plants.md).

Lecteur Rust du paquet vegetal et acquisition optionnelle Studio raccordes :
index/fermeture/tailles/SHA controles, tests sync/async et refus invalides PASS.
Bibliotheque locale avec les quatre paquets preparee et reverifiee, WASM compile.
Ressources GPU vegetales, population, picking et gestes Studio restent ouverts.
[Lecteur vegetal](../evidence/tidewater/2026-09-27-plant-package-reader.md).

Textures vegetales partagees et preparation dans le monde Studio raccordees :
cinq images du paquet reel verifiees, mipmaps/formats conserves, generations
isolees et entrees invalides refusees. Capture native et deux sorties d'ombres
identiques a la reference Forge precedente ; Studio WASM compile. Transport de
population, projection/picking et gestes GUI restent ouverts. Aucun acquittement
GPU Studio ni parite upstream complete revendiques.
[Textures vegetales](../evidence/tidewater/2026-09-27-plant-textures.md).

Transport natif des blocs vegetaux canoniques ajoute : fragments bornes et lies
a la tete de zone, assemblage verifie, commande Studio native/WASM compilee.
Tests de refus/corruption PASS et lecture reelle de 65 383 instances en 48
fragments PASS, sans mutation canonique. Consommateur asynchrone navigateur,
projection et edition/picking restent ouverts.
[Transport vegetal](../evidence/tidewater/2026-09-27-vegetation-transport.md).

Consommateur asynchrone Studio raccorde aux blocs canoniques : acquisition
atomique, annulation des generations remplacees et reutilisation de la base
identique. Probe du coeur partage PASS sur 65 383 instances (IDs/transforms
identiques a la lecture directe), refus async PASS, compilation WASM PASS.
Parcours IPC/GUI et projection GPU/picking encore non qualifies.
[Acquisition population](../evidence/tidewater/2026-09-27-population-acquisition.md).

Projection vegetale raccordee au canvas importe : preparation depuis le paquet
portable et la population canonique, IDs attaches aux meshes, selection near/far
selon camera et reception des ombres de scene. Probe ECS PASS sur 65 383 IDs,
retour camera et correction de hauteur ; capture native et ombres inchangees,
WASM compile. GUI non observee, picking/edition et casters vegetaux ouverts.
La premiere version recree les entites a chaque deplacement camera : optimisation
et performance interactive restent requises.
[Projection des plantes](../evidence/tidewater/2026-09-27-population-scene.md).

Projection residente raccordee : ressources conservees et differences d'entites
appliquees lors des changements de vue. Probe CPU PASS : selections identiques
a la methode precedente, 8 meshes/8 materiaux/12 buffers constants, repetitions
sans creation/suppression, camera invalide sans mutation et retrait complet.
WASM compile. Performance et rendu GPU interactif non qualifies ; picking ouvert.
[Plantes residentes](../evidence/tidewater/2026-09-27-resident-plants.md).

Complement GPU de la projection residente : selections A/B/A avant rendu,
capture finale A et deux sorties d'ombres identiques a la precedente reference
Forge. Aucun verdict sur les frames intermediaires, la navigation GUI ou les FPS.
Voir le complement dans la preuve des plantes residentes.
