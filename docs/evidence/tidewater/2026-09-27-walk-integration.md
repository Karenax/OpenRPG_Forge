# Priorite parcours jouable

Le proprietaire demande ce qui manque pour jouer et signale la duree excessive.
Priorite immediate : parcourir la scene existante avec le controleur Forge,
sans attendre la selection fine des arbres. Le perimetre complet reste actif.

Le probe natif accepte `--walk` en derniere option. Ce mode conserve la scene
complete deja assemblee, desactive capture/sortie automatique et la passe de
selection, et installe le PlayerControllerPlugin existant. Depart X42/Z-90 sur
la plage, hauteur issue du terrain canonique (environ 3,034 m avant capsule).
256 tuiles physiques du relief sont derivees par le module partage. Les meshes
visibles du village recoivent leur collision triangulee. Le controleur attend
la synchronisation du relief et la readiness des pipelines. Le terrain CDLOD
et la selection residente des plantes suivent la camera.

Le clic capture la souris, Echap la libere ; commandes physiques WASD, Maj,
Espace et V du controleur existant. Le script `scripts/tidewater/Walk-Tidewater.ps1`
compile et lance cette version de developpement avec le bundle local sur E:.
Ce n'est pas encore la distribution Runtime autonome.

Clippy cible strict PASS : `walk-clippy-003.log`, 3,90 s. Corrections pendant le
controle : reconstruction du plan derive non Clone depuis document/hauteurs,
et import TextureUsages deplace en tete. Compilation native lancee dans
`walk-build-001.log`, resultat attendu. Aucune execution jouable validee encore.

Restent a verifier : demarrage GPU, contact et deplacement reels, collisions
du village, comportement aux limites et niveau d'eau, performances et lancement
par le proprietaire. Ombres/océan/nuages restent ceux de la scene de reference
fixe ; une camera libre ne prouve pas leur adaptation dynamique complete.
Herbe, ombres dynamiques et animation source complete restent dans le chantier.

## Premier parcours natif valide

Compilation PASS (`walk-build-001.log`, 6 min 51 s). Clippy du smoke PASS
(`walk-smoke-clippy-001.log`, attente Cargo comprise). Le compilateur a lu le
module smoke ajoute pendant la compilation du moteur : le binaire resultant
accepte bien `--walk-smoke` et l'a execute jusqu'au succes.

Execution du lanceur avec `-SkipBuild -Smoke -CapturePath .../native-walk-001.png`
terminee exit 0. Rapport `native-walk-001.walk.json` : depart
[42, 3.945817, -90], arrivee [41.989552, 3.312436, -75.112801], distance
horizontale 14.887203 m, 271 ticks observes tous au sol, requete camera reussie
et identite du backend stable. Les commandes passent par le controleur reel,
avec la scene et les collisions completes, et non par une translation forcee.

Capture 1920x1080 inspectee : capsule de personnage sur le sable, village,
ponton, vegetation et ocean visibles depuis la camera du joueur. PNG SHA-256
79be66c02d6e455b5be055d965c9f60e643a95cbdd99a49a396b63ed00c582a1.
Executable SHA-256
596cbcb29d1f025576f88f469b91910d833dbb38d046eaf133fb2aba6492cd9a.

`scripts/tidewater/Jouer-Tidewater.cmd` lance directement ce binaire local sans
recompilation. Le PS1 sans SkipBuild reste le chemin de reconstruction.
La marche par commandes et le rendu natif sont acquis pour ce court parcours.
Clavier/souris OS, traversee du village, longue navigation, eau/nage et parite
visuelle complete ne sont pas qualifies par ce resultat. La capsule est un
personnage provisoire. Aucun processus de test ne reste actif.

## Ocean suivant la camera

La selection CDLOD ocean etait figee sur la camera de reference. Le mode Walk
recalcule desormais les noeuds pour la camera du joueur, avec la configuration
source existante. Il remplace le mesh uniquement si la liste change, en gardant
materiau, textures et buffers FFT. La construction du mesh est partagee avec
le setup initial. Le smoke exige plusieurs actualisations effectives.

Clippy cible PASS (`walk-ocean-clippy-001.log`). Build PASS 2 min 15 s, attente
du verrou incluse (`walk-ocean-build-001.log`). Execution du meme lanceur PASS,
exit 0 : `native-walk-ocean-001.walk.json` enregistre 14.887203 m, 272 ticks
au sol, 14 actualisations ocean, 131 noeuds finaux, camera et backend valides.
La capture a ete inspectee. Ceci valide un court parcours et la selection
de geometrie, pas encore le temps continu des vagues ni un tour de l'ile.

Executable SHA-256 :
4280fd839f72c27dcea50d3f3bc7068c8a9fbe069fa943db1712d6a51168c8c1.
PNG SHA-256 :
38660188e8985a5b63ef68d5d35ebbc89a86589b0dc828f3871b45029c3b2ccd.
Le lanceur direct pointe maintenant sur cette version. Processus de test termine.

## FFT continue apres la sequence de reference

Le mode Walk active une horloge ocean apres le readback complet de la sequence
initiale de 120 pas. La FFT conserve ses buffers et son temps, avance a pas
fixes 1/60 s vers l'horloge de jeu, avec au plus quatre pas par soumission pour
borner le cout. Les snapshots initiaux ne sont plus recopies apres leur preuve.
Le materiau optique recoit l'index et le temps du dernier pas encode observe.
Le mode de capture fixe reste inchange. Le CPU peut observer le pas GPU avec
un decalage d'une frame ; la cadence temps reel soutenue n'est pas qualifiee.

Clippy cible PASS (`walk-live-ocean-clippy-002.log`) et compilation PASS en
1 min 59 s (`walk-live-ocean-build-001.log`). Execution native exit 0 :
`native-walk-live-ocean-001.walk.json`, 14.887203 m, 462 ticks au sol, 14 mises
a jour CDLOD. Deux lectures de la texture GPU de deplacement, aux pas observes
151 et 181, donnent des SHA differents :
a03292850a4b8793059abff6766a7c6e6aeb86e74f5f7995111571bdb1ae955d et
ff0b4933b6a7e5adf357596cb639a657c1273476681f16754927cf68924b17f4.
La simulation produit donc des deplacements nouveaux au-dela de la reference.
Capture inspectee, executable SHA-256
2f33ad42e4c6da9a74b3f824872a7543091f858770f6020cbc5d508179baedf6,
PNG e6ed4e2aee2b69ca5ac86986ab85d4c689eeb1db01c46da48f51ab3c7ade636c.

Limite explicite : ShoreSim, emission/integration des embruns et nuages gardent
encore leur etat final de reference. Ce premier prolongement est ocean seul,
pas la parite animee globale. Leur boucle couplee doit maintenant etre prolongee.
Le lanceur direct utilise ce binaire. Le processus de test est termine.

## Prolongement couple ocean, rivage et embruns

Le mode Walk prolonge maintenant ShoreSim et l'emission/integration des
particules avec la FFT. Le pas suivant attend le retour GPU du compteur de
particules pour alimenter le controleur natif de budget. Ce mode remplace la
continuation ocean seul precedente. Les buffers persistants sont conserves ;
les copies et lignes d'historique restent bornees aux 120 pas de reference.
Le rivage publie ensuite sa texture a chaque nouveau pas, et le materiau des
sprites suit le temps encode de la simulation. Les captures initiales sont
lues avant la reprise, pour ne pas melanger qualification et etat vivant.

Clippy cible PASS (`walk-coupled-clippy-002.log`). Build PASS 2 min 24 s,
execution native exit 0 (`walk-coupled-build-001.log`, `native-walk-coupled-001.log`).
Le rapport `native-walk-coupled-001.walk.json` confirme des contenus GPU
differents aux pas observes 150 et 180 pour la FFT, la texture ShoreSim et le
buffer de positions des particules. Marche 14.887203 m, 831 ticks au sol,
camera et backend valides, 14 actualisations du maillage ocean.

Capture inspectee. Executable SHA-256 :
48b38a6e7107304fbca6ebc6171ffd3593a6fd715eac7457cfac99906b232027.
PNG SHA-256 :
f09e0976eb5cbb65cb4ea576b7040973c5fcba7a473fdaf5b764fc7f705fa1f9.
Le lanceur direct utilise cette version ; le test est termine.

Limites : avance coherente prouvee, pas de cadence temps reel soutenue acquise.
Le retour CPU du budget et les allocations/pipelines du probe restent couteux
et doivent etre optimises. Le Frame de calcul d'emission conserve encore la
camera de reference ; adaptation aux longs parcours, nuages, vent/vegetation,
parite animee appairee et qualification WebGPU restent ouverts.

## Ressources GPU immuables des embruns conservees

`gpu_cache.rs` conserve les deux pipelines, leurs layouts compatibles, neuf
samplers, les textures fixes de hauteur/deltas/rivage, les stations et uniformes
constants. Les buffers query/requests sont seulement lus par le shader ; ils
restent nuls dans cette promenade sans bateau. Les donnees par pas, les sorties
mutables et les images FFT restent distinctes. Le cache appartient a l'Input
immuable du probe ; aucun rechargement dynamique de cet Input n'est qualifie.

Clippy plant + spray strict PASS (`walk-cache-clippy-001.log`), compilation
PASS 1 min 47 s (`walk-cache-build-001.log`). Parcours avant/apres exit 0 :
`native-walk-cache-before-001` et `native-walk-cache-after-001`, 14.887203 m,
contacts sol et requete camera valides. Le second termine en 270 ticks au sol.
Capture apres inspectee ; executable SHA-256
1b6573e2ac118276276b3c8577c3be5bdc1a1a995af8df1595272f8065af0ecf,
PNG 30a7fbe308aaab6566e52a7e45962d7def525dfe07084780228fd41809ece54d.

`walk-cache-comparison-001.json` compare 30 sorties : 27 byte-identiques,
dont FFT, ShoreSim, cretes, compteur, depots, historique et budget natif.
Les trois tableaux de particules ont des ordres differents, deja observes
entre deux executions de l'ancien binaire. Les 40960 lignes completes
(position + vitesse + info, sans dissocier leurs champs) triees avec
multiplicite sont byte-identiques, SHA-256
19859ef6af0db91c277986292360cf538ddcf9cfff8dd88ccfbffacc4bc4a7db.
FFT et rivage restent egalement identiques aux pas observes 150 et 180.

Duree totale du scenario mesuree au chronometre externe sur cette machine :
19.9365111 s avant, 9.9206301 s apres. C'est une observation du scenario entier,
qui comprend chargement, 120 pas initiaux, readbacks, marche et capture ;
ce n'est ni un benchmark FPS ni une qualification de cadence temps reel.
Les allocations repetitives retirees reteleversaient au moins 20 Mio de
textures fixes par pas. Le cout du retour GPU/CPU pour le budget reste ouvert.

## Emission des embruns autour de la camera du joueur

Le mode Walk transmet chaque frame la position monde de ForgePlayerCamera
apres propagation des Transform. A partir du pas 121, la simulation l'encode
dans BreakersParams.cameraPos (octets 0..12), effectivement lu par camFade dans
le kernel source. Le budget aux octets 28..32 est conserve. Les matrices Frame
et les 120 premiers pas de reference ne sont pas modifies. Le transfert est
un petit snapshot partage synchronise ; il ne depend pas du seuil 1 m du LOD.

Clippy strict PASS (`walk-camera-clippy-001.log` avant retrait de l'ecriture
inutile de Frame.cameraPos), build final PASS 1 min 45 s
(`walk-camera-build-001.log`). Smoke exit 0 : `native-walk-camera-001.walk.json`,
14.887203 m et 270 ticks tous au sol. Le rapport enregistre les entrees camera
reellement encodees : pas 121 [41.991142,5.389086,-88.624802], pas 199
[41.989552,4.848039,-79.514465]. Ces deux valeurs sont des preuves CPU des
uniformes encodes, distinctes des readbacks GPU FFT/rivage/particules du rapport.
Ces derniers changent entre les pas observes 150 et 180.

Les 30 sorties initiales sont conservees avec la meme equivalence documentee
precedemment (27 fichiers bruts identiques et multiensemble complet des
particules identique) : `walk-camera-comparison-001.json`.
Executable SHA-256 ba2891b8313e8faf335c29795edc4f70a2de26891b50e7afc5e0102492f632ea.
PNG SHA-256 acc49aa404229b2c52fdd30b0ee5257e22e5b7253dc0615b03b3817931ed965e.
Un warning de canal readback ferme survient apres le PASS pendant l'arret ;
le processus termine exit 0, sans erreur de validation GPU pendant le parcours.

## Horloge commune des couches d'eau visibles

Le systeme `water_time.rs` lit une seule fois le dernier pas de simulation
shore/spray, puis met a jour WaterMaterial, SprayMaterial et LipMaterial avec
le meme index/temps. Il ignore les pas deja appliques pour eviter de modifier
inutilement les assets. Cela anime notamment la dentelle de la déferlante,
qui utilisait encore le temps fixe 2 s. Le sable humide reste alimente par
la texture ShoreSim partagee ; le shader terrain actif n'a pas besoin d'une
nouvelle horloge pour cette humidite.

Formatage et diff --check PASS. Clippy cible strict PASS
(`walk-water-time-clippy-002.log`, 5,85 s) apres regroupement des parametres du
systeme de controle. Build PASS 1 min 46 s (`walk-water-time-build-001.log`).
Smoke final exit 0, capture inspectee : `native-walk-water-time-001.walk.json`.
Distance 14.887203 m, 270 ticks au sol, 15 mises a jour ocean, camera et backend
valides. Les trois assets materiaux exposes au renderer ont le meme index 200
et temps 3,3333333 s. Cette verification CPU des uniforms complete les lectures
GPU FFT/rivage/particules aux pas observes 150/180 ; elle ne constitue pas une
comparaison visuelle animee appairee avec l'upstream.

Executable final SHA-256 :
5bed14d716cc369078f2a435364a340075b4952679cfda3ac7fb5659a58b322f.
PNG SHA-256 :
b1715bae0be610330ec29bf769de5db4571449ad5343f0525f75bfc29d6ed8f4.
Le lanceur direct utilise ce binaire. Aucun processus de test Tidewater ne
reste actif. Le warning readback a l'arret decrit ci-dessus est encore present.

Commandes de validation dans le workspace Rust, avec CARGO_TARGET_DIR et
TEMP/TMP sur E: comme le lanceur, RUSTFLAGS retire :

```powershell
cargo clippy -p forge_engine --example tidewater_plant_probe --example tidewater_spray_probe --locked --offline -j 1 --no-deps -- -D warnings
cargo build -p forge_engine --example tidewater_plant_probe --locked --offline -j 1
./scripts/tidewater/Walk-Tidewater.ps1 -SkipBuild -Smoke -CapturePath E:/OpenRPG_Forge/.tidewater-integration/native-walk-water-time-001.png
```

Utiliser un nouveau nom de capture a chaque repetition. Les tests de cette
reprise portent sur les exemples natifs ; pas de qualification Studio/WebGPU,
clavier/souris OS, parite finale ni publication Git.

Prochain point de fluidite : l'avance de la simulation reste bloquee sur chaque
retour du compteur GPU. Le controleur SprayBudget partage prend deja en charge
la derniere valeur asynchrone repetee ; son raccordement live doit conserver
un timestamp de pas attache a la copie GPU, sans reutiliser le timestamp CPU
de demande de lecture. Conserver separement le protocole exact des 120 pas de
reference. Nuages mobiles, vent/ombres, longs parcours et integration Runtime
restent aussi ouverts.

## Feedback GPU asynchrone et essai prolonge

L'attente du retour CPU pour chaque pas ne s'applique plus apres la sequence de
reference. La simulation conserve un pas FFT -> ShoreSim -> spray par soumission,
avec permission du suivant des que le dernier a ete encode. Le controleur natif
consomme le dernier compteur recu, meme repete, comme Breakers.js upstream.
Un buffer de huit octets contient head et numero de pas ; les deux champs sont
copies dans le meme encoder apres integration, avant le readback Bevy. Le numero
provient d'un buffer COPY_SRC immuable par pas : aucune queue.write_buffer sur
un stamp partage. Les retours anciens/dupliques sont ignores. Le temps du sample
est derive de son propre tag GPU, pas de la frame CPU qui demande/recoit la copie.

Les pas 1..120 gardent leur protocole strict. Le passage 121 et l'ancrage de
l'horloge live attendent la fin des readbacks initiaux shore/spray, en plus de
l'ocean. La revue de l'ordre RenderGraph puis copies Readback de Bevy 0.19 n'a
pas releve de rupture du couple head/pas. Les locks budget/statistiques sont
pris dans le meme ordre dans le calcul et la reception.

Premier parcours court : build PASS 1 min 56 s (`walk-async-build-001.log`),
Clippy PASS (`walk-async-clippy-001.log`), exit 0 `native-walk-async-001`.
142 pas live encodes, 141 samples acceptes, cadence observee 60.7292 pas/s sur
ce court intervalle. Les sorties initiales restent equivalentes :
`walk-async-comparison-001.json`, 27/30 fichiers bruts identiques et les lignes
completes des trois buffers particules identiques apres tri avec multiplicite.

Le lanceur accepte maintenant `-LongSmoke` (`--walk-smoke-long`) : meme marche
14,9 m, puis repos sur place jusqu'a 1800 ticks physiques. Les preuves GPU sont
repoussees aux pas observes 1500+ au lieu de 150+, et le controle refuse toute
valeur non finie dans les etats du rivage et les positions des particules lus.
Les statistiques d'affichage utilisent les intervalles Time<Real> du thread
principal apres une seconde de feedback live : moyenne, p95 des intervalles,
et FPS moyens des 1 % d'intervalles les plus lents. Ce ne sont pas des timers GPU.

Clippy strict trois exemples PASS (`walk-soak-clippy-002.log`, 5,89 s), apres
extraction du collecteur de temps du systeme de fin. Formatage, diff --check et
parsing PowerShell PASS. Build final PASS 1 min 49 s (`walk-soak-build-001.log`).
Commande executee, exit 0 :

```powershell
./scripts/tidewater/Walk-Tidewater.ps1 -SkipBuild -LongSmoke -CapturePath E:/OpenRPG_Forge/.tidewater-integration/native-walk-soak-001.png
```

Resultat `native-walk-soak-001.walk.json`, capture inspectee :

- 1800 ticks physiques, tous au sol ; 14.887203 m parcourus, camera/backend stables ;
- 1666 pas live encodes jusqu'au pas 1786, 1665 samples acceptes jusqu'a 1785 ;
- neuf emissions ont reutilise un feedback plus ancien que le pas precedent ;
- cadence observee des tags GPU : 59.9762393 pas/s, hors sequence initiale ;
- FFT, rivage et particules changent aux pas observes 1501/1531 ; rivage et positions finis ;
- trois materiaux eau/sprites/lip synchronises au pas 1786 ;
- 3849 intervalles d'affichage : moyenne 143.8871 FPS, p95 7.7228 ms,
  1 % low 98.1054 FPS ;
- duree totale externe 35.6555032 s, initialisation et capture incluses.

Limites de cette mesure : fenetre/capture 1920x1080, profil dev optimise avec
symboles, courte marche puis camera stationnaire ; ce n'est pas le benchmark
de toute l'ile ou du viewport Studio/WebGPU. L'inventaire Windows indique une
RX 7800 XT et un iGPU Radeon, mais cette mesure n'enregistre pas encore l'identite
de l'adaptateur effectivement choisi par wgpu. Pas de verdict artistique ni de
parite animee upstream. Le warning readback a l'arret reste present apres PASS.

Executable final SHA-256 :
d980a766cc607de593f5691f1614bccdcd6c964ce03b2f34ea63a4482b461848.
PNG SHA-256 :
c5eed7d498363031be5463b093bc4767ef0f963a5b050b890bab7903fc3d50f4.
Le lanceur direct utilise cette version ; processus de test termine.

Le prochain travail visuel reste le mouvement des nuages et du vent/vegetation
avec leurs ombres, puis les parcours plus etendus. L'edition individuelle des
instances et leur reutilisation Studio, les autres comportements de jeu et
l'integration des adaptateurs dans Runtime restent ouverts dans le plan complet.

## Suite : nuages continus

La continuation des nuages et de leurs ombres est maintenant raccordee a la
camera et au temps de la promenade. Le demarrage attend les readbacks initiaux
de l'eau. Apres correction d'invalidations inutiles des materiaux, l'essai natif
de 30 s passe : moyenne 142,785 FPS, simulation eau 60,088 pas/s, textures GPU
nuageuses changeantes et finies, camera deplacee et historique conserve.
La reference initiale des dix textures cloud reste identique a l'octet.
[Preuves, limites et hashes du nouveau binaire](2026-09-27-live-clouds.md).
Le vent visible des plantes et ses casters restent le prochain travail.
