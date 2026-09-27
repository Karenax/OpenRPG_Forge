# Gate 1 — étude d'atlas de groupes feuillus

9 septembre 2026, Rust, local non publié. Gate 1 ouverte.

## Dernier verdict : candidat recadré, gain natif confirmé

### Fondu des ombres par distance caméra — candidat B, 9 septembre

#### Règle de distance commune à la projection et aux preuves

`VegetationLodRanges::from_governed_profile` centralise maintenant la bande
proche : utilisée par la projection des prototypes, l'uniforme de fondu et
la trace du recorder. La trace choisit ce mode seulement si la projection
gouvernée est présente sans erreur ; elle conserve le mode procédural sinon
et exporte `governed_prototypes` pour lever l'ambiguïté. Correction du diagnostic
ancien 64–72 m pour une forêt réellement à 24–32 m, pas changement de distances.

Tests natifs avec features `native,qualification` : cinq tests du recorder
PASS, puis 18 tests filtrés `governed_` PASS (un test commun aux deux groupes).
Cas explicite gouverné 24–32 / procédural 64–72, complémentarité medium et HLOD
inchangé. Diff-check PASS. Aucune nouvelle capture ni mesure : comportement
graphique inchangé, ancien EXE conserve l'ancien format de trace jusqu'au
prochain build. Les anciens journaux ne sont ni réécrits ni requalifiés.

#### Coût natif mesuré, profils dev comparables

ABBA `r1-shadow-fade-dev-abba-20260909` terminé : A = EXE GLTF-range
`e22a0857…`, B = fondu caméra `3f0abde6…` (hashes complets ci-dessous et dans
les pins). Deux builds dev, même kit recadré, eau optique, ombres équilibrées,
parcours 1800 ticks sans VSync. Pas une qualification release/GPU cible.

| Passage | FPS trajet | FPS forêt | p95 forêt ms | 1 % low forêt FPS |
| --- | ---: | ---: | ---: | ---: |
| A | 66,47 | 51,54 | 21,97 | 44,27 |
| B | 65,83 | 50,22 | 22,69 | 42,70 |
| B | 66,86 | 50,64 | 22,75 | 38,89 |
| A | 67,66 | 51,72 | 21,98 | 43,63 |

Coût moyen forêt ~2,3 %, p95 +~0,75 ms ; le 1 % low se dégrade aussi.
Ne pas annoncer une optimisation ou une absence de coût. Le candidat reste
celui qui corrige le saut visuel, mais le seuil forêt 60 FPS n'est pas atteint.
Ne pas relancer cette série inchangée ; intégrer ce compromis à la prochaine
qualification commune, avec les autres critères de Gate 1 encore ouverts.

Analyse complète PASS : 1519 ticks communs, écart maximal de position 7,53 mm,
boom caméra 1,88 mm, eau/ombres/fenêtre/axes et entrées communes concordants.
`comparison.json` SHA-256
`0A0CC97E23D3EBBCD2DF36C84F945A302378290685646CF88A6E2F3E8D88B641`.
PIDs 22780/26864/30912/33548 terminés code 0 et absence vérifiée.

`Run-FoliageAtlasCandidate.ps1` accepte maintenant un EXE et son hash attendu,
tout en gardant l'EXE historique par défaut. Mauvais hash testé : rejet avant
création de sortie/lancement. Les quatre passages valident les pins réels.

#### Intégration Studio : chargement et capture vérifiés

Paire Studio réutilisée ensuite pour le vent, sans relance :
`forge-viewport-1788962349847.png` (SHA
`06FA812E707D3B14EA3E5D403E9E564839871C82D65D6B0BA3433448305D2597`)
et `forge-viewport-1788962362649.png` ci-dessous, mêmes dimensions 1408×966,
même session sans déplacement ni changement de profil entre exports. Les deux
PNG sont archivés dans le candidat. Inspection : variation des brins/fougères,
personnage et cadrage stables. Analyse RGB en lecture seule, régions choisies
sur les images avant calcul : végétation [200,350,550,500], sol témoin
[30,600,180,750], ciel témoin [50,20,500,160]. Écart absolu moyen par canal
respectivement 1,84628 / 0,11237 / 0,00954 sur 255. Ce n'est ni une mesure
d'amplitude physique ni un test isolé vent actif/inactif avec horloge épinglée.
Le lancement WebGPU et la variation proche sont désormais prouvés ; le défaut
de preuve du vent lointain (`R1_FAR_WIND_ISOLATION.md`) n'est pas levé.
R1-014 reste ouvert avec ce périmètre précisé, pas avec « WebGPU non lancé ».

Frontend reconstruit par Trunk (dev), puis paquet Tauri séparé
`r1-shadow-studio-candidate-20260909`, kit recadré et reflets transmissifs
conservés. WASM `5E26707431878A5FF965D812061FEE2AB14762E826A30B4FC3A01606DEB3EFF5`,
EXE `20E04AEFDB05C26C58C99FD2F44FAD3AFEE27BF2E5C356F7568C62680748469F`.
Lancement dans la vraie fenêtre Studio, scène affichée, PNG exporté puis
ouvert : terrain, herbe, personnage, forêt et ombres présents. Pas de parcours
automatique demandé dans ce paquet : le fondu en mouvement n'est pas qualifié
par cette vue de départ.

Preuves copiées dans le dossier du candidat, originaux Downloads conservés :

- `forge-viewport-1788962362649.png` (1408×966), SHA
  `CEEBCC5DF732D196B516F3D48C745A3B939A2E2A639028FC9917451052F64F48` ;
- `forge-diagnostic-1788962373737.json`, SHA
  `61F36674B4DD758545844CCE4F6613E5FAB5DF75F9E389BD410599F979EDE3B4`.

JSON : phase `prepared`, 99 pipelines, pending/failed 0, huit observations
stables, warmup 47,7 s ; 20/20 instances, 36 colliders, 1662 arbres.
`transient_shader_errors_peak=18` compte uniquement ShaderNotLoaded et
ShaderImportNotYetAvailable selon `render_warmup/web.rs`, pas une erreur
shader définitive. Collecte arrêtée après préparation : aucune garantie
d'absence d'erreur future. Profil High toujours incomplet, exports non
atomiques, pas de benchmark ni d'acceptation humaine. Fermeture de la fenêtre,
PID 11836 et ses enfants directs absents au contrôle CIM. Aucun serveur ajouté.

**Le saut précis 161/162 n'est plus visible dans ce replay.** Les images
161, 162 et 180 ont été ouvertes et inspectées : chemin sans apparition
brutale à 162, silhouette atténuée plus loin à 180. Ce constat borné ne
qualifie ni toutes les transitions ni la qualité artistique globale.

Implémentation Forge : `shadow_fade_distances` correspond à la bande proche
gouvernée (High 24–32 m). La passe profondeur sans sorties normales/mouvement/
deferred utilise un fragment dédié, conserve `prepass_alpha_discard`, puis
un motif de tramage fixe 4×4 selon la distance de la racine à
`view.lod_view_world_position`. Bevy 0.19.1 `bevy_render/src/view/mod.rs`
1067 et suivantes fournit la caméra associée aux ombres directionnelles.
Pas de buffer de plages requis dans cette passe, pas de bruit temporel,
pas d'arbre ajouté, pas de distance étendue. Les matériaux procéduraux
gardent leur ombre pleine ; le fondu cible les prototypes gouvernés.

Premier candidat : erreur shader, fonction Bevy conditionnelle indisponible.
Runtime exact PID 6936 arrêté, session de capture terminée en échec ; journal
`r1-shadow-camera-fade-motion-20260909` conservé. Remplacement par tramage
local, puis second build dev PASS en 1 min 32 s. Candidat B :

- EXE `r1-shadow-camera-fade-build-20260909-b/runtime.exe`, SHA-256
  `3f0abde6e4a88995314327bb3127cfab5d581fde200f7931799435618556e231` ;
- sortie `r1-shadow-camera-fade-motion-20260909-b`, 271 PNG Full HD,
  traversée/collisions/arrêt automatique PASS, 109 pipelines prêts, zéro
  pending/failed, huit observations stables, 206 frames de warmup ;
- vidéo `236bc3bda0b7399035f9c7bf0864482659d08e695f11b08c4969a91f8e1fb865` ;
- journal `2bed99cca629283054a273391b297e93ff9408be502f6cd057648d678233155a` ;
- session 58599 terminée code 0, aucun runtime/encodeur de ces deux essais
  encore actif au contrôle CIM.

Même kit recadré, WorldSketch, High, clair et forêt batches que le replay
GLTF précédent. Vidéo hors temps réel à 30 images/s imposées : aucun FPS
ou gain de performance déduit. Coût du fragment supplémentaire sur casters
opaques, autres profils, Studio/WebGPU et stabilité sur toute la bande
restent à qualifier. Conserver le candidat pour ces validations, ne pas
répéter le diagnostic initial comme si le saut 161/162 restait inchangé.

Validation CPU : `cargo test -p forge_engine --lib --features native governed_
--locked -j 2`, 16 tests PASS, dont la concordance du fondu avec les plages
réelles pour quatre distances proches. Warning préexistant de méthodes
`capture_readiness` inutilisées hors qualification ; aucune preuve clippy
globale sans warnings revendiquée. Formatage ciblé et diff-check PASS.

### Projection des distances GLTF — 9 septembre, correctif en qualification

#### Corrélation du saut avec la coupure LOD proche

Lecture du journal de ce replay, sans nouveau lancement : parmi les racines
logiques de forêt, une seule passe de >=32 m à <32 m entre frames 160 et 162.
ID `8b68a32b-ca41-5876-b41e-ef32bd87e9a6`, racine
`[-83.53271484375, 3.5074431896209717, -147.9527587890625]`.
Distances 3D à la caméra : 32,229242 m (160), 32,065301 m (161),
31,901355 m (162). Cette entrée coïncide avec le saut inspecté.

Le code Bevy 0.19.1 `bevy_light/src/lib.rs` 440–448 exclut les casters
directionnels hors `VisibleEntityRanges` avant le dessin. Dans Forge,
`vegetation/prototype_projection.rs`, seuls les LOD0 projettent des ombres ;
la plage proche gouvernée High se termine à 32 m. La spécialisation d'ombre
`bevy_pbr/src/render/light.rs` 2460–2495 construit les clés mesh/matériau sans
ajouter la clé de fondu de visibilité. Cela concorde avec l'absence de binding
de plage dans les shaders d'ombre inspectés précédemment.

Conclusion bornée : candidat spatial unique et mécanisme de coupure identifiés,
pas encore preuve d'identité du draw GPU. Prochaine correction à évaluer :
transition effective des casters de forêt à la limite LOD, en conservant un
budget borné et les mêmes arbres. Un changement de couleur d'ombre, de warmup
ou de plage des seules instances GLTF ne traite pas ce mécanisme. Le fondu doit
utiliser la distance de la caméra principale, non celle de la vue lumière.

**Replay exécuté : saut d'ombre toujours présent.** Build dev natif terminé
en 1 min 35 s ; exécutable séparé `r1-gltf-range-build-20260909/runtime.exe`,
SHA-256 `e22a08571468cee7ea68184ef0d69fddb41d188fd50fc3c437d74cf3469a196c`.
Même WorldSketch, kit recadré, catalogue, météo claire, High et forêt en batches
que `r1-bridge-readiness-motion-20260909`. Sortie
`r1-gltf-range-motion-20260909` : 271 PNG Full HD, traversée/collisions et arrêt
automatique PASS, 109 pipelines prêts, zéro pending/failed, huit observations
stables après 203 frames de préchauffage. Vidéo 9,033333 s à 30 images/s imposées,
pas mesure de performance. SHA vidéo
`d18202c145a1e1af3a6a693e4eafe7d17fd468f1069ad91db691804e2c2ebc1a`,
journal `9f0159dc65a1a24a8a73dfc3e27153b001f81efee2cfec5f0e189603651b6693`.
Images 161/162 ouvertes et inspectées : une ombre apparaît encore brutalement
sur le chemin à la sortie du pont. Ce correctif d'adaptateur est conservé pour
la conformité des plages GLTF, **pas comme solution au saut**. Ne pas rejouer
ce candidat inchangé pour ce défaut. Session de capture terminée code 0 ;
aucun runtime de ce chemin ni encodeur de cette sortie encore actif au contrôle.
La qualification visuelle globale et Gate 1 restent ouvertes.

Inspection du code Bevy 0.19.1 (`bevy_camera/src/visibility/range.rs`, lignes
57–59) : `VisibilityRange` ne se propage pas aux enfants. L'adaptateur du kit
plaçait ces composants sur les nœuds GLTF uniquement. Les primitives `Mesh3d`
reçoivent désormais explicitement la plage de leur LOD, sans modifier les
distances, les assets ou la politique `NotShadowCaster` des LOD1/2. Les systèmes
nœuds/primitives sont déjà chaînés. Test ECS ajouté pour les trois LOD et un
maillage extérieur au kit. Ce défaut est établi, mais son lien avec le saut
d'ombre 161/162 et l'effet visuel du correctif ne sont pas encore vérifiés.

Validation : `cargo test -p forge_engine --lib governed_asset_kit::tests --locked
-j 2` terminé avec huit tests PASS (0,63 s après compilation), formatage et
`git diff --check` PASS. Premier essai non compilable car `VisibilityRange`
n'implémente pas `Debug` ; assertion d'égalité corrigée sans changer sa portée.
Warning préexistant de méthodes inutilisées dans `capture_readiness.rs` avec
ces features par défaut ; pas de revendication clippy sans warnings. Aucun
nouveau runtime graphique construit ou lancé à ce stade initial ; replay ultérieur ci-dessus.

La relecture GPU `r1-shadow-draw-inspection-20260909` s'est terminée PASS,
processus 10440 fermé (`host-result.json`). Huit draws inspectés, dix shaders
compilés distincts : cibles d'ombre D32 4096², quatre couches, distinctes de la
profondeur principale 1920×1080. Des shaders standard et de végétation étendue
participent aux ombres. Les noms de fonctions sont supprimés du compilé : leur
absence textuelle ne démontre pas une fonctionnalité absente. Le caster précis
du saut reste non identifié ; ne pas qualifier ce diagnostic comme correction
visuelle, ni rejouer les deux variantes shader déjà rejetées.

### Revue temporelle et garde de démarrage — 9 septembre

#### Capture GPU courante acquise et relue

9 septembre : collecteur RenderDoc étendu au manifeste vidéo courant, sans
commande libre et sans changement du moteur. Cinq entrées et manifeste épinglés,
35 tests Python PASS ; trois contrôles synthétiques d'identité de processus PASS.
Premier lancement refusé par le runtime (capture fixe + probe pont interdits) ;
rapport FAIL conservé dans `r1-shadow-caster-capture-20260909`, processus sortis.
Collecteur corrigé pour ne demander que la vidéo et fermer uniquement le processus
lié au chemin exact `--bridge-frames` de l'essai.

Acquisition suivante PASS : `r1-shadow-caster-capture-20260909-b/candidate_frame286.rdc`,
863158026 octets, SHA-256
`f03bbea613b8514af22fe4e179015e433e1f76dad790b3f2c0d24d00535e4cd5`.
Runtime release `4c852a2a…` et kit recadré courants, pas un candidat shader rejeté.
Déclenchement après readback de frame-0162 : **pas une correspondance atomique
avec cette frame PNG ni une paire avant/après**. PID runtime 26660 sorti, outil
21780 sorti ; host-result PASS, profils jetables archivés, garde mémoire actif.

Replay isolé PASS dans `r1-shadow-caster-replay-20260909` : Vulkan non dégradé,
un Present, 1073 draw leaves, 3217 instances soumises et 6338534 triangles soumis.
Ce sont des comptes de capture incluant les répétitions entre passes, pas objets
uniques, FPS ou qualification Gate 1. Aucune durée GPU demandée.

Limite observée : pas de marqueurs nommant les cascades dans ce binaire release.
Des draws ont les mêmes nombres d'indices que les primitives sources LOD0 :
15114/44508 (bois chêne/conifère), 19182/48966/19212 et 21060/52746/21210
(feuillages). Exemples EID1399 : 15114, Vertex seul ; EID1403 : 19212,
Vertex+Fragment ; EID1432 : 19182, Vertex+Fragment. Une égalité de taille ne
constitue pas encore l'identité du caster. **Suite : relever cibles de profondeur,
shaders et ressources de ces draws dans cette capture existante avant une nouvelle
modification du renderer. Pas de nouvelle capture nécessaire à cette inspection.**

#### Variante incluant les passes opaques : rejetée

Après l'essai alpha seul, variante ajoutant un fragment de rejet aux passes
d'ombre sans fragment, tout en conservant les fragments alpha existants et
excluant normales/motion/deferred. Test de spécialisation PASS, build dev PASS.
Assets lus sans modification : bois `OPAQUE`, feuillages `BLEND` dans les GLB ;
ne pas qualifier ces derniers de masques alpha sans observation de l'adapter.

Un replay `r1-shadow-opaque-fade-motion-20260909` : 271 images Full HD, 107
pipelines prêts, warmup 186, zéro ERROR, traversée/encodage PASS. **Images 161/162
inspectées : saut toujours présent. Variante retirée**, helper/appel/test et
fragment WGSL propres à l'essai supprimés, changements antérieurs conservés.
Processus fermés, absence vérifiée. Cache debug contenant encore le candidat
rejeté, ne pas livrer sans reconstruire. Aucun gain visuel ou FPS démontré.

Binaire conservé `r1-shadow-opaque-fade-build-20260909/runtime.exe`, SHA-256
`9c30026acc6c1e7984144180a59005835ca31ba5da11c250d6acddc89c1d1c76`.
Vidéo `403de1e24e8fa863eda5bb75c46572141ad5c5550d903b275f6da8f5e6177bb7` ;
log `4385c8f71d5d1e4c848854149095a45d0691186abac2c456b3e36de9c48fa0c7`.
La causalité n'est pas démontrée par les seules clés de pipeline. **Prochaine
action : identifier le draw/caster GPU de l'ombre apparue avant une nouvelle
modification. Ne pas rejouer les deux variantes de fondu inchangées.**

#### Diagnostic du fondu des ombres et correction de portée de l'audit

**Verdict final de cet essai : rejeté et code candidat retiré.** Binaire dev
`r1-shadow-dither-build-20260909/runtime.exe`, SHA-256
`86ce240353a10c53feb6410f400b36bb9b4acab7293a7bd7e7190b2160748a82`.
Replay `r1-shadow-dither-motion-20260909` : 271 images, traversée/encodage PASS,
107 pipelines prêts après 189 frames, zéro ERROR. Images 161 et 162 inspectées :
le saut persiste. Vidéo SHA-256
`2c8f854985590d8243ca0be55d4120fb8ab707c934e8e3a470ffe5fea49e6978`.
Sources conservées, processus fermés et absence vérifiée. Pas de benchmark.
Helper, appel et test propres à cet essai retirés sans toucher aux changements
antérieurs. Le binaire debug en cache contient encore le candidat rejeté : ne
pas le livrer sans reconstruire. Le release `r1-bridge-readiness-build-20260909`
reste la base précédente. **Ne pas répéter cet essai inchangé.** La suite doit
traiter les casters effectifs, notamment les géométries opaques exclues de ce
correctif, et non supposer qu'un test de flags prouve un fondu GPU.
Les paragraphes suivants décrivent la tentative avant son rejet.

Source locale Bevy 0.19.1 inspectée : `render/light.rs` construit la clé du
shadow pipeline sans `VISIBILITY_RANGE_DITHER`, contrairement aux prépasses
et au rendu visible. Le shader standard `pbr_prepass.wgsl` sait rejeter les
fragments selon ce fondu si la définition est présente. La géométrie Forge
fournit déjà le niveau de fondu depuis `view.lod_view_world_position`.
Correctif candidat dans la spécialisation du matériau Forge : transmettre
la définition aux deux stages des prépasses alpha-testées dotées d'un fragment.
Pas de modification des sources Bevy, distances, cartes, assets ou nombre de
casters. Bois opaque et pipelines sans fragment inchangés. Rendu du candidat
non encore vérifié ; les ombres des LOD1/2 restent désactivées, limite distincte.
Test `masked_shadow_dither_is_paired_idempotent_and_does_not_change_other_passes`
PASS : définition présente une fois dans les deux stages, passes non concernées
et absence de fragment préservées. Ce test CPU ne compile pas le shader GPU.

Attention : le journal réel du replay indique `detail_fade_start_m=24` et
`detail_fade_end_m=32`. Les bandes 64–72 de l'ancien audit sont celles du profil
procédural, **pas la transition proche effective des prototypes gouvernés**.
Les comptes historiques restent des mesures de racines à ces distances ; ils
ne qualifient pas le fondu proche de ce kit. Le collecteur doit utiliser les
plages effectives avant toute nouvelle qualification de cette transition.

Recherche primaire : [VisibilityRange Bevy](https://docs.rs/bevy/0.19.1/bevy/camera/visibility/struct.VisibilityRange.html)
et [incident upstream 23991](https://github.com/bevyengine/bevy/issues/23991).
Cet incident upstream est fermé ; ne pas l'attribuer automatiquement au défaut
Forge ni mettre à jour la dépendance sur cette seule ressemblance.

#### Replay du correctif terminé

Release construite (7 min 20 s), binaire conservé dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-readiness-build-20260909/runtime.exe`,
SHA-256 `4c852a2a592043a67d664fd1aba7136f4aef7ee6e137653f1e77a6780f4df131`.
Un seul replay dans `r1-bridge-readiness-motion-20260909` : départ différé jusqu'à
223 frames, 107 pipelines prêts, zéro pending/failed, huit observations stables.
271 PNG Full HD, 9,033333 s à 30 images/s imposées, traversée et encodage PASS,
sources épinglées conservées. Processus runtime/collecteur/encodeur terminés ;
absence vérifiée par chemin de commande. Aucun benchmark déduit.

Frame 0 inspectée : scène complète visible, plus de départ noir. Frames 72/73
inspectées : ancien saut absent à cet endroit. Mais frames 161/162 montrent
encore une apparition brusque d'ombre au débouché du pont : **stabilité des
ombres non résolue**. Frames 23/24 inspectées aussi (entrée sur le pont).
Analyse numérique des 271 images sur rectangle (500,365)-(1450,950) : luminance
moyenne minimale 76,52/255, premiers quinze échantillons ~107 ; diagnostic
seulement, pas verdict de qualité de chaque pixel ou absence globale de popping.

Inspection du code : `vegetation/prototype_projection.rs` marque tous les LOD
autres que 0 `NotShadowCaster`, règle confirmée par son test de profils ; les
batches lointains sont aussi exclus des ombres. Cette coupure est une piste
concrète pour les apparitions pendant l'approche, pas encore une causalité GPU
démontrée. Ne pas ajouter un délai de démarrage pour masquer ce défaut distinct.

Vidéo SHA-256 `5bdc356ac34e88a1cc6de7a22c2304d23f813c663d7ee17640946b490838b192`.
Log SHA-256 `def749433ecf25bf35e4b9aa4d2633f2f08a620dc3bd24b90b854a5f6557ce82`.
La suite ci-dessous conserve le diagnostic et les vérifications antérieurs au replay.

La revue supplémentaire de la séquence ci-dessous contredit une qualification
visuelle globale : frame 10 noire hors overlays, frame 11 avec ciel seul, scène
visible frame 12 ; entre 72 et 73, une ombre apparaît brusquement sur le pont.
Cause de ce dernier saut non établie. Vidéo et manifeste originaux conservés :
`video_checks_passed_review_pending` attestait encodage et traversée, pas qualité.

Cause du départ prématuré confirmée dans le recorder : 120 frames principales
et kit prêt suffisaient, sans observation du cache de pipelines. Correctif
implémenté : réutilisation de `CaptureReadinessPlugin`, fenêtre non vide et kit
prêt, huit observations graphiques nouvelles et stables sans pipeline en attente
ou erreur avant de libérer la traversée. Watchdog 180 s conservé. Événement
`bridge_recording_render_ready` enregistré et exigé par le collecteur vidéo.
Ce garde ne garantit pas la disponibilité de variantes futures ni chaque pixel.

Quatre tests Rust du recorder PASS (cache absent/répété/en attente/en erreur,
kit, callbacks et délais), trois tests Python du collecteur PASS avec refus de
preuve graphique absente ou incomplète. Pas encore de nouvel exécutable graphique
ni de vidéo du correctif : disparition du démarrage noir et saut d'ombre non
qualifiés. Aucun critère de Gate 1 fermé.

Non-régression : quatre tests `capture_readiness::tests` PASS et
`cargo fmt --all -- --check` PASS. Le collecteur actuel refuse effectivement
la séquence historique avec `expected one bridge_recording_render_ready, got 0`
(code 1 attendu), sans modifier ses artefacts.

### Séquence native de traversée — 9 septembre

Réutilisation du recorder existant, kit recadré, forêt batches, profil High :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-atlas-cropped-bridge-motion-20260909/`.
271 PNG Full HD consécutifs, vidéo 30 images/s imposées de 9,033 s ; traversée
physique réussie, colliders et sources préservés, sortie runtime sans ERROR.
Encodage/décodage ffprobe concordants. **Capture hors temps réel, aucun FPS acquis.**
Cette séquence emploie l'eau de référence et les ombres High, pas la combinaison
optique/ombres du benchmark natif ou du paquet Studio : elle qualifie uniquement
la production de preuve de mouvement avec le kit, pas leur parité globale.

Vidéo `bridge.mp4`, SHA-256
`39CD60137E96A52C2B780967DCBDCCD53A03D7D0C60BBDB1B9E2E5903929A5D0` ;
log `203CEF5E5DB37262A3F45782D11C3D3C76A0E69D9A89A85DB28049D4F8589851`.
Manifest avec hashes de chaque frame et des entrées ; audit complémentaire
`lod-audit.json` produit par `Analyze-BridgeLodTrace.ps1 -RequireScreenProjection`.
76 racines traversent entièrement la bande logique 64–72 m (62 ont un point
racine projeté dans le champ pendant la bande), 181 la bande 172–180 m (153).
Ces racines ne prouvent ni visibilité des couronnes, ni absence d'occlusion,
ni choix GPU du LOD. Ne pas convertir ces comptes en transitions validées.

Frames 191, 215, 238 réellement inspectées pour la racine centrale
`8cf40834-2fcb-524d-9593-9584cf8c9797` : distances 71,95 / 67,96 / 64,14 m.
La forêt reste présente sur ces échantillons ; la comparaison espacée ne prouve
pas l'absence de popping entre toutes les frames. Le panneau de métriques masque
une partie haute gauche de l'image, limite de cette preuve conservée. Vidéo
complète non déclarée revue/acceptée. Aucun critère de transition fermé.
Processus runtime/encodeur terminés, aucun processus portant cette sortie restant.
Ne pas réenregistrer ce parcours inchangé : exploiter la vidéo existante pour la
revue temporelle, puis corriger uniquement un défaut observé.

Le candidat distinct `r1-atlas-cropped-runtime-kit-20260909` réutilise les mêmes
textures v4 et triangles que le premier candidat, mais recadre chaque carte sur
les texels alpha non nuls avec une marge d'un texel. Aucun seuil alpha relevé,
aucun arbre retiré : correspondance affine positions/UV conservée. Surface
cumulée des cartes réduite de 71 % (chêne) et 68 % (conifère) ; ce n'est pas une
mesure des pixels GPU. Sources, LOD0/2, bois et collisions restent inchangés.

Six tests CPU PASS, deux validations Khronos sans erreur (neuf warnings source
par GLB conservés). Capture native 1920×1080 inspectée ; 1662 arbres présents.
Pas de nouveau rendu Blender pour cette variante, ni de validation humaine.

| Passage | FPS trajet | FPS forêt | p95 forêt ms | 1 % low forêt |
| --- | ---: | ---: | ---: | ---: |
| Original A | 65,21 | 44,98 | 25,02 | 37,51 |
| Recadré B | 74,00 | 53,09 | 21,73 | 43,94 |
| Recadré B | 73,95 | 53,02 | 21,70 | 44,70 |
| Original A | 64,20 | 44,61 | 25,13 | 38,50 |

Même release, RX 7800 XT, Vulkan, Full HD, marche 1800 ticks, ombres candidates
2048/trois cascades, eau/reflets constants. 1428 ticks communs, écart position
maximal 7,53 mm et boom 1,882 mm ; pins inchangés. Gain forestier répété ~18 %.
Les quatre processus sont sortis avec code zéro et fermés. **Candidat conservé
pour qualification, pas kit courant remplacé** : forêt encore sous 60 FPS,
mips, transitions, Studio, GPU cible et acceptation artistique non qualifiés.

Preuves sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :

- `r1-atlas-cropped-runtime-abba-20260909/comparison.json`, SHA-256
  `B11B9FB321FA16FCE173A3421FF396E63A77CE6897126870D1EC508F785E695A`.
- `r1-atlas-cropped-runtime-capture-20260909/capture.png`, SHA-256
  `C0B5282D496345955AAC3E0DD99E82CFC3E941DAFFC8D9424F1AC8CC773DD58A`.
- `r1-atlas-cropped-runtime-kit-20260909/atlas-kit-proof.json`, SHA-256
  `C5044F85F6F5D71C2E704247B04A14CC55CA983E44FEDE96BAC839432A80C050`.

Le verdict négatif ci-dessous concerne uniquement la variante **non recadrée**.
Ne pas répéter son benchmark ni confondre les deux identités de kit.

### Paquet Studio recadré — construit puis lancé

`Build-StudioCandidate.ps1` : entrées validées (14 fichiers), puis build Tauri
terminé avec code zéro en réutilisant le frontend milieu optique existant,
WASM `43029A051371B42DC6C1036098090BA9D4BB5AA44B0672A90C859A9780923089`.
Sortie séparée `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-atlas-cropped-20260909/`.
`build-proof.json` épingle frontend et kit, statut `BUILD_ONLY` ; copies et
sources contrôlées après build. Entrée vallée gouvernée, forêt batches et
`clear-medium-preview`, sans vue figée. Aucun ancien paquet remplacé.
Le contrôle initial était limité au build. Rejeu réel du 9 septembre à 14:04
locale : lancement Tauri, initialisation unique, kit 20/20, 36 colliders,
1662 arbres et 16620 primitives observés. Capture exportée depuis le bouton
Studio et inspectée, 1408×966 : feuillage présent, pas de texture manquante
évidente ; sol vert/ocre, ciel uniforme et silhouettes encore stylisés.
Ce cadrage de départ n'est ni un parcours forestier ni une validation artistique.

Dans le même dossier, fichiers copiés depuis Downloads, hashes des copies vérifiés :

- `forge-diagnostic-1788955588200.json` :
  `9AA2422B131905718CA85C44D8F7FB5C1E9D9CF28100CEDCB2880154228B77E4`.
- `forge-viewport-1788955600871.png` :
  `8DACF8ABB36946BBDED46FFFEBDB5202DFBCB15B892E13DC2E6A96C1C21D7572`.

Préparation 43 660,6 ms, 94 pipelines, zéro attente/échec final, pic transitoire
de 18 erreurs conservé. Diagnostic et PNG non atomiques. Le diagnostic garde
`unresolved_capabilities`, `full_preset_applied=false` et présentation GPU non
observée : le PNG prouve séparément un affichage, pas la complétude du profil.
Plan d'ombres 4096/quatre cascades, distinct du benchmark natif 2048/trois.
120 échantillons de boucle WASM au démarrage : moyenne 25,366 ms, p95 27,9 ms,
1 % low 34,07 FPS ; non Full HD, sans déplacement, **pas un benchmark de gate**.
Mémoire linéaire WASM 1 751 187 456 octets, pas une mesure VRAM.

Fermeture par Alt-F4 ; absence vérifiée des PID 23968, 8688, 33556, 27404,
18240, 31012, 28048 et 14660. Aucun processus utilisateur fermé.
**Restent non qualifiés : transitions/mips, performance Studio en déplacement,
profil complet et acceptation humaine.** Les mesures natives ci-dessus ne sont
pas des mesures Studio. Ne pas répéter ce cadrage inchangé comme preuve de gain.

### Diagnostic du profil incomplet après le smoke

### Candidat combiné feuillage et reflets — 9 septembre, 14:14 locale

Paquet distinct construit puis lancé :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-atlas-reflections-20260909/`.
Frontend de `r1-studio-forward-water-20260909` réutilisé, WASM
`E8BE76136F003CE1941DE83AEA3B014C99718BE514594DB558ED56E29AD980F3` ;
EXE `1A106E846BEAC7B8DD2551B3D29010FFAA212CFBE3E739E190606B472A692227`.
Entrée explicite `clear-reflections-preview`, kit atlas recadré inchangé.
Ce frontend ne contient pas encore la correction textuelle de statut ci-dessous.

Exports produit copiés et hashes contrôlés dans le dossier du paquet :

- `forge-diagnostic-1788956127373.json` :
  `2486424F48EC4845020A5D09C778DBEF8B9C3379C1CA0DA884E6D5AE82B9B0D8`.
- `forge-viewport-1788956139931.png` :
  `658CAF683F414F0CD4D07A56016AC22BFE9053A846B97280FF0020D0AC30CF8B`.

1662 arbres / 16620 primitives et 36 colliders, une initialisation. Observation
de réflexion transmissive 48 pas / 96 m, réflectance 0,360, `projection_matches`
et `shader_uniforms_match_profile` vrais ; pas de SSR différé caméra. Préparation
42 818,4 ms, 94 pipelines, zéro attente/échec final, pic transitoire 18 conservé.
PNG 1408×966 réellement inspecté : vallée et forêt affichées, eau trop lointaine
pour qualifier les reflets rapprochés, aspect encore stylisé. Aucun gain FPS Studio
ou passage de gate déduit ; ombres de référence, pas le budget natif 2048/3.

Pins du bundle inchangés après essai. Alt-F4 puis absence vérifiée des PID 24084,
31544, 33396, 28068, 33220, 9432, 3912, 22052. Aucun onglet ni processus de cet
essai laissé actif. Ce paquet combiné remplace les deux candidats séparés comme
base de la prochaine qualification, sans écraser leurs fichiers ni qualifier le
kit courant. Prochaine preuve utile : mouvement/transitions et vue d'eau proche,
pas une nouvelle capture identique du point de départ.

### Cause du statut incomplet

### Eau rapprochée : paire WebGPU avec/sans reflets écran

9 septembre, 14:19 locale. Paquet `r1-studio-atlas-water-close-20260909`, même
frontend/kit combinés, caméra candidate `water_close` [-48,9,-145] vers
[-55,2,-163], FOV 58°. Première construction refusée car vues absentes du
frontend ; aucun fichier produit par ce refus. Copie frontend dédiée, ajout des
vues déjà utilisées dans le lot milieu clair, vérification document identique
hors caméras avant construction. SHA-256 des vues
`74E6EAAB1488C079B5FEFF228CFBE9ABBB24F695453D8F632B40E0919871507D`.

Deux JSON et deux PNG téléchargés par l'interface, copiés dans ce paquet et
contrôlés par hash, originaux préservés :

| État | JSON | PNG |
| --- | --- | --- |
| Reflets écran | `forge-diagnostic-1788956409927.json` | `forge-viewport-1788956418913.png` |
| Environnement seul | `forge-diagnostic-1788956451448.json` | `forge-viewport-1788956462647.png` |

Hashes JSON : `BEB709236DBB847E8063C71BEF83587CE4A64AF9D34120C3B385FDBCAC98E89B`,
`8CFAE4A3BBE3AEF3658BD4E54E571109F147A0962C684269E01C7C4D1F9712C8`.
Hashes PNG : `2031CF19615BEE6C6D08A9D59A803B6C906039EA55E42A6FD85191E64DB8CFDF`,
`7A4392AF67A642A0FF3AD33C55FFD189A5FD73AD3D24634323A8D804A9D09ECE`.

Paire contrôlée : scène/manifestes/source et caméra identiques, temps virtuel
0 s, budget 48 pas/96 m puis 0/0, réflectance 0,360 constante, projections
concordantes. Les deux PNG 1408×966 ont été inspectés : reflets des arbres
visibles puis absents en repli, fond visible, contact sans l'ancienne bande
séparée dans ce cadrage. Berges encore anguleuses, horizon plat et aspect
stylisé ; aucune acceptation artistique déduite. Pas de mesure temporelle ni
de FPS de qualification, pas de cycle de restauration revendiqué ici.
Préparation 32 589,1 ms, 74 pipelines, zéro attente/échec final, pic transitoire
18 conservé. Les exports JSON/PNG restent non atomiques.

Pins du paquet inchangés. Fermeture Alt-F4, absence vérifiée des PID 9356, 32880,
32108, 29208, 25864, 30700, 31028, 5052. Cette paire apporte la preuve visuelle
rapprochée manquante au smoke précédent ; ne pas la répéter inchangée. Restent
stabilité en mouvement, transitions forestières, profil complet et revue humaine.

### Cause technique (suite)

La lecture du projecteur confirme que `stats.quality` est volontairement absent
en `optical_depth_preview` : les observations du prototype passent séparément par
`forward_reflection_quality`. Ce n'est pas un acquittement perdu ou un calcul en
attente. La qualification globale exige encore de raccorder un contrat optique
complet ; ne pas remplacer ce champ par une observation du SSR différé.

Le premier paquet atlas utilisait le frontend milieu clair, antérieur aux
commandes de réflexion transmissive déjà qualifiées partiellement dans
`R1_WATER_FORWARD_REFLECTION.md`. Le builder accepte maintenant l'option explicite
`-WaterReflectionPreview` (route `clear-reflections-preview`) ; les trois modes
d'eau sont mutuellement exclusifs. Sept cas d'entrée et onze refus PASS, y compris
les trois paires et le triplet incompatibles ; aucune qualification graphique
déduite de ces fixtures de packaging.

Le statut n'est pas un travail de compilation toujours actif :
`global_quality/effective.rs::capabilities` conserve explicitement la végétation
à prototypes en `Unqualified`. Le candidat optique ne satisfait pas tous les
champs requis par `global_quality.rs::observed_domains` pour l'eau. Le résolveur
refuse donc honnêtement le profil complet (végétation, reflets environnement et
écran, teinte et réfraction). Ni suppression des blocages ni promotion implicite.

Correction source Studio : le statut principal utilise désormais les capacités
bloquantes du plan au lieu de « application en cours ou domaine indisponible ».
Les libellés sont partagés avec le détail existant ; aucune mutation du plan,
aucun changement moteur, de profil ou d'asset. Cinq tests ciblés de présentation
PASS, dont blocages prioritaires même si les budgets CPU sont projetés et absence
de mutation. Le paquet Studio précédemment inspecté ne contient pas encore cette
correction textuelle ; ne pas lui attribuer une vérification visuelle inexistante.
`cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features
--features web --locked -j 2` terminé avec code zéro ; avertissement de future
incompatibilité de la dépendance `proc-macro-error2 2.0.1` conservé. Format ciblé
et `git diff --check` PASS. Aucun build de distribution ou nouvel essai graphique
pour ce changement de message, aucune capacité renderer déclarée acquise.

## Verdict runtime historique : premier candidat non retenu

Le kit séparé `r1-atlas-runtime-kit-20260909` a été assemblé, chargé dans le
release natif existant `2C507A416D46FD39835715DB18022CC845CAD3E190FFAC40353CD92CBFB15BCA`,
puis mesuré en marche ABBA. Aucun code moteur ou profil n'a changé. Quatre assets
non arborés sont identiques, deux GLB remplacés uniquement dans cette copie.
Capture Full HD inspectée : 1662 arbres présents, 16620 primitives LOD préparées,
pas de texture manquante évidente. Ce cadrage ne qualifie pas les transitions.

| Passage | FPS trajet | p95 trajet ms | 1 % low trajet | FPS tiers forestier | p95 forêt ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ancien A | 64,59 | 24,31 | 37,77 | 44,78 | 25,92 |
| Atlas B | 62,74 | 26,34 | 35,06 | 40,63 | 27,82 |
| Atlas B | 62,45 | 26,40 | 35,43 | 40,46 | 27,88 |
| Ancien A | 64,25 | 24,38 | 39,16 | 44,80 | 25,24 |

**Régression répétée, candidat non retenu.** Ne pas présenter la réduction de
triangles comme un gain ni promouvoir ce kit. Les grandes cartes transparentes,
leurs surfaces vides et le filtrage sans mips sont des pistes à examiner, pas une
attribution GPU démontrée. Ne pas répéter ce comparatif sans changement pertinent.

RX 7800 XT, Vulkan, 1920×1080, parcours vivant 1800 ticks `1:0.22`, mêmes sept
demandes High, ombres candidates 2048/trois cascades, eau optique/reflets constants.
1285 ticks communs, écart position max 7,53 mm, boom caméra max 1,882 mm, yaw/pitch
identiques. `Analyze-FoliageAtlasAbba.py` recalcule les métriques et tous les tiers
fixes, vérifie pins, fenêtre, ombres et eau (seul compteur `animation_updates`
exclu). Rapports Rust concordants à 1e-8. Pas de GPU cible, Studio ou art qualifiés.

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-atlas-runtime-abba-20260909` :
quatre sous-dossiers avec arguments, pins, logs, report et process ; `comparison.json`
SHA-256 `71FBB6F9E7501A40CA04B63FC66B162085F2319923E6DB12B9BEFF6921AD479E`.
Capture `r1-atlas-runtime-capture-20260909/capture.png` :
`501DE6A24D5EC5F1969CABAF502502219285540B9238194C628E2A23DA88B293`.
Kit build : `2A7692100F6D1312CBC6273844F0DC5AB89853BB6D681DF7B392775E2DEA084F`.
PIDs 28704 (capture), 12272/33252/33456/30288 (ABBA) terminés et absence vérifiée.
Le kit courant reste inchangé, aucune publication.

### Conversion PBR corrigée avant cette mesure

Le test de couches transparentes a reproduit le défaut indépendamment des arbres :
quatre couches alpha 0,5 donnent un échantillon de radiance blanche 0,9375 mais
alpha 0,90625. Diviser par cet alpha au stockage PNG déforme les valeurs du
matériau ; avec 16 couches, la radiance peut dépasser 1 et serait écrêtée en PNG.
Les valeurs de radiance et d'alpha ne sont donc pas des données PBR interchangeables.

Le worker conserve maintenant trois EXR flottants 32 bits puis normalise leurs
RGB par la référence d'émission unitaire du canal R ORM, avec couverture strictement
identique entre canaux. Ce n'est pas un réglage de rugosité ou un clamp correctif.
`foliage_bake_pixels.py` encode ensuite la couleur en sRGB, les données en linéaire,
et retourne les lignes Blender bas→PNG haut. L'alpha n'est pas modifié.
Quatre tests CPU PASS (normalisation, référence partagée, PNG/sRGB/orientation,
refus) et trois cas réels 1/4/16 couches PASS sans élargir la tolérance 2/255.
`r1-orm-layers-20260909-v2/results.json` :
`ACDC215A93CF9A5D56931D4D24D6D6154325200CA01825A1788F446632C39188`.
Les premiers cas PNG en échec restent conservés.

Atlas complets v4 générés (~19/~16 s), sources intactes ; contrôle PNG indépendant :
R opaque exactement 255 et B exactement 0 pour les deux espèces, G 178–224 / 192–232,
normales dans l'hémisphère attendu. Les GLB v2 issus de ces atlas passent Khronos
(zéro erreur, warnings des primitives source conservés) et sont ceux du kit mesuré.
PIDs Blender 24172/4032 terminés ; le petit test de couches a également quitté.
Cette correction de fabrication est conservée, **pas le verdict de performance
du LOD**. La fidélité des normales double-face et des transitions reste à qualifier.

## Complément : deux GLB LOD1 dérivés et réouverts

`Build-FoliageAtlasLod.py` remplace uniquement le mesh de feuillage LOD1 dans une
copie GLB. Toute la zone binaire source reste un préfixe identique ; les ressources
des atlas et des cartes sont ajoutées. Tous les autres champs JSON sont comparés
à l'original après retrait des ajouts autorisés. LOD0/LOD2, bois, collisions,
transforms et placements ne sont pas retouchés. Refus des arbres authorés du
manifeste, hashes divergents, nœuds transformés/partagés et sorties existantes.

| Source | Triangles feuillage LOD1 avant | Candidat | Sommets candidat |
| --- | ---: | ---: | ---: |
| Chêne | 7462 | 1626 | 3252 |
| Conifère | 5992 | 780 | 1560 |

Export déterministe répété en mémoire, conversion Blender Z→glTF Y explicite,
normales et tangentes fournies, RGB étendu de quatre texels sans modifier alpha
ni pixels couverts. Quatre tests CPU PASS : conservation des pixels, isolation
des tuiles, axes/UV/winding/tangente, refus d'une base gauche. Aucun code moteur
ou dépendance Rust ajouté. `Verify-FoliageAtlasLod.cjs` : Khronos 2.0.0-dev.3.10,
zéro erreur pour chaque GLB ; neuf warnings chacun sur les primitives **inchangées**
sans tangentes, informations de ressources source devenues inutilisées conservées.

`Inspect-FoliageAtlasLod.py` : deux réimports source/candidat et quatre rendus
512×768 inspectés, même caméra par paire. Comptes de triangles concordants.
Les candidats gardent une silhouette arborée mais les cartes horizontales et
différences de densité sont visibles : **pas de parité artistique acceptée**,
distance/transitions et coût runtime non vérifiés. Sampler linéaire explicitement
sans mips pour ce candidat ; stratégie mip/LOD à qualifier avant promotion.

Les atlas v3 ajoutent roughness/metallic via les entrées du matériau source ; les
sources à occlusion explicite sont refusées. Attention : un contrôle numérique
supplémentaire du canal R attendu neutre a échoué sur les PNG opaques (minimum
218 chêne / 227 conifère, au lieu de ≥253 sur 255). Le canal R n'est **pas lié à
une occlusion** dans le GLB candidat, mais cela interdit de qualifier l'extraction
PBR comme numériquement exacte. Cause à isoler avant promotion, sans masquer ou
affaiblir ce contrôle. Plages G observées 166–225 / 181–232, B 0–1. Ces rendus
de matériaux restent approximatifs, malgré zéro erreur structurelle glTF.

Preuves locales : atlas `r1-full-atlas-{oak,pine}-20260909-v3`, dérivés
`r1-atlas-lod-{oak,pine}-20260909`, réimports/captures
`r1-atlas-lod-inspect-{oak,pine}-20260909`, tous sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/`. Rapports `atlas.json`,
`derivation.json`, `validation.khronos.json`, `inspection.json` ; logs adjacents.
SHA-256 GLB chêne `DD5CD6479E88F8F7515330C58A3D96DE01620071F10F1D5428FB542F5AD24D3A`,
conifère `E64FB36518CA2CA0E3E0A0FDAB688B58B2BD020A63B0B5E742AC98A0018F6D0B`.
Sources recontrôlées inchangées. PIDs 33756/9328 (atlas) et 26252/28192
(inspection) terminés et absence vérifiée. Aucun kit courant, index Vault ou
Studio remplacé, aucune publication, aucun gain FPS ni critère de gate promu.

## Complément : atlas complets couleur/alpha et normales

`Bake-FoliageAtlas.py` fabrique désormais toutes les vues des groupes, dans un
mesh temporaire projeté par tuiles : **deux rendus par arbre**, pas un rendu par
groupe. Les deux exécutions Blender 5.2.1 ont terminé avec code 0. Les quatre
PNG ont été ouverts et inspectés : groupes feuillus distincts, espace inutilisé
transparent, aucune nouvelle silhouette d'arbre ou vue runtime encore produite.

| Arbre | Groupes | Vues dans chaque atlas | Taille par carte | Durée Blender observée |
| --- | ---: | ---: | ---: | ---: |
| Chêne | 271 | 813 | 2048 × 2048 | ~13,5 s (v2) |
| Conifère | 130 | 390 | 2048 × 2048 | ~12 s (v2) |

Cycles CPU, deux threads, 16 samples, deux processus séquentiels, watchdog externe
180 s chacun. PIDs initiaux 3136/30876 et corrigés 3068/15172 terminés. Aucun navigateur,
Runtime ou build Rust lancé. Les deux GLB sources sont inchangés par SHA-256.

`foliage_atlas_layout.py` refuse un dépassement du budget de 2048², sans réduire
silencieusement la résolution. Tuiles de 64², contenu 56² et marge de quatre
pixels. Trois tests CPU PASS : comptes réels/budget mémoire, absence de
chevauchement/correspondance groupe-axe, refus des budgets et entrées invalides.
Les quatre tests de partition ont aussi été rejoués avec succès. Chaque face
source figure exactement une fois dans la partition, puis dans chacune des trois
projections. Le worker vérifie les pixels finis, les marges externes transparentes
et la présence de chaque groupe. Aucune vue vide observée : alpha > 0,1 sur
40 à 885 pixels par vue chêne, 40 à 762 par vue conifère.

Budget **théorique**, pas mesure VRAM : RGBA8 16 Mio par carte au niveau principal,
22 369 620 octets avec chaîne complète de mips (~21,33 Mio). Les deux cartes
actuelles représenteraient ~42,67 Mio par arbre avec ces mips. Les PNG ne
contiennent pas cette chaîne ; elle n'est pas générée ou qualifiée. L'ajout d'ORM
porterait ce budget à ~64 Mio par arbre. Ne pas générer 512² par groupe.

Normales : `foliage_cluster_materials.py` conserve le graphe NormalMap source ou
les normales de coins du mesh, puis encode dans la base droite/haut/avant de la
carte, en RGB linéaire `0.5 N + 0.5` (lecture Non-Color). Le mesh temporaire
préserve les UV, indices de matériaux et normales de coins transformées. Le
retournement vers l'hémisphère de la vue reste une approximation double-face ;
une normale interpolée devra être renormalisée au rendu. Les normales ne sont
pas des couleurs sRGB.

Les cinq rendus synthétiques de `Test-FoliageClusterNormals.py` passent dans
`r1-cluster-normal-fixtures-20260909-v4/results.json` (rejeu sans débruitage) : face avant, arrière,
normale liée au matériau, angle rasant et caméra tournée, erreur ≤ 2/255.
Les deux premiers essais avaient une référence de test erronée (roulis réel de
la caméra ignoré) ; la référence a été corrigée par projection dans la base réelle,
sans élargir la tolérance. Les essais historiques sont conservés. Avertissement
de dépréciation `Material.use_nodes` pour Blender 6 dans cette fixture, pas un
échec sous 5.2. Les tests de normales ne qualifient pas un futur matériau Bevy.

Correction après contrôle numérique des premiers atlas : sept pixels opaques
du conifère avaient B < 126/255 (minimum 117), hors hémisphère encodé. Les réglages
factory de Blender ont été interrogés : débruitage et échantillonnage adaptatif
actifs. Ils sont désormais explicitement désactivés pour les cartes de données,
sans augmenter les samples ni corriger/clamp les pixels après coup. Les deux
arbres ont été rejoués en sorties neuves `-v2`, sans changer sources ou seuil.
Le contrôle est maintenant intégré au worker. Vérification indépendante des PNG
avec Pillow/NumPy : minimum B opaque 136 chêne / 139 conifère ; hashes sources,
scripts et PNG tous concordants. Les quatre PNG v2 ont été inspectés. Les cinq
fixtures de normales ont également été rejouées (code 0, Blender fermé).
Le passage ~74–76 s à ~12–14 s concerne la fabrication CPU, pas les FPS du jeu.

Preuves corrigées (rapports contenant également hashes des scripts et PNG) :

- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-full-atlas-oak-20260909-v2/atlas.json`
  SHA-256 `481C3376542E1A4E438BD9B370910A5DC1B1AB7BB01A979DA723C9EAD5267BBC`.
- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-full-atlas-pine-20260909-v2/atlas.json`
  SHA-256 `B1820320D6EC1B97093BFA1446A30A2B94B2647254499E8E6C0D41B53C91A9BC`.

Premiers essais conservés, remplacés par v2 pour la suite d'intégration :

- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-full-atlas-oak-20260909/atlas.json`
  SHA-256 `CB06FDBB5B6F706816FF0FBE743737AA71A8BAD85612C3A380FF62C90C1787DA`.
- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-full-atlas-pine-20260909/atlas.json`
  SHA-256 `4BB7F5D30CFC004A891EB1265D88A3ADE9134E818DEAE8986E3E2DA290BDDCBC`.
- Logs `.stdout.log` et `.stderr.log` adjacents aux deux répertoires.

Ligne de reproduction : Blender `--background --factory-startup --disable-autoexec
--python-exit-code 1 --python scripts/blender/Bake-FoliageAtlas.py -- --source GLB
--sha256 HASH --object-name OBJET_LOD0_FEUILLES --output-dir SORTIE_NEUVE_E`.
Le rapport contient source, hash, nom de l'objet et taille de cellule (1,5 m).
Les bases/centres sont en monde Blender Z vertical ; lignes de tuiles depuis le
bas de l'image. Une conversion Y vertical explicite sera nécessaire pour glTF.

**Suite d'intégration, non accomplie :** dilatation RGB et isolation des mips,
roughness/ORM source, cartes 3D et export/réimport GLB, remplacement du seul LOD
intermédiaire dans un kit dérivé, contrôle du volume/transitions puis comparaison
runtime. Les PNG et tests ne prouvent aucun gain FPS, aucune disponibilité Studio
ni validation artistique. LOD0, bois, collisions et placements du kit actuel
n'ont pas été modifiés. Le protocole d'assets a guidé les sorties dérivées,
empreintes et refus ; aucune importation/indexation Vault ou publication effectuée.

## Implémenté et exercé

Partitionnement déterministe des faces, par arêtes de positions exactes : les
deux triangles d'une feuille restent ensemble malgré des sommets dédoublés par
l'export. Les feuilles voisines ne sont pas soudées par tolérance. Cellule définie
par le centre des positions uniques du composant ; chaque face affectée une fois.
Quatre tests CPU PASS : sommets dupliqués/feuille complète, conservation et
déterminisme, coordonnées négatives, entrées invalides/non finies.

Worker isolé Blender **5.2.1 LTS**, build `9e2066aef7ef`, installation existante.
GLB source épinglé, ressources externes refusées, objet LOD0 et matériaux de
feuillage balisés exigés. Import sans autoexec ; aucune sauvegarde sur l'original.
Copies du mesh et des matériaux pour capturer l'albedo et l'alpha sans éclairage.
Trois axes orthographiques, Cycles CPU/deux threads/16 samples, RGBA 512².
Rapport et octets de chaque PNG liés par SHA-256. Les coordonnées du rapport sont
en mètres dans le monde **Blender Z vertical**, pas dans un document Forge Y vertical.

| Source courante | Faces LOD0 feuillage | Groupes à 1,5 m | Faces du groupe étudié | Vues |
| --- | ---: | ---: | ---: | ---: |
| Chêne | 29120 | 271 | 736 | 3 |
| Conifère | 31672 | 130 | 1052 | 3 |

Deux exécutions terminées, codes 0, sources inchangées, six PNG inspectés : amas
de feuilles et bouquets d'aiguilles visibles selon les trois axes. Pas de nouvelle
silhouette de forêt montrée ou acceptée. Le PNG contient un fond transparent ;
le fond noir du visualiseur n'est pas un matériau ajouté. Les variations de teinte
restent celles des textures du kit, ce n'est pas une correction artistique.

## Preuves locales

- Racine chêne : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-cluster-oak-20260909/`.
- Racine conifère : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-cluster-pine-20260909/`.
- Logs adjacents `.stdout.log`/`.stderr.log`, watchdog 180 s par exécution.
- Rapport chêne : `1395AF4AF02F3FAFD63C64287D873C7ED7020ADED40FB32B56359E71F691DB10`.
- Rapport conifère : `5F26B7B090AE44CA58F2DAFDAAAE7C82FA024B422E4928BF92DA3F6748CAF257`.
- Worker : `F2BEE42FB5159E4D541624AFF3EA9C6A3BCE216BA7FB5D24B03F391BF105AF72`.
- Partition : `7C9EAB272D5A36AD3BFE8844A7BDCD6D75052FB355DAC374C5F4CB3B496973EF`.

Les hashes GLB sont enregistrés intégralement dans les rapports et concordent avec
le `build.json` du kit `r1-texture-colorspace-20260908/runtime-kit`. Provenance de
ce kit : `LicenseRef-OpenRPG-Forge-Project-Generated`, usage de preuve interne,
revue de publication humaine toujours requise. Aucun contenu Cadle/SpeedTree copié.
PIDs Blender 15600 et 25160 terminés ; aucun navigateur ou Runtime lancé.

## Limites de l'étude initiale (dépassées partiellement par le complément)

Cette étude produit **un groupe par arbre**, pas l'atlas complet ni un LOD optimisé.
À ce stade initial restaient les normales et autres paramètres PBR, le packing avec marges et mips,
les cartes 3D conservant le volume et les transitions, les conversions d'axes,
l'export/réimport glTF et l'intégration dans un kit dérivé. Les positions d'arbres,
le bois, les collisions et LOD0 devront rester inchangés. Pas d'indexation Vault
ou disponibilité Studio déduite de ces PNG, pas de gain FPS annoncé.

Une génération de tous les groupes à 512² sans budget n'est pas autorisée par ce
résultat : il faut borner la surface totale des atlas avant de passer à l'arbre
complet. Pas de nouvelle dépendance Rust ni reconstruction moteur pour cette étude.
