# Raccord solaire et environnement dynamique — 3 octobre 2026

L'objectif Tidewater complet reste actif. Ce rapport couvre la séquence de
rafraîchissement et l'acquisition des programmes ; aucun rendu Studio dynamique
ni verdict de parité n'en découle.

## Ordonnancement original

Fixture `F:/OpenRPG_Forge/.build/solar-environment-controller-20261003-v1`.
`Capture.mjs` exécute les méthodes bornées originales `Environment._buildSteps`
et `Environment.update`, ainsi que `Vector3.angleTo`, au commit propre
`1438b1abfcaee3267092b75573014f4d9b4a983c`. Les dispatches sont enregistrés par
des doubles de kernels ; aucun GPU n'est exécuté dans cette acquisition.

Le contrôleur Rust reproduit les six faces, les mips, les six niveaux GGX,
l'irradiance SH et la publication du tampon arrière. Le premier rafraîchissement
et les demandes forcées exécutent la séquence entière ; les suivants exécutent
une unité par frame. Le déclenchement conserve l'intervalle de trois secondes
et le seuil angulaire de 0,004 radian.

`result-v2.json` termine avec retour 0 : trois tests, 240 frames et 286 unités
exactes. Timer bit-identique, indices/soleil/étapes exacts, publication en dernière
unité, progression à delta nul et refus atomiques des entrées invalides.
Oracle SHA-256 `4b24ebab142fc12d4eb62b08165922e6aa620187c9be85e2a69d7148e412aa76` ;
contrôleur `99369ed2dd961411ac23b451c8176e2aa5238a42735582f439354625cec28d3c`.
Le premier harnais ne compile pas à cause de la priorité de la négation sur
`to_bits` dans ses littéraux générés. Les logs/entrées/result.json V1 sont
conservés ; V2 groupe ces littéraux sans changer contrôleur ni oracle.
Ce contrôleur reste isolé et n'encode pas encore les unités sur le GPU produit.

## Acquisition des programmes vivants

Le nouvel outil `scripts/tidewater/export-environment-programs.mjs` construit
les véritables Atmosphere/Sky/SkyProClouds/Environment dans la référence
headless existante. Il refuse une source non épinglée/non propre, une sortie
existante ou interne à la source, et toute requête de données autre que les
deux fichiers de bruit des nuages. Les sources suivies sont hachées avant/après.
La licence originale accompagne le paquet.

Commande exécutée avec retour 0 :

```powershell
node scripts/tidewater/export-environment-programs.mjs `
  E:/OpenRPG_Forge/.tidewater-integration/upstream `
  F:/OpenRPG_Forge/.build/solar-environment-programs-20261003-v1
```

Acquis : 22 programmes WGSL (6 faces, 2 mips, 12 GGX pour les deux tampons,
2 SH), 4 blocs uniformes avec offsets et octets initiaux, 10 identités de
ressources. Les deux variantes de chaque GGX et SH sont byte-identiques en
shader ; leurs sorties sont distinctes. Les bindings originaux, visibilités,
formats et vues/mips sont conservés, y compris ceux non utilisés dans un shader.
La création des pipelines source est validée sous scope WebGPU.

Les octets uniformes sont ceux des constructeurs, pas ceux d'un état solaire
en jeu. L'outil ne lance pas App, ne dispatch pas l'environnement, n'acquiert
pas ses pixels et ne remplace pas les providers de ressources de Bevy.

## Qualification Bevy préparée

`crates/forge_engine/examples/verify_tidewater_environment_programs.rs` utilise
le RenderDevice de son RenderApp Bevy pour créer les 22 pipelines avec les
layouts explicitement acquis. Il vérifie le hash externe du manifeste et tous
ses fichiers avant création ; les types non qualifiés sont refusés.
Il ne fournit pas de ressources neutres et ne dispatch pas les kernels.

`Run-Bevy.py`, dans la fixture d'acquisition, fige l'exemple et ses dépendances
natives existantes. Il lance d'abord un check metadata, puis conditionnellement
compile/lance le vérificateur et revérifie tous les pins. Session 80144 : aucun
verdict terminal n'est encore acquis à la rédaction de cette section.

Suite requise : fournir les LUTs atmosphériques et les paramètres solaires
vivants, encoder les unités ordonnées dans le renderer, publier ensemble cube
et SH après la dernière unité, puis raccorder nuages/matériaux/ombres/lampes.
La qualification visible native et WebGPU et les vues appariées restent ouvertes.

## Nettoyage du format d'acquisition

Une seconde acquisition neuve `solar-environment-programs-20261003-v2` termine
avec retour 0. Le format évite de sérialiser l'objet UniformBlock vivant dans
chaque binding ; le layout qualifié reste enregistré une seule fois. Il indique
explicitement les getters de ressources vivantes, notamment `cloudsView`.
Le manifeste passe de 1 577 820 à 180 886 octets.

`comparison-v1.json` vérifie les 27 payloads byte-identiques, les 22 programmes,
tous les layouts et les sources/provenances/uniformes/ressources inchangés.
Manifeste V1 `c8c6fe6b4d5d8d92c8fe514cb16e91ede3b2ac5762ac6e79719f589adee08cea` ;
V2 `6dcf263fd9ad30c2e46491c0dd02879ea4766a55ad2dd8d04b969487e6fbc19f`.
La compilation Bevy déjà lancée garde sa fixture V1 intacte.

Le mapping des faces exige Frame 720 octets, AtmosphereParams 64, CloudsParams 80,
SkyParams 32 ; puis LUT transmittance/skyView, ombre/panorama/vue des nuages et
cube source 128 à huit mips. Les deux cubes GGX ont six mips et les deux buffers
SH ont neuf vec4 (144 octets). Les dimensions de la vue des nuages capturées au
constructeur ne doivent pas être imposées au provider vivant après son resize.

## Correction du vérificateur Bevy

La metadata V1 termine à 09:59:34 UTC avec retour 1 avant création GPU : deux
usages d'une ancienne API wgpu dans le nouvel exemple. La version verrouillée
29 attend des layouts `Some(...)` et retourne un ErrorScopeGuard dont la future
est `scope.pop()`. L'exemple reprend maintenant ces appels déjà utilisés par
`source_mesh_pipeline.rs`. Les entrées/logs/résultats V1 restent intacts.
`Run-BevyV2.py` prépare une nouvelle copie et des sorties `bevy-validation-v2` ;
session 17087, metadata puis compile/GPU conditionnels. Aucun seuil, shader
source, layout acquis ou manifeste n'est changé par cette réparation.
Le verdict Bevy reste en attente.

## Création des pipelines Bevy qualifiée

La suite V2 termine avec retour 0 à 10:02:38 UTC : metadata et compilation
natives sans diagnostic, puis création validée des 22 pipelines sur le
RenderDevice du RenderApp Bevy. Adaptateur AMD Radeon RX 7800 XT, Vulkan.
Le reçu `bevy-validation-v2/gpu-result.json` conserve chaque hash WGSL et les
layouts explicites. `result.json` revérifie les pins source/dépendances/paquet.
Aucune ressource de scène ni dispatch n'est exécuté dans cette première preuve.
Le V1 en échec reste conservé.

## Entrées vivantes et oracle de sortie original

Fixture neuve `solar-environment-programs-20261003-v2/live-source-fixture-v1`.
`Prepare-Live.py` copie l'outil environnement et son helper CloudCapture sans
modifier les originaux. L'overlay ajoute COPY_SRC aux textures de l'environnement
avant allocation, puis force un rafraîchissement original complet après les
120 frames froides, sur l'état réel des nuages à 2 s. Les programmes GPU et
les paramètres de référence ne sont pas retouchés. Ce rafraîchissement explicite
ne reproduit pas l'ordonnancement temporel de l'application complète.

Référence : `cloud-seaward-reference-001.json`, heure 16,2, caméra fixe et vent
diagnostique 6 m/s, direction (1,0), conservés tels quels. Cette condition n'est
pas substituée aux valeurs de vent du produit ni aux vues Village appariées.

`Run.py` termine avec retour 0 à 10:13:30 UTC. Il vérifie 747 fichiers acquis,
48 faces source sur huit mips, 36 faces GGX sur six mips, SH9 (144 octets),
et les quatre blocs effectifs Frame/Atmosphere/Clouds/Sky (720/64/80/32 octets).
Le helper refuse une divergence entre les octets CPU packés et ceux du dernier
upload effectif. Le snapshot des textures nuages et ses uniforms sont conservés.
Les sources, helpers, manifeste programmes et référence restent inchangés.

Manifeste environnement `437f26a7e4ceb57d719a4c736f523583c4278137493e0c3d9c0b29e094f3bf16` ;
entrées vivantes `57c0918a9fbaefadb913b7d7073cafcfa63cf4c40f060aaa266cd8503a17a8af`.

## Exécution des filtres préparée

L'exemple Bevy reçoit un mode optionnel avec racine et hash du manifeste vivant.
Son module `verify_tidewater_environment_programs/ibl.rs` prépare le vrai cube
source acquis, ses huit mips et le Frame effectif. Il encode les six filtres
GGX puis SH pour chacun des deux tampons, lit les résultats matériels et mesure
72 faces et deux SH9 contre les sorties originales. Il conserve les shaders,
layouts et samplers originaux et refuse des sorties non finies/alpha incorrects.
Il ne remplace pas la radiance par une couleur neutre et n'impose pas un seuil
colorimétrique qui ferait disparaître un écart : les mesures restent explicites.

`Run-Ibl.py` fige les deux sources et tous les payloads/dépendances, puis lance
metadata, compilation et GPU conditionnels. Session 48141, verdict attendu.
Les faces du ciel et les mips ne sont pas encore générés par Bevy dans ce mode :
le cube est l'entrée originale réellement lue sur le GPU source. Leur génération,
le contrôleur vivant et l'adoption cohérente dans Studio restent requis.

## Exécution matérielle GGX et SH terminée

La session 48141 termine à 10:42:30 UTC avec retour 0 : metadata,
compilation et exécution GPU passent, avec entrées inchangées. Le reçu
`bevy-ibl-v1/gpu-result.json` a le SHA-256
`160eb070293aa8d4cad1bbcdba31a9bdf9be432323e2be4a5d797d812b90a411`.
Les 14 dispatches utilisent le RenderDevice et le RenderQueue de Bevy,
sur AMD Radeon RX 7800 XT / Vulkan, avec le vrai cube acquis et ses uniforms.

Sur 72 faces comparées, 40 sont identiques octet par octet ; l'écart absolu
RGB maximal est 0,00048828125. Les deux sorties SH sont identiques entre les
tampons Bevy, mais pas octet par octet à l'original ; l'écart absolu maximal
est 7,450580596923828e-09. Ces mesures ne sont pas un verdict de parité visuelle.
Le cube source et ses mips sont toujours acquis dans l'original : leur génération
Bevy, l'ordonnancement vivant et la publication dans Studio restent à exécuter.

## Génération des mips Bevy en qualification

Le vérificateur accepte désormais `--generate-mips` après les deux arguments
du paquet vivant. Ce mode charge uniquement le niveau zéro du cube original,
puis exécute les programmes acquis `mips-a` (niveaux 1 à 5) et `mips-b`
(niveaux 6 et 7), avec leurs layouts et dispatches originaux. Les filtres GGX
et SH consomment ensuite ces niveaux calculés sur Bevy. Le mode précédent
reste disponible et les preuves terminées ne sont pas remplacées.

`Run-MipsIbl.py` lance une nouvelle fixture `bevy-mips-ibl-v1` (session 89271).
Son verdict de compilation et d'exécution reste attendu. Les comparaisons
finales GGX/SH mesurent la chaîne ; elles ne comparent pas directement chaque
mip intermédiaire. La génération des faces du ciel reste distincte.

La metadata de cette fixture termine avec retour 0 ; rustc puis le linker
MSVC sont observés actifs (PID 45800 et 15084). Aucun verdict GPU terminal
de la session 89271 n'est encore acquis à ce stade.

## Génération des faces du ciel préparée

Le mode `--generate-sky` prépare maintenant les six faces originales, leurs
mips, GGX et SH. Il ne charge aucune face du cube source : il utilise les LUTs
transmittance/skyView, l'ombre des nuages, leur panorama et le vrai tampon
d'historique indiqué par la frame 120, avec les uniforms effectifs acquis.
Les 22 programmes, les layouts et les dispatches restent ceux du paquet épinglé.
Le sampler R32 filtré exige explicitement FLOAT32_FILTERABLE, sans remplacement
silencieux. Les buffers uniformes ont leurs tailles source vérifiées.

Ce mode ajoute aussi une lecture comparée des 48 faces du cube source sur huit
niveaux, en plus des 72 faces GGX et des deux SH. La comparaison des mips directs
fait désormais partie du code courant ; la fixture précédente, déjà figée,
ne contient pas cette extension. `Run-SkyIbl.py` est préparé avec une nouvelle
destination `bevy-sky-ibl-v1`, sans lancement à ce stade. Formatage Rust PASS.
La génération reste un vérificateur isolé, sans adoption dans Studio et sans
qualification visuelle, navigateur ou cycle solaire dynamique.

La session 70866 lance ensuite `Run-SkyIbl.py`. Elle fige séparément les deux
sources du vérificateur, le paquet programmes, les 747 payloads acquis et les
dépendances natives, puis conditionne le lien/exécution aux checks précédents.
Elle ne modifie ni la fixture mips déjà active ni le candidat Studio. Le verdict
metadata, lien et GPU de cette nouvelle chaîne reste attendu.

## Verdicts terminaux et reprise séquentielle

Les fixtures `bevy-mips-ibl-v1` et `bevy-sky-ibl-v1` terminent respectivement
à 11:29:49 et 11:29:43 UTC avec metadata PASS, compilation retour 1 et
MSVC LNK1102 (mémoire insuffisante). Aucun dispatch GPU n'a été exécuté.
Les reçus confirment les entrées inchangées ; journaux et fixtures restent conservés.

`Run-SkyIbl-v2.py` relance uniquement la chaîne complète dans `bevy-sky-ibl-v2`,
à partir des deux sources figées et vérifiées de V1. Les arguments ajoutés sont
`-C debuginfo=0`, `-C link-arg=/DEBUG:NONE` et `-C link-arg=/INCREMENTAL:NO`.
Shaders, layouts, payloads, dispatches et comparaisons sont conservés. La metadata
passe ; la session 86812 reste active sans verdict de lien/GPU à cette observation.

Le lecteur solaire ECS V3 termine séparément avec compilation retour 1 :
`bevy_rapier3d` requis par le moteur mis en cache n'est pas disponible sous la
forme rlib attendue. Ses sept tests synthétiques ne sont donc pas exécutés.
Ce défaut de dépendance est distinct du manque de mémoire des vérificateurs GPU.

## Politique solaire vérifiée sur la référence canonique

Le reçu `solar-readback-lighting-20261003-v1/canonical-policy-v1.json` vérifie
HEAD propre `1438b1abfcaee3267092b75573014f4d9b4a983c`. Le fichier Atmosphere.js
et la méthode bornée `applyAtmosphereReadback` sont identiques à la copie locale
inspectée précédemment. En revanche, App.js entier diffère : cette copie omet
les appels clouds/environment à l'endroit où la référence canonique les exécute.
Elle ne constitue donc pas une preuve d'ordonnancement complet.

La référence canonique ordonne updateSun, atmosphere.update, application du
dernier readback, clouds.update puis environment.update. Elle conserve les
données reçues pendant la requête suivante et réévalue la lumière avec le soleil
courant. Le lecteur V3 préparé suit cette politique de cache, mais demeure non
qualifié tant que ses tests ne tournent pas. L'intégration vivante dans Studio,
le contrôleur périodique et la comparaison visuelle restent à réaliser.

### Correction après inspection du lecteur V3

La lecture effective de V3 révèle que sa transformation préparatoire n'avait
pas retiré la comparaison des paramètres ni l'effacement du Snapshot au nouveau
request. L'affirmation précédente de conformité du cache V3 est donc contredite
par le code ; elle ne vaut pas qualification. Ses tests de soleil mobile auraient
détecté ces défauts s'ils avaient pu être exécutés.

La nouvelle fixture `solar-readback-lighting-20261003-v1/reader-v4` retire ces
deux comportements et les champs de requête devenus inutiles. Elle conserve
les gardes d'identité de génération et de tampon, la validation du soleil courant,
la plage de lecture de 48 octets et les tests de remplacement/annulation.

Son harness utilise la définition Targets extraite exactement du fichier de
production, avec hash source et hash de définition dans `preparation.json`.
Il n'importe plus le vieux moteur mis en cache, dont une dépendance physique
rlib manque. Ce harness qualifie uniquement les événements ECS synthétiques,
pas l'intégration du moteur complet. La metadata est lancée en session 97809 ;
l'édition des liens sera séquentielle après celle du ciel pour limiter la mémoire.

## Mise à jour des LUTs résidentes dans le moteur

`tidewater_atmosphere::update_parameters` et le plan de travail extrait vers
RenderApp sont ajoutés au code de production. Le stage initial conserve ses
quatre dispatches. Les mises à jour suivantes conservent les handles et l'Arc
de génération ; elles demandent skyView si les paramètres changent, les LUTs
statiques sur invalidation explicite, et irradiance sur demande du contrôleur.
Les bits non encore encodés restent cumulés, avec accusé d'encodage par révision.
Cet accusé ne constitue pas une fence GPU.

Trois tests couvrent identité/cumul des travaux, absence de répétition après
encodage et refus atomique des valeurs invalides/dépassement de révision.
La fixture `solar-readback-lighting-20261003-v1/live-atmosphere-v1` fige le module
complet, acquisition comprise, pour une compilation metadata (session 8681).
Formatage et diff-check ciblés PASS ; compilation et tests restent attendus.
Le contrôleur de période 0,25 s, l'ordre atmosphere/clouds, les uniforms des
nuages et l'adoption par Studio restent à raccorder avant qualification dynamique.

## Chaîne ciel complète exécutée sur GPU

La session 86812 termine avec retour 0 à 11:48:32 UTC : metadata, lien natif et
exécution GPU passent, avec entrées inchangées. Le reçu `bevy-sky-ibl-v2/gpu-result.json`
a pour SHA-256 `fb5f52f75959a642a8e8fee94cf6e15beab0fb3b7e2bd51aa398e8af4e910f63`.
Les 22 dispatches génèrent les faces du ciel, les mips, GGX et SH à partir des
vraies LUTs et textures de nuages acquises ; aucune face source n'est chargée.

Sur les 48 faces du cube généré comparées, trois sont identiques ; l'écart RGB
absolu maximal est 0,03515625. Sur les 72 faces GGX comparées, aucune n'est
identique et le maximum est également 0,03515625. Les deux SH Bevy sont
identiques entre eux ; leur écart maximal à la référence est 0,00005793571472167969.
Le mode précédent avec cube importé était nettement plus proche : cette différence
localise un travail de fidélité dans la génération du ciel/mips, sans en prouver
encore la cause. Aucune correction ni tolérance n'a été introduite pour masquer
ces écarts. Ils doivent être analysés et confrontés aux vues rendues.

Cette preuve porte sur une chaîne GPU isolée à état figé, pas sur le cycle solaire
vivant, les matériaux Studio, le navigateur ni une parité visuelle acquise.

## Suite des validations et diagnostic des écarts

Le lecteur V4 termine sa metadata avec retour 0. La session 28989 lance
ensuite le lien et ses sept tests, après la fin de la chaîne ciel GPU ; verdict
encore attendu. Les événements restent synthétiques et le contrat Targets est
extrait de production : cela ne qualifie pas le lecteur dans Studio.

La metadata de `live-atmosphere-v1` termine retour 1 : le `#[path]` du harness
modifiait la résolution des sous-modules acquisition/live (E0583). La fixture
`live-atmosphere-v2` conserve les sources de production byte-identiques, vérifiées
par SHA, et utilise la déclaration normale du module. Session metadata 28298
active ; ce défaut de harness n'établit pas un défaut du module produit.

Le comparateur courant ajoute moyenne, percentile 95, RMS, pic de référence et
erreur L2 relative par face, sans seuil de réussite ni correction d'image.
Ces nouvelles mesures n'ont pas encore été exécutées. L'inspection des samplers
canoniques révèle également que nearestRepeat répète W, alors que le vérificateur
utilisait clamp sur W. Cette différence est corrigée dans le code courant ; son
effet sur les écarts observés n'est pas établi. Le reçu V2 existant reste intact.

### Lecteur corrigé promu, qualification complète encore ouverte

La session 28989 termine retour 0 à 11:53:07 UTC : les sept tests synthétiques
passent. Le lecteur V4 et la fonction CPU de lumière sont copiés byte-identiques
dans `tidewater_atmosphere/readback.rs` et `tidewater_sky/solar_lighting.rs`, puis
exposés par leurs modules. Le reçu `reader-v4/promotion.json` conserve les SHA
et la limite de qualification. Le contrôleur Studio ne les consomme pas encore.

La metadata `live-atmosphere-v2` passe (session 28298). Son essai de lien/tests
termine ensuite retour 1 avec E0463 « can't find crate for bevy » dans live.rs,
bien que le rlib Bevy explicite soit présent avec le SHA attendu. Aucun des trois
tests de cette fixture n'est exécuté. Le diagnostic du graphe de dépendances et
la qualification du module complet restent ouverts ; aucune réussite metadata
n'est présentée comme réussite des tests ni intégration dynamique.

## Contrôleur périodique et ordre de calcul ajoutés

La compilation diagnostique (session 74237, mêmes sources V2 et dépendances,
avec RUSTC_LOG locator) termine retour 0. L'erreur E0463 précédente n'est donc
pas reproduite ; sa cause n'est pas établie. L'exécutable diagnostique lancé
avec `--nocapture` passe les trois tests de mise à jour résidente (retour 0).

Le module de production `tidewater_atmosphere/controller.rs` ajoute la cadence
source de 0,25 seconde, une attente de la révision encodée avant requête et
une seule lecture pending. La scène conserve son dernier readback pendant
la requête suivante. Des paramètres/frames invalides sont refusés avant mutation.
Le controller possède une annulation explicite liée à la fermeture de scène.
La quantification de hauteur et la publication vers les matériaux appartiennent
encore au caller à raccorder ; la fonction n'est pas consommée par Studio.

Un set RenderGraph `AtmosphereCompute` précède désormais les kernels clouds.
Cette contrainte est ajoutée au code, sans preuve d'exécution GPU dynamique à ce
stade. La fixture `integrated-atmosphere-v1` fige le module atmosphere complet,
le contrôleur, le lecteur, les fonctions solaires et l'oracle, directement depuis
les sources de production. Session 95678 : compilation et tests combinés lancés,
verdict attendu. Les tests restent CPU/ECS, sans qualification Studio/WebGPU.

## Soleil et lumière transmis aux frames de nuages

La fixture `integrated-atmosphere-v1` termine avec compilation et douze tests
PASS. La fixture suivante `integrated-solar-clouds-v1` (session 51158) compile
les modules de production atmosphere/solar/clouds ensemble et passe vingt tests.

`tidewater_clouds::live::submit_solar` transmet le soleil courant et la dernière
lumière reçue dans le Frame original, en gardant les textures temporelles.
Les offsets sont vérifiés par exécution de la définition canonique Frame.js :
sunDir 592, night 604, sunColor 608, skyIrradiance 624 et horizonColor 640,
sur 720 octets (`frame-lighting-layout-v1.json`). Les champs caméra, exposition,
eau et vent restent conservés. Le shader trace reçoit aussi un uniform atmosphere
de 64 octets propre à cette soumission, au lieu du seul uniform initial en cache.

L'API refuse les générations de LUTs différentes, l'absence de lumière reçue,
les valeurs non finies et une nouvelle soumission avant encodage de la précédente.
Les chemins existants à lumière figée conservent leur comportement. Le test des
offsets et du refus atomique fait partie des vingt tests PASS. Un test complet
readback → submit_solar → backpressure/génération est ensuite ajouté : nouvelle
fixture `integrated-solar-clouds-v2`, session 51985, verdict attendu.

Ces preuves sont CPU/ECS et compilation des modules, sans exécution des kernels
clouds dynamiques ni activation du cycle solaire dans Studio. Le publisher des
matériaux et la génération vivante d'IBL restent à raccorder et qualifier.

La session 51985 termine ensuite retour 0 : compilation et vingt et un tests
PASS, y compris `solar_submission_uses_completed_light_and_refuses_replaced_atmosphere`.
Le reçu est `integrated-solar-clouds-v2/compile-result.json` ; les sorties sont
conservées dans tests.stdout/stderr. La portée CPU/ECS ci-dessus reste inchangée.

## Vérification GPU du contrôleur vivant préparée

L'inspection du chemin de rendu montre que des couleurs de soleil de certains
shaders terrain/plantes sont encore injectées comme constantes à la préparation
des paquets. Une mise à jour de la direction seule ne constitue donc pas le
publisher complet requis. Ces couleurs et l'IBL vivant doivent être raccordés
avant toute affirmation de cycle solaire complet dans Studio.

`verify_tidewater_live_atmosphere.rs` vérifie maintenant le graphe de production
atmosphere et le contrôleur sur le RenderApp Bevy réel, sans fenêtre. Il demande
successivement 16,2 h, 18,2 h, 23 h et 16,2 h, attend un nouveau readback réussi
à chaque phase (identité du lecteur de completion distincte), compare les valeurs
jour/nuit et refuse tout remplacement de handles/Arc résident. Ce diagnostic
d'identité est ajouté au lecteur ; il n'est pas un identifiant persisté de document.

La fixture `live-atmosphere-gpu-v1` fige les modules de production et l'exemple,
avec adaptation explicite de l'import forge_engine vers les modules locaux.
L'acquisition `atmosphere-island-source-002` et ses payloads sont vérifiés par SHA,
et la bibliothèque GPU reste celle de Bevy. Session 92525 : compilation puis
exécution GPU conditionnelle lancées, verdict attendu. Cette vérification ne
dessine pas les matériaux et ne qualifie ni Studio ni la parité visuelle.

La session 92525 termine avec compilation PASS mais exécution retour 101 :
le harness appelait register après finish/cleanup, ordre interdit par Bevy.
Il n'a donc pas qualifié le contrôleur GPU. L'enregistrement du plugin est
déplacé avant la finalisation ; le stage des données reste après celle-ci.
La reprise `live-atmosphere-gpu-v2` est lancée en session 93776, sans remplacer
les sorties V1. Compilation/exécution et lectures des quatre phases restent attendues.

### Cycle atmosphère réellement exécuté, puis reprise propre

La session 93776 termine retour 0 : compilation et exécution GPU passent,
avec quatre nouvelles completions réelles, valeurs jour/nuit différentes et
handles/Arc conservés. Le retour à 16,2 h retrouve les valeurs de la première
phase. Le reçu V2 a pour SHA-256
`37f3d97d1991d293bd7315d862f8deb105dc680ec9cdded16d488de3a2039b83`.
Son journal contient néanmoins des avertissements de double retrait d'entité.

Bevy tente une lecture à chaque frame tant que le composant Readback existe.
Le lecteur ajoute une garde atomique par requête pour ne publier et retirer
qu'une fois les completions déjà en queue avant les commandes différées.
La reprise `live-atmosphere-gpu-v3` (session 72470) termine également retour 0,
avec entrées inchangées et journal gpu.stderr vide. Les quatre phases GPU
et la conservation des ressources sont à nouveau vérifiées.

Cette preuve qualifie le contrôleur et le lecteur sur le graphe atmosphère réel,
avec l'adaptation de limite de LUT déjà utilisée par Forge. Elle ne compare
pas encore ces quatre phases à un oracle GPU source multihoraire et ne qualifie
pas les couleurs injectées des matériaux, l'IBL vivant ou la scène Studio.
