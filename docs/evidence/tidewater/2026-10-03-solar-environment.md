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

## Adaptateur des constantes terrain/plantes préparé

`tidewater_material::scene_uniform::prepare` transforme l'interface acquise en
uniform aligné de direction, couleur et neuf coefficients SH à la binding 54.
Il retourne également les valeurs initiales extraites, afin que le raccordement
des matériaux conserve leur état acquis avant la première publication dynamique.
Les facteurs d'ombre restent dans les appels, et les remplacements sont bornés
aux affectations/appels de lumière ; les vecteurs identiques d'autres calculs
restent intacts. Les interfaces incompatibles et les collisions sont refusées.

Les deux tests std passent. Le probe local
`scene-uniform-adapter-20261003-v1/probe.exe` transforme aussi les vrais shaders
plant/terrain importés de la fixture candidate-025 en fichiers WGSL séparés.
Les deux seeds obtenus sont identiques pour la direction, la couleur et les SH.
Les fichiers source importés restent inchangés. Ces sorties ne sont pas encore
validées par compilation GPU : aucun champ de matériau ni pipeline Studio n'a
encore adopté la binding 54. Le raccordement des handles/uniforms et la validation
du shader préprocessé restent nécessaires.

Le diff-check Git de ces deux sources retourne 1 sans diagnostic de whitespace,
avec avertissements de conversion LF/CRLF. Ce contrôle n'est pas décrit comme
PASS ; les tests Rust et la transformation des deux shaders ont bien terminé.

## Raccordement des matériaux au tampon de lumière

Les matériaux TerrainMaterial et PlantMaterial exposent maintenant la binding
54 comme storage read-only, avec un handle de scène stable. L'adaptateur WGSL
utilise donc désormais un stockage aligné (onze vec4 : direction, couleur, SH9),
au lieu de l'uniform préparatoire V1. Le chemin terrain édité et les installations
de shaders terrain/plantes exécutent cet adaptateur et extraient la seed acquise.
Les constructeurs Studio et des probes sont mis à jour avec le même handle.

`scene_buffer::install` initialise seulement un tampon absent, pour ne pas
réinitialiser la lumière lors d'une reconstruction de mesh. Le stage d'un nouvel
environnement publie explicitement son snapshot. Studio republie la lumière de
ResidentEnvironment à la soumission de ses nuages ; les payloads identiques sont
dédupliqués. Le cycle solaire Studio et l'IBL dynamique restent encore à raccorder.

Le nouveau test vérifie 176 octets alignés, publication de couleur/SH, conservation
après reconstruction et refus atomique des valeurs invalides. La fixture isolée
`scene-lighting-buffer-20261003-v1` échoue avant ses tests : le harness omettait
CARGO_MANIFEST_DIR, requis par ShaderType. V2 conserve ce journal, définit le
contexte Cargo et fige les deux modules actuels plus la définition exacte de
SceneLighting. Session 47641 : compilation et tests lancés, verdict attendu.
Ce harness ne qualifie pas les pipelines complets des matériaux.

Les commandes Git utilisent désormais une exception safe.directory limitée à
la commande et au dépôt exact, sans modification globale. Le diff-check ciblé
des sources raccordées passe dans ce contexte. Aucune preuve de rendu ni nouvelle
qualification visuelle n'est déduite de ce contrôle.

### Tampon validé et vérification des shaders importés

La session 47641 termine retour 0 : compilation et trois tests V2 passent.
Ils couvrent l'adaptateur borné, les refus et la publication du tampon aligné,
sans preuve de dessin des matériaux.

La fixture isolée E:/OpenRPG_Forge/.tidewater-integration/scene-lighting-shader-gpu-20261003-v1
fige l'adaptateur courant et les deux shaders acquis de candidate-025. Elle
initialise Bevy sans fenêtre, récupère ses vrais assets Shader et utilise son
ShaderCache avec la validation GPU activée. Elle demande les quatre variantes
de plantes et le terrain après adaptation. Session 57740 lancée ; verdict
attendu. Cette première tranche vérifie l'adaptation lumineuse seule, pas encore
les transformations supplémentaires de mouvement/ombres/caustiques du stage,
les layouts de matériaux ou un rendu Studio.

V1 termine avec une erreur de compilation du harness : les capacités downlevel
sont disponibles sur RenderAdapter, pas Device. V2 corrige cet accès et compile,
mais le ShaderCache direct omet la définition globale
AVAILABLE_STORAGE_BUFFER_BINDINGS. V3 reprend la valeur réelle de
device.limits().max_storage_buffers_per_shader_stage comme PipelineCache Bevy.
Compilation et exécution GPU passent : les 156 vrais assets Shader de Bevy sont
chargés ; les quatre variantes PLANT_KIND_0..3 et le terrain sont validés.
Les essais précédents restent conservés avec leurs échecs.

### Combinaison des adaptations de production validée sur GPU

La reprise `scene-lighting-shader-gpu-20261003-v4` (session 44211, retour 0)
valide aussi les transformations combinées dans l'ordre des stages actuels :
lumière → mouvement → ombres → directive de dérivées pour les plantes ;
lumière → ombres → caustiques pour le terrain. Les fonctions de transformation
sont extraites textuellement des sources actuelles, sans traduction, et leurs
hashes ainsi que ceux des sources complètes et des quatre inclusions WGSL
caustiques sont enregistrés dans result.json. Les programmes GPU sont créés avec
create_and_validate_shader_module après le vrai prétraitement ShaderCache et
les imports Bevy ; gpu.stderr est vide. Le compilateur signale seulement des
fonctions/champs inutilisés dans ce harness réduit.

Les cinq variantes passent avec le stockage de lumière à la binding 54.
Cette preuve ferme le contrôle de compilation des shaders combinés acquis de
candidate-025 sur le GPU natif présent. Elle ne couvre pas un pipeline de dessin,
les layouts AsBindGroup, les constructeurs Studio complets, WebGPU navigateur,
le cycle solaire dans Studio ou la comparaison visuelle. Ces validations restent
requises avant livraison. Aucune parité visuelle globale n'est déclarée.

### Matériaux et pipelines natifs complets

La fixture `scene-lighting-shader-gpu-20261003-v5` (session 22138, retour 0)
compile les définitions Rust exactes de PlantMaterial et TerrainMaterial avec
leurs implémentations Material/AsBindGroup et les types de paramètres requis.
Les sous-modules d'installation et de scène sont exclus de ce harness ; chaque
extraction et source complète est épinglée. Les layouts GPU sont acceptés :
23 entrées pour les plantes, 28 pour le terrain, dont binding 54 storage read-only.
Cette vérification ne compile pas encore le reste de forge_engine ou Studio.

V6 ajoute la création de pipelines mais échoue à compiler le harness : dans
Bevy 0.19, RenderPipelineDescriptor.layout attend des descripteurs de layout,
pas des layouts GPU déjà créés. V7 corrige ce type, conserve V6 et termine
compilation/exécution retour 0 (session 97267), avec gpu.stderr vide.

V7 spécialise le vrai MeshPipeline de Bevy, ajoute les layouts AsBindGroup des
matériaux à l'index 3 et leurs shaders aux stages concernés, applique le vrai
PlantMaterial::specialize pour chacune des quatre variantes, puis demande les
cinq pipelines à PipelineCache. Les cinq états terminent CachedPipelineState::Ok,
sans retirer de ressource ni contourner la validation. Un accesseur de clé
PlantKey est ajouté uniquement dans le module extrait du harness, pour appeler
la spécialisation publique avec la clé privée ; le code produit reste inchangé.

La vérification couvre une projection perspective, MSAA 1 et des layouts de
mesh synthétiques compatibles avec les attributs des matériaux. Elle qualifie
la compilation complète de ces pipelines natifs sur le GPU présent, y compris
l'accord shader/layout du nouveau tampon lumineux. Elle ne soumet aucun dessin,
ne vérifie pas les images/buffers d'une scène, les constructeurs Studio, d'autres
clés de pipeline, WebGPU navigateur ni la fidélité visuelle. Ces contrôles restent
ouverts ; le prochain jalon est la compilation de l'application et un rendu réel.

### Vérification du moteur entier en cours

L'inspection actuelle confirme que live_cloud_step de Studio republie encore
le snapshot scene_lighting acquis et soumet les nuages par live::submit. Le
cycle solaire, live::submit_solar et l'IBL renouvelé ne sont donc pas installés
dans cette scène ; les pipelines validés ne changent pas cette limite.

La tentative `solar-engine-metadata-20261003-v1` cherche un graphe natif complet
dans le cache F:/OpenRPG_Forge/.build/cargo-target et s'arrête avant rustc :
aucun graphe enregistré ne contient la dépendance forge_navigation courante.
Elle ne qualifie aucune compilation. Aucun cache F: n'est écrit ou nettoyé.

Une vraie vérification Cargo est lancée dans la fixture neuve
E:/OpenRPG_Forge/.tidewater-integration/solar-engine-cargo-check-20261003-v1,
session 50808 : cargo check --offline --locked -p forge_engine --lib, avec
target/temp sur E:, incremental désactivé et debuginfo 0. Les sources Rust/WGSL,
manifests et lockfile sont épinglés avant et après. La session compile réellement
les dépendances courantes, dont forge_navigation. Verdict terminal attendu ;
aucune compilation Studio, liaison d'application ni exécution n'est déduite.

La session 50808 termine retour 0 après 3 min 19 s : le vrai cargo check du
moteur entier passe avec ses dépendances actuelles et source_unchanged=true.
Le seul avertissement est l'import `ecs as bevy_ecs` inutilisé du lecteur
d'atmosphère. Cet import est supprimé, le fichier passé par rustfmt édition 2024,
et une reprise V2 est lancée en session 93048 avec le même cache E: et des
journaux/pins séparés. Cette reprise vérifie la correction ; verdict attendu.
Le diff-check ciblé passe et la modification du lecteur se limite à cette ligne.

La session 93048 termine également retour 0 avec source_unchanged=true ; V2
compile le moteur entier sans avertissement. Le raccord Studio et le cycle
solaire visible restent ouverts : ce résultat est une preuve de compilation
actuelle, pas une qualification d'application ou d'image.

### Payload lumineux commun aux nuages et à l'eau

`tidewater_sky::frame_lighting::FrameLighting` prépare une fois les treize mots
solaires du Frame.js source (direction key/night, couleur directe, irradiance,
horizon). Il refuse les valeurs non finies/non représentables, couleurs négatives,
direction non unitaire et nuit hors domaine. Ses écritures byte/word utilisent
les mêmes offsets qualifiés dans le frame de 720 octets. Les champs voisins,
dont caméra, horloges, taille solaire, immersion et hauteur d'eau, restent intacts.
Le chemin cloud `write_solar` utilise ce payload à la place de son packer privé.

`tidewater_ocean_optics::update_lighting` reçoit ce même payload et les paramètres
atmosphériques de la frame. Il refuse les paramètres invalides ou les ressources
absentes avant de modifier le matériau ; les handles et les autres paramètres
optiques restent conservés. Le caller de scène doit fournir le même état solaire
et l'environnement cohérent. Cette API n'est pas encore appelée dans Studio :
elle ne prétend ni activer le cycle ni remplacer l'IBL dynamique restant à porter.

La fixture `frame-lighting-tests-20261003-v1` (session 42775) compile et exécute
les quatre tests exacts des modules figés : les 48 échantillons solaires source,
pause/wrap/refus, identité des écritures cloud/water et conservation de tous les
autres octets, refus atomiques. Tous passent. Le moteur entier passe de nouveau
cargo check sans avertissement dans `solar-engine-cargo-check-20261003-v3`
(session 59366, retour 0, source_unchanged=true). Les sources ont ensuite seulement
reçu l'ordre de modules rustfmt dans tidewater_sky.rs. Aucun dessin de l'eau ni
nouvelle frame Studio n'est qualifié par ces contrôles.

### Programmes d'environnement disponibles dans le moteur

`tidewater_environment::programs::Input::from_acquisition` vérifie le manifeste
épinglé, sa version/source, les 27 payloads et leurs SHA, les 22 identités uniques,
les dispatches source exacts, les groupes et bindings explicites et l'ABI des
quatre uniformes (720/64/80/32 octets). La lecture utilise seulement le transport
du caller. `Input::prepare` prépare ensuite les vrais shaders/pipelines/layouts
sur le RenderDevice Bevy fourni, avec scopes de validation asynchrones ; aucune
seconde instance GPU ou donnée de remplacement n'est introduite. Les identités
de ressources restent exposées pour le futur raccordement aux textures résidentes.

Le parser de layout est extrait du probe précédent sans changer ses règles.
La première compilation complète Cargo du nouveau module passe dans V4
(session 58790, retour 0, sources inchangées). Les dispatches sont ensuite
resserrés à leurs dimensions acquises exactes, au lieu d'une simple borne.

La fixture `environment-programs-native-20261003-v1` (session 20354, retour 0)
fige les deux sources de production et fait consommer le paquet réel épinglé
6dcf263f…bc19f par ce nouveau chargeur, puis initialise Bevy et attend sa
préparation GPU. Les 22 programmes passent ; gpu.stderr est vide. Les essais
négatifs refusent un dispatch altéré, une identité dupliquée et un manifeste de
hash différent. Les avertissements du harness concernent seulement les anciens
helpers/variable de son probe, devenus inutilisés après remplacement par le
chargeur de production.

Cette étape prépare les programmes, mais ne les ordonnance pas encore dans le
RenderApp, ne lie pas les LUTs/nuages résidents et ne publie ni cube ni SH vivant.
Le champ dispatch_executed de ce reçu vaut false ; les anciennes preuves de
calcul sous données acquises restent distinctes. Une reprise Cargo V5 du moteur
entier est lancée en session 19753
pour les sources finales ; aucun verdict anticipé de scène ou de parité.

La session 19753 termine retour 0 après 35,04 s, sans avertissement et avec
source_unchanged=true. Le moteur entier compile donc avec le chargeur et les
dispatches source finaux. Le diff-check ciblé passe. La liaison des ressources
vivantes, le calcul dans le graphe de scène et la publication de l'IBL restent
les prochaines étapes ouvertes.

### Encodage des calculs sur ressources empruntées

`Prepared::encode` reçoit le device issu de Bevy, le command encoder du caller
et les ressources explicites de la phase (ciel, mips, filtre GGX ou SH). Il
vérifie les tailles/usages des buffers et cubes, la non-aliasation du filtre,
les phases et l'ordre des bindings source. Il ne crée ni texture de remplacement,
ni queue, ne soumet pas les commandes et ne publie aucun état de scène. Le caller
reste responsable de l'ordonnancement et de l'acquittement GPU.

Le check complet V6 passe avec la première signature RenderDevice. Cette
signature est ensuite adaptée au device wgpu emprunté à Bevy pour l'utiliser
directement dans le graph et le probe ; le check complet V7 (session 40895)
termine retour 0 sans avertissement, source_unchanged=true.

`environment-programs-native-20261003-v2` (session 43541, retour 0) utilise
les modules de production figés pour la préparation et les 22 encodages effectifs.
Le harness conserve l'acquisition, upload, readback et comparaison du probe,
mais remplace ses trois blocs de dispatch (faces, mips, GGX/SH) par Prepared::encode.
Le fichier transformé est épinglé en plus des sources originales. Il conserve
les données réelles du snapshot cloud final/atmosphère et le manifeste live
437f26a7…3bf16. gpu.stderr est vide ; les 22 dispatches passent sur le GPU Bevy.

Les 48 faces de source générée ont 3 faces byte-identiques ; les 72 faces GGX
n'ont aucune identité byte complète. L'erreur RGB absolue maximale est 0,03515625,
et l'erreur SH maximale 5,793571472167969e-05. Ces maxima correspondent aux
écarts de la précédente génération sky/mips sous le probe ; les statistiques
mean/p95/RMS/relative L2 par face sont désormais conservées. Exemple face 0,
mip 0 : p95 0,00048828125, RMS 0,000234605, relative L2 0,000783990.
Ce résultat prouve l'exécution du chemin de production sous acquisition réelle,
pas une identité numérique ou une parité visuelle acceptée. L'analyse de fidélité
reste ouverte, ainsi que le raccord aux ressources vivantes RenderApp/Studio.

Clippy du moteur entier avec warnings en erreurs termine retour 101 dans la
fixture `solar-engine-clippy-20261003-v1`, avec sources inchangées. Ses diagnostics
doivent être inspectés et corrigés dans le périmètre concerné ; aucun PASS de
ce contrôle n'est anticipé.

Le premier Clippy s'arrête dans forge_assets sur neuf diagnostics hors fichiers
modifiés ici. V2 utilise --no-deps pour atteindre le moteur : 45 diagnostics,
dont longueur/arguments/docs/default du nouveau chargeur et encodeur, ainsi que
trois points de style dans l'atmosphère/nuages. Les descripteurs de bindings sont
désormais Copy (ils ne possèdent que des références), les docs/defaults sont
précisés et les closures/branche sont simplifiées. Deux allow too_many_lines
motivés conservent la validation atomique d'acquisition et l'ordre explicite des
quatre phases de ressources ; aucune vérification fonctionnelle n'est supprimée.

V3 termine retour 101 avec 36 diagnostics, dont un dans solar_lighting sur le
test exact de l'alpha GPU. La comparaison utilise maintenant les bits f64 du
scalaire décodé et de 1,0 ; elle conserve le refus exact sans introduire de
tolérance. V4 est lancée en session 33744 pour contrôler les diagnostics restants.
Le Clippy global reste non qualifié tant que des diagnostics hors raccordement
subsistent ; ce contrôle ne remplace pas le cargo check ni les preuves GPU.

V4 termine retour 101, sources inchangées : 35 diagnostics dans d'autres modules,
aucun dans les fichiers environnement/atmosphère/nuages/ciel/optiques. Ce résultat
est un contrôle ciblé sans diagnostic solaire, pas un PASS Clippy du moteur.
Le diff-check global passe. Une reprise cargo check V8 vérifie la compilation
des sources après ces corrections de style ; verdict attendu.

V8 (session 84830) termine retour 0, sans avertissement, sources inchangées,
en 33,44 s. Les maxima RGB/SH de la nouvelle exécution sont aussi relus et
confirmés identiques aux maxima du reçu antérieur bevy-sky-ibl-v2. Cette égalité
des maxima ne signifie pas identité de tous les pixels ou validation visuelle.

### Ordonnancement source et raccord à l'encodeur

Le contrôleur d'environnement conserve les quinze étapes logiques de
Environment.js : six faces, une étape contenant les deux dispatches de mips,
six niveaux GGX, SH, puis bascule du front. Premier usage/force : tout le cycle ;
ensuite une étape par mise à jour, après trois secondes ou un déplacement
angulaire du soleil supérieur à 0,004 radian. Un cycle calcule seulement le
back buffer : quinze dispatches GPU, distincts des vingt-deux calculs de la
fixture précédente qui évaluait simultanément les deux sorties pour comparaison.

`Controller::plan` ne modifie pas l'état ; `validate` refuse une ancienne
révision/génération avant encodage, `commit` acquitte les commandes acceptées.
`Prepared::encode_cycle` relie ces étapes aux programmes de production sur
ressources empruntées, refuse l'aliasation des sorties front/back, et retourne
une publication seulement à la dernière étape. Un caller doit abandonner son
encoder si une phase échoue ; aucune soumission, clôture GPU, lecture CPU ou
publication de matériau n'est implicite.

La fixture `environment-controller-oracle-20261003-v1` compare exactement
l'ordre des dispatches et le front sur 900 mises à jour. Les méthodes
`_buildSteps` et `update` sont extraites sans réécriture du source épinglé et
exécutées par Node, avec ses vraies classes Vector3/Quaternion. Entrées, méthodes,
mathématiques source et Rust sont hashés dans le reçu. Cadence, changements de
soleil et forces pendant un cycle sont couverts ; comparaison PASS, deux tests
Rust PASS. Ce contrôle ne fait pas intervenir le GPU.

Cargo check V9 (session 1799) termine retour 0, sans avertissement, en 40,16 s,
avec sources inchangées. Clippy V5 (session 93991) termine retour 101 : les
35 diagnostics globaux subsistent, aucun dans tidewater_environment, y compris
le contrôleur et le raccord cycle. Ce n'est toujours pas un PASS Clippy global.

La première fixture GPU `environment-cycle-native-20261003-v1` ne compile pas :
sa transformation avait retiré la déclaration du vecteur de readback `reads`.
Aucun GPU n'y est exécuté ; les sources de production ne sont pas en cause.
V2 corrige seulement cette extraction et tourne en session 53308. Son verdict
reste attendu. Le raccord aux ressources vivantes RenderApp/Studio et la
publication cohérente dans les matériaux restent ouverts.

V2 termine retour 0 (session 53308), compilation et exécution GPU réussies,
gpu.stderr vide. Les sources de production figées exécutent un cycle initial
sur cible 1, puis quinze mises à jour logiques sur cible 0 : trente dispatches
effectifs au total. Le front reste inchangé avant chaque commit, et la publication
est absente jusqu'à la dernière étape. Les essais négatifs refusent une ancienne
révision et l'aliasation des deux cubes. Les données d'atmosphère/nuages sont les
vraies données acquises, maintenues fixes pour cette comparaison.

Les maxima restent RGB 0,03515625 et SH 5,793571472167969e-05 ; 3/48 faces source
sont byte-identiques, 0/72 faces GGX. Les deux sorties SH produites sont identiques
entre elles mais diffèrent de la référence. Ce harness regroupe les étapes
logiques dans un même command encoder avant sa soumission finale : il ne prouve
pas encore une exécution sur quinze frames réelles du Studio ni un éclairage
variant pendant ces frames. Les avertissements de compilation concernent des
helpers/variables du harness devenus inutilisés. Aucune acceptation visuelle,
aucun nouveau build de distribution et aucune publication ne sont déduits.

### Sorties résidentes adoptées par la préparation de scène

`tidewater_environment::resident::stage` prépare sur les Assets Bevy existants
un cube source 128×128×6 à huit niveaux, deux cubes filtrés à six niveaux et
deux buffers SH de 144 octets. Les usages incluent stockage, sampling et copie
GPU ; les vues de cube et le filtrage restent ceux du contrat acquis. Les deux
cubes filtrés et les deux buffers SH sont initialisés avec les données acquises,
sans éclairage neutre de remplacement. Le source n'est pas affiché et sera
écrit par les six faces du premier cycle. Les dimensions, les octets et les
coefficients finis sont validés avant toute allocation.

`PreparedEnvironment::stage` appelle désormais ce chemin, conserve ses handles
dans `ResidentEnvironment::environment_targets`, et utilise le premier cube
filtré comme environnement affiché initial. Une génération suivante reçoit
de nouveaux handles et ne détruit pas ceux qu'un consommateur conserve encore.
Le système d'exécution/publication du cycle dans RenderApp n'est pas encore
installé par cette modification ; les matériaux gardent l'éclairage acquis.

La fixture `environment-resident-20261003-v1` compile et passe trois tests,
dont un vrai upload par les plugins Bevy/GpuImage/GpuShaderBuffer, suivi de
readback sur le même device. Les données viennent de l'environnement Studio
dddf01a2…bd097 : cube SHA fa25fd9b…acc31 et éclairage ed804986…c7f7b.
Les 72 faces/niveaux filtrés et les deux buffers SH sont byte-identiques après
upload. Les deux tests CPU couvrent l'absence d'allocations après refus et la
conservation des anciennes générations. Aucun dispatch d'environnement ni
dessin Studio n'est exécuté dans cette fixture.

Cargo check V10 termine retour 0 en 33,46 s. Clippy V6 ajoute un diagnostic
owned : l'image acquise était passée par valeur sans être consommée. Le stage
emprunte maintenant cette image ; V2 de la fixture recompile la source finale
et repasse ses trois tests, y compris les 72 faces et deux buffers GPU identiques.
Cargo check V11 termine retour 0 en 36,52 s, sans avertissement ; Clippy V7
retrouve seulement les 35 diagnostics hors raccordement, aucun dans les modules
environnement ni préparation de scène. Sources inchangées pendant ces contrôles.
Le Clippy global reste en échec, et aucun nouvel exécutable du Studio ou verdict
de parité visuelle n'est déduit des tests d'upload.

### Cycle installé dans le graphe de rendu Bevy

Le nouveau module `tidewater_environment::live` enregistre un système
EnvironmentCompute après CloudCompute et avant camera_driver. CloudCompute est
désormais un SystemSet explicite du vrai système nuages, lui-même placé après
AtmosphereCompute. L'installation reçoit les programmes préparés sur le device
Bevy et les handles de cette génération ; elle ne substitue pas de renderer.

Une seule frame immuable attend son encodage. Elle transporte les uniformes
source 720/64/80/32 octets, le vrai soleil et l'identité du step/vue nuages. Les
entrées finies, le soleil et les paramètres courants d'atmosphère sont contrôlés.
Le système attend les ressources GPU et exactement le step nuages demandé ;
une frame dépassée produit une erreur lisible au lieu de sampler une autre
histoire temporelle. Un remplacement de génération invalide la soumission et
la publication de l'ancien cycle.

L'encodeur environnement est privé jusqu'à la réussite de toutes ses phases.
RenderContext::add_command_buffer l'ajoute après les commandes précédentes :
une erreur peut donc abandonner ce travail sans perdre les calculs atmosphère
et nuages. Les uniformes sont des buffers immuables propres à cette frame. Le
contrôleur est ensuite committé et un couple cube/SH publié seulement au swap.
La publication acquitte l'encodage ordonné, pas la fin GPU ni un readback CPU.

Cargo V12 échoue sur l'import manquant de DVec3, corrigé explicitement depuis
bevy::math. V13 termine retour 0 en 33,61 s, sans avertissement, sources
inchangées. Clippy V8 ajoute cinq diagnostics sur les paramètres Res Bevy et
une closure redondante. Un allow motivé conserve la signature de système
attendue par Bevy ; Vec4::to_array remplace la closure. V9 termine retour 101
avec uniquement les 35 diagnostics hors environnement, aucun dans ces fichiers.

La fixture `environment-live-20261003-v1` compile, mais son essai d'intégration
s'arrête avant initialisation du renderer : le reader cherchait une clé sha256
dans une table qui contient directement les hashes. Quatre tests CPU passent,
l'essai intégré échoue ; aucune exécution GPU n'est déduite de V1.

V2 corrige cette lecture stricte. Les cinq tests passent (session 20024), dont
le vrai système live sur seize app.update/frames Bevy : refresh initial cible 1,
puis quinze étapes réparties sur les frames suivantes avant publication cible 0.
Les 72 faces/niveaux et deux buffers SH résidents sont byte-identiques aux
sorties de l'encodeur de cycle précédent. Le refus d'une seconde frame pendante,
de mauvaises entrées et d'une ancienne génération sont également contrôlés.
tests.stderr est vide. Les types Targets atmosphère/nuages sont extraits du
produit ; leurs données GPU proviennent du snapshot réel acquis, mais leurs
kernels ne sont pas exécutés par ce harness. Cette preuve couvre l'exécution
multi-frame de l'environnement et son upload Bevy, pas toute la chaîne animée.

Le Studio n'appelle pas encore register/stage/submit sur ce module et les
matériaux n'adoptent pas encore les publications. Le chargement des programmes
par la bibliothèque, l'activation solaire et la synchronisation cube/SH/eau
restent ouverts, ainsi que les captures comparatives et le nouveau portable.
L'attribution des samplers est précisée après V2 vers GPU.js/Shader.js ; seul
le commentaire change. Cargo V14 contrôle les sources finales, verdict attendu.

V14 (session 93669) termine retour 0 sans avertissement, sources inchangées.
Le formatage des modules environnement/nuages et le diff-check passent. Le
Clippy global et la validation Studio/WebGPU restent distincts et ouverts.

### Publication GPU dans le buffer des matériaux

`live::bind_lighting` adopte un buffer stable de 176 octets. Après chaque frame,
le command encoder copie le préfixe direction/couleur de 32 octets depuis la
lumière source validée. Les 144 octets de SH sont copiés seulement au swap,
depuis le même back buffer que le cube publié. Une frame idle met ainsi à jour
le soleil sans réécrire la lumière ambiante. Les usages et tailles réels du
buffer destination sont vérifiés avant l'encodage ; aucun readback CPU n'est
nécessaire pour ces copies.

Le module scene_buffer/ownership protège ce handle d'une réinitialisation CPU.
Un ancien détenteur ne peut pas libérer un nouveau détenteur. La publication
CPU refuse un buffer contrôlé par le GPU, et le caller nuages Studio saute son
upload acquis lorsque ce contrôle est actif. La préparation d'une nouvelle
scène libère l'ancienne génération avant de publier ses seeds.

Inspection du code Bevy ExtractResource : enlever la ressource Main ne retire
pas automatiquement sa copie Render. `live::clear` invalide donc explicitement
le Progress partagé via un AtomicBool, en plus de libérer le lease et retirer
les ressources Main. Le système vérifie cette invalidation avant calcul et
avant acceptation des commandes. Une ancienne frame pendante ne peut ainsi
écraser le seed de la scène suivante.

La fixture `environment-live-lighting-20261003-v1` passe neuf tests (session
22724), dont l'exécution GPU sur les vraies ressources acquises et le buffer
176 octets utilisé par les matériaux. Les 72 faces et les deux SH restent
identiques au précédent encodeur ; le tail SH du buffer partagé correspond au
hash de la sortie GPU. Une frame idle modifie le rouge de lumière directe sans
modifier un seul octet SH ni publier un nouveau cube. Un clear avec frame
pendante, suivi de deux app.update, préserve le nouveau seed CPU sur le GPU.
Ces essais couvrent 19 mises à jour Render ; ils ne dessinent pas de terrain
ou d'arbre et ne font pas tourner les kernels atmosphère/nuages animés.

Cargo V15 passe avant l'ajout de l'invalidation active. Clippy V10 ne signale
qu'un nouveau if imbriqué dans ownership, simplifié sans changer son contrôle
de lease. V2 de la fixture reprend les sources finales : neuf tests PASS,
tests.stderr vide, session 65855. Cargo V16 passe en 31,88 s sans avertissement,
sources inchangées. Clippy V11 garde les diagnostics globaux hors raccordement ;
le contrôle ne qualifie toujours pas le moteur entier. Les matériaux bâtiments
et eau, l'adoption du cube par les matériaux, le chargement de programmes et
l'activation de tout le cycle dans Studio restent ouverts. Le petit caller
Studio modifié est formaté mais aucun nouveau build Studio n'est encore produit.

### Cube affiché stable et publication dans la même frame

Les matériaux utilisent désormais un quatrième cube résident, distinct du
source et des deux cibles de travail. Il est initialisé avec les octets acquis.
Au swap, six copies GPU transfèrent les niveaux du filtre terminé dans ce cube
stable, dans le même command buffer que la copie SH et avant la caméra. Le
handle affiché reste identique : aucune adoption Main de handle différée d'une
frame n'est nécessaire. `ResidentEnvironment::environment` et Publication::cube
désignent cette sortie stable. Le calcul GGX/SH source n'est pas modifié.

Le système refuse l'aliasation du cube affiché avec les textures de travail,
les dimensions/formats/niveaux différents et les usages de copie manquants.
Les tests CPU vérifient désormais quatre images, toutes les sorties affichées
initialisées à l'identique, et les générations conservées lors d'un remplacement.

La première compilation V17/fixture display V1 échoue : Texture Bevy n'expose
pas PartialEq directement. Le contrôle compare maintenant les textures wgpu
sous-jacentes. V2 passe ses neuf tests GPU/CPU et Cargo V18 passe en 44,60 s.
Les bornes exactes et les usages de sampling/copie sont ensuite resserrés ;
display V3 passe à nouveau neuf tests en 2,83 s, Cargo V19 en 31,64 s.
Les 36 faces/niveaux affichés correspondent exactement au filtre terminé ;
les 72 faces de travail et deux SH gardent les résultats du probe précédent.

Un probe distinct injecte un pixel magenta fini dans la cible de travail après
filtrage et avant publication. Le pixel affiché reste inchangé jusqu'à l'étape
de swap, puis prend exactement cette valeur. Cette injection vérifie seulement
l'isolation et la copie : elle n'entre pas dans les comparaisons source. La
fixture soumet 32 frames environnement, puis deux mises à jour après clear ;
la conservation du SH pendant une frame idle et l'annulation de l'ancien cycle
restent vérifiées. Aucun terrain/arbre n'est dessiné par ce harness.

Clippy V12 signale six appels deref explicites dans le nouveau contrôle. La
déréférence opérateur remplace ces appels sans changer la comparaison ; V13
retrouve seulement les 35 diagnostics hors environnement, aucun dans ces modules.
Le graphe live est aussi enregistré dans l'installation d'environnement Studio.
Sans stage de programmes et submit de frames, son run_if le laisse inactif.

Le cargo check complet du frontend WebGPU est lancé avec --no-default-features
--features web --target wasm32-unknown-unknown. V1 compile avec retour Cargo 0
en 4 min 57 s, mais son receipt refuse la stabilité source : cinq fichiers
navigation/gameplay ont changé pendant la compilation. Ces changements sont
préservés. V2 reprend le même cache et retourne 0 en 0,74 s avec tous les pins
source inchangés. Cela couvre la compilation du vrai caller Studio, du moteur
WebGPU et du nouveau graphe ; aucun exécutable/WASM lié ni lancement navigateur
n'est déduit. L'avertissement future-incompatibility est dans proc-macro-error2,
pas dans les fichiers de ce raccordement.

La croissance du cache WebGPU exige une libération d'espace. Seuls onze
exécutables temporaires tests.exe/probe.exe issus de fixtures déjà terminales
sont supprimés, après contrôle de leurs chemins et des processus tests/probe.
Chaque chemin, taille et hash est conservé dans
environment-check-binary-cleanup-20261003.jsonl ; sources, scripts, acquisitions,
captures et receipts restent présents. Aucun portable utilisateur ni cache
Cargo en cours n'est supprimé. Une reprise native V20 contrôle l'état courant
après les changements navigation, sans requalifier la fidélité visuelle.

V20 (session 45961) termine retour 0 en 32,12 s, sans avertissement et avec
sources inchangées. Formatage et diff-check passent. L'activation des programmes
et des frames live, les autres familles de matériaux et la vérification visible
de toute la scène restent ouvertes ; l'objectif n'est pas déclaré achevé.

### Acquisition des programmes par la bibliothèque Studio

`forge_assets::tidewater_environment::programs::ProgramsPackage` acquiert les
27 fichiers via un transport async borné. Le manifeste doit correspondre au
hash attendu, au commit source et à l'allowlist exacte avant toute requête de
payload. Chaque payload est vérifié et un échec arrête les requêtes suivantes.
Les métadonnées source/kernels du manifeste sont conservées pour la validation
moteur ; aucune instruction ni source JavaScript n'est exécutée.

Le candidat de bibliothèque accepte `environment_programs_sha256`. Lorsque
présent, il exige aussi les ressources d'environnement, charge
`environment-programs.json`, valide les identités/layouts via
`Input::from_package`, puis publie les programmes avec les autres ressources
seulement après vérification complète et contrôle du ticket de chargement.
Un nouveau candidat sans programmes remplace aussi le slot précédent par None.
Les candidats existants restent acceptés ; aucun candidat déployé n'est modifié
dans cette tranche.

Fixture `environment-programs-transport-20261003-v1` : trois tests PASS,
incluant rejet du pin avant transport, rejet d'un chemin hors allowlist,
arrêt au premier payload corrompu et acquisition réelle des 27 fichiers du
package source `6dcf263fd9ad30c2e46491c0dd02879ea4766a55ad2dd8d04b969487e6fbc19f`.
Source inchangée pendant les tests. Le check du vrai frontend WebGPU V3 termine
retour 0 en 1 min 17 s, sources inchangées. Formatage et diff-check passent.
Ces preuves ne lancent ni Studio ni GPU. Il reste à exposer les programmes au
cycle de scène, les préparer sur son RenderDevice, déployer le candidat complet
et soumettre les frames cohérentes atmosphère/nuages/éclairage.

Clippy : une première invocation choisit accidentellement le driver 1.99 via
rustup malgré le PATH 1.98, puis le cache natif partagé contient un memchr
incompatible. Aucun nettoyage global n'est effectué. La reprise avec
RUSTUP_TOOLCHAIN explicite et un target isolé termine l'analyse : deux
must_use du nouveau module sont corrigés ; l'ultime invocation conserve neuf
diagnostics dans les autres fichiers forge_assets, aucun dans programs.rs.
Le contrôle Clippy global n'est donc pas PASS. Avant une nouvelle utilisation
native du cache partagé, requalifier/reconstruire ses dépendances concernées.
Les tests et check WebGPU ci-dessus précèdent uniquement les deux attributs
must_use ; aucun changement de comportement après ces preuves.

### Préparation sur le device Studio et soumission cohérente

Le caller Studio prépare maintenant Input sur le RenderDevice existant, puis
stage les 22 programmes avec les targets résidents et lie le buffer partagé de
lumière. La préparation async appartient au World via un slot Arc/Mutex :
remplacer la préparation abandonne son ancien résultat. Le ticket de publication
et l'identité Arc de la génération nuage sont contrôlés avant adoption. Une
bibliothèque remplacée invalide également les programmes déjà actifs, y compris
si le nouveau staging échoue. Les erreurs de calcul sont rapportées une fois.

Le producteur Studio des nuages soumet ensuite une FrameInput environnement
avec les bytes global, le view_index, le step et les paramètres publics du
Pending nuage exact. Le ciel est choisi par sa texture de transmittance de cette
génération. Le producteur attend aussi le pending environnement : il ne peut
pas avancer son historique avant l'encodage de la dépendance. L'heure solaire
reste celle de l'acquisition actuelle ; ce raccordement n'active pas encore
le Clock/readback solaire dynamique dans le caller.

Cargo check frontend WebGPU V4, V5, V6 et V7 terminent retour 0, pins inchangés.
V7 couvre le dernier caller avec signalement d'erreur (16,39 s). Aucun WASM lié,
lancement navigateur, dessin de carte ou acceptation visuelle n'est déduit.
Le candidat déployé ne contient pas encore environment_programs_sha256 ; ce
chemin ne sera actif dans la scène utilisateur qu'après déploiement du paquet
et du frontend correspondant. Les autres familles de matériaux restent ouvertes.

La fixture cloud-frame-snapshot-20261003-v1 compile puis exécute les tests des
modules de production atmosphère, nuages, solaire et packer, sur dépendances
Bevy natives mises en cache. Le test dependent_snapshot_preserves_submitted_uniforms_and_history
passe avec les autres tests : step/view/720 bytes/paramètres publics exacts et
snapshot conservé après suppression du Pending. Receipt terminal retour 0,
GPU et Studio non exécutés. Les warnings dead_code de ce harness partiel ne
valent pas diagnostic du frontend complet. Formatage et diff-check passent.

### Paquet candidat complet et restauration du consumer ponton

L'overlay studio-environment-library-20261003-v1 ajoute les 27 payloads et le
manifeste des programmes au candidat desktop motion existant. Tous les champs
précédents sont conservés, notamment pier_motion_sha256 ; le candidat initial
reste inchangé. Candidat overlay SHA a157ae09f255d9407f242dcc6a2146bce868512210ec9ccf145852794a40ff5f.
L'overlay seul n'est ni une bibliothèque complète ni un déploiement.

La vérification révèle que la source courante ne raccordait pas les modules
ponton/vent déjà utilisés dans le candidat motion figé. Ils sont maintenant
exportés ; le loader vérifie le manifeste et les phases avec la géométrie avant
publication atomique. Le caller motion du snapshot motion V2 est intégré sans
écraser d'autre fichier. Le rebind conserve états et phases lors du changement
commun de placement. WindFrame et math motion correspondent byte pour byte au
snapshot V2. V8 échoue sur deux méthodes rebind absentes ; ajout de la méthode
conservant l'état, puis check WebGPU V9 PASS en 1 min 02 s, pins inchangés.

Les contrôles suivants atteignent la limite disque E : Clippy engine et fixture
ECS échouent sur no space, sans verdict source. Seuls le target Clippy isolé et
deux exécutables de tests terminés sont supprimés après contrôle des processus
et chemins ; journal environment-programs-cleanup-20261003-v1.jsonl. Les sources,
preuves, candidats et cache partagé sont conservés. La nouvelle fixture ECS
F:/OpenRPG_Forge/.build/pier-motion-restored-20261003-v1 termine PASS, deux tests :
240 frames comparées à l'acquisition, deux instances, copie/rotation/retrait,
rebind après édition avec phases préservées. Elle utilise le nouveau module
animation courant et la géométrie cached ; GPU/Studio non exécutés.

Le build complet du frontend est lancé dans un nouveau dossier isolé
F:/OpenRPG_Forge/.build/studio-live-environment-20261003-v1 (session 76683).
Sources courantes figées, caches release copiés depuis la baseline en lecture,
aucun cache partagé modifié ; compiler/flags épinglés 1.98 et offline/locked.
Il reste à lire son receipt terminal, produire les bindings/worker et assembler
la bibliothèque complète avant un lancement. Ce build n'est pas déclaré PASS
à son lancement et aucun déploiement utilisateur n'est effectué.

Précision de suivi : la description ci-dessus est celle du runner lancé, pas
la preuve que copie/compilation sont terminées. Le premier poll constate encore
la phase de gel (928 fichiers, 10 367 412 octets écrits), sans source-inputs.json
ni build-request.json. La session 76683 est confirmée active ; aucun redémarrage.

### Assemblage de la bibliothèque complète et pipeline frontend

La reprise de session 76683 confirme le runner de build actif ; aucun nouveau
build n'est lancé. Le cache natif copié atteint 2 451 fichiers/919 918 796 octets,
puis la copie des artefacts release WASM commence. Cela ne constitue pas encore
un retour Cargo.

Assemble.py --library-only (session 7472) termine retour 0 : 1 787 fichiers
vérifiés et copiés dans la bibliothèque complète library-stage, avec empreintes
de la preuve du desktop motion pour les assets existants et pins du manifeste
pour les nouveaux programmes. Candidat SHA a157ae09f255d9407f242dcc6a2146bce868512210ec9ccf145852794a40ff5f.
Tous les champs précédents sont conservés. Receipt library-stage.json sur F.
Ce stage contient forge-imports et forge-kit ; aucun frontend neuf ni déploiement
utilisateur n'est déduit.

Les étapes restantes sont préparées en scripts contrôlés dans la fixture E :
Assemble.py exige le receipt terminal PASS du build et revalide les pins avant
wasm-bindgen 0.2.127. BuildWorker.py exige ce résultat et les bindings, construit
le worker sur les mêmes sources figées et produit frontend-ready.json seulement
après succès. Les trois scripts passent py_compile ; ces deux étapes aval ne
sont pas encore exécutées. Browser et qualification visuelle restent ouverts.

Le suivi suivant confirme encore la session 76683 active, sans relance. La
copie WASM atteint 4 867 fichiers/2 571 991 726 octets ; le cache source contient
4 919 fichiers/2 801 554 414 octets. La compilation n'est pas déduite de ces
copies seules. Le helper de lecture 80972 est terminal ; seul 76683 reste actif.

Le worker préparé émet maintenant également le manifeste exact exigé par
Build-StudioCandidate.ps1 (schema forge.navigation-worker-build, sources et
artifacts épinglés), après réussite seulement. Pour le candidat natif authoring,
le kit doit rester séparé dans l'input du constructeur : il refuse une seconde
copie ambiguë ; ne pas supprimer celui du frontend navigateur actuel.

Le launcher demandé est identifié dans launcher-aube et le prototype Windows
OpenRPG_Forge-Launcher-Prototype/prototypes/forge_launcher. Sa documentation et
Build-WindowsPlaytest.ps1 décrivent le token FORGE_LAUNCHER_AUBE_FRONTEND_READY,
l'UI empaquetée et runtime/tidewater.exe. Le test HTTP 8848 refuse actuellement
la connexion ; cela ne détermine pas l'état d'un binaire empaqueté. Aucun ancien
launcher Godot n'est repris, aucun nouveau lancement n'est encore exécuté.

Le dernier poll confirme build-request.json présent et le journal Cargo affiche
Compiling web-sys v0.3.104 : la phase de compilation a réellement commencé,
session 76683 toujours active. Bibliothèque assemblée PASS ; frontend encore
sans receipt terminal. Ne pas relancer le build ni exécuter les étapes aval
avant son retour.

### Préparation du test natif pendant la compilation

La session 76683 est repollée et confirmée active ; le journal Cargo progresse
jusqu'à bevy_core_pipeline. Aucun receipt terminal ni relance. StageNative.py
prépare le split frontend/kit exigé par le constructeur et vérifie les pins
worker contre les sources canoniques avant toute copie. Son contrôle de syntaxe
passe ; le stage n'est pas encore exécuté faute de frontend-ready.json.

PrepareRuntime.py termine retour 0 : 26 fichiers du fixture authoring motion
sont vérifiés/copiés, source inchangée, locks exclus, nouveau profil WebView dans
studio-live-environment-20261003-v1/runtime-fixture. Aucun projet utilisateur
actif n'est modifié et aucune fenêtre nouvelle n'est lancée.

Le skill Computer Use est lu (SKILL/guidance/confirmations/api), @oai/sky est
initialisé par node_repl et list_windows répond. Aucune fenêtre forge-studio
n'est retournée ; aucun input UI ni screenshot ne qualifie donc encore la scène.

### Runner natif préparé et attente vérifiée

La session 76683 reste active et le journal progresse jusqu'à bevy_pbr, sans
receipt terminal. Le dernier tour est une attente vérifiée d'un handle live,
pas un build considéré arrêté ni une relance.

Un contrôle CIM autorisé constate zéro cargo/rustc/clippy dont la command line
référence F:/OpenRPG_Forge/.build/cargo-target. Ce constat vaut au moment du
contrôle seulement. BuildNative.py répète ce contrôle avant utilisation, exige
native-inputs.json et les sources canoniques identiques au snapshot frontend,
puis conserve un inventaire avant/après. Le script passe py_compile mais n'est
pas exécuté : aucune adoption du cache ni nouvelle compilation native n'est
encore déduite. Les étapes bindings/worker restent dépendantes du retour Cargo.

### Reprise après omission de ressources de compilation

La session 76683 termine avec code 101, sources figées et canoniques inchangées.
Les huit diagnostics forge_world sont des include_str! absents du snapshot,
pas des erreurs de logique du moteur. failed-attempt-1.json conserve ce résultat.
ResumeFrontend.py ajoute et épingle les 13 ressources littérales externes
référencées par les sources Rust (exemples JSON et deux ressources de scripts),
sans modifier les fichiers canoniques. compile-resource-repair.json décrit les
hashes et source-inputs-before-resources.json conserve l'ancien inventaire.
Le cache reste en place ; aucune recopie des dépendances ni suppression.

Le build reprend sous session 9488 après autorisation automatique des écritures
isolées sur F. Le handle répond vivant ; aucun résultat réussi n'est déduit.
Les logs de cette tentative sont retry-1.stdout/stderr. Les étapes bindgen,
worker et candidat natif restent non exécutées. BuildFrontend.py résout désormais
les include_str!/include_bytes! littéraux pour éviter de reproduire cette omission.

La lecture du caller Studio confirme aussi que l'environnement reçoit les frames
nuageuses courantes, mais pas encore le Clock solaire et submit_solar dynamiques.
Cette limite reste à corriger après la validation de ce candidat ; aucune parité
jour/nuit n'est annoncée sur la base du seul raccordement GPU existant.

### Suivi de la tentative corrigée et préparation du lancement

La session 9488 répond toujours active ; retry-1.stderr atteint forge_engine,
après forge_world, forge_studio, leptos_dom et forge_assets. La lecture des
messages Cargo ne retourne aucun diagnostic error à ce point. result.json
conserve encore le FAIL de la première tentative jusqu'au retour terminal ;
ne pas le confondre avec un nouveau résultat.

LaunchNative.py est préparé et passe le contrôle de syntaxe. Il exige un reçu
natif PASS et l'exécutable correspondant, vérifie les 26 fichiers du projet
isolé avant lancement, puis utilise FORGE_AUTHORING_ROOT et un profil WebView
séparés. Il n'est pas exécuté : aucun lancement ni dessin nouveau n'est prouvé.

Le garde CIM de BuildNative.py est corrigé pour normaliser les séparateurs
Windows avant comparaison. Les témoins avec chemins slash/backslash passent ;
la lecture CIM autorisée constate zéro lecteur du cache natif au moment du
contrôle. Le runner répétera ce contrôle à l'exécution ; ce constat n'est pas
une réservation ni une preuve de construction native.

### Contrôle live et récepteurs lumineux restants

Session 9488 confirmée active. Lecture CIM : rustc PID 2568 compile forge_engine
sur la cible isolée (178,1 secondes CPU au relevé). Pas de relance ni de verdict
terminal. Le matériau Village garde SceneLighting en uniform binding 18 et
local_lights en storage 54 ; terrain/plantes utilisent le buffer de scène partagé
au binding 54. imported_scene.rs clone encore environment.scene_lighting pour
Village, et fish_stand_scene.rs le clone également. Le raccord lumineux commun
reste donc à traiter ; choisir un binding compatible avec les lampes Village.
Ces observations de code ne prouvent pas un écart pixel précis ni un dessin GPU.

### Attente bornée et dépendance du flare

Le poll long de la session 9488 retourne après 30 secondes avec le même handle
actif, sans résultat terminal. Pas de seconde compilation. La lecture actuelle
confirme FishStandMaterial.scene_lighting au binding uniforme 18. Le système
TidewaterLensFlare prend aussi direction/couleur dans le premier VillageMaterial
(avant fallback DirectionalLight) : sa copie CPU devra suivre le snapshot solaire
courant lorsque le cycle sera raccordé. update_lighting de l'océan existe mais
aucun appel Studio n'est trouvé. Ces récepteurs restent ouverts ; aucune nouvelle
qualification graphique ou de parité n'est déduite.

### Artefacts WASM du moteur produits

Le processus rustc 2568 a terminé ; sa disparition seule n'est pas un verdict.
Le journal Cargo de la même session 9488 contient désormais compiler-artifact
forge_engine avec fresh=false, rlib/rmeta sous target/wasm32-unknown-unknown/release.
retry-1.stderr poursuit vers leptos. Le poll borné suivant retourne encore le
handle actif ; aucun reçu de frontend terminé ni aucune étape aval exécutée.

### Frontend release réellement compilé

Session 9488 terminale retour 0 : Cargo termine le profil release optimisé en
16m47s. result.json attempt 2 indique frozen_source_unchanged=true et aucune
modification canonique. Les 995 entrées ont aussi été vérifiées indépendamment
contre les deux racines. forge_studio_frontend.wasm : 115692594 octets, SHA256
109edcb0420a41efc3b19604683eb71a1e80b5f8e15fb60617e70fa5331ba161.
Le warning future-incompat concerne proc-macro-error2, pas un échec du build.
La première tentative FAIL reste conservée. Assemblage/bindgen lancé en session
98880 sous autorisation automatique des écritures isolées sur F, encore actif.
Aucun lancement, dessin de matériau ou verdict de parité n'est déduit du build.

### Bindings terminés et worker lancé

Session 98880 terminale retour 0 : les 1787 fichiers de bibliothèque sont copiés,
wasm-bindgen 0.2.127 termine, frontend-bindgen.json contient les pins de tout le
frontend. Le worker de navigation n'est pas encore qualifié par ce reçu.
BuildWorker.py est maintenant réellement lancé en session 14839 sur les mêmes
995 entrées figées et le cache isolé, sous autorisation automatique F.
Aucun candidat natif ni fenêtre nouvelle n'est encore lancé.

### Worker release terminé, préparation native en cours

Session 14839 terminale retour 0. navigation-worker-result.json confirme les
sources figées inchangées ; le WASM brut fait 418403 octets, SHA256
2dc0ea0ae22c8f8c4b5a30b07ba0d8152f8f80bef2c8712adf671bd964a310a1.
Le JS, le WASM lié et le worker source possèdent leurs pins ; le manifeste
forge.navigation-worker-build et frontend-ready.json sont maintenant produits.
StageNative.py est réellement lancé sous session 87025 pour revalider les pins
et préparer le split frontend/kit. Aucun build natif ou lancement n'est encore
annoncé ; cette compilation ne qualifie pas l'exécution du worker en WebView.

### Inputs natifs prêts, construction Windows lancée

Session 87025 terminale retour 0 : native-inputs.json confirme le frontend
vérifié, le kit séparé et le WASM lié SHA256
74f9843e1f8fe8ac8a8e635e9fbda9da3d398f18b1f52c4a618472131b1efb2b.
BuildNative.py est réellement lancé sous session 21493, avec contrôle de sources
et garde CIM avant usage du cache F. Le reçu natif et le lancement restent
ouverts ; cette étape construit le shell Studio hébergeant WebGPU, pas le
runtime natif autonome ni le paquet L'Aube final.

### Candidat natif construit et première scène réellement visible

Session 21493 terminale retour 0 ; native-result.json et build-proof.json
confirment les sources inchangées et l'exécutable SHA256
6b311fb8b9bedec70a7467189476a6fe7b9f94f57701ac017a769dc232f9cd49.
La construction dev optimisée termine en 6m43s. Le premier lancement PID 30032
existe à trois secondes puis disparaît après un timeout d'autorisation de capture.
Aucun événement Windows de panne correspondant ni log runtime ne donne la cause ;
le projet de 26 fichiers reste intact. Ne pas annoncer un crash démontré.

Le lancement supervisé suivant utilise un profil neuf et conserve son handle
24154 (PID 35020). La capture fonctionne. UI : ouvrir la zone par la liste legacy
est refusé pour ownership workspace ; passage par Projets et modules > Ouvrir le
workspace > Ouvrir Tidewater island · zone 1 réussit. 33 objets/couches sont listés.
Objets > Fiche > Bibliothèque de bâtiments importés > Vérifier et charger la
bibliothèque retourne 23 bâtiments et environnement vérifiés. La préparation
termine et la scène affiche terrain, bâtiments, quai, végétation, nuages et eau.
La capture JPEG first-scene.jpg (1922×1112) est conservée dans la fixture de scripts E.

Ce résultat prouve un lancement et un premier dessin de scène dans WebView/WebGPU,
pas le cycle GPU complet de publication de l'environnement, la parité source,
les interactions, l'édition complète ni le runtime natif autonome. Aucune zone
utilisateur n'est touchée ; aucune publication, gate ou qualification finale.
Le handle du superviseur reste actif pour poursuivre les vérifications.

### Playtest visible, capture d’entrée et effet graphique observés

Même candidat SHA256 6b311fb8b9bedec70a7467189476a6fe7b9f94f57701ac017a769dc232f9cd49,
superviseur 24154 confirmé vivant au début de ce contrôle. Entrée réelle dans
Jouer : vue FirstPerson au niveau du village. Éclairage de travail désactivé et
case visiblement décochée. Tester & suivi expose les paramètres graphiques et
les diagnostics. Lecture runtime : position 53.60 / 3.70 / -77.00 m, au sol true,
simulation prête true ; ticks, images de nuages et étapes FFT/ShoreSim progressent.
Ces compteurs sont annoncés comme observations CPU, sans preuve de publication GPU.

Cliquer dans le canvas obtient Exploration active et entrée active true.
Les commandes w puis z injectées par Computer Use ne produisent pas de mouvement
horizontal ; le diagnostic DOM rapporte code physique absent. Cela ne démontre
pas un défaut des touches physiques du produit et ne valide pas la marche.
Échap rend ensuite les entrées libres. Pas de contournement du contrat de focus.

Contrôle réel du potentiomètre Saturation : 1.06 → 0.00 affiche une scène
désaturée, puis retour 1.06 restaure les couleurs. Preuve limitée à ce réglage
et à ce cadrage ; pas de qualification globale des autres effets. Captures JPEG
1922×1112 conservées sous
.tidewater-integration/studio-environment-library-20261003-v1/ :
playtest-color-baseline.jpg, playtest-saturation-zero.jpg,
playtest-color-restored.jpg. playtest-runtime-observation.json conserve
l’observation UI et la limite de l’injection clavier. Préférences isolées dans
le profil de test ; aucune sauvegarde de zone demandée. Parité source, mouvements
physiques, collisions en déplacement et publication cohérente de l’environnement
GPU restent ouverts. Objectif complet toujours actif, aucune publication ni gate.

### Récepteurs bâtiments et stand raccordés au buffer partagé

Écart source confirmé : VillageMaterial et FishStandMaterial utilisaient uniquement
leur uniforme 18 figé, alors que terrain/plantes lisent le buffer stable de scène
mis à jour par le graphe d’environnement. Correction canonique : nouveau binding
storage 55 pour ces deux familles, clé de spécialisation explicite et déclaration
WGSL conditionnelle. L’adapter Studio active le récepteur partagé ; les probes
indépendants gardent leur uniforme existant. L’initialisation d’un binding inutilisé
ne remplace jamais un seed déjà présent. Binding 54 de lampes Village conservé,
sans collision avec le buffer scène. Le buffer de 176 octets et son ownership GPU
existants restent l’autorité ; aucune copie CPU du SH dynamique inventée.

Session 9810 terminale 0 : solar-studio-wasm-check-20261003-v10/result.json indique
source_unchanged=true. Cargo check du frontend WASM complet termine en 1m36s.
Formatage des fichiers touchés et diff --check ciblé terminés. Cette preuve ne
compile pas les pipelines WGSL et ne lance pas la modification dans le candidat.
Le programme ouvert SHA256 6b311fb8… reste antérieur à cette correction.
Tests CPU isolés lancés avec sources copiées/pinnées en session 63435 ; résultat
encore attendu lors de cette entrée. La cohérence soleil/eau/flare, le cycle
solaire et le résultat visuel de ces récepteurs restent ouverts. Pas de publication.

### Première tentative du harnais CPU conservée, reprise corrigée

Session 63435 terminale 1 : le harnais rustc autonome omettait
CARGO_MANIFEST_DIR, nécessaire aux derives Bevy ; aucun test exécuté. Il s’agit
d’une erreur de lancement du harnais, pas d’un échec Cargo du produit.
La tentative v1 et ses journaux restent conservés. Script corrigé : environnement
de macro explicite et clés de pins normalisées en chemins POSIX (pour que le
contrôle des fichiers copiés ne puisse pas sélectionner zéro entrée).
Nouvelle tentative isolée shared-lighting-receivers-20261003-v2, session 62397,
confirmée encore active par son handle. Aucun verdict de tests anticipé.

### Tests CPU des récepteurs partagés terminés

Session 62397 terminale 0. shared-lighting-receivers-20261003-v2/tests.stdout :
7 tests PASS, zéro filtré/ignoré, sources des récepteurs inchangées. Le contrôle
inclut les déclarations du village acquis et du shader stand embarqué, refus de
collision/duplication, conservation de la publication au réarmement, ownership
GPU et disposition alignée du buffer. Portée CPU ; aucun draw ou verdict visuel.

Build-SharedLightingFrontend-20261003.py lancé réellement en session 45314 sous
approbation automatique de l’écriture F. Snapshot nouveau sous
F:/OpenRPG_Forge/.build/studio-shared-lighting-20261003-v1 ; cache du candidat
précédent copié vers un target séparé, aucun ancien artefact écrasé. Handle
confirmé actif pendant la copie. Le check WASM précédent reste distinct du lien
release en cours. Qualification ShaderCache/Naga des 32 variantes de récepteur
original/embarqué lancée en session 18351 : pas de résultat anticipé et pas de
qualification de toute la chaîne d’adaptation scène ni de GPU par cette étape.

### Composition WGSL des deux récepteurs validée

Session 18351 terminale 0. shared-lighting-shaders-20261003-v1/tests.stdout
confirme 32 variantes : sept kinds village et un stand, chacun en buffer partagé
ou uniforme, avec meshes storage ou uniformes. Imports réels Bevy 0.19.1 et
Validator Naga exécutés ; le type lumière conserve 176 octets et offsets 0/16/32.
Binding 55 read-only pour le chemin partagé et binding 18 uniforme pour le chemin
indépendant, vérifiés par réflexion. Un test exécuté, zéro filtré/ignoré. Cela
qualifie la composition du shader acquis/embarqué avec la nouvelle déclaration,
pas toute la chaîne d’adaptation scène, un pipeline GPU, un draw ou la parité.
Le lien release en session 45314 reste attendu ; aucun nouveau candidat prêt.

### Assemblage indépendant du candidat à éclairage partagé

Scripts d’assemblage/worker/staging natif/build shell préparés dans
.tidewater-integration/studio-shared-lighting-library-20261003-v1. Ils gardent les
vérifications d’identité et refusent les outputs existants ; les assets viennent
du baseline portable et de l’overlay de programmes déjà pinnés. Aucun script
aval dépendant du lien release n’est exécuté avant son reçu terminal PASS.

Assemble.py --library-only réellement lancé en session 92437 sous approbation
automatique des écritures isolées F ; handle confirmé vivant et dossier
library-stage en cours. BuildFrontend session 45314 également confirmé vivant,
source-inputs.json créé et target en copie. Pas encore de journal Cargo ni de
library-stage.json : ne pas présenter ces opérations comme terminées. Aucun
ancien paquet ni candidat modifié. Cette attente porte sur ces handles précis.

### Copie du cache arrêtée avant Cargo, reprise avec artefacts conservés

Diagnostic CIM des processus exacts : Python 31836 avait copié environ 834 Mo
après plusieurs minutes ; Python 6660 assemblait les assets. Le snapshot source
était terminé, mais aucun build-request.json ni enfant Cargo n’existait encore.
Arrêt ciblé du seul PID 31836, nom/commande et absence d’enfants vérifiés ;
session 45314 terminale 1 par interruption. Copie partielle conservée, aucun
cache ni fichier de preuve supprimé. Ce choix corrige le coût de la duplication,
pas un timeout traité comme terminaison.

Resume-SharedLightingFrontend-20261003.py réellement lancé sous session 54829.
Garde CIM du cache existant vérifiée libre. Les WASM bruts antérieurs sont copiés
et rehashés dans prior-raw-artifacts avant sa réutilisation : frontend
109edcb0420a41efc3b19604683eb71a1e80b5f8e15fb60617e70fa5331ba161,
worker 2dc0ea0ae22c8f8c4b5a30b07ba0d8152f8f80bef2c8712adf671bd964a310a1.
cache-reuse-receipt.json conserve leurs chemins de récupération. Frontend lié,
candidat Desktop, projet et profil précédents inchangés. BuildWorker adaptera
son target depuis build-request.json ; aucune nouvelle source canonique modifiée.

Session 92437 terminale 0 : library-stage.json, 1787 fichiers vérifiés,
manifest candidat a157ae09f255d9407f242dcc6a2146bce868512210ec9ccf145852794a40ff5f.
Assemblage d’assets seulement, sans déploiement. Le handle 54829 est confirmé
vivant après création de cache-reuse-receipt.json ; reçu release encore attendu.

### Audit de la portée végétale pendant le lien Studio

Session 54829 repollée vivante ; Cargo atteint forge_studio puis forge_assets.
Aucun result.json terminal à cette observation, aucun assemblage frontend aval.
La sélection des casters dans tidewater_plants/scene/shadow.rs utilise le rayon
de requête et les seuils par instance du même groupe que le LOD visible, avec
plafond de capacité ; elle n'utilise pas directement la portée de 400 m des
cascades (sun_cascades.rs, splits 10/60/400, résolution 2048).

Le vegetation-scene.json vérifié du library-stage fournit : palmiers rayon de
requête 130 m et fin du LOD proche 120.01 m ; bananiers 130/120 ; monsteras et
broadleaf 115/105 ; canopy 75/65.01. Ces valeurs viennent du manifeste acquis,
pas d'un réglage arbitraire ajouté pendant cet audit. Le shader de profondeur
conserve le masque/dither source avec vegParams.camPos = caméra principale.
Les casters préparés excluent explicitement understory et sélectionnent cinq
groupes ; le passage Studio n'ajoute pas le résident grass à ces casters.
Cela identifie une portée à qualifier séparément pour l'herbe et le sous-bois,
sans prouver que la distance actuelle explique le défaut visuel signalé.
Pas de correction de portée ni de verdict de parité issus de ce seul audit.

### Projet isolé du candidat partagé préparé

PrepareRuntime.py dans studio-shared-lighting-library-20261003-v1 a terminé
en session 30613, exit 0 : 26 fichiers copiés et rehashés, source inchangée,
aucun fichier .lock copié. Nouveau runtime-fixture sur F et profil WebView neuf.
Le lancement supervisé est préparé en conservant son processus parent jusqu'à
la fermeture réelle ; il n'a pas encore été exécuté. Aucun ancien projet ni
profil modifié. Compilation frontend session 54829 toujours vivante, observée
au stade forge_engine ; aucune preuve d'un nouveau draw à ce stade.

### Chaîne solaire encore absente de la boucle Studio

Lecture directe d'authoring_imported_terrain.rs::update_live_clouds : la boucle
soumet les nuages avec live::submit et réutilise les paramètres atmosphériques
résidents. Elle n'avance pas de solar::Clock, n'appelle pas le contrôleur
d'atmosphère ni submit_solar. tidewater_atmosphere/controller.rs::advance attend
un delta réel, une frame solaire et 16 valeurs, puis attend l'encodage avant
le readback asynchrone à cadence 0.25 s. clouds/live.rs::submit_solar refuse un
readback absent et vérifie l'identité de la génération partagée. Il faudra donc
conserver le chemin initial acquis jusqu'au premier readback terminé, puis
publier une frame cohérente aux nuages/environnement et aux autres récepteurs.
tidewater_ocean_optics.rs::update_lighting accepte FrameLighting et quatre Vec4
atmosphériques sans modifier les champs caméra/vagues/immersion. Ce raccordement
n'est pas implémenté pendant la compilation figée du candidat partagé.

Contrôle CIM ciblé : superviseur Python 12104, Cargo 18500 et rustc 6256 actif
sur forge_engine avec target WASM/release et opt-level 3. Les observations du
handle 54829 restent vivantes, sans reçu terminal ni relance. Pas de draw,
de cycle solaire ni de qualification visuelle ajoutés par cette observation.

### Qualification de la chaîne réelle des shaders lancée

Check-SharedLightingChain-20261003.py lancé en session 13950. Le harnais extrait
les fonctions réelles du récepteur d'ombres, de l'atténuation sous l'eau, du
rebond au sol et des lampes locales, avec leurs snippets WGSL, puis applique
le récepteur partagé dans l'ordre de VillageMaterial::stage_shader. Le stand
garde sa chaîne effective, limitée à son adaptation d'éclairage partagé.
Les sources extraites sont pinnées ; aucun fichier canonique modifié. La même
matrice de 32 variantes utilise ShaderCache Bevy et Naga. Handle confirmé
vivant, aucun verdict terminal disponible à cette entrée. Le build frontend
54829 reste également vivant ; ce contrôle ne constitue pas un draw GPU.

### Chaîne complète des shaders validée

Session 13950 terminale 0 : compilation du harnais PASS, un test PASS, zéro
filtré/ignoré, 32 variantes de la chaîne effective Village/Stand validées par
ShaderCache et Naga. tests.stdout indique 3.34 s pour le test. Le reçu porte
full_scene_adapter_chain_tested=true, gpu_executed=false et studio_executed=false.
Post-test-source-verification.json vérifie les onze pins après l'exécution ;
sources inchangées. La déclaration partagée conserve son binding 55 et sa
disposition de 176 octets dans ces transformations. Le candidat frontend
54829 reste en attente de son résultat terminal ; aucun draw ni parité ajouté.

### Frontend partagé release lié avec succès

Session 54829 terminale 0 : build release WASM PASS, frozen_source_unchanged=true,
canonical_changed_since_freeze vide. Les 996 sources canoniques ont aussi été
comparées aux pins avant ce résultat, sans différence. WASM brut 115 697 941
octets, SHA256 6f1a725fd1c8eb320ab3ea2c1e81866959e4f24a2e4c342172147890dd59acf6.
Ce résultat est un lien réussi, sans exécution navigateur ni parité visuelle.

Assemble.py lancé en session 90349 après ce résultat, avec vérification des
assets et du WASM brut avant bindgen. Nouveau frontend séparé sur F, aucun
ancien candidat remplacé. Le worker, le staging natif et le build Windows
restent dépendants des reçus terminaux des étapes précédentes.

### Copie frontend suivie et staging natif préparé sans duplication intermédiaire

Session 90349 toujours vivante. Observation intermédiaire du frontend : 1027
fichiers, 192 405 390 octets ; JS et WASM liés encore absents. Le library-stage
vérifié contient au total 355 284 531 octets. Ne pas présenter bindgen comme
démarré sur cette seule présence du répertoire frontend.

StageNative.py, pas encore exécuté, utilise désormais des liens physiques pour
ses entrées de build immuables sur le même volume F, avec contrôles de hash
avant et après création. Le constructeur Windows conserve sa copie indépendante
dans le candidat sur E ; aucun lien vers le projet auteur. Ce changement évite
une duplication intermédiaire, sans supprimer les vérifications ni toucher au
frontend courant en assemblage. Syntaxe du helper vérifiée, comportement et
reçu de staging encore attendus. E dispose d'environ 1.99 Go à cette mesure.

### Bindgen terminé et worker lancé

Session 90349 terminale 0. frontend-bindgen.json référence 1795 fichiers et
wasm-bindgen 0.2.127. WASM lié 123 712 540 octets, SHA256
b52eee92a61471b894dab546c4b2cec1ae7a21866dd30eedc300fb88f25de55e.
Le JS et le WASM sont présents ; aucun lancement navigateur déduit de ce reçu.

BuildWorker.py lancé en session 88539 après la terminaison de l'assemblage et
contrôle CIM du cache WASM libre. Sources figées et worker release offline/locked
conservés. Handle confirmé vivant ; résultat du worker et frontend-ready.json
encore attendus. Aucun staging natif ni build Windows démarré à cette entrée.

### Worker réussi, staging repris après refus des liens physiques

Session 88539 terminale 0 : worker release PASS, sources figées inchangées.
WASM brut 418 403 octets SHA256 2dc0ea0ae22c8f8c4b5a30b07ba0d8152f8f80bef2c8712adf671bd964a310a1 ;
WASM lié 385 607 octets SHA256 084dfd02043b39c9141026d6e43038c3b3f866ef37b9df33055bb649b2e875e4.
frontend-ready.json est créé. Aucune exécution du worker dans le navigateur
n'est prouvée par ce build. La fenêtre précédente du candidat isolé est encore
retournée par sky.list_windows ; une autre fenêtre Studio indépendante est
présente et n'est pas une cible de ce parcours.

Premier StageNative.py terminal 1 : os.link échoue immédiatement sur le WASM
avec WinError 1 (« Fonction incorrecte »). Le staging native-frontend est
conservé ; l'optimisation proposée n'est donc pas acquise sur ce volume.
Helper repris avec copies indépendantes dans native-frontend-v2, sans suppression
ni écrasement de la tentative. Session 32832 lancée et encore vivante. Hashes
avant/après copie et pins du worker conservés. Reçu natif et build Windows
encore attendus ; aucun nouveau programme lancé.

### Staging validé et build Windows réellement lancé

Session 32832 terminale 0 : native-inputs.json, staging_mode
verified-independent-copy ; frontend-ready SHA256
b44ab96f49a26df83041e90e805bc637cc52e451a8a11b8ab6b440e5c5c7e946.
Le helper utilise native-frontend-v2 et le kit vérifié du frontend complet.
BuildNative.py lancé en session 20226 ; contrôles des sources et du cache
exécutés avant le constructeur. Nouveau dossier studio-shared-lighting-desktop-
20261003-v1 sur E ; build.log existe et Tauri vérifie ses packages. Handle
confirmé vivant, résultat natif encore attendu. Aucun autre Studio interrompu.

Observation sky du candidat antérieur : village, relief, végétation et ciel
visibles ; position 53.60/3.70/-77, simulation prête, exposition 0.10 EV,
saturation 1.06, contraste 1.04, rebond 1.00, lumière de travail décochée.
Capture JPEG 1922x1112 et arbre UI conservés dans les fichiers
prior-candidate-before-shared-lighting.jpg/.json du dossier helper courant.
Les compteurs nuages/FFT/ShoreSim avancent, mais ce candidat reste antérieur
au nouvel éclairage. Le temps des nuages n'est pas apparié à une autre capture ;
aucune parité ni nouveau draw du candidat partagé ne vient de cette observation.

### 2026-10-04 — Récupération du binaire compilé et ouverture de la zone

Le build Windows 20226 a terminé la compilation Tauri en 11 min 07 s,
mais la copie finale vers E a échoué avec « Espace insuffisant sur le disque ».
Le résultat initial FAIL est conservé. RecoverNative.py (66883, code 0)
a vérifié les 1800 entrées frontend et les pins des sources puis récupéré
le binaire déjà compilé sur F, sans recompilation. Le reçu séparé
native-result-recovered-v1.json indique desktop-build-only-recovered,
runtime_verified=false et visual_parity=false.

Binaire : F:/OpenRPG_Forge/.build/studio-shared-lighting-20261003-v1/
recovered-native-v1/forge-studio-candidate.exe ; SHA256
7dd59e9333c2c78ca486e3c606ccc303a556fa0e2486a8a92e473a0ed8f6538c.

Le candidat est réellement retrouvé par Computer Use, fenêtre 3739950,
avec ce chemin de processus exact. Le workspace isolé est ouvert, puis
« Ouvrir Tidewater island · zone 1 » : la zone affiche 33 entrées,
23 bâtiments et 10 couches végétales. Dans Objets > Fiche, le chargement
vérifié annonce les 23 bâtiments et les ressources ciel/nuages/végétation
chargés. La vue est encore noire pendant la préparation ; aucun rendu
des nouveaux matériaux ni parité visuelle n'est déduit de cet état.
Aucun autre Studio interrompu, aucune zone canonique sauvegardée.

### Nouveau candidat : rendu visible en édition puis en playtest

Après la préparation, Computer Use observe effectivement le terrain,
les bâtiments, la jetée, la végétation, le ciel et l'océan dans le candidat
F récupéré. L'éclairage de travail est désactivé en édition. Le passage
Jouer remonte la scène et conserve la bibliothèque en mémoire : l'écran
vide initial est une préparation transitoire, pas une demande de rechargement.
En revanche, le nouvel état de playtest réactive l'éclairage de travail ;
la case est décochée à nouveau avant la capture de comparaison.

Captures et arbres UI sauvegardés dans le dossier helper :
shared-lighting-playtest-first-20261004.jpg/.json (lumière temporaire encore active)
et shared-lighting-playtest-worklight-off-20261004.jpg/.json (case décochée).
La seconde observation relève position 53.60/3.70/-77, au sol true,
simulation prête true, entrée active false, Tick 4310, nuages 707,
FFT/optique/ShoreSim étape 825. Des observations antérieures donnent
Tick 1575, nuages 242 et étape 360 : les compteurs avancent.
Aucun mouvement horizontal observé ; aucun test physique du clavier déduit.

Cette preuve établit un rendu réel du nouveau candidat et une simulation
active. Elle ne démontre pas la publication dynamique du buffer partagé,
la cohérence du cycle solaire, les distances d'ombres ni la parité avec
la référence. Le statut complet reste non qualifié.

### 2026-10-04 — Préférence de lumière de travail conservée entre sessions

Le parcours graphique précédent démontre une case décochée en édition puis
cochée après Jouer. authoring_controls.rs::replace_session_projection appelle
reload_projection, donc un rechargement complet de la fenêtre reconstruit
l'état thread-local avec sa valeur initiale true.

Correction bornée dans authoring_lighting.rs : préférence versionnée
forge.studio.work-light.v1 dans sessionStorage, lue une seule fois et gardée
en cache local ; les changements de case actualisent le cache et la session.
Les valeurs absentes ou invalides conservent le défaut true. Un échec d'écriture
est signalé dans la console. Aucune préférence ajoutée au document auteur,
aucun changement de matériau, d'ombre ou de réglage d'exposition.

Vérification work-light-wasm-check-20261004-v1, session 55931 : cargo check
--offline --locked -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown retourne 0 en 28.49 s, sources inchangées.
Rustfmt --check et diff --check ciblés passent. Avertissement de compatibilité
future de proc-macro-error2 2.0.1 dans les dépendances, pas de warning du fichier
modifié. Le correctif n'est pas lié ni déployé dans le candidat ouvert ;
le parcours UI édition/Jouer/Retour reste à rejouer sur un nouveau build.

Audit du raccord solaire actuel : update_live_clouds utilise toujours
live::submit (éclairage acquis), sans controller::advance ni submit_solar.
La chaîne existante peut accepter une frame solaire et un readback asynchrone
validé, mais ce raccord Studio reste à implémenter. Un rendu visible des
matériaux partagés ne prouve pas la cohérence solaire dynamique.

### 2026-10-04 — Raccord Studio atmosphère/soleil/nuages/eau implémenté

Nouveau module authoring_imported_terrain/solar.rs et raccord dans le caller
update_live_clouds. Une horloge solaire appartient à la génération atmosphérique
identifiée par Arc ; son défaut est l'heure source 16.2, vitesse 0. Le soleil
acquis doit correspondre exactement au défaut initial : toute autre pose est
refusée explicitement, jamais déplacée silencieusement. Aucun contrôle UI pour
changer l'heure ou la vitesse n'est ajouté par cette tranche.

Avant la soumission nuages, le caller avance le contrôleur atmosphérique avec
la hauteur caméra quantifiée comme la source et la vraie direction du soleil.
Les matériaux du ciel reçoivent ces paramètres, la lune et la nuit cohérentes.
Avant le premier événement de lecture GPU terminé, le seed acquis est conservé.
Après completion valide, submit_solar réutilise les textures temporelles et le
dernier retour atmosphérique, puis le paquet soumis fournit le frame environnement.
FrameLighting alimente aussi l'optique d'eau sans changer caméra ni vagues.
Les données directes CPU sont mises à jour pour les consommateurs d'ombres et de
flare ; le SH GPU n'est pas remplacé par une copie CPU. Les matériaux Village
partagés ne sont modifiés que lorsque direction/couleur changent.

Les diagnostics de playtest distinguent premier retour GPU en attente et soleil
avec identifiant du dernier reader réellement terminé dans la génération active.
Un compteur d'encodage seul n'est jamais présenté comme une lecture terminée.

Cargo check complet WASM : V1 15.79 s, V2 16.51 s, V3 final 15.05 s, retours 0
et sources inchangées ; reçus solar-studio-bridge-check-20261004-v1/v2/v3.
Rustfmt --check et diff --check ciblés passent. Avertissement de compatibilité
future proc-macro-error2 préexistant. Clippy V1 retourne 101 avec 17 erreurs
forge_assets hors fichiers modifiés, avant de contrôler le frontend. V2 ciblé
--no-deps retourne 101, liaison du build script Studio : LNK1318 PDB LIMIT (12).
Ces deux échecs sont conservés dans solar-studio-bridge-clippy-20261004-v1/v2 ;
aucun PASS Clippy ni contrôle des nouveaux diagnostics Clippy n'est revendiqué.

Ce code n'est pas lié dans un nouveau frontend/exécutable ni exécuté dans la
fenêtre candidate actuellement ouverte. Il reste à tester la cadence/readback,
le remplacement de génération et la continuité des nuages/eau sur le GPU réel,
puis comparer les ombres et la lumière à la référence à réglages appariés.
Aucun cycle jour/nuit complet, parité visuelle ou clôture de gate déduit.

### 2026-10-04 — Nouveau candidat solaire préparé sur F

E ne dispose plus que de 42 295 296 octets libres au contrôle initial.
Build-SolarStudioFrontend-20261004.py est lancé en session 25265 et retrouvé
comme processus Python 36452. La copie des sources est réellement en cours :
754 fichiers présents lors du contrôle CIM associé. Le build-request Cargo
n'est pas encore créé à cette observation : aucun lien release déduit.
Dossier : F:/OpenRPG_Forge/.build/studio-solar-bridge-20261004-v1.

Le helper fige les sources courantes avec pins et includes, ajoute les scripts
nécessaires à un build natif depuis cette copie, puis préservera les produits
WASM bruts précédents avant réutilisation du cache release libre. L'ancien
frontend complet et les deux candidats Studio ouverts ne sont pas remplacés.
La compilation externe source_fragment utilise un cache distinct F:/OpenRPG_ImportWork ;
aucune interruption ni nettoyage de ce travail.

Assemble.py, BuildWorker.py, StageNative.py et BuildNativeFrozen.py préparés
dans studio-solar-bridge-library-20261004-v1 ; syntaxe Python PASS.
La bibliothèque précédente est référencée par un manifeste séparé, avec hash
du manifeste original ; Assemble reverifiera les assets avant la copie finale.

BuildNativeFrozen.py n'est pas encore exécuté. Il appelle les validations du
constructeur canonique avec ValidateOnly, puis compile le shell depuis les
sources figées sur F et un frontendDist relatif sur le même volume. Il conserve
le binaire brut antérieur, vérifie les pins avant/après et génère un reçu
BUILD_ONLY séparé. Cette voie évite la copie sur E presque plein, sans changer
le constructeur canonique ni prétendre qu'un candidat est déjà compilé.
Aucune exécution GPU, parité visuelle ou qualification ajoutée à cette entrée.

La préparation est ensuite terminée : 1298 fichiers figés, source-inputs.json et cache-reuse-receipt.json présents ; build-request.json et les journaux Cargo sont créés. La session 25265 reste active, aucun résultat terminal encore disponible.


Précision du reçu effectif : le helper annonce frozen_files=1334. Le décompte 1298 ci-dessus était une observation intermédiaire de la copie, et non le nombre final du snapshot. Le frontend release est lancé sur cette copie de 1334 fichiers.


### 2026-10-04 — Vérification du soleil initial sur le snapshot candidat

TestSolar.py ajouté dans studio-solar-bridge-library-20261004-v1. Le helper vérifie les hashes de solar.rs et solar/oracle.json dans source-inputs.json, puis ceux de buildings.json et du params.bin désigné par environment_sha256 dans library-stage.json. Il compile les deux tests solaires existants et un troisième test comparant les trois composantes f32 du soleil par leurs bits aux paramètres réellement embarqués. Les dépendances natives réutilisées sont hashées dans le reçu ; aucun résultat GPU n'est déduit de ce contrôle CPU.

Session de test 10716 lancée, Python 36904 et rustc 18132 confirmés actifs par CIM ; aucun verdict terminal à cette observation. La session frontend 25265 reste active : Cargo 18380 et rustc 24432, compilation forge_engine après forge_assets. Les refus d'accès CIM en sandbox ont été contournés par une lecture escaladée autorisée, sans interruption ni relance des builds. Aucun assemblage frontend ou nouveau binaire natif encore exécuté.

Préparation runtime du candidat solaire terminée (session 22936, retour 0) : 26 fichiers copiés et hashés dans son runtime-fixture sur F, projet parent inchangé, aucun fichier lock copié, profil WebView neuf. PrepareRuntime.py et LaunchNative.py préparés et analysés par ast.parse ; le lancement vérifiera aussi le reçu natif, le hash du binaire, le manifeste source et les pins du frontend. Aucun lancement du candidat solaire encore effectué.

Test solaire V1 (10716) terminé avec retour 1 : le générateur du harness avait produit un suffixe u32 après le tableau au lieu de chaque entier ; Rust refuse cette syntaxe avant les tests. Il ne s'agit pas d'un résultat négatif sur la direction solaire. Journal conservé dans solar-oracle-test/compile.stderr. Générateur corrigé, V2 lancée en session 62200 dans solar-oracle-test-v2, sans modifier les sources figées ni écraser la V1. Aucun PASS solaire revendiqué à ce stade.

Test solaire V2 terminé avec retour 1 avant exécution : collision StableCrateId entre deux variantes itoa apportées par les bibliothèques du cache. La syntaxe du harness est désormais correcte ; aucune direction solaire comparée lors de cette tentative. Le Run.py existant de frame-lighting-tests-20261003-v1 désigne la paire bevy-8c930d018d27237c/serde_json-3c97f842c5d56ab7. V3 relancée avec cette paire en session 53485, journaux V1/V2 conservés. Le module moteur et les sources figées restent inchangés. Au contrôle CIM, rustc 35960 compile le test, rustc 24432 compile forge_engine pour le frontend.

### 2026-10-04 — Enchaînement de construction du candidat solaire

ContinueCandidate.py lancé en session 28145. Il attend le superviseur Python original 36452, qui écrit le reçu terminal du frontend ; il ne relance aucune compilation frontend. Après reçu 0 et sources figées inchangées, il exécutera séquentiellement Assemble.py, BuildWorker.py, StageNative.py et BuildNativeFrozen.py, chacun avec son journal et son hash vérifié. Le premier échec termine la chaîne. Les helpers sont épinglés dans assembly-pipeline/state.json. État initial observé : waiting-existing-frontend, aucune étape d'assemblage exécutée. La chaîne n'effectue aucun lancement runtime ni publication et ne prétend aucune parité visuelle.

Test solaire V3 terminé (session 53485, retour 0) : compilation 0 et trois tests exécutés PASS. source_solar_matches_48_executed_upstream_samples vérifie les 48 cas de l'oracle source épinglé ; source_solar_pause_wrap_and_refusals_preserve_last_valid_frame vérifie pause, wrap et refus sans perdre le dernier état ; initial_sun_matches_packaged_atmosphere_bits vérifie l'égalité exacte du soleil f32 initial avec params.bin réellement désigné par la bibliothèque. Bits attendus [3210926367, 1055454139, 1035241672], params SHA bd09ce785bff5d33579201b2f643d7989f5e6a6443b501628def63ec779f0ed3. Reçu solar-oracle-test-v3/result.json, sortie test.stdout. Les V1/V2 restent conservées. Ce PASS CPU prouve la compatibilité de l'état initial et les cas de l'oracle ; aucun raccord atmosphère/readback GPU, rendu Studio ou cycle jour/nuit complet n'est qualifié.

### 2026-10-04 — Arrêt LLVM et reprise du frontend figé

Les handles 25265 et 28145 sont devenus absents. CIM confirme ensuite l'absence des superviseurs/builds originaux ; build.stderr contient rustc-LLVM ERROR: out of memory et Allocation failed. Aucun result.json terminal n'a été produit et l'ancienne chaîne reste figée sur waiting-existing-frontend ; cet état de fichier ne signifie plus qu'elle est active. Aucun PASS ni code terminal précis déduit de cette interruption.

RecoverFrontend.py vérifie tous les pins du snapshot existant, la présence du diagnostic OOM, l'absence des anciens processus et la disponibilité du cache avant reprise. Session 70261, superviseur Python 37196 et Cargo 10372 confirmés par CIM. Les seuls overrides sont profile.release.package.forge_engine.codegen-units=16 et profile.release.package.forge_studio.codegen-units=16 : optimisation 3 et LTO thin conservés. Sources, shaders, données, features et cible inchangés. Les nouveaux journaux vont dans frontend-recovery-v1 ; les anciens restent intacts. Un résultat réussi sera copié vers result.json avec provenance de cette reprise, sans requalifier la performance.

ContinueRecoveredCandidate.py lancé en session 87459 attend le superviseur 37196 ; état séparé assembly-pipeline-recovery-v1, mêmes quatre helpers épinglés. L'ancienne chaîne n'est pas écrasée. Le test solaire V3 PASS reste une preuve CPU distincte de ce build en cours.

Le contrôle UI parallèle du candidat shared-lighting a atteint un brouillon X=78.5 pour building:8, à partir de X=77.5 dans une copie isolée, avec snapshot shared-edit-before.json. L'action de prévisualisation et son observation ont été interrompues ; application, annulation, sauvegarde et réouverture ne sont pas prouvées. Le contexte Computer Use a été perdu après l'interruption ; aucun ancien index ou handle de fenêtre n'est réutilisé pour saisir.

Après interruption, list_windows ne retourne plus aucune fenêtre forge-studio. La comparaison sur disque de la copie shared-lighting avec shared-edit-before.json trouve zéro fichier modifié et zéro fichier ajouté : le brouillon X=78.5 n'a pas produit de modification persistée dans cet essai. Reçu partagé shared-edit-after-interruption.json. Cette absence de modification n'est pas une preuve de sauvegarde ou de réouverture ; le parcours UI doit être rejoué.

La reprise franchit forge_engine : le journal Cargo contient son compiler-artifact, fresh=false, pour les features physics-rapier/qualification/web. Le processus enfant courant devient rustc 9664, crate forge_studio_frontend, codegen-units=16 confirmé via CIM. Ce résultat intermédiaire prouve la production du module Engine dans cette reprise ; le frontend final, son lien, le worker et le natif restent en construction ou en attente. Aucun PASS de l'application complète déduit.

### 2026-10-04 — Frontend solaire lié avec succès

La reprise frontend-recovery-v1 termine en session 70261 avec retour 0, profil release optimisé, durée Cargo 17 min 08 s. Tous les fichiers figés restent identiques. Le WASM brut fait 124585092 octets, SHA 962176b412df6784362c889e55f93858b64cad41efe6352359ee123809f6bce5 ; chemin enregistré dans result.json avec le reçu de reprise. L'avertissement de compatibilité future proc-macro-error2 2.0.1 demeure, sans nouveau refus de compilation. La performance reste non qualifiée avec les unités de compilation modifiées.

La chaîne 87459 passe réellement de waiting-existing-frontend à running, current_step Assemble.py ; Python enfant 24244 confirmé. Le frontend complet, le worker et le natif ne sont pas encore prêts au moment de cette observation. Aucune exécution WebView/GPU ni parité déduite du lien réussi.

### 2026-10-04 — Assemblage frontend et worker terminés

Assemble.py retourne 0 dans la chaîne 87459 ; bibliothèque complète rehashée puis copiée, bindings générés avec wasm-bindgen 0.2.127. BuildWorker.py retourne également 0 : Cargo release du worker termine en 1 min 42 s, ses sources figées sont inchangées, ses artefacts et sa configuration release sont inscrits dans le manifeste worker. frontend-ready.json marque linked_frontend=true et navigation_worker_built=true, browser_executed=false. La chaîne passe à StageNative.py, Python enfant 27364 confirmé : préparation native encore en cours. Aucun lancement WebView/GPU ni nouveau rendu qualifié.

### 2026-10-04 — Préparation et validation native réussies

StageNative.py retourne 0 dans la chaîne 87459 : 1791 fichiers hors kit, 467857526 octets copiés indépendamment et vérifiés, kit conservé comme entrée séparée. Les pins worker sont confrontés aux sources canoniques actuelles avant la copie. La chaîne passe à BuildNativeFrozen.py, Python 6212 confirmé ; native-input-validation.json est produit par le constructeur canonique avec ValidateOnly. La requête native est écrite et cargo tauri démarre depuis les sources figées sur F, avec frontendDist relatif sur ce volume. La compilation native et son résultat restent en cours ; aucune fenêtre du nouveau candidat encore lancée.

### 2026-10-04 — Build natif réussi et lancement isolé

La chaîne 87459 termine avec retour 0 pour ses quatre étapes. BuildNativeFrozen.py produit native-result.json : exit_code=0, frozen_source_unchanged=true, frontend_inputs_unchanged=true. Durée Cargo native 8 min 50 s. Le candidat est desktop/forge-studio-candidate.exe, SHA a60647d68ea838b7ffd0738ce277e5409ede3a6a174d0df4e5b9c43a0de7900d. Le reçu conserve BUILD_ONLY, runtime_verified=false et visual_parity=false.

LaunchNative.py vérifie binaire, preuve native, manifeste source, pins frontend et les 26 fichiers de la copie fraîche avant lancement. Session superviseur 16970, enfant candidat PID 24064, encore actif après trois secondes ; profil WebView et FORGE_AUTHORING_ROOT appartiennent au runtime-fixture du nouveau candidat. La fenêtre et le dessin GPU restent à observer. Aucun PASS GPU ni parité ne découle du lancement de processus.

### 2026-10-04 — Premier contrôle UI du candidat solaire : chargement refusé

La fenêtre du candidat a été retrouvée par son chemin exact et observée dans
Computer Use. Le workspace isolé s'ouvre ; Tidewater island est ouvert par le
module, avec 33 entrées de scène (23 bâtiments et 10 couches végétales).
Cela prouve la lecture de la session native, pas le dessin de la carte.

Le démarrage du playtest puis le rechargement explicite de la projection
restaurent le mode Playtest depuis le service natif. Le chargement automatique
échoue sur heights.f32.bin (16777216 octets) : délai de 20 secondes dépassé.
Une reprise explicite par Objets / Fiche / Bibliothèque de bâtiments importés
progresse jusqu'à terrain-normal.bin (22369620 octets), puis échoue avec le même
délai. Les deux fichiers existent dans le frontend natif préparé. Aucun dessin
GPU, retour solaire terminé ou déplacement n'est qualifié dans cet essai.
Le retour à l'édition refuse faute de bilan runtime initialisé ; ce refus est
observé, sans contourner le service ni modifier la copie de session sur disque.

Le réglage d'éclairage de travail, décoché avant le rechargement complet,
reste décoché après celui-ci : persistance UI de préférence constatée. Son effet
sur le rendu et le parcours complet édition / jeu / retour restent non vérifiés.
Preuves UI : runtime-load-timeout-20261004.json et
worklight-session-reload-off-20261004.json dans
.tidewater-integration/studio-solar-bridge-library-20261004-v1.

Inspection du transport : governed_kit/transport.rs impose une échéance totale
fixe de 20000 ms, incluant en-têtes et lecture de tout le corps. La bibliothèque
Tauri embarquée utilise BrotliDecompress avant restitution de l'asset ; le profil
dev optimise déjà les dépendances à 3. Une lenteur de décompression est donc
une hypothèse à mesurer, pas une cause démontrée. Prochaine action : mesurer le
transport natif des gros payloads et corriger la stratégie d'échéance si une
lecture qui progresse est annulée. Ne pas remplacer ces échecs par un PASS de
compilation ou par une validation uniquement HTTP.

### 2026-10-04 — Mesures du transport et première acquisition complète réussie

Deux mesures isolées conservent le candidat a60647d68ea838b7ffd0738ce277e5409ede3a6a174d0df4e5b9c43a0de7900d et ne changent pas le transport produit. Le probe Brotli décode les payloads embarqués en 116,791 et 125,599 ms ; les octets décompressés sont exactement égaux aux fichiers originaux. Ce contrôle du décompresseur ne prouve pas à lui seul le transport natif.

Dans une nouvelle instance du même exécutable, avec racine auteur vide et profil WebView neuf, le transport réel charge heights.f32.bin en 247,4 ms et terrain-normal.bin en 1271,1 ms, HTTP 200, tailles et SHA-256 conformes. Reçus sur F : studio-solar-bridge-20261004-v1/embedded-asset-probe-v1/result.json et native-transport-probe-v1/result.json. L'instance diagnostic est fermée, les sessions préexistantes sont conservées.

MeasureFullWorkflow.ps1 ouvre ensuite le workspace et la zone par les helpers UI/CDP existants, puis vérifie et charge la bibliothèque. Chaque essai possède une copie indépendante des 26 fichiers épinglés du fixture parent, sans lock ni modification du parent. V1 échoue dans le helper avant le clic de chargement : sélecteur accentué mal encodé ; aucun échec produit déduit. V2 corrige le helper en UTF-8 et aboutit : 23 bâtiments et ressources terrain, ciel, nuages et végétation chargés en mémoire, environ 16,27 s depuis installation de la trace. Les deux gros payloads terminent leur lecture en 214,8 et 231,8 ms. La trace contient 1917 requêtes, dont les IPC ; les aborts après lecture complète correspondent au nettoyage normal de Deadline, pas à une expiration.

Reçu complet : native-full-workflow-probe-v2/result.json. Les wrappers JS de mesure délèguent à fetch/getReader/read originaux et conservent résultats et erreurs ; leur instrumentation limite la portée temporelle de cette mesure. Cet essai ne reproduit pas les expirations UI originales ; leur cause reste inconnue. Aucun délai produit n'est augmenté. Le processus est fermé alors que l'environnement est encore en calcul : acquisition de la bibliothèque prouvée, dessin GPU et retour solaire non prouvés par V2.

V3 est lancée en session 73776 avec fixture et profil indépendants pour observer au-delà de l'acquisition, jusqu'à 75 secondes après installation de la trace. Résultat terminal et diagnostic GPU encore attendus à cette entrée. Aucun verdict de parité visuelle, performance, édition persistante ou distribution ajouté.

V3 termine ensuite avec retour 0, observation finale à 80,46 s. Environnement calculé et collisions du terrain synchronisées ; le diagnostic du canvas contient un océan au pas 782, une réfraction encodée 663 fois et des retours GPU des requêtes de surface/cartes lumineuses sous-marines. Ces compteurs ne prouvent pas une présentation visuelle. Ombres : generations=664, pending=true, encoded_frames=0, cascade_layers=[0,0,0], wind_time_s=2.0. Le rendu des ombres n'est pas qualifié ; ce résultat révèle une génération perpétuellement remplacée dans cet essai.

Inspection associée : authoring_imported_shadows::Source inclut toutes les matrices des casters opaques dans caster_poses. Les poses du pier sont animées à chaque Update par imported_scene/motion.rs ; si une de ces poses change, le PostUpdate abandonne le target pending et restage. Le chemin live::Frame anime actuellement terrain/plantes/caméra, mais pas les matrices des draws opaques. Une correction doit transmettre les poses opaques au frame resident, avec contrôle de cardinalité et d'ordre, plutôt que désactiver leurs mouvements ou supprimer leurs ombres. Cette inspection fournit une cause probable, pas une correction exécutée ni une qualification.

V4 et V5 atteignent l'acquisition complète puis déclenchent Jouer. Le rechargement de projection efface la trace JS et le helper échoue en lisant started sur une trace absente ; journaux failure.json conservés, instances fermées dans finally. Aucun échec produit ou retour solaire déduit de cet échec du helper. V5 avait ajouté une réinstallation au nouveau document sans activer Page ; cette mesure n'a pas résisté au remplacement de contexte. V6 active Page, réinstalle la trace quand elle manque et tolère la courte fenêtre entre navigation et observation ; si cette réinstallation tardive est utilisée, les premières requêtes après navigation restent hors observation et cette limite sera inscrite dans trace-reinstall.json. Fixture indépendant vérifié, session 30935 lancée ; résultat terminal encore attendu.

### 2026-10-04 — Passage en jeu et premier retour solaire GPU du nouveau candidat

V6 termine avec retour 0, session 30935. Le rechargement natif passe réellement au Playtest. L'observation finale, 54,08 s après installation de la trace dans le document rechargé, montre simulation prête=true, Tick 1240, joueur au sol en (53.60, 3.70, -77.00), nuages dynamiques 227 images, soleil 16.20 h avec retour atmosphère GPU terminé 20998v0. L'identité du reader provient de last_completed_reader, qui exige un résultat réussi de la génération atmosphérique courante. FFT océan prête étape 345, géométrie 98 nœuds, optique et ShoreSim au même pas. Aucun déplacement horizontal exercé ; cadence de readback à 0,25 s non mesurée par ce seul dernier reader.

ready=false dans ce reçu est le critère du helper portant sur l'output de la bibliothèque, absent du panneau Terrain en Playtest ; ce booléen ne remplace pas les diagnostics de simulation. Le helper termine sur sa durée d'observation, sans présenter cette valeur comme un verdict produit. viewport.png capturé par CDP puis inspecté : le terrain, les bâtiments, la végétation, le chemin et le ciel sont réellement visibles. Cette vue n'est pas une comparaison appariée à la référence et n'établit aucune parité visuelle ni performance.

Les ombres restent défaillantes dans ce même parcours : generations=227, pending=true, encoded_frames=0, cascade_layers=[0,0,0], wind_time_s=2.0. Prochaine correction : transmettre les poses des casters opaques animés à la génération résidente sans restage permanent, avec refus des changements de cardinalité/identité. Garder les reconstructions complètes pour les vraies modifications de contenu et ne pas figer les animations pour masquer le défaut. Les sources du candidat figé et les sessions utilisateur restent intactes ; l'instance diagnostic est fermée par son propre finally. Les éditions persistantes, le cycle jour/nuit complet, la parité et le paquet distribuable restent ouverts.

### 2026-10-04 — Correction du flux des poses d'ombres opaques

Correction canonique limitée à cinq fichiers : authoring_imported_shadows.rs, tidewater_object_shadow.rs et ses modules live.rs, animation.rs, graph.rs. Source conserve les identités ordonnées des casters, mais leurs matrices ne déclenchent plus une nouvelle génération. Chaque frame animée collecte leurs poses actuelles, refuse une identité ou un ordre différent du resident, puis transmet les matrices avec terrain/plantes/caméra. Une modification de topologie, bibliothèque, terrain, population ou soleil conserve le chemin de restage. Les animations visibles ne sont ni gelées ni supprimées.

live::Frame transporte une liste optionnelle de matrices opaques. La soumission valide leur finitude et leur cardinalité avant mutation de l'état ; le batch GPU valide toutes ses dispositions avant écriture, puis met à jour matrice de projection et pose pour les seules cascades dues. Les autres couches restent inchangées jusqu'à leur tour. La géométrie demeure résidente, l'ABI de l'uniforme shader reste inchangée. Les anciens callers sans poses dynamiques conservent leur comportement.

Le test d'extraction existant est étendu : la pose opaque de la frame précédente extraite reste intacte après avance de la frame main suivante. Un test refuse les cardinalités divergentes et matrices NaN/Infinity. rustfmt et git diff --check PASS sur les sources modifiées ; ces contrôles ne qualifient pas le GPU.

Validations sur F:/OpenRPG_Forge/.build/shadow-opaque-poses-20261004-v1 : CheckWasm.py lance le check complet forge_studio web dans un cache neuf, session 71634 encore active lors du contrôle des processus et de la progression Bevy. TestShadow.py copie et épingle les vrais modules d'ombres dans un harness natif, en réutilisant les modules voisins non modifiés et dépendances d'un Engine natif antérieur. V1 échoue à résoudre les sous-modules ; V2 corrige le point d'entrée mais manque CARGO_MANIFEST_DIR pour les macros Bevy ; V3 ajoute ce contexte et échoue sur trois dépendances Forge non exposées au harness. Les trois échecs terminent avec code 1, sources possédées inchangées, aucun test exécuté. V4 sélectionne forge_core/world/project par correspondance exacte des fingerprints de l'Engine épinglé, puis est lancée en session 68081. Les logs et reçus précédents restent intacts. Aucun PASS de tests V4 ou check complet n'est revendiqué tant que leurs résultats terminaux ne sont pas lus.

Le candidat natif a60647d6 et sa capture V6 précèdent cette correction. Un nouveau lien/frontend/candidat et une preuve GPU des cascades avançant sur la même génération sont encore requis, ainsi que la comparaison visuelle des ombres et la persistance de l'édition. La compilation externe source_fragment_render_probe dans F:/OpenRPG_ImportWork est laissée intacte ; aucune publication ni nouveau verdict de gate.

V4 des tests natifs termine en session 68081 avec retour 0 : compilation 0, 14 tests exécutés PASS, sources possédées inchangées. Les dépendances forge_core/world/project sont sélectionnées par égalité des fingerprints de l'Engine natif de référence, et leurs bibliothèques sont hashées dans request.json. Le harness compile les vrais modules d'ombres modifiés ; les modules voisins proviennent du cache natif épinglé. Il ne qualifie ni Studio ni GPU.

Le check complet Studio WebAssembly termine en session 71634 avec retour 0, source_unchanged=true. Il compile forge_engine puis forge_studio avec la correction canonique et couvre donc aussi le collecteur Studio des poses et identités, absent du harness natif. Reçu wasm-check/result.json ; aucun lien release ou rendu GPU déduit de cargo check.

PrepareCandidate.py lancé en session 25438 crée une copie indépendante dans studio-shadow-poses-20261004-v1 sur F, à partir des chemins du snapshot solaire et des sources Rust/WGSL/TOML supplémentaires présentes dans apps/crates. Les fichiers sont pris depuis le checkout canonique courant, hashés avant/après copie ; les différences avec le snapshot parent sont consignées. Le processus Python 4368 est confirmé actif ; la copie source existe, le manifeste final et les helpers clonés ne sont pas encore prêts à cette observation. Aucun build release lancé tant que cette préparation n'a pas terminé.

BuildFrontend.py et RunCandidate.py sont préparés dans .tidewater-integration/studio-shadow-poses-library-20261004-v1. Ils exigent les deux reçus PASS et l'égalité du snapshot avec les pins du check complet avant le lien, vérifient que le cache release réutilisé est libre, puis enchaînent frontend/bindgen/worker/préparation native/build natif avec arrêt au premier échec. Les anciens candidats, manifests et journaux ne sont pas remplacés. PrepareRuntimeProbe.py est prêt à créer une copie fraîche du fixture et à mesurer les trois couches d'ombres sur cinq secondes, en distinguant compteurs de passes encodées et preuve de présentation visuelle. Il n'est pas exécuté à ce stade.

La préparation termine ensuite avec retour 0, session 25438 : 1334 fichiers figés, source inchangée pendant la copie, exactement les cinq fichiers d'ombres comme différences avec le snapshot solaire. source-inputs.json SHA e756b63a555430e60ec7b046ec04cf9d54d5a7511ae8ba0b27271427953d339a. La bibliothèque précédente est référencée avec hash de son manifeste ; l'assemblage vérifiera ses fichiers avant copie.

ContinueWhenPrepared.py, prévu pour attendre le PID de gel identifié, refuse son attente sur un retour PowerShell non nul sans stdout/stderr. À la vérification suivante, le gel a réellement terminé avec son reçu 0 ; le processus 4368 est absent. Le gel n'est pas relancé et aucune compilation n'avait démarré dans cette continuation. RunCandidate.py est lancé directement sur le snapshot terminé, session 91143, avec pipeline fraîche et helpers épinglés. Cette reprise ne remplace pas l'échec d'attente par un PASS.

La chaîne franchit les prérequis CPU/WASM, la comparaison de tous les pins du check complet avec le snapshot et la vérification du cache release libre. Avant réutilisation, les deux anciens WASM bruts sont copiés et vérifiés dans prior-cache-products : frontend SHA 962176b412df6784362c889e55f93858b64cad41efe6352359ee123809f6bce5, 124585092 octets ; worker SHA 2dc0ea0ae22c8f8c4b5a30b07ba0d8152f8f80bef2c8712adf671bd964a310a1, 418403 octets. Le candidat natif précédent et son frontend restent intacts.

La continuation suivante confirme par Get-Process les superviseurs 31580/34916 et Cargo 27104 encore présents ; le journal avance jusqu'à forge_world puis forge_studio. La compilation n'est pas relancée. PrepareRuntimeProbe.py termine avec retour 0 : copie indépendante de 26 fichiers vérifiés, parent inchangé, nouveau profil WebView et aucun jeu lancé. Le reçu est native-full-workflow-probe-v1/fixture-preparation.json dans le nouveau candidat. MeasureRuntime.ps1 généré passe le parse PowerShell avec zéro erreur. Ses compteurs utilisent les mêmes noms que le diagnostic canonique data-tidewater-live-shadows ; la mesure demeure non exécutée tant que le lien natif n'a pas abouti.

La revue du harness canonique Test-CommonPlaceableStudio.ps1 et du rapport COMMON_PLACEABLE_EDITING_VALIDATION_20261002.md confirme que les six anciens cas d'édition ont une preuve propre, sur un autre candidat. Le nouveau candidat doit être contrôlé séparément. Le harness exige une racine E dédiée avec candidat, frontend et fixture confinés ; il ne peut pas être lancé tel quel sur F. Aucun garde de confinement n'est retiré, aucune fixture d'édition ni source auteur existante n'est modifiée dans cette continuation.

Le contrôle suivant confirme rustc 13144, enfant de Cargo 27104, et son linker 3280 pour le build-script Studio. Aucun redémarrage ne découle du silence temporaire du journal. EvaluateRuntime.py est ajouté à côté du probe, hors sources produit et hors helpers épinglés du pipeline. Il refuse une génération pending, l'absence de casters opaques, des passes ou une cascade immobiles et un vent figé. Sur les deux observations, il exige la même génération et l'avance des trois couches ; il vérifie aussi le clic Jouer et le mode réellement observé via le statut public, un tick positif et le contrôle de comparaison activable. MeasureRuntime.ps1 collecte explicitement ces deux dernières observations UI, avec parse PowerShell zéro erreur après modification.

Le contrôle négatif utilise le véritable résultat V6 du précédent candidat solaire, sans fabriquer de diagnostic. Il termine avec code Python 1 et statut FAIL attendu, sur cinq critères : génération non publiée, casters absents, aucune passe encodée, trois couches immobiles, vent figé. Reçu baseline-starvation-evaluation.json à côté du helper. Ce rejet prouve que l'évaluateur ne promeut pas ce défaut connu ; il ne prouve pas encore la correction. Le verdict reste limité aux passes effectivement encodées et à la scène observée, sans fence GPU ni parité visuelle.

PrepareCommonEditing.py termine avec retour 0 et prépare une racine E dédiée .placeables-smoke-shadow-poses-20261004-v1, conforme au confinement du harness existant. Les 25 fichiers hors locks du fixture-source antérieur sont copiés et hashés, source inchangée avant/après. La référence workspace pointe vers l'objet immuable 50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720, dont le hash concorde aussi avec l'archive de zone. Les points de jonction et liens sont refusés. Les deux locks restent dans le parent et ne sont pas copiés ; aucun parent n'est édité.

fixture-preparation.json épingle la recette UI précédente et le harness canonique ; recipe.json conserve les noms affichés des quatre cas source et du placement manuel. La plante exige une nouvelle inspection du canvas : aucune coordonnée du candidat ancien n'est traitée comme une preuve actuelle. Aucun candidat n'est encore copié sur E, aucun essai d'édition n'est lancé et aucun PASS nouveau n'est revendiqué. La session de build 91143 répond toujours ; Cargo 27104 possède désormais rustc 24176, observé lors de la compilation Engine.

Le probe runtime est enrichi après sa capture non figée : il active le contrôle public de comparaison seulement après la mesure des cinq secondes d'animation, puis attend le retour GPU existant du buffer récepteur. Il exige une génération publiée, la quiescence de l'océan et une caméra GPU concordante à 1 mm avec la pose publique. L'évaluateur vérifie en outre trois cascades de 2048, control/enabled actifs et trois matrices finies ; son reçu distingue gpu_receiver_readback_verified de gpu_completion_verified, qui reste false. Aucun retour de texture de profondeur ni parité visuelle n'est déduit de ce buffer. viewport.png reste la capture animée initiale ; viewport-held.png est séparée. Parse PowerShell zéro erreur et parse AST Python PASS. Aucun probe nouveau n'a encore tourné.

Inspection des distances : tidewater_plants/scene/shadow.rs reprend le niveau near et ses trois valeurs range depuis le paquet de plantes, avec la même sélection spatiale que la végétation visible ; understory est exclu des groupes casters. plant_shader.rs conserve le masque acquis avec params.lod_range et la caméra principale pour le LOD, tout en projetant la profondeur depuis la caméra d'ombre. Cette lecture ne remplace pas la mesure des portées ni la comparaison visible. Le processus Engine 24176 avance de 10,72 à 34,58 secondes CPU pendant la continuation ; aucune compilation n'est redémarrée.

Le fichier vegetation-scene.json du stage réellement référencé par le nouveau candidat est vérifié contre library-stage.json : taille concordante et SHA-256 3a5bb005384fe4de89c47de9ef977ddcfa27031090fab56adefc21cf46612308. Les paramètres near transmis aux casters sont ceux-ci ; la sélection spatiale est bornée séparément par query_radius et les rayons par instance.

| Groupe | Rayon de requête (m) | Range near du paquet | Capacité near |
| --- | ---: | --- | ---: |
| palms | 130 | [0, 120, 120.01] | 1100 |
| bananas | 130 | [0, 100, 120] | 1100 |
| monsteras | 115 | [0, 85, 105] | 1693 |
| broadleaf | 115 | [0, 85, 105] | 1100 |
| canopy | 75 | [0, 65, 65.01] | 6000 |

understory possède [0, 120, 140] dans le paquet mais est explicitement exclu par le constructeur des ombres ; ses distances ne doivent pas être présentées comme une portée de caster. Les niveaux far de palms/canopy ne sont pas utilisés dans cette passe. Aucun seuil produit ni paquet n'est changé pendant le build ; l'adéquation visuelle de ces portées reste à observer avec les ombres publiées. Le processus Engine 24176 est à 39,19 secondes CPU au début de cette vérification.

La compilation Engine termine ensuite sa propre étape : build.stdout contient compiler-artifact pour forge_engine, fresh=false, avec libforge_engine-5c7969df188c9b62.rlib et .rmeta dans le cache wasm release. Cargo 27104 poursuit avec rustc 33256, identifié comme forge_studio_frontend. La session 91143 reste active. Cette progression ne constitue pas encore un résultat terminal du frontend, du bindgen, du worker ni de l'exécutable natif ; aucun test GPU du nouveau candidat n'est exécuté à cette entrée.

Le frontend termine ensuite sa compilation release en 50 min 02 s, après le lien wasm par rust-lld 4588. result.json donne exit_code=0, frozen_source_unchanged=true et linked_application=true. Le WASM brut fait 124675997 octets, SHA-256 ef65a5c211143cd1e28f48c36d4a80fae637bcd1d40d349290c266e56fd8fe53. Le journal conserve l'avis de compatibilité future de la dépendance proc-macro-error2 ; aucun verdict Clippy supplémentaire n'est déduit du build. La pipeline passe réellement à Assemble.py le 4 octobre à 03:01:18 UTC. Aucun navigateur ni GPU nouveau lancé, worker et build natif encore attendus à ce checkpoint.

Assemble.py termine ensuite la vérification des 1787 fichiers de bibliothèque, leur copie indépendante et wasm-bindgen 0.2.127. frontend-bindgen.json est produit ; le journal termine par « Bindings generated; current navigation worker still required ». La pipeline passe à BuildWorker.py le 4 octobre à 03:11:48 UTC. Le WASM présent avant le reçu n'avait pas été considéré comme un frontend complet. La navigation et le build natif restent des étapes séparées ; aucun résultat GPU ni parité visuelle nouveau n'est revendiqué.

Le worker termine avec retour 0 en 2 min 11 s, sources figées inchangées. Son WASM brut conserve SHA-256 2dc0ea0ae22c8f8c4b5a30b07ba0d8152f8f80bef2c8712adf671bd964a310a1 ; le bindgen navigation est 084dfd02043b39c9141026d6e43038c3b3f866ef37b9df33055bb649b2e875e4. frontend-ready.json décrit 1800 fichiers. StageNative.py démarre réellement à 03:14:02 UTC, processus 25180 observé. Ces résultats restent build-only.

StageCommonFrontend.py termine avec retour 0 : les 1800 fichiers sont copiés indépendamment sous .placeables-smoke-shadow-poses-20261004-v1/candidate/web, hashés et source revérifiée inchangée. frontend-stage.json épingle le WASM Studio bda4e18ac2d6975b6c6575ead06af8ecdb81d1e4faa89b24febbc1c19368191c, 140235008 octets. Aucun exécutable ni UI nouveau n'est lancé par cette copie.

La qualification révèle une incompatibilité du harness commun actuel avec ce frontend : son wildcard *_bg.wasm sélectionne deux fichiers, Studio et navigation. Le script canonique était propre dans Git avant la correction ; une copie byte-exact est conservée dans harness-before-worker-support.ps1, SHA 775d109459cf66a745f25b452f4e8723e767ebf529c7a8a8eedcff584166a4f7. La sélection devient explicitement forge_studio_bg.wasm ; le contrôle d'unicité, le hash attendu de Studio, les pins de tous les fichiers frontend et tous les gardes d'isolation restent en place. Sur le frontend réel : ancien filtre 2, nouveau filtre 1, SHA Studio concordant. COMMON_PLACEABLE_STUDIO_SELF_TEST_PASS et git diff --check PASS. Ce correctif de harness ne modifie aucune source produit ni le candidat figé, et ne constitue pas un PASS d'édition UI.

harness-worker-support-proof.json compare ensuite les sources avant/après avec normalisation CRLF/LF et vérifie exactement le remplacement unique du filtre ; les autres lignes sont inchangées. Le reçu conserve les deux SHA et ne revendique aucune exécution d'UI.

StageNative.py termine et prépare 1791 fichiers hors kit dans native-frontend-v2. La pipeline passe à BuildNativeFrozen.py à 03:21:20 UTC, processus 4688 observé. native-request.json est réellement créé : cargo tauri build --debug --no-bundle depuis les sources figées, configuration frontendDist relative ../../../frontend et entrée index.html?authoring=1. Le journal natif commence par le contrôle des versions Tauri. Ce candidat de qualification est distinct d'une distribution release partageable ; aucun résultat terminal natif ou nouveau rendu GPU n'est encore disponible à ce checkpoint.

Le préflight canonique natif retourne INPUTS_VALID pour 1800 fichiers. Le build est confirmé vivant : Cargo Tauri 10452, cargo-tauri 26932 puis Cargo interne 34744/9208 ; le journal avance dans forge_core, forge_studio, forge_gameplay et forge_project. MeasureRuntime.ps1 ouvre désormais le panneau public de diagnostics par son summary avant de cliquer le contrôle de comparaison, puis refuse un contrôle invisible. Parse PowerShell zéro erreur après ce changement du helper ; aucune source produit ni helper épinglé du build actif n'est modifié.

build-request.json est créé le 4 octobre à 02:09:53 UTC ; Cargo PID 27104 confirmé actif pour forge_studio web release, optimisation inchangée et seize unités de compilation pour Engine/Studio, cache studio-live-environment-20261003-v1/target. La chaîne est réellement à BuildFrontend.py, superviseurs Python 31580 et 34916 observés. Aucun résultat terminal du lien, worker ou natif encore disponible. Aucun nouveau GPU, comparaison visuelle, performance ou édition persistante qualifié.

### Résultat terminal du candidat shadow-poses et premier rendu natif

Le build natif termine avec retour 0 en 18 min 40 s. `native-result.json` confirme les sources figées et les 1800 entrées frontend inchangées. L'exécutable `studio-shadow-poses-20261004-v1/desktop/forge-studio-candidate.exe` porte SHA-256 `16937fc4afbe60e3ba12400fe23678aafa9573602e1cdbf431ece674482d82b9`. La pipeline 91143 termine réellement avec les cinq étapes à zéro ; ces résultats remplacent les observations historiques de compilation active ci-dessus. Il s'agit toujours d'un candidat debug de qualification, pas de la distribution finale L'Aube.

`native-full-workflow-probe-v1` lance cet exécutable dans sa copie isolée, charge la bibliothèque puis passe par le bouton public Jouer. La mesure `shadow-cadence.json` observe une génération unique publiée, `pending=false`, 148 parties opaques et trois cascades positives ; les passes encodées, les trois compteurs de cascades et le vent avancent entre les deux échantillons. Les contrôles de cadence et de simulation de `native-shadow-runtime-evaluation-v1.json` passent. La capture `viewport.png`, inspectée, montre l'île et des ombres projetées sur la plage et le chemin. Cela ne prouve ni la parité avec une caméra source appariée, ni la distance limite des arbres.

Le verdict global du premier essai reste FAIL : le contrôle public de comparaison se trouve dans l'onglet masqué « Tester & suivi ». Le helper refuse de cliquer le contrôle invisible et conserve `shadow-receiver-gpu.json` avec `requested=false`. Aucun retour GPU n'est déduit de ce refus. Les fichiers v1 sont préservés. `PrepareRuntimeV2.py` prépare 26 fichiers épinglés inchangés et un profil frais ; `MeasureRuntimeV2.ps1` ouvre cet onglet par le bouton public, puis les summaries des éventuels details parents, et garde le refus d'un contrôle invisible. Parse PowerShell PASS. Le second essai est distinct et en cours à ce checkpoint ; aucune source produit modifiée pour contourner la mesure.

`StageCommonExecutable.py` termine à zéro : l'exécutable natif est copié indépendamment à côté du frontend du test commun d'édition. Le reçu `executable-stage.json` épingle les hashes des reçus natifs, les 1800 fichiers vérifiés et le même SHA de l'exécutable. Aucun essai d'édition UI ni validation de sauvegarde/réouverture n'est déduit de cette préparation.

Le second essai natif termine ensuite avec retour 0. `native-shadow-runtime-evaluation-v2.json` est PASS sans contrôle échoué : génération 1 publiée, 148 parties opaques, cadence des trois cascades et du vent en progression, playtest observé sans erreur de bibliothèque. Le contrôle public de comparaison est effectivement visible et cliqué ; `shadow-receiver-gpu.json` donne `requested=true`, `observed=true` après 291,6 ms. L'échantillon figé montre `pending=false`, 1611 passes encodées, compteurs de cascades `[1611,806,404]`, océan en pause et quiescent. Le buffer de paramètres lu sur GPU confirme ombres activées, trois cascades, atlas 2048, matrices finies et caméra `(53.60006,4.44773,-76.99976)` concordante avec la caméra publique. La lecture vérifie ce buffer ; elle n'est ni une lecture des textures de profondeur ni une preuve de fence GPU pour chaque passe. `visual_parity=false` reste explicite. Le candidat est fermé par le finally du helper ; aucun processus externe n'est arrêté. Les preuves v1 échouées restent intactes. `git diff --check` passe pour le rapport et le harness d'édition.

### Reprise de la qualification commune d'édition sur ce candidat

Les inspections sous `.placeables-smoke-shadow-poses-20261004-v1` conservent chaque tentative dans un répertoire frais. v1/v2 échouent à exposer CDP sous le contexte de lancement sandbox ; le processus ne crée aucun enfant WebView2 observé, uniquement conhost. v3, exécuté dans le même contexte natif approuvé que les essais GPU réussis, atteint la page Studio puis révèle l'absence d'installation de l'audit dans le nouveau helper d'inspection. Cette préparation est corrigée pour v4 ; aucun garde du harness canonique n'est retiré. Chaque cleanup confirme 25 fichiers sources inchangés et fermeture du seul processus lancé.

v4 ouvre la carte et la vue d'ensemble mais échoue au contrôle de rotation : « Stable changed plant camera ... camera did not change » après 120 s. Ce résultat reste une observation à diagnostiquer, sans correction du produit ni PASS de navigation déduit. v5 capture la vue d'ensemble sans demander cette rotation facultative, sortie 0 ; `plant-view.png` inspecté, SHA `bd7d5fb74306f537da864988a2db42d737363a7d5b2ed2a7c6d182448f59c79e`. La caméra publique est stable, viewport 1314×797, origine écran `(249,193.9375)`, position `(42,14,-90)` et vue 180°. Les données originales restent intactes. Cette capture sert à choisir un point visible ; elle ne qualifie pas l'édition ni la sélection.

`RunCommonEditingV1.ps1` lance ensuite le harness canonique sur l'exécutable SHA `16937fc4...482d82b9`, avec les quatre cas hérités, le cas manuel et sélection GPU obligatoire. Le point inspecté `(315,435)` correspond aux coordonnées normalisées `(0.0502283105022831,0.30243864188818315)` de la vue d'ensemble. `common-ui-v1/common-placeable-studio-ledger.json` termine `failed_closed` : « Aucune plante visible à ce point ». Aucun ID n'est injecté, aucun succès de sélection ou de modification n'est revendiqué. Le ledger confirme sources et frontend préservés. La capture d'échec est inspectée. Le prochain travail doit établir un point plantable réel et expliquer le comportement de rotation avant de conclure à l'édition de la végétation ; sauvegarde et deux réouvertures restent non qualifiées sur ce candidat.

### Diagnostic de la caméra technique et correction du filtre partagé

`plant-inspection-v6` termine à zéro en conservant un refus de rotation dans son reçu. L'observateur passif `camera-events.json` voit neuf événements trusted sur le canvas : un mouvement initial, mousedown droit (`button=2`, `buttons=2`), six mouvements de X906 à X1106 avec `buttons=2`, puis mouseup. Aucun blur/mouseleave n'est observé dans cette séquence. La signature publique reste inchangée. Cela établit la réception des événements DOM, sans prétendre instrumenter l'état interne Rust de navigation. Le fichier `authoring_camera.rs` vivant est identique au fichier figé du candidat, SHA `395078d9f6366a0b070bfa01d66e0348b3771523ca866fe56b2e2dc8af1129fc`.

L'inspection du code montre un conflit concret : `authoring_camera::navigate` demande une caméra unique sous `SceneCameraFilter`, qui incluait toutes les `Camera3d` hors preview. Or `tidewater_refraction::spawn_camera` ajoute une `Camera3d` avec marqueur `CaptureCamera`, cible image et ordre -10. La caméra visible et cette caméra technique satisfaisaient donc le même filtre. Le filtre partagé de `tidewater_sky/preview.rs` exclut désormais `CaptureCamera` ; les outils de relief, gizmo, chemins et sélection qui utilisent ce filtre partagent cette correction. Aucun réglage propre à un module n'est ajouté, et la caméra de réfraction reste disponible pour le rendu de l'eau.

Le test canonique `scene_tools_keep_the_visible_camera_when_offscreen_cameras_exist` crée une caméra visible, une preview et une capture puis exige la caméra visible unique. `TestSceneCamera.py` compile le module modifié réel avec dépendances natives épinglées et prévoit un contrôle négatif avec l'ancien filtre sur ce même test. Validation native session 29903, Python33484/rustc18604 confirmés actifs ; vérification complète de l'adaptateur Web session44605/Python22556 également active dans un dossier frais. Aucun résultat terminal de ces validations ni nouveau candidat corrigé n'est encore disponible à ce checkpoint. `rustfmt --check` passe. La correction ne transforme pas l'échec de sélection précédent en PASS ; la navigation et la sélection GPU doivent être rejouées après construction d'un nouveau candidat.

Le premier harness natif termine ensuite avec retour 1 : les macros Bevy panique à la dérivation faute de `CARGO_MANIFEST_DIR`. Les deux compilations échouent et aucun test n'est exécuté ; `scene-camera-filter-20261004-v1/result.json` conserve donc FAIL, sources inchangées. Ce résultat décrit la préparation du harness, pas un échec démontré du correctif. `PrepareSceneCameraTestV2.py` produit un helper frais avec `CARGO_MANIFEST_DIR` et `CARGO_PKG_NAME` correspondant à forge_engine, comme les validations natives précédentes. Le rejeu ne démarre qu'après résultat terminal du premier processus. Session37938, Python28912 et rustc15104 confirmés vivants sur `scene-camera-filter-20261004-v2/corrected/harness.rs` ; verdict en attente. Le contrôle Web originel continue sans redémarrage : Cargo6612 et rustc3756 confirmés vivants, `forge_engine` sous wasm32-unknown-unknown. Aucun nouveau runtime ni build candidat corrigé n'est déduit de ces compilations encore actives.

`PrepareSceneCameraCandidate.py` prépare ensuite des helpers distincts sous `studio-scene-camera-library-20261004-v1` et lance uniquement le gel des sources vers `studio-scene-camera-20261004-v1/source`, session60541. Les scripts générés passent le parse Python AST. `BuildFrontend.py` exige le PASS du nouveau test caméra, son SHA de source concordant et la réussite du nouveau contrôle WASM dont les pins doivent correspondre au snapshot. Les validations d'ombres antérieures restent aussi requises. La bibliothèque épinglée est réutilisée sans modifier le candidat shadow-poses. À ce checkpoint, 317 fichiers de source sont réellement copiés mais le manifeste n'est pas encore prêt : aucun gel complet, lancement de build ou produit nouveau n'est revendiqué. Les compilateurs natif15104 et Web3756 restent observés actifs ; la CPU du compilateur Web avance à 26 s. Les avertissements dead-code du harness isolé sont conservés et ne constituent pas un verdict de test.

Le contrôle Web complet termine ensuite à zéro, source-before/source-after identiques : `scene-camera-filter-20261004-v1/wasm-check/result.json` confirme `cargo check --offline --locked -p forge_studio --lib --no-default-features --features web --target wasm32-unknown-unknown`. Il ne s'agit ni d'un lien applicatif ni d'une exécution GPU. Session44605 est réellement terminée. Le test natif v2 a atteint le linker MSVC35896, enfant de rustc15104, CPU positive et environ 2,18 Go résidents ; session37938 reste active sans résultat de test terminal. Le gel des sources reste lui aussi actif, Python32380/session60541. Aucun build du nouveau candidat lancé à ce checkpoint.

Le gel termine ensuite avec retour 0, 1334 fichiers et sources inchangées pendant la copie. Les seuls écarts au parent sont `crates/forge_engine/src/tidewater_sky/preview.rs` et `scripts/Test-CommonPlaceableStudio.ps1`. `checked-source-concordance.json` compare les 872 pins du contrôle WASM réussi au manifeste figé : aucun écart. Cette concordance ne remplace pas le test natif, qui reste explicitement pending. Session60541 est terminée. Le linker35896 reste réellement vivant, avec CPU cumulée et mémoire résidente en progression ; aucun redémarrage du test ni lancement du nouveau build n'est effectué.

Le test natif v2 termine ensuite réellement avec retour 0. `scene-camera-filter-20261004-v2/result.json` donne PASS, sources inchangées et SHA `43558c9c02789bef459bdd4b8233449c857ff5c0210754a09192674936f7de74`. Le module corrigé compile puis exécute son test à zéro ; le même test sous l'ancien filtre compile mais échoue avec retour101, comme attendu. Les deux verdicts sont conservés. C'est une preuve ECS du filtre réel, pas une preuve de navigation UI ou de GPU sur un nouveau candidat.

Après ces résultats et la concordance du snapshot, `RunCandidate.py` est lancé pour `studio-scene-camera-20261004-v1`, session88213. La pipeline entre à BuildFrontend.py le 4 octobre à 04:45:29 UTC ; `build-request.json` décrit le build release WASM depuis les sources figées, optimisation conservée, cache isolé antérieur vérifié disponible et produits précédents préservés. Aucun frontend lié ni exécutable natif nouveau n'est encore produit à ce checkpoint.

`PrepareProofs.py` termine à zéro : 26 fichiers du scénario runtime et 25 du scénario commun d'édition sont copiés dans deux nouvelles fixtures isolées, parents revérifiés inchangés. Les helpers de mesure et de staging sont distincts de ceux épinglés par la pipeline active. Le scénario d'inspection conserve l'observation passive des événements et exige une nouvelle capture pour les coordonnées de plante ; aucun ancien point échoué n'est promu en sélection valide. Aucun nouveau test UI lancé avant la fin du candidat. Les validations d'ombres, la rotation, la sélection GPU, les transformations et les deux réouvertures restent à rejouer sur ce build.

### 2026-10-04 — frontend du candidat avec filtre de caméra compilé

Le pipeline `studio-scene-camera-20261004-v1` a terminé `BuildFrontend.py` avec code 0. Le journal Cargo indique une compilation release optimisée en 41 min 27 s. Le reçu `F:\OpenRPG_Forge\.build\studio-scene-camera-20261004-v1\result.json` confirme les sources figées inchangées et le WASM lié (124738579 octets, SHA-256 `242e0e33de4c21a5d5a41b4ef2e798d7d21bac89eed75b1a9a9b33af1ba8b76a`). Le superviseur poursuit `Assemble.py` dans la même session 88213 ; aucun build n’a été redémarré. Ce résultat ne prouve encore ni l’exécution du nouveau candidat dans Studio, ni l’édition des plantes, ni la parité visuelle.

Le même pipeline a ensuite terminé `Assemble.py` avec code 0 (état mis à jour à 2026-10-04T05:41:09Z) et poursuit `BuildWorker.py`. Les bindings Studio ont été générés ; le reçu `frontend-bindgen.json` est disponible. L’exécution dans Studio et la validation d’édition restent à faire après le worker et l’exécutable natif.

Le worker de navigation du même candidat a terminé avec code 0 (`BuildWorker.py`, 2026-10-04T05:43:20Z ; Cargo release 2 min 07 s). Le frontend complet contient 1800 fichiers. `StageCommonFrontend.py` a terminé avec code 0 (session 65453), vérifiant la copie indépendante des 1800 fichiers dans `E:\OpenRPG_Forge\.placeables-smoke-scene-camera-20261004-v1\candidate\web`, les empreintes et la source inchangée. Aucun lancement ni verdict d’édition à ce stade. Le pipeline poursuit la préparation native dans la session 88213.

La préparation native `StageNative.py` a terminé avec code 0 dans le même pipeline, état au 2026-10-04T05:48:37Z. Le reçu `native-inputs.json` est présent. Le superviseur a enchaîné `BuildNativeFrozen.py` sans redémarrage du frontend. La réussite de la construction native, l’exécution WebGPU et les cas d’édition restent non prouvés pour ce nouveau candidat.

### 2026-10-04 — candidat natif avec filtre de caméra construit

La session 88213 est terminée avec code 0 : les cinq étapes du pipeline ont réussi. Cargo natif indique 16 min 22 s. `native-result.json` confirme les sources figées et le frontend inchangés ; exécutable `desktop\forge-studio-candidate.exe`, SHA-256 `5d6468ce6744066511cd5b58f8205d0ab3516ddeaf9572690c7ee63216be7d0b`. La copie E isolée de cet exécutable a été vérifiée par `StageCommonExecutable.py` (code 0), avec concordance des 1800 fichiers frontend et du WASM Studio `68da90cdb7cde834afe5f45346bbe2d1d4a8b9a340a5cb48e9663e9812da0f10`.

Le diagnostic réel `MeasureRuntime.ps1` est lancé dans la fixture native dédiée (session 53620 ; lancement épinglé dans `native-full-workflow-probe-v1\launch.json`, PID 33256). Son verdict est encore attendu : ne pas déclarer le runtime, l’édition ni la parité visuelle validés sur la seule réussite du build.

Le diagnostic natif V1 (session 53620) est terminé avec code 0, mais l’évaluation stricte `native-shadow-runtime-evaluation-v1.json` est `FAIL` : les captures de cadence observent la projection juste après `Jouer` (simulation encore en mode édition, tick nul, vent à 2 s, atlas passant de pending à 148 parties sans passe encodée). Le readback du receiver est observé, sans suffire à valider le jeu ou la cadence. La capture `viewport.png` montre l’île chargée, pas une qualification de parité.

Le timer initial de 105 s ne garantissait pas la disponibilité de la nouvelle projection après la transition. V1 est conservé. `PrepareRuntimeV2.py` a préparé une copie fraîche de 26 fichiers, parent inchangé (session 97651, code 0). `MeasureRuntimeV2.ps1` attend les preuves publiques de simulation active avec tick positif, contrôle prêt, atlas peuplé et passe encodée, vent > 2 s, puis observe encore 5 s avant la mesure de cadence originale. L’évaluateur strict est inchangé. Le même exécutable est relancé uniquement pour ce nouvel essai isolé (session 2122), sans rebuild ni modification produit ; verdict encore attendu.

V2 (session 2122) est terminé avec code 1 : erreur de préparation du diagnostic, accès à un attribut d’un canvas temporairement absent lors du rechargement après `Jouer`. La fixture et le résultat sont conservés ; ce n’est pas un verdict runtime du produit. V3 corrige uniquement la gestion de ce cas nul (attente maintenue, critères stricts inchangés), parse PowerShell PASS et fixture fraîche de 26 fichiers vérifiée, parent inchangé. `MeasureRuntimeV3.ps1` est lancé sous session 63003 sur le même candidat.

L’inspection caméra/plantes V1 du candidat corrigé (session 35277) s’est arrêtée sur une réponse CDP annulée ; `plant-inspection-v1\failure.json` et `cleanup.json` sont conservés. Aucun cas d’édition ni rotation de caméra n’est ainsi prouvé. Cette défaillance d’observation exige un diagnostic/rejeu ; les validations d’édition restent ouvertes.

Le diagnostic runtime V3 (session 63003) est terminé avec code 0. Après la disponibilité réellement observée de la nouvelle projection (simulation active, joueur au sol, tick positif), l’évaluateur strict inchangé a rendu `PASS`, aucun contrôle échoué (`native-shadow-runtime-evaluation-v3.json`). Cela couvre le mode jeu observé, l’absence d’erreur bibliothèque, la génération d’ombres engagée avec casters opaques, la progression des trois cascades et du vent, et le readback du receiver cohérent avec la caméra courante. Cela ne prouve pas les textures de profondeur, une clôture GPU par passe ni la parité visuelle. L’évaluation tentée prématurément avant création du fichier de cadence avait seulement échoué sur FileNotFoundError, sans reçu ; le verdict présent concerne les fichiers terminaux.

L’inspection caméra/plantes V2, avec journal d’étapes et pile d’erreur, est lancée dans une nouvelle copie isolée (session 88187), après la fermeture du diagnostic runtime. La validation complète d’édition reste à faire.

### 2026-10-04 — rotation de caméra réellement observée dans Studio

L’inspection caméra/plantes V2 est terminée avec code 0 (session 88187). Le diagnostic public confirme une rotation et une stabilisation après glissement droit de 200 pixels : vue initiale 180°, vue finale 262°, caméra passant de (42,14,-90) à (98.425079,14,-13.285446). L’inspection, les événements DOM et la capture sont conservés dans `E:\OpenRPG_Forge\.placeables-smoke-scene-camera-20261004-v1\plant-inspection-v2`. Cette preuve confirme la navigation corrigée, pas encore la sélection GPU ni l’édition.

La capture actuelle a été inspectée visuellement ; un point (1383,515) sur la canopée d’un palmier de la plage droite a été choisi pour un vrai pick GPU. `RunCommonEditingV1.ps1` vérifie les empreintes capture/exécutable/WASM, reproduit la vue d’ensemble et la rotation 200 pixels, conserve les six cas requis avec `RequirePlant=true`, sans injecter d’identifiant. Le parcours complet isolé est lancé sous session 55437 ; verdict encore attendu.

Le parcours d’édition V1 (session 55437) est terminé `failed_closed`. Les six cas (4 sources, bâtiment manuel, plante GPU) ont atteint translation/rotation/échelle, copie et vérification du verrouillage. Le vrai pick a sélectionné la plante `00a6faaa-17df-50ab-abc6-bef5a75a2f90` (`trees`), sans ID injecté. Le premier checkpoint édité est sauvegardé (`cad0474c6f145ac1d85983e70fbfcd9fbf0d6c182d5376b210cf5f386ffc71d8`). Échec ensuite : panneau « Position précise et duplication » non visible après réouverture/sélection ; aucune suppression ni second checkpoint qualifiés. Sources et frontend préservés.

Une copie isolée du harness V2 attend l’action publique idempotente « Réglages avancés → » et ses résumés réellement visibles après reconstruction de l’inspecteur. Tous les cas, contrôles de transformations, sauvegardes/réouvertures et suppressions sont conservés. Le harness canonique est inchangé (preuve SHA dans `common-harness-v2-proof.json`) et SelfTest PASS. V1 est conservé ; un parcours frais V2 est lancé. Git ne reconnaît actuellement ni le chemin Rust comme dépôt et aucun `.git` n’est présent aux deux racines inspectées ; aucune opération Git ni modification produit n’a été entreprise pour ce diagnostic.

V2 (session 36971) a échoué sur une réponse CDP annulée avant tout cas (0/6), sources et frontend préservés ; il ne valide pas le parcours. Les helpers partagés imposent par défaut 15 s à une réponse CDP, indépendamment du délai de disponibilité UI. Une copie isolée V3 porte ce budget d’observation à 60 s, ajoute la pile d’erreur et conserve les six cas et tous les contrôles de résultat. SelfTest PASS, nouvel essai lancé, même candidat.

Correction du diagnostic Git précédent : `.git` est un fichier caché du worktree, présent avec `-Force`, pointant vers `E:/OpenRPG_Forge/OpenRPG_Forge/.git/worktrees/OpenRPG_Forge-Rust`. La lecture Git hors sandbox confirme `Rust...origin/Rust` et les modifications existantes. L’échec antérieur de Git en contexte par défaut ne prouvait pas l’absence du dépôt. Aucune réparation Git, publication ou modification produit n’a été faite.

### 2026-10-04 — parcours complet d’édition du candidat caméra PASS

La session 5458 est terminée avec code 0, `COMMON_PLACEABLE_STUDIO_PASS`. Le reçu terminal `E:\OpenRPG_Forge\.placeables-smoke-scene-camera-20261004-v1\common-ui-v3\common-placeable-studio-ledger.json` couvre six cas : passerelle source, quai source, accessoire de place source, Échoppe source, bâtiment manuel et plante sélectionnée réellement sur le GPU. Tous ont six reçus de commandes, transformations/copie/verrouillage, sauvegarde puis réouverture avec identités/pins/échelles concordants, suppression de l’original et de la copie, annulation/rétablissement, deuxième sauvegarde et deuxième réouverture sans résurrection. Six suppressions vérifiées. Les métadonnées de végétation source, la fixture source et le frontend sont préservés ; listener CDP fermé.

Les empreintes des deux checkpoints ont été relues indépendamment : édité `a913949d5f41371831f53deed9fd6059801731d56554a3adc761b01ab106829a`, supprimé `9da0675238a60a3f35c2ea582766e9445371c3150bd4d2120a00bd6d4df8b691`, concordance PASS. Exécutable/WASM concordent avec le candidat construit (`5d6468ce...be7d0b` / `68da90cd...da0f10`). V1 et V2 restent conservés en échec. Ce résultat utilise la copie isolée du harness V3 (attente du panneau public et budget d’observation CDP), pas une nouvelle compilation produit. Il ne qualifie ni tous les assets, ni la traversée gameplay, ni la performance GPU, ni les pixels par rapport à Three.js, ni une gate ou la livraison complète.

### 2026-10-04 — capture Village sur une carte intacte

Le harness historique ReferenceVillageOnly ajoute une échoppe avant la capture ; ce parcours ne convient pas à une comparaison de la carte intacte. Le helper isolé CaptureVillageV1.ps1 reprend le chargement public validé du runtime V3, sans commande de placement, et sélectionne le diagnostic caméra Village existant avant Jouer. Il exige la simulation réellement disponible, les casters opaques engagés et le statut de caméra stable ; il prépare une capture pleine toile 1920×1080 et conserve le temps observé, sans déclarer la parité temporelle par défaut. Les sources produit ne sont pas modifiées.

La préparation V1 vérifie 26 fichiers, source inchangée. La session 8955 termine avec code 1 sur une réponse CDP annulée avant Jouer ; failure.json est conservé dans studio-scene-camera-20261004-v1/village-capture-v1. Aucune image ou comparaison réussie n'est déduite. La préparation V2 (session 61524, code 0) conserve les mêmes critères et porte uniquement le budget par défaut d'observation CDP à 60 s, dans un helper et une fixture frais ; parse PowerShell PASS. Le deuxième essai démarre sous session 99778 : candidat PID 36156 réellement observé avec son parent PowerShell 1192, exécutable SHA 5d6468ce...be7d0b confirmé dans launch.json. À ce checkpoint, aucun verdict terminal ni capture V2 n'est encore revendiqué. L'objectif complet reste actif.

La session de capture Village V2 99778 termine avec code 0. Audit indépendant PASS : 26 fichiers de fixture inchangés, PNG 1920×1080 et hashes concordants, caméra publique du receiver (42,14,-90), exposition observée +0,10 EV, 148 parties opaques. L'image source et la nouvelle capture ont été inspectées. La comparaison mesure une luminance moyenne native 108,5543 contre 109,2250, erreur absolue 9,3405 ; elle ne prouve pas la parité. Le temps Studio 2,5923 s diffère de la source 2 s. L'inspection révèle des accessoires de cour absents ; leur production dans _villageProps est vérifiée dans la source figée. La prochaine tranche doit couvrir leur conversion complète et leurs placements éditables. [Comparaison, métriques et écarts](2026-10-04-village-native-comparison.md).
