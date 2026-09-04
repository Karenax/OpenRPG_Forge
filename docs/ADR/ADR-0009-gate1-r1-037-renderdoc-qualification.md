# ADR-0009 — Qualification RenderDoc bornée de R1-037

Date : 2026-09-03
Statut : **accepté ; qualification locale Windows/Vulkan R1-037 PASS le 4 septembre 2026**
Amendement du 4 septembre 2026 : couche Vulkan autorisée et enregistrée ; reprise sans rebuild et
contrôle du profil QRenderDoc chargé corrigés après le premier lancement réel.
Portée : branche `Rust`, Gate 1, route `R1-037`, Windows x64/Vulkan
Complète : `ADR-0004-gate0-draw-call-telemetry-exception.md`

## Contexte

`R1-037` exige les draw calls, triangles et instances réellement soumis pendant une frame de
`visual_valley`. Les compteurs logiques Forge, les entités ECS, les maillages source, les statistiques
de pipeline et les invocations shaders ne décrivent pas la même chose. ADR-0004 autorisait donc
uniquement Gate 0 à afficher honnêtement l'indisponibilité d'un compteur global ; cette exception ne
ferme pas `R1-037` et ne peut pas être prolongée tacitement.

Instrumenter exhaustivement Bevy/wgpu pour cette seule preuve étendrait le renderer et pourrait
modifier la charge mesurée. Forge retient donc une capture GPU externe reproductible avec RenderDoc,
sans dépendance de production, sans `unsafe` ajouté au runtime et sans copie de l'outil dans le dépôt.

L'acquisition portable officielle RenderDoc 1.46 a été vérifiée hors de tout worktree. Le ZIP et sa
signature détachée sont valides, les binaires cœur sont signés, la licence MIT est conservée et aucune
couche Vulkan n'a été enregistrée. Cette acquisition ne contient aucune capture et ne qualifie encore
ni `R1-037`, ni Gate 1.

## Décision

### Matrice qualifiable

La seule matrice qualifiable par ce protocole est actuellement :

- Windows x64 réel ;
- backend `Vulkan` exact ;
- `visual_valley`, profil `High`, 1920 × 1080, requête de présentation `AutoNoVsync` ;
- météo `clear` puis `rain` ;
- deux lancements indépendants par météo, soit quatre processus et quatre captures au total.

Chaque répétition part d'un nouveau processus. Aucun meilleur run n'est choisi rétrospectivement et
un échec n'est pas masqué par une répétition supplémentaire.

`D3D12` est **NOT QUALIFIED**. Le Browser WebGPU est **UNSUPPORTED BY RENDERDOC 1.46** et ne peut pas
être assimilé à la capture Vulkan native. Toute extension de backend exige une nouvelle qualification
du chemin de capture et une mise à jour explicite de la présente décision.

### Workload déterministe

Avant le déclenchement externe, le runtime doit :

1. charger la composition complète Visual Valley High avec caméra fixe et sources à seeds fixes ;
2. atteindre l'état météo demandé (`clear` ou `rain`) sans adaptation de qualité ;
3. constater les pipelines prêts et stables pendant 30 frames Render consécutives ;
4. remettre les horloges virtuelle et fixe au tick zéro, les figer, puis laisser 30 frames de rendu
   supplémentaires propager cet état déterministe ;
5. vérifier que la caméra, la projection, la qualité, la météo et les hashes des sources gouvernées ne
   changent plus ;
6. écrire atomiquement un reçu prêt avant capture.

Le reçu `forge.gate1-renderdoc-ready` v1 porte exactement le nonce, `process_id`, commit, profil de
build, backend requis, version RenderDoc, frame prête, settle, hash et objet `frozen_state`, contrat de
fichiers et frontière de qualification. `frozen_state` porte exactement les hashes et quatre seeds de
sources, l'objet `tick` (temps virtuel/fixe et tick à zéro, timestep positif),
`vsync_requested=false`, météo,
qualité High fixe, état des pipelines, caméra, fenêtre 1920 x 1080, identité GPU/pilote et hashes de la
scène gouvernée. Le manifeste versionne chaque ensemble de champs exact. L'adaptateur et
l'orchestrateur refusent toute propriété manquante ou supplémentaire ainsi que toute divergence de
nonce, PID, backend, GPU, pilote, horloge, seed ou hash.

`vsync_requested=false` prouve la requête Bevy `PresentMode::AutoNoVsync`, également imposée par
`CaptureOptions.allowVSync=false`; ce champ ne prétend pas observer le mode WSI finalement négocié
par le pilote.

Le hash végétation conserve les sources, placements, projections et tous les compteurs de visibilité,
mais neutralise uniquement `visibility.observation_frames` dans une copie de l'observation. Ce champ
mesure l'âge administratif de l'audit dans le main world et avance même lorsque le temps virtuel est
figé ; il ne décrit pas la scène. La ressource observée n'est pas modifiée. Une variation des comptes
réels, de caméra, de qualité ou d'identité GPU reste un échec.

### Capture externe

Le contrôleur utilise le `qrenderdoc.exe` épinglé et son mode Python. Il lance le processus cible avec
`ExecuteAndInject`, rejoint le Target Control, attend et valide le reçu atomique, puis appelle
`TriggerCapture(1)`. Il accepte exactement une nouvelle capture correspondant à exactement un
`Present` du processus attendu. Le replay compte les actions portant réellement `ActionFlags.Present`
et recoupe ce total avec `present_count` déclaré par l'adaptateur ; les deux valeurs doivent être
exactement égales à un.

Le signal `forge.gate1-renderdoc-capture-signal` v1 lie aussi les SHA-256 exacts de
`qrenderdoc.exe`, `renderdoccmd.exe` et `renderdoc.dll` au ledger Rust. Le contrôleur externe recoupe
ces trois binaires avec le manifeste avant tout build ou lancement.

Le miroir d'exécution n'est pas validé par quatre fichiers sentinelles seulement : les 72 fichiers
du bundle portable, leurs tailles et leurs SHA-256 sont recoupés avant et après copie avec le manifeste
d'extraction externe dont l'empreinte est épinglée dans le manifeste Forge. Les racines Git, bundle,
sortie et target Cargo doivent être disjointes dans les deux sens. Le build Release neuf dispose d'une
borne de 45 minutes, supérieure au temps de référence déjà observé à un job.

Après un échec de capture externe, la reprise peut réutiliser le binaire déjà construit en épinglant
explicitement son commit source et son SHA-256. Le commit source doit être ancêtre du checkout propre ;
le diff Git exclut seulement les documents et scripts RenderDoc, et doit être vide pour tous les autres
fichiers, notamment sources compilées, assets, Cargo.lock et manifestes. Aucun build n'est exécuté dans
ce mode. Les reçus distinguent le commit du runtime et celui de l'outillage ; le binaire est recoupé
avant/après, les quatre captures indépendantes restent requises et les échecs antérieurs sont conservés.

QRenderDoc 1.46 charge `UI.config` avant ses scripts Python et embarque Qt Core 5.15.2. Comme
`AppDataLocation` passe par le Known Folder Windows, le contrôleur miroir le bundle complet, lance
une copie de l'exécutable nommée avec le nonce et précrée son profil jetable avec
`Analytics_TotalOptOut=true`. L'adaptateur confirme le `ConfigFilePath` exact et l'opt-out booléen avant
toute injection. QRenderDoc 1.46 complète et sauvegarde ce fichier avant Python : les hashes seed et
final sont conservés séparément, sans imposer leur égalité ;
le host conserve la configuration finale comme preuve, supprime le profil nonce en `finally` sous
mutex global et exige que l'empreinte du vrai profil `qrenderdoc` soit inchangée. Cette route écrit
transitoirement sous RoamingAppData, sans réutiliser ni modifier durablement le profil utilisateur.

Le reçu final `forge-gate1-renderdoc-adapter/v1` et ses objets `target`, `capture`, `files` et
`qualification` ont eux aussi des ensembles de champs exacts versionnés dans le manifeste. Le host
refuse une propriété manquante ou supplémentaire avant de publier le verdict local.

La capture est invalide si le processus sort avant le reçu, si plusieurs captures apparaissent, si le
nonce ou le PID divergent, si la scène se défige, ou si l'outil ne peut pas identifier sans ambiguïté
le `Present` capturé. Le contrôleur écrit son signal et ses résultats atomiquement dans un dossier neuf
propre au lancement.

Le script d'acquisition Forge ne doit jamais enregistrer, réparer ni supprimer lui-même la couche
Vulkan RenderDoc. Il vérifie seulement son état. L'enregistrement absent au préflight du 3 septembre
a été autorisé séparément par l'utilisateur (« vas y »), puis effectué le 4 septembre avec le binaire
signé épinglé et `renderdoccmd vulkanlayer --register --system` : sortie 0 et `--explain` confirme
l'enregistrement. Seules les deux valeurs RenderDoc x64/x86 ont été ajoutées ; les entrées OBS et
Steam sont conservées. La capture elle-même ne modifie pas ce registre. Cette autorisation ne vaut
pas validation humaine.

Le lancement réel a révélé deux instances de `renderdoc.dll` : celle injectée depuis le miroir et
celle chargée depuis le manifeste système original. Pour les seuls processus de capture non élevés,
`VK_IMPLICIT_LAYER_PATH` pointe maintenant vers le miroir vérifié et
`ENABLE_VULKAN_RENDERDOC_CAPTURE=1` active sa couche. Injection et Vulkan utilisent ainsi la même
copie. Les autres couches implicites, dont OBS/Steam, sont exclues de ce processus de capture ; leurs
clés et les autres processus Windows restent inchangés. Les variables sont restaurées en `finally`
et ce choix est consigné dans le résultat. Un lancement élevé est refusé, car le loader ignore alors
l'override. Source :
[Khronos, Windows Layer Discovery](https://github.com/KhronosGroup/Vulkan-Loader/blob/main/docs/LoaderLayerInterface.md#windows-layer-discovery).

### Replay et enveloppe normalisée

Chaque `.rdc` doit être ouvert avec :

- `LocalReplaySupport == Supported` ;
- `pipelineType == Vulkan` exactement ;
- `degraded == false` ;
- une seule frame capturée, une seule action `Present` observée et `present_count == 1`.

L'adaptateur RenderDoc produit ensuite un document normalisé
`forge-gate1-renderdoc-actions/v2`. Cette version rend obligatoires l'état d'instanciation explicite
et le comptage des actions `Present` ; une analyse acceptée produit
`forge-gate1-renderdoc-analysis/v2`. Le modèle pur versionné dans
`scripts/renderdoc/gate1_renderdoc_model.py` ne charge aucune DLL RenderDoc et n'accède ni au GPU, ni
au registre. Cette séparation permet de tester exhaustivement la sémantique de comptage sans capture.

### Sémantique des compteurs

L'analyse parcourt récursivement les markers et `MultiAction`. Un parent contenant des enfants n'est
jamais compté une seconde fois. Une action contribue à `draws` si et seulement si elle est une feuille
portant `ActionFlags.Drawcall`.

Pour chaque feuille valide :

- le champ normalisé `instanced` doit concorder avec `ActionFlags.Instanced` ;
- un draw réellement non instancié utilise `instanced=false` et le sentinel `numInstances=0`, puis
  contribue une instance effective ;
- un draw instancié utilise `instanced=true` et contribue exactement `numInstances`, y compris zéro ;
  un draw instancié zéro reste un draw, mais ajoute zéro instance et zéro primitive ;
- `TriangleList` contribue `(numIndices / 3) × instances`, uniquement si `numIndices` est divisible
  par trois ;
- `LineList` contribue `(numIndices / 2) × instances`, uniquement si `numIndices` est divisible par
  deux ;
- `PointList` contribue `numIndices × instances` ;
- points et lignes restent séparés des triangles ;
- tous les cumuls utilisent la borne entière non signée 64 bits et échouent en cas de dépassement.

Une capture échoue fermement en présence de strip, fan, adjacency, patch, topologie inconnue,
géométrie, tessellation, mesh/task shader, `MultiAction` feuille non développé, indirect non résolu,
compte indivisible, métadonnées d'instanciation incohérentes, nombre de `Present` différent de un,
divergence entre `present_count` et les actions, identifiant d'événement dupliqué ou overflow. Une
action indirecte est admissible seulement si l'adaptateur a résolu ses comptes exacts avant l'analyse.
La qualification R1-037 exige en outre `draws > 0`, `instances > 0` et `triangles > 0` dans chacune
des quatre analyses ; le host dérive ses booléens de verdict de ces résultats contrôlés.

### Frontière temporelle

RenderDoc ne fournit aucun FPS qualifiant dans ce protocole. Les compteurs de travail GPU de R1-037
ne doivent ni calculer, ni recopier, ni présenter un FPS. Les routes PresentMon et leurs contrats
temporels restent séparés et inchangés.

## Acquisition épinglée

Le manifeste suivi `scripts/gate1-renderdoc.v1.json` fixe notamment :

- RenderDoc `1.46`, tag `v1.46` ;
- commit release `e4bd23b671d3d5a747ff5221dbe08a63eb6ca200` ;
- ZIP officiel SHA-256
  `9CA4D09ECABA2CC791168660D6FC2A7E3D70FE67146E87EC05C5F8DEA70772F7` ;
- clé OpenPGP primaire `1B039DB9A4718A2D699DE031AC612C3120C34695` ;
- signataire Authenticode `F91353AE1E90CBEFF15585165D1611C5CE72E346` ;
- `qrenderdoc.exe` SHA-256
  `C5E9D3F254D80A7649FBD9EAE1C67618CB19278C603710370C220DE469397277` ;
- `renderdoccmd.exe` SHA-256
  `400CB013EA52B9BAA818D69DC0A6341776D2A9560EDAD5BCC6E200FE8C03806D` ;
- `renderdoc.dll` SHA-256
  `809DA38E3867D9FD09CC5C30DD5310500DEE75E999166D7A14AD1EF6E9ECA65D`.

La licence du paquet est MIT et reste avec la copie externe. Aucune redistribution de RenderDoc dans
Forge n'est décidée. Toute redistribution future impose une revue distincte.

## Preuves exigées avant fermeture de R1-037

La route reste ouverte jusqu'à obtention simultanée de :

- quatre reçus runtime atomiques valides ;
- quatre `.rdc` indépendants et leurs SHA-256 ;
- quatre replays locaux `Supported`, Vulkan exacts et non dégradés ;
- quatre analyses sans rejet sémantique, chacune avec une action `Present` réellement observée ;
- égalité des compteurs entre les deux répétitions d'une même météo ;
- inventaire hashé des artefacts et validation finale fail-closed ;
- confirmation que le runtime normal, PresentMon, la CI et les assets Cadle n'ont pas été utilisés
  comme substituts.

Les `.rdc`, binaires RenderDoc et captures de travail restent externes et ignorés. Le dépôt conserve
uniquement le contrat, le modèle pur, les tests, les hashes, les résultats numériques et la synthèse
textuelle nécessaire à l'audit.

## Conséquences

ADR-0004 n'est pas étendue : l'overlay Gate 0 continue d'afficher le compteur global indisponible.
R1-037 passe le 4 septembre avec le runtime `5f6ed579` et l'outillage `7d2db384` : quatre captures et
replays indépendants, compteurs identiques par météo, garde Git finale et profils utilisateur inchangés.
Clair : 195 draws / 195 instances soumises / 280 981 triangles ; pluie : 196 / 196 / 313 747.
[Preuve et frontières](../evidence/gate1/R1_037_RENDERDOC_GPU_WORK.md).
Gate 1 reste ouverte ; aucun FPS, verdict humain, D3D12 ou Browser WebGPU n'est qualifié ici.
Gate 2 n'est pas autorisée par cette seule décision.
