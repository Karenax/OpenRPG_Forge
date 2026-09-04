# R1-023/025/026 — État GPU lié, natif High

Date : **2026-09-04**. Statut : **PASS_BOUND_STATE_ONLY**.
Les trois routes restent **OUVERTES**. Ce résultat ne qualifie ni un pixel précis ni Gate 1.

## Acquisition et intégrité

Le replay offline utilise l'outillage committé
`e64415109a9c65bb955ccb7084bfc2faa94cad05`, sans relancer Forge et sans build.
Les quatre RDC sont les captures R1-037 déjà qualifiées du runtime Release
`5f6ed5798fd93613200e0e8fbb4f9251f77e0e43`, binaire SHA-256
`9ec24448e0a77849d952a8df1cd2c0ba45a2eeeedf92706e3067f2351e166815`.
Les entrées compilées `apps`, `crates`, `Cargo.toml` et `Cargo.lock` sont inchangées depuis ce runtime.

- Machine : RX 7800 XT, Vulkan, pilote 26.8.1 (LLPC), High 1920 × 1080.
- Capture parent : `E:\OpenRPG_Forge\.gate1-tools\evidence\rd-final-20260904T071600Z`.
- Nouveau replay : `E:\OpenRPG_Forge\.gate1-tools\evidence\material-replay-20260904-04`.
- Reçu parent R1-037 SHA-256 :
  `c0d9d5d98f53a6928203dbdf07845522564e55713f3c90baaaff66daaaac9251`.
- Reçu lanceur `material-replay-launcher.json` : **PASS**, achevé à 07:45:59 UTC,
  SHA-256 `77091c2ca9b46172011b583d692f2fb90645306b01be09e1c9385ab177982bab`.
- Validation externe `material-bound-state.validation.json` :
  SHA-256 `c883b0d7483df188dce2fa58e9c47d3c53dbb5790664fd1910032367ded47997`.
- [Copie JSON sémantiquement identique suivie](r1-023-025-026-bound-state-20260904.validation.json) :
  quatre résultats et **44 artefacts** avec tailles/SHA-256 ; les médias et RDC restent externes.

Les hashes des RDC, scripts, manifeste, 72 fichiers du bundle, branche/HEAD/statut Git et profil
utilisateur sont vérifiés. Le profil réel QRenderDoc reste absent avant/après ; les profils nonce et
le miroir possédé sont supprimés. Aucun registre, code Rust, asset, capture originale ou profil
utilisateur n'est modifié. Aucun processus Forge, cargo ou PresentMon n'est lancé.

## Résultats observés dans les captures

Vingt draws sont inspectés par capture : un représentant de chaque matériau terrain/végétation/eau,
seize draws Bloom, puis le tonemapping. Les autres événements candidats sont conservés dans les
extractions ; cette sélection ne prétend pas couvrir chaque matériau glTF ou chaque pixel.

| Draw / donnée | Clair A/B | Pluie A/B |
|---|---|---|
| Végétation procédurale, eventId | 770 | 799 |
| Terrain, eventId | 819 | 848 |
| Eau, eventId | 919 | 948 |
| Bloom, 8 downsample + 8 upsample | 936 à 1124 | 986 à 1174 |
| Tonemapping, eventId | 1140 | 1190 |
| Wetness terrain **et** végétation | `[0, .32, .72, 0]` | `[.78, .32, .72, 0]` |
| Exposition / post-saturation | `0 / 1.04` | `-.35 / .86` |
| Contraste, trois composantes | `1.06` | `1.08` |
| Eau : transmission / épaisseur / IOR | `.18 / 3.2 / 1.33` | identique |
| Eau : optique extension | `[.84, .16, 1.33, .72]` | identique |
| Constante RGB du dernier blend Bloom | `.08` | `.08` |

Valeurs arrondies pour lecture ; les reçus conservent les f32 exacts et les octets des buffers.
Les champs vérifiés sont identiques entre répétitions d'une météo. Les quatre disassemblages
principaux sont aussi identiques entre clair/pluie et A/B.

Les bindings **réels SPIR-V** sont terrain/végétation/eau `set 3, binding 13` (WGSL source 100),
StandardMaterial `3:0`, View du tonemapping `0:0`, transmission `0:15`, IBL spéculaire `1:1`.
Wetness se trouve aux offsets 96/128 dans les blocs de 112/160 octets ; l'optique eau est à offset64
dans son bloc80 ; le grading commence à offset608 dans View784.

Le replay relie l'attachment MSAA du draw eau à sa résolution HDR par un même événement
`ResolveSrc/ResolveDst` avant Bloom. Il vérifie les ressources et mips de la pyramide, puis la
lecture du HDR composé par le tonemapping. Les sept downsample ordinaires n'ont pas de cbuffer
actif : il est éliminé par compilation. Le premier downsample et les upsample utilisent le bloc48
au binding `0:2`. Les huit constantes RGB d'upsample sont bornées, avec `.08` au dernier draw.
Cela n'est pas une mesure de contribution effective de 8 % à chaque pixel.

## Attribution des shaders et consommation compilée

Les noms internes Release sont supprimés. L'attribution ci-dessous est une inférence recoupée par
layout, bindings et calculs, **pas une trace d'exécution d'un fragment**. Les fichiers sont sous
`<nouveau replay>/clear-01/` ; les offsets/lignes sont ceux des disassemblages exacts.

| Shader | SHA-256 du disassembly | Chaîne inspectée |
|---|---|---|
| `shader-19720.txt`, terrain | `f676ee43dc9f10f86dd8fe21430ea9bac8c3d9e1f665c58a0bdb0af2ec9d46ca` | lectures wetness X/Z/Y, clamp et mix lignes5026–5034 ; albédo5035–5038 ; rugosité5039–5042 ; sortie5069 |
| `shader-21599.txt`, végétation | `32c59840d6e5794d5b65cf19617f2dc17b9b988deac4218a18856fad27b0cee9` | wetness4972–4980 ; albédo4981–4984 ; rugosité4993–4996 ; sortie5027 |
| `shader-27872.txt`, eau | `ed0cafa72a2238a3fc7d8cd55ba2ec0586ddaafcaac78e72697a1c93d94360af` | extension eau 5183–5226 ; éclairage 5235 ; sortie 5246 ; chemin transmission à 16 taps et IBL présents |
| `shader-21784.txt`, tonemapping | `16fdeb7d1e55126db5a260862cea98f10a7ab578a4706f870c5eaa72f12f3e03` | lecture HDR et grading310–313 ; exposition223–226 ; saturation288–290 ; sortie333 |

Sources Forge : `crates/forge_engine/src/shaders/visual_valley_terrain.wgsl`,
`visual_valley_vegetation_deform.wgsl`, `visual_valley_water.wgsl`, et `weather.rs`/`water.rs`.
Les calculs de lumière, transmission et tonemapping proviennent de Bevy 0.19.1 épinglé.

Lecture croisée supplémentaire des données déjà extraites (distincte des assertions du modèle) :

| Run | Event copie | HDR source → transmission destination | Cubemap IBL slot0 |
|---|---|---|---|
| clear-01 | 902 | 831 → 945 | 1647 |
| clear-02 | 902 | 828 → 940 | 1639 |
| rain-01 | 931 | 828 → 945 | 1724 |
| rain-02 | 931 | 830 → 946 | 1720 |

Les usages `CopySrc`/`CopyDst` ont le même eventId `vkCmdCopyImage`, puis la destination apparaît
en `PS_Resource` au draw eau. Les deux images sont HDR `R16G16B16A16_FLOAT` 1920×1080.
Le bloc LightProbes1840 contient, dans chaque run : compteur de probes locaux0 à offset1792,
index de cubemap de vue0 à1800, dernier mip8 à1804, intensité1 à1824. Le shader eau charge cet index
et cette intensité lignes3256–3264, puis référence `_175[index]` lignes3300/3355. Le slot0 est donc
sélectionnable par le chemin compilé avec un index valide ; ce n'est toujours pas une observation
de la branche effectivement prise par un pixel contribuant à l'image.

## Limites qui restent dues

- Pas de pixel debug : la présence du chemin compilé et de ses données liées ne démontre pas
  qu'une branche IBL/transmission ou un fragment choisi contribue à l'image finale.
- IBL : un cubemap à plusieurs mips est lié au slot0. Le validateur ne prouve pas le filtrage réel
  des mips, leur radiance, ni l'index dynamiquement sélectionné. Aucun SSR n'est revendiqué.
- Transmission : texture1080p liée avec `CopyDst` avant l'eau ; le validateur ne recoupe pas à lui
  seul la source de cette copie. La qualité des bords/réfraction reste à inspecter.
- Les quatre processus sont indépendants et figés : pas de preuve GPU de retour sec dans une même
  instance. Les anciens endpoints CPU/ECS et captures sec/pluie/sec restent complémentaires.
- Pas de couverture GPU Low/Medium/Ultra ou Browser par ces RDC High ; les anciennes preuves
  natives/Browser restent valides uniquement dans leurs propres frontières.
- Aucune revue humaine effectuée, aucune nouvelle mesure de performance. Les critères différés
  restent différés sous ADR-0010, la CI distante `NOT RUN` sous ADR-0006.

**R1-023, R1-025 et R1-026 restent décochées ; Gate 1 =43/55 ; Gate 2 interdite.**

## Validation et échecs conservés

**49/49 tests Python**, dont24 nouveaux cas synthétiques du modèle matériaux ; **23/23 contrôles
PowerShell** statiques/synthétiques ; parsing et `git diff --check` PASS. Aucun rebuild Rust n'est
nécessaire : ce lot ne modifie aucune entrée compilée du moteur.

Les matrices externes `material-replay-20260904-01/02/03` restent **FAIL**, non réécrites :
la première rencontre un infini légitime de projection refusé par JSON, puis02/03 révèlent deux
caches Python de démarrage QRenderDoc. Les octets de buffers restent intacts ; les infinis sont
désormais représentés explicitement dans les champs diagnostiques. QRenderDoc initialise
PySide2/shiboken2 avant le script ; interdire le bytecode dans le script ne suffit donc pas.
Le lanceur final conserve les deux caches exacts à la racine externe, en vérifie les hashes et
retire uniquement ces copies générées du miroir possédé avant le contrôle inchangé des72 originaux.
Tout autre fichier inattendu échoue toujours. La matrice04 passe les quatre replays et ces gardes.

Rejeu, depuis la branche Rust :

```powershell
pwsh -NoProfile -File scripts/Invoke-Gate1MaterialReplay.ps1 `
  -RepoRoot <worktree-Rust> -CaptureRoot <R1-037-existant> `
  -OutputRoot <nouveau-dossier-externe> -RenderDocRoot <bundle-1.46-epingle>
python -S -B scripts/renderdoc/gate1_material_model.py <nouveau-dossier-externe> `
  --output <nouveau-recu.json>
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
pwsh -NoProfile -File scripts/Test-Gate1MaterialReplay.ps1
```
