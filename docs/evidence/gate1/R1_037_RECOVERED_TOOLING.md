# R1-037 — Acquisition et comptage natifs, qualification ouverte

## Dernier résultat — répétition indépendante du 8 septembre

Une seconde acquisition et sa relecture **séparée** ont réussi, sans modifier le
candidat ni reconstruire le moteur. Les onze pins des entrées sont identiques à
ceux de la première capture ; les arguments sont identiques après neutralisation
du seul chemin de sortie `--capture`. RenderDoc 1.46, Vulkan Supported/non dégradé,
vue human_bridge figée, clair High, même machine RX 7800 XT.

Dossiers externes sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :

- acquisition : `r1-renderdoc-repeat-20260908`, `candidate_frame304.rdc` ;
- replay : `r1-renderdoc-repeat-replay-20260908` ;
- référence comparée : `r1-renderdoc-replay-20260908-c`, frame 305.

| Mesure soumise, toutes passes | Frame 305 | Frame 304 |
| --- | ---: | ---: |
| Actions visitées / Present | 2 779 / 1 | 2 779 / 1 |
| Draws | 1 641 | 1 641 |
| Instances | 3 065 | 3 065 |
| Triangles | 5 755 071 | 5 755 073 |
| Triangles de la passe UI | 1 574 | 1 576 |

Agrégation récursive par marqueur racine : les dix-sept passes autres que `ui`
ont exactement les mêmes nombres de draws, instances et triangles. La seule
différence est la géométrie UI (+2 triangles). Cela ne prouve pas quel texte ou
glyphe a changé. Les identifiants d'événements ne sont pas une correspondance
stable entre acquisitions. Aucune égalité binaire des captures ni identité des
pixels n'est revendiquée ; aucune correction ou nouvelle capture motivée par cet
écart UI n'est nécessaire. Les instances et triangles incluent les passes
répétées, notamment les ombres : ce ne sont pas des objets uniques ni des timings.

SHA-256 du second résultat :

- RDC : `51E2BEB03F18AD7709644D4EBA069236ED69A4A9C643025970D38818A3F725C1` ;
- actions : `934C130AC5EBDDDA06F33161A38111EB58A5C003178B0409738FA0B80D1DB1EF` ;
- analyse : `AA74246484959836096A7C3D7EF9BD6CFB6783476F25A51A657A60BCE7345EE2`.

Les deux reçus hôte sont PASS. Le runtime de capture est arrêté par le nettoyage
ciblé avant replay (`runtime_cleanup=terminated`) : ce n'est pas une preuve de
sa sortie normale. QRenderDoc sort normalement ; aucun QRenderDoc/runtime de test
restant au contrôle. Aucun navigateur ouvert pour ce lot. Dernier échantillon du
replay à 84,115 s : 12 559 376 Kio physiques et 15 901 440 Kio virtuels libres.

**Acquis :** acquisition/relecture indépendantes répétées et comptages par passe
stables hors UI sur ce candidat natif. **Encore non qualifié :** couverture Studio,
révision exacte/propre ADR-0006, matériel cible et validations humaines. R1-037 et
Gate 1 restent ouverts. Ne pas recommencer ces deux mesures à entrées inchangées.
Les sections suivantes conservent l'historique daté, pas le statut courant.

## Extraction des actions raccordée — 8 septembre

`scripts/renderdoc/gate1_renderdoc_replay.py` intègre la partie replay de l'ancien
adapter Forge, avec API injectée pour tester la frontière sans GPU. Il sélectionne
l'événement de chaque draw avant lecture du pipeline, normalise topologie/stages,
recoupe les Present et transmet les actions à l'analyseur. Il refuse les replays
non Vulkan/dégradés, les MultiAction non développées et les indirects zéro ambigus.
Pas de reprise du host, de son profil QRenderDoc ou de son cycle de processus.

**25 tests PASS** : 18 modèle et sept frontière API synthétique. Les sept nouveaux
tests couvrent événement/pipeline, multi-draw développé avec zéro instance explicite,
backend/dégradation, échec d'ouverture, échec de replay, multi-draw non développé,
indirect ambigu et libération des ressources sur ces chemins. Aucun GPU, fichier
`.rdc` réel ou nouveau rendu exercé. Ce résultat ne remplace pas l'acquisition,
la preuve de readiness/gel du candidat, ni sa qualification par backend.

Commande exécutée :
`python -B -m unittest discover -s scripts/renderdoc -p "test_gate1_renderdoc_*.py" -q`.
Code 0, `git diff --check` PASS ; pas de Cargo relancé pour cette frontière Python.

## Reprise de l'analyseur — 8 septembre

Analyseur pur, trois fixtures et suite de tests intégrés dans `scripts/renderdoc/`.
Le contrat de comptage a été extrait sous un nouveau nom/schéma, sans importer
la décision ADR historique, son acquisition lock, le host ou l'adapter runtime.
Les assertions antérieures sont conservées (seuls chemin/schéma de l'extrait changent).

17 tests historiques PASS à l'import. Une régression supplémentaire reproduit un
sous-comptage possible : un parent Drawcall avec enfants, non déclaré MultiAction,
était ignoré. Ce cas est désormais refusé. Première correction : diagnostic de cycle
masqué par ce nouveau refus ; ordre corrigé sans modifier l'attente du test existant.
Résultat final : **18 tests PASS**, dont les 25 cas de refus d'action et neuf cas
de refus de capture des fixtures. Commande :
`python -B -m unittest discover -s scripts/renderdoc -p test_gate1_renderdoc_model.py -q`.

CLI nominal exécuté sur fixture : quatre draws, sept instances soumises, cinq
triangles, deux lignes, quinze points ; **tous synthétiques**, pas des mesures GPU.
Entrée d'un autre schéma refusée avec diagnostic JSON `contract_shape`, sans métrique
partielle. `git diff --check` PASS ; aucun test/build Rust requis par cette reprise
Python isolée. Aucune capture, injection, nouveau rendu ou qualification de backend.

SHA-256 analyseur : `80741C7C6D62F36803AF7426C62D4F8116A80AD323BBD2EA44C06091FF3A01E2`.
SHA-256 tests : `C54D1CA721B84CCA48C26A073C17F4FD652CF66A756472BBAB714E35315E3F6C`.
Readiness du candidat, extraction des actions et provenance de capture restent à
raccorder. R1-037 demeure ouvert ; le compteur global de l'overlay reste indisponible.

## Inspection préalable

8 septembre 2026, inspection en lecture seule ; workspace actif sur Rust.
Pas de capture, modification de registre, copie de code, build ou changement Git.

## Résultat qui change la prochaine action

RenderDoc n'est pas dans PATH, mais sa distribution portable existe déjà sous
`E:/OpenRPG_Forge/.gate1-tools/renderdoc/v1.46/official-win64-acq-001`.
Ne pas retélécharger l'outil ni refaire son acquisition sur la seule absence de PATH.
L'aide de `renderdoccmd` et celle de `capture` ont été exécutées avec succès.
Les hashes présents concordent avec le manifeste historique :

- `renderdoccmd.exe` : `400CB013EA52B9BAA818D69DC0A6341776D2A9560EDAD5BCC6E200FE8C03806D` ;
- `renderdoc.dll` : `809DA38E3867D9FD09CC5C30DD5310500DEE75E999166D7A14AD1EF6E9ECA65D`.

L'acquisition historique et sa licence sont documentées dans le dossier externe ;
ce contrôle ne rejoue pas la vérification cryptographique complète ni une capture GPU.

## Préparation de code existante, non transposable telle quelle

Copie de preuve retrouvée : `E:/OpenRPG_Forge/.gate1-renderdoc-v2-ed802bc0`,
HEAD lu `62a158c57f8cba3c189dbbd4ce3f78b28f897e06`. Sources pertinentes :

- `scripts/gate1-renderdoc.v1.json` ;
- `scripts/Invoke-Gate1RenderDoc.ps1` ;
- `scripts/renderdoc/gate1_renderdoc_model.py` et tests associés ;
- `scripts/renderdoc/gate1_renderdoc_qrenderdoc.py` ;
- `apps/forge_runtime/src/gate1_renderdoc_capture.rs` ;
- `docs/evidence/gate1/R1_037_RENDERDOC_GPU_WORK.md`.

Le rapport y annonce explicitement **CAPTURES NOT RUN**. L'adaptateur runtime
importe notamment `SceneryProjectionStats`, `SceneryProjectionStatus` et
`validate_governed_gate1_render_state`, absents du moteur actif après recherche.
Il ne suffit donc pas de lancer cet ancien harness sur le nouveau candidat.
L'orchestrateur exige en outre un worktree propre sur commit exact ; cette exigence
ne sera pas désactivée pour faire passer le workspace modifié.

Le numéro ADR-0009 historique désigne RenderDoc, alors que l'ADR-0009 du workspace
actif concerne la navigation. Ne pas écraser ce fichier ni importer implicitement
la décision historique. Son manifeste limite la capture à Vulkan et exclut Browser
WebGPU : aucune qualification WebView2 ou compteur global ne peut en être déduite.

## Suite et verdict

### Autorité du protocole — éviter les critères supplémentaires implicites

ADR-0004 impose avant clôture R1-037 une instrumentation exhaustive avec sémantique
multi-draw documentée, ou une capture externe reproductible pour chaque backend
qualifié. Son paragraphe sur la preuve ponctuelle exige outil, version, backend,
scène, frame et définition des commandes multi-draw. ADR-0006 conserve les règles
de révision exacte et de qualification locale. Aucun de ces textes ne prescrit
spécifiquement le mécanisme appelé « reçu atomique » dans les notes précédentes.

Les mentions « reçu atomique dû » de l'historique décrivent donc une préparation
d'outillage, pas une nouvelle condition normative. Elles ne doivent pas conduire
à prolonger la gate par un protocole auto-imposé. Une solution retenue doit toujours
établir la provenance et les conditions du résultat ; cette clarification n'accepte
ni des captures anonymes ni une stabilité supposée à partir d'un délai fixe.

État courant : capture/replay Vulkan ponctuels acquis, calcul multi-draw testé,
processus terminés et sources/pins conservés. Répétabilité complète et couverture
des backends de qualification non établies. La revue d'ADR-0004 exigée avant
clôture reste à formaliser ; aucune exception Gate 1 n'est accordée par ce rapport.
Pas de nouveau lancement, build ou modification du renderer dans cette clarification.

### Répartition de la capture acquise — aucune nouvelle charge GPU

Analyse en lecture seule des actions normalisées du replay c, hash
`4C450233C56210E7E5DED8DE0F10C84A2BDDF7E58F7129EF8A7011F3C57F04B2`.
Par racine RenderDoc, descente dans les enfants et comptage des seuls Drawcall
feuilles ; expansion MultiAction conservée. Instances explicites pour les draws
instanciés, multiplicateur 1 sinon. Tous les draws de ce fichier déjà validé sont
TriangleList : triangles soumis = num_indices / 3 × multiplicateur.

| Groupe de marqueurs réels | Draws | Instances soumises | Triangles soumis |
|---|---:|---:|---:|
| shadow_directional_light_0_cascade_0 à _3 | 587 | 1 211 | 3 608 620 |
| main_transparent_pass_3d | 437 | 453 | 1 072 000 |
| early prepass + early deferred prepass | 297 | 689 | 557 300 |
| main_opaque_pass_3d | 294 | 686 | 515 550 |
| Autres draws (UI, bloom, clustering, post-traitement, ciel) | 26 | 26 | 1 601 |
| **Total recoupé avec analysis.json** | **1 641** | **3 065** | **5 755 071** |

Les quatre cascades représentent environ 63 % des triangles soumis. La passe
transparente représente environ 27 % des draws. Ces proportions ne sont **pas**
des parts de temps GPU : shaders, pixels couverts, bande passante et coût des
dispatchs ne sont pas mesurés par ce regroupement. Aucune optimisation ou réduction
de qualité justifiée à elle seule par ces chiffres ; mesurer les temps par passe
avant d'intervenir. En particulier, ne pas réécrire l'herbe ou la carte à partir
du seul nom de la passe transparente.

Les marqueurs lightprobe_copy/downsampling/radiance/irradiance apparaissent aussi
dans l'arbre d'actions ; l'absence de draw dans ces groupes ne signifie pas coût
nul. Le relevé ne prouve pas un état GPU entièrement stabilisé ni un préchauffage
exhaustif. Leur présence ne suffit pas non plus à diagnostiquer une régénération
incorrecte. Aucun nouveau replay, fenêtre, processus graphique ou build lancé.
Sommes recoupées en PowerShell sur toutes les racines ; diff documentaire PASS.

### Reprise propriétaire — premier comptage GPU ponctuel réussi

8 septembre, relecture `r1-renderdoc-replay-20260908-c`, sans recapture/runtime ni
nouvel onglet. Mémoire virtuelle libre avant lancement : 14 502 984 Kio, contre
environ 7 à 9 millions lors des échecs précédents. Même fichier frame 305 et hash
`B5BAA8D980362DC85D9F5507A9CE8657C6ED98EBB59259ED9904961AA35AB62D`.

**PASS ponctuel** : support local Supported, Vulkan, degraded=false, un Present.
2 779 actions visitées ; **1 641 draw leaves**, **3 065 instances soumises**,
**5 755 071 triangles soumis**, zéro ligne/point. Tous les draws sont TriangleList.
Ce sont les soumissions de toutes les passes : ni objets/triangles uniques de la
carte, ni pixels visibles, ni mesure FPS. Les enfants MultiAction sont comptés
suivant le contrat existant, pas les parents. Aucune convention assouplie pour ce run.

Artefacts dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-replay-20260908-c` :

- `actions.json`, SHA-256 `4C450233C56210E7E5DED8DE0F10C84A2BDDF7E58F7129EF8A7011F3C57F04B2` ;
- `analysis.json`, SHA-256 `8D65EBD64D57E9B09487C1E581C5EA61E59D60FFB689D221B603392FE6D8608A` ;
- `smoke.json` : gpu_replay_executed=true, capture_launched=false, qualification=false ;
- `host-result.json` : PASS, PID 17636, process_exited=true, tool_cleanup=already_exited.

Commande du modèle rejouée sur les actions réelles avec Python normal : mêmes
totaux, sortie 0. Le garde-fou reste actif : dernier échantillon à 86 514 ms,
minimum physique libre 5 316 292 Kio, virtuel libre 6 953 936 Kio. Le run complet
est sorti 0 et aucun QRenderDoc/runtime ne reste après contrôle.

Nettoyage renforcé dans le même lot : inputs.json écrit avant injection permet
de retrouver un runtime même si target.json manque ; chemin exécutable, argument
de capture unique et date de démarrage requis, ambiguïté refusée. Sélection testée
sur quatre métadonnées synthétiques (un seul candidat accepté), parse et diff PASS.
Ce fallback runtime n'a pas été exercé par un nouveau lancement GPU ; le nettoyage
du processus de replay terminé est, lui, attesté par le reçu ci-dessus.

R1-037 reste **ouvert** : ce comptage ne prouve pas la répétabilité complète,
un reçu atomique de l'état pendant la capture, le backend Studio ou un compteur
global d'overlay. Aucun budget de performance ou passage de Gate 1 n'en est déduit.
Les échecs historiques ci-dessous sont conservés, mais le blocage mémoire du
fichier précis n'est plus l'état courant. Ne pas répéter cette relecture inchangée.

### Relecture isolée — pression mémoire observée

Seconde tentative isolée, suffixe `r1-renderdoc-replay-20260908-b` : autorisée par
un changement observé de disponibilité (9 349 184 Kio virtuels libres, contre
environ 7 millions au début du premier essai), sans nouvelle capture ni runtime.
Même arrêt mémoire à **14 789 ms**, 2 066 860 Kio virtuels libres ; le reçu hôte
`host-result.json` confirme FAIL et `process_exited=true` (PID 44328).
Le chemin d'échec du nouveau reçu est donc maintenant réellement exercé.
Pas d'actions/analysis ni comptage acquis. Ne pas lancer de troisième essai avec
une marge similaire : besoin de mémoire supplémentaire encore non quantifié.
L'obstacle persiste sur les trois continuations consacrées à ce blocage, incluant
la mise à jour du suivi entre les deux relectures. Progression automatique suspendue
sans modifier les critères de Gate 1, les réglages système ou les autres applications.

Commande exécutée le 8 septembre, sans runtime vivant :

```powershell
./scripts/Test-RenderDocPython.ps1 -ReplayCapture E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-capture-20260908-d/candidate_frame305.rdc -ReplaySha256 B5BAA8D980362DC85D9F5507A9CE8657C6ED98EBB59259ED9904961AA35AB62D -Output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-replay-20260908
```

**FAIL contrôlé**, pas un test encore en cours. Le hash d'entrée a été vérifié,
`replay-input.json` et `replay-live.log` produits. À 12 476 ms, la mémoire virtuelle
libre système descend à 2 090 572 Kio, sous le seuil de sécurité de 2 097 152 Kio
(2 Gio) ; mémoire physique libre 4 182 324 Kio. Le processus QRenderDoc appartenant
à l'essai a été arrêté par le garde-fou. Aucun runtime n'a été lancé, aucun autre
processus utilisateur arrêté, aucun réglage de pagination Windows modifié.

Les onze échantillons de `memory.jsonl` attestent la pression système pendant la
relecture, pas une attribution exclusive à RenderDoc. Ils ne prouvent pas la cause
racine du crash précédent. Pas de `actions.json`, `analysis.json` ou reçu PASS ;
aucun comptage GPU qualifié. Aucun QRenderDoc/runtime présent au contrôle suivant.
Le fichier `.rdc` demeure conservé ; ne pas le recapturer ni relancer ce replay
inchangé sans davantage de mémoire disponible.

Hôte complété après cet arrêt : reçu `host-result.json` distinct du reçu Python
(qui peut manquer lors d'un crash/arrêt). Régression SDK normale réellement PASS
dans `r1-renderdoc-host-regression-20260908`, sans capture ni relecture.
Trois refus avant écriture réellement vérifiés : capture et replay simultanés,
SHA-256 absent, SHA-256 incorrect. 28 tests Python PASS, parse PowerShell et diff
PASS. Le nouveau reçu hôte sur le chemin d'échec mémoire n'a pas été rejoué.
R1-037 reste ouvert ; les mesures cible et validations humaines restent dues.

### Complément acquisition réelle — 8 septembre

Le mode explicite `Test-RenderDocPython.ps1 -CaptureProbe` lance le candidat natif
épinglé `281218D783D8BDA33D481E8D8AB214C4F2D92DE7A42A1EF3316642B31CC65423`,
WorldSketch/kit rev.4/650 arbres, vue human_bridge figée, clair High. Ses onze
entrées sont contrôlées contre le manifeste précédent avant lancement. Ce chemin
de diagnostic n'est pas le protocole de qualification sur arbre propre.

Essais sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/` :

- `r1-renderdoc-capture-20260908` : API encore vide refusée avant déclenchement,
  runtime PID 42600 arrêté après vérification de son chemin. Aucun `.rdc` reçu.
- suffixe `-b` : attente du message RegisterAPI implémentée, mais déconnexion sans
  capture. Lecture du registre : couche RenderDoc enregistrée sur la distribution
  originale, alors que l'injection utilise la copie du dossier de preuve.
- suffixe `-c` : sélection de la couche copiée par variable d'environnement locale
  `VK_IMPLICIT_LAYER_PATH`, sans écriture registre ; trigger Vulkan attesté,
  déconnexion avant réception, aucun comptage. Pas de qualification du motif exact
  de cette déconnexion.
- suffixe `-d` : journal interne explicitement conservé ; RegisterAPI Vulkan,
  progression de capture puis **NewCapture reçu**. Fichier local
  `candidate_frame305.rdc`, **848 464 512 octets**, message et hash conservés.
  La relecture immédiate échoue avec sortie QRenderDoc **-1073740791**, sans reçu
  final ni actions/analysis JSON. Le journal inclut des assertions de libération
  de ressources ; la cause racine du crash n'est pas établie. Aucun total GPU
  ne peut être déduit de la présence du fichier seul.

Les PID de l'essai d et QRenderDoc ne sont plus présents au contrôle final.
Un lancement PowerShell concurrent a aussi échoué à initialiser son compilateur ;
le suivant a réussi. Une pression mémoire est une hypothèse, pas une cause prouvée.
Acquisition et replay ont ensuite été séparés dans le code pour ne plus les
superposer ; cette séparation est vérifiée syntaxiquement, pas rejouée en GPU.
Ne pas refaire l'acquisition avant d'avoir tenté la relecture isolée du fichier d.

28 tests Python synthétiques PASS (25 existants + trois gardes API asynchrones).
Pas de build moteur, nouvelle qualification artistique, benchmark ni passage de
gate. Le signal PNG n'est pas un reçu atomique attestant l'état pendant la capture.
La fin normale du runtime et la cohérence GPU complète restent non attestées.

Références officielles :
[RenderDoc target control v1.46](https://github.com/baldurk/renderdoc/blob/v1.46/renderdoc/core/target_control.cpp)
(GetAPI mis à jour par RegisterAPI présentant) et
[Khronos layer discovery](https://github.com/KhronosGroup/Vulkan-Loader/blob/main/docs/LoaderLayerInterface.md)
(override implicite local, ignoré en élévation). Ce diagnostic exclut les autres
overlays implicites du processus ; ne pas le comparer à une performance ordinaire.

### Complément du 8 septembre — interpréteur embarqué

`scripts/Test-RenderDocPython.ps1` et `scripts/renderdoc/smoke_embedded_python.py`
exécutent maintenant un smoke SDK isolé, sans capture ni injection.

- Premier essai `r1-renderdoc-python-20260908` : timeout, aucun reçu Python ;
  pas de tests attestés. Processus appartenant à l'essai arrêté, profil nonce
  archivé sur E:. Le lancement de diagnostic `qrenderdoc --help`, également
  arrêté, n'était pas isolé ; absence de changement du profil ordinaire non prouvée.
- Essai `r1-renderdoc-python-20260908-b` : rapport FAIL explicite en Python
  3.8.20, `No module named '_overlapped'` pendant l'import de unittest.
  Cela explique un défaut du smoke ; ce reçu ne prouve pas à lui seul la cause
  exacte de tous les symptômes du premier lancement.
- Essai `r1-renderdoc-python-20260908-c` : rapport PASS, sortie processus 0,
  profil isolé vérifié puis archivé. Imports analyseur/adaptateur réussis ;
  fixture synthétique nominale : 4 draws, 7 instances, 5 triangles, 2 lignes,
  15 points. Présence des API OpenCaptureFile/ExecuteAndInject/CreateTargetControl
  vérifiée sans les appeler. Aucun replay, aucune `.rdc`, aucune mesure GPU.
- Les 25 tests modèle/replay synthétiques passent séparément avec Python normal
  (0,005 s rapportées), pas avec l'interpréteur embarqué. Aucun QRenderDoc encore
  actif lors du contrôle final. `git diff --check` PASS ; pas de rebuild Rust.

Les trois dossiers sont sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/` et conservés.
Le script fixe désormais TEMP/TMP sur E: pour chaque essai, désactive les checks
de mise à jour dans le profil jetable et vérifie les invariants du profil plutôt
qu'une égalité avec sa graine : RenderDoc y ajoute ses valeurs par défaut.

Source officielle inspectée :
[qrenderdoc.cpp v1.46](https://github.com/baldurk/renderdoc/blob/v1.46/qrenderdoc/Code/qrenderdoc.cpp),
option `--python`, journalisation des exceptions et traitement de SystemExit.
Le code retourne 0 même après SystemExit Python non nul : le reçu PASS est donc
indispensable et a effectivement refusé l'essai b. Aucun profil ordinaire n'est
supprimé/restauré à l'aveugle. L'intégrité complète du bundle et l'absence de
modification du profil utilisateur ne sont pas qualifiées par ce smoke.

SHA-256 hôte : `55039F06CD36FAB227674D2C2C5FA6D60EC162D54E9B8279BBD7EBF913251255`.
SHA-256 smoke : `6C58E82578E399F0868E62BAF9184A927CB6EB55D1B42374BB1F8DBE61BDCE3F`.

Ce résultat lève le défaut du smoke embarqué, pas R1-037. Reste à raccorder le
déclenchement réel au reçu de readiness/gel du candidat et vérifier le replay.

Réutiliser d'abord l'analyseur et son contrat après lecture complète, puis adapter
la readiness, les sources et le déclenchement au candidat WorldSketch/kit/forêt
actuel. Une capture ponctuelle doit conserver la définition des multi-draw et les
frontières de backend prévues par ADR-0004. Le périmètre Web et la décision de
qualification restent à résoudre explicitement. Aucun nombre de meshes ou de
primitives ne remplace un draw call réel ; R1-037 et Gate 1 restent ouverts.
