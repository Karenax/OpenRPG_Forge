# Gate 0 — Validation navigateur et Studio

Date : 30 août 2026
Branche : `Rust`
Bundle navigateur qualifié : `7286740c18fe9afa6cc207e388d5f335495fc650`
Preuve Tauri finale : `a7c506f099b50100ad49f3ae6d01d541c9b63d49`
Verdict : **CHROME 728 ACQUIS — EDGE 728 NOT RUN — TAURI A7C506F ACQUIS AUTOMATIQUEMENT**

## Portée et règle de preuve

Cette preuve sépare strictement :

- le bundle WebGPU construit au commit `7286740c` et exécuté dans Chrome desktop ;
- le contrôle Edge desktop historique exécuté au commit `528a9097` ;
- l'absence de replay Edge desktop sur le bundle `7286740c` ;
- le replay Tauri/WebView2 final du commit `a7c506f`, acquis et validé automatiquement ;
- la CI distante, restée `NOT RUN` selon ADR-0006.

La revue humaine de Gate 0 est différée par ADR-0005. Les captures et ledgers automatisés conservent
donc leur état réel ; aucune observation de cette page ne signifie qu'une personne a qualifié les
pixels :

```text
review.status = uninspected
human_review_performed = false
proof_status = pending_human_review
qualified = false
```

Les contrôles différés restent `G0-HR-001`, `G0-HR-002` et `G0-HR-003`. Cette dérogation Gate 0 ne
remplace pas la revue humaine comparative exigée à Gate 1.

## Contrat partagé

Les surfaces observées consomment le même document moteur-neutre :

```text
schema       forge.scene-proof
version      1
entities     4
SHA-256      0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df
```

La scène contient le sol, le cube PBR, la lumière directionnelle et la caméra. La hiérarchie,
l'inspecteur et le viewport dérivent des mêmes types et données Rust. Aucun fichier JavaScript ou
TypeScript métier n'est suivi ; le JavaScript livré est la glue produite par `wasm-bindgen`.

## Bundle WebGPU exact — commit 7286740c

Le bundle Trunk release qualifié pour Chrome a terminé avec succès. Les fichiers servis portent :

| Fichier | Taille | SHA-256 |
|---|---:|---|
| `forge_studio_bg.wasm` | 34 989 519 octets | `4b775441cac2864618985c7824a73cb56344f2a2937e2493470e1ed2466ba5e6` |
| `forge_studio.js` | 184 651 octets | `7339dca55cff566a42ea2fa69b23b01d764587deffdd678de28fe6a6536c225e` |
| `index.html` | 1 025 octets | `7ce400ae1b55e3f621355d3fa21546ce6ed4e3be485b2811c4a4070389ce7f15` |
| `styles.css` | 3 103 octets | `c33688b71bb4cde7cbf52ee8cad9f6e0a7453c4326ff914590464aef5b72cc2d` |

La durée locale du build release était de 18 min 43 s. Cette durée est une observation de build, pas
une métrique de performance du Studio.

## Chrome desktop — chemin positif 7286740c

URL cache-bustée exécutée :

```text
http://127.0.0.1:1420/?build=7286740-chrome
```

Le ledger positif, enregistré à `2026-08-30T20:58:17.730Z`, a confirmé :

- `webgpu.status = ready` et `diagnostic_injection = false` ;
- première frame Bevy réelle avant publication de l'état prêt ;
- `viewport.starts = 1`, canvas `1171 × 789` ;
- monde initialisé une seule fois ;
- sélection du bouton `Procedural cube`, EntityId
  `ae91d914-98e9-52d4-80d1-4fddc482988d` ;
- un rerender UI après sélection, sans reconstruction du viewport ;
- une observation de canvas stable, zéro observation instable ;
- aucun warning et aucune erreur dans le journal console collecté.

Chargement et mémoire enregistrés :

```text
loading.wasm_started_ms       = 392.9000000357628
loading.webgpu_ready_ms       = 1114.9000000357628
loading.scene_ready_ms        = 1114.9000000357628
memory.wasm_linear_bytes      = 50069504
memory.js_heap_used_bytes     = 11198251
memory.js_heap_limit_bytes    = 4395630592
memory.js_heap_api_available  = true
```

Distribution `requestAnimationFrame` collectée après 60 échantillons de warm-up :

```text
samples                       = 120
duration_ms                   = 120064.9
mean_ms                       = 1000.5408333333336
p50_ms                        = 1000.1
p95_ms                        = 1006.2
p99_ms                        = 1007.5
max_ms                        = 1067.5
mean_fps                      = 0.9994594590
one_percent_low_fps           = 0.963855
hitches_over_16_7_ms          = 120
hitches_over_33_3_ms          = 120
```

Le navigateur automatisé était throttlé à environ une frame par seconde. Ces valeurs prouvent que la
fenêtre statistique de 120 échantillons et le contrat de télémétrie fonctionnent ; elles sont
**non représentatives** et ne constituent ni un benchmark Gate 0, ni une limite du renderer.

L'interaction hiérarchie-vers-post-paint a fourni un échantillon de
`1991.5999999642372 ms`. Cette valeur est elle aussi affectée par le throttling. Elle prouve le passage
de la frontière post-paint et la stabilité du canvas, pas une latence utilisateur qualifiée.

### Capture Chrome éphémère

La capture JPEG locale et non suivie porte :

| Format | Dimensions observées | Taille | SHA-256 |
|---|---:|---:|---|
| JPEG | 1683 × 903 | 80 023 octets | `a01efcdbfea1bab4ea60852777a9a537a886b94d8b6c0ba8359128f6cab5078a` |

Les dimensions ont été confirmées depuis le marqueur SOF0 du JPEG. L'inspection automatisée a trouvé
la surface Studio à trois colonnes, la scène visible, le cube sélectionné, l'overlay WebGPU prêt et le
monde initialisé une fois. La capture reste éphémère, locale et `uninspected` au sens humain d'ADR-0005.

Le mode navigateur autonome ne possède pas le backend Tauri. Les statuts de permission/IPC qui y
signalent l'absence du backend ne qualifient donc pas l'ACL native ; cette qualification appartient au
replay Tauri/WebView2.

## Chrome desktop — chemin négatif 7286740c

URL de preuve déterministe :

```text
http://127.0.0.1:1420/?forge_gate0_probe=webgpu-unavailable&build=7286740-chrome-neg
```

Le chemin s'arrête volontairement avant la création de l'adapter. Le ledger et le DOM ont confirmé :

- `webgpu.status = unavailable` ;
- `diagnostic_injection = true` ;
- raison explicite : injection diagnostique avant création de l'adapter ;
- `loading.wasm_started_ms = 167.80000001192093` ;
- `loading.webgpu_ready_ms = null` et `loading.scene_ready_ms = null` ;
- `frame = null` ;
- `viewport.starts = 0`, dimensions du canvas nulles ;
- aucune mesure d'interaction et aucun démarrage de monde ;
- alerte visible avec diagnostic WebGPU et conseil de récupération ;
- aucun warning et aucune erreur console collectés.

Ce contrôle qualifie le chemin d'erreur déterministe et son message utilisateur. Il ne prétend pas
qu'un GPU ou un navigateur physiquement dépourvu de WebGPU a été utilisé.

## Edge desktop

### Historique — commit 528a9097

Edge avait chargé `?build=528a909-edge` et prouvé sur ce bundle historique : WebGPU prêt, scène
visible, sélection du cube, EntityId cohérent, un rerender UI sans reconstruction du monde et aucun
warning ou erreur console. Cette preuve reste valide comme historique du socle ; elle ne lie pas Edge
au bundle `7286740c`.

### Bundle Chrome qualifié — commit 7286740c

**NOT RUN.** Edge desktop n'était pas disponible dans le contrôleur de navigateur de la session
(`Browser is not available: edge`). Aucun résultat Chrome ni WebView2 n'est extrapolé en résultat Edge
desktop. `R0-031` conserve donc une preuve Edge historique, mais pas de replay Edge commit-bound 728.

## Tauri / WebView2 — commit a7c506f

Le replay contrôlé a acquis la preuve finale au commit
`a7c506f099b50100ad49f3ae6d01d541c9b63d49`, arbre
`7ca544c9eac98af558a4d91390c74ae08fa2ff11`, depuis une source `Rust` propre. Le build Tauri
commit-bound s'est exécuté de `2026-08-30T22:25:01.2306453+00:00` à
`2026-08-30T22:27:16.7336881+00:00` avec succès.

| Artefact final | Taille | SHA-256 |
|---|---:|---|
| `forge-studio.exe` | 16 116 224 octets | `2cd63455a8ba76762519a7194ea9ff66bc24b7d4fac6e262f0884d74dc77c458` |
| télémétrie JSON | 1 618 octets | `de03d21de9c0ee0574e09fbc56daaf160672653e06def4fee048891cacac720e` |
| capture PNG | 342 314 octets | `3e0f37b466a47fe75168d6a10f12caee1b82dbc127788da0d3bbd83e1820e6ed` |
| ledger de capture JSON | 3 225 octets | `b8ae014c5c8dd2927ac306db69a6fd00a82315d4d3173dd8f373115f2147ca75` |

Le ledger de capture `forge.gate0-studio-window-capture` v2 et le ledger de télémétrie
`forge.gate0-studio-telemetry` v1 valident leurs schémas et contrôles sémantiques. La fenêtre exacte,
PID `30324` et HWND `0x000000000054056a`, portait le titre attendu, était visible, non minimisée et
liée au même processus avant et après capture.

La capture `1616 × 939` a été acquise par `PrintWindow(PW_RENDERFULLCONTENT)`, flags `2`, avec retour
Win32 positif. Le foreground n'était ni requis ni observé avant ou après ; cette méthode capture la
fenêtre vérifiée sans voler le focus. Le PNG est non vide et possède 192 couleurs échantillonnées.

Le ledger WebView2 enregistré à `2026-08-30T22:27:20.998Z` confirme :

```text
webgpu.status                  = ready
diagnostic_injection           = false
loading.wasm_started_ms        = 490.9000000357628
loading.webgpu_ready_ms        = 1155.300000011921
loading.scene_ready_ms         = 1155.300000011921
memory.wasm_linear_bytes       = 50069504
memory.js_heap_used_bytes      = 9323224
memory.js_heap_limit_bytes     = 4395630592
viewport.starts                = 1
viewport.canvas                = 1088 × 786
frame.warmup_samples           = 60
frame.samples                  = 120
frame.duration_ms              = 833.3999999999997
frame.mean_ms / p50 / p95      = 6.944999999999998 / 6.8999999999999995 / 7.5
frame.p99 / max                = 7.7 / 8.1
frame.mean_fps / 1_percent_low = 143.98848092152633 / 126.58227848101264
frame.hitches_16_7 / 33_3      = 0 / 0
interaction.samples / p95_ms   = 1 / 316.29999999999995
canvas stable / instable       = 1 / 0
```

`execute_studio_command` a réussi avec quatre entités et le hash source attendu.
`plugin:window|set_title` a retourné la raison exacte
`Commande Tauri échouée : Command plugin:window|set_title not allowed by ACL`, classée
`explicitly_denied` sans accepter une autre erreur.

L'inspection automatisée indépendante du PNG original montre le shell complet à trois colonnes, le
terrain vert, le cube bleu sélectionné, le canvas non noir, les résumés WebGPU/IPC/ACL et aucune
occlusion grossière. Elle relève toutefois plusieurs glyphes carrés « tofu » dans l'overlay Bevy du
canvas, pour des séparateurs et certains caractères accentués. Ce défaut cosmétique de fonte est
visible et reste à reprendre.

Cette inspection est uniquement automatisée. Le ledger reste `review.status = uninspected`,
`human_review_performed = false`, `proof_status = pending_human_review` et `qualified = false` ; ADR-0005
diffère la revue humaine sans la déclarer réussie.

## Exception CI distante ADR-0006

Le workflow GitHub Actions `Rust gate`, run
[`33339077298`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33339077298), visait le commit
final `a7c506f`. Ses quatre jobs ont une conclusion d'interface `failure`, mais zéro étape exécutée
(`steps = []`) à cause du blocage de facturation/plafond :

| Job distant | ID | Étapes | Statut de preuve |
|---|---:|---:|---|
| `native (windows-2025)` | `99331257296` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99331257267` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99331257192` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99331257301` | 0 | `NOT RUN — infrastructure billing` |

La formulation probante reste donc :

```text
tests locaux automatisés = PASS pour les commandes effectivement consignées
CI distante Windows = NOT RUN — infrastructure billing
CI distante Linux = NOT RUN — infrastructure billing
CI distante WASM = NOT RUN — infrastructure billing
CI distante supply-chain = NOT RUN — infrastructure billing
```

Le run historique `33335683606` du commit `7286740c` présentait le même blocage et reste conservé
comme trace. ADR-0006 autorise une substitution locale bornée jusqu'à Gate 4, mais ne transforme
jamais la CI du HEAD `a7c506f` en succès et ne prouve pas Linux.

## Synthèse probante

### Prouvé au commit 7286740c

- bundle WASM/JS/HTML/CSS exact, hashé et lancé dans Chrome ;
- WebGPU positif, même scène et même document source ;
- sélection, rerender unique et viewport démarré une fois ;
- chargement, mémoire, frame time et interaction sérialisés ;
- chemin WebGPU indisponible injecté, explicite et sans démarrage de monde ;
- capture Chrome éphémère hashée et inspectée automatiquement ;
- aucune erreur ou warning console collecté dans les chemins positif et négatif.

### Prouvé au commit a7c506f

- build Tauri contrôlé et exécutable exact lié à une source Git propre ;
- fenêtre WebView2 visible, non minimisée et identifiée avant/après par PID, HWND et titre ;
- WebGPU prêt, 120 frames après warm-up et viewport démarré une seule fois ;
- interaction Tauri confirmée avec canvas stable et sans reconstruction ;
- commande Forge autorisée et commande `set_title` explicitement refusée par l'ACL ;
- capture `PrintWindow(PW_RENDERFULLCONTENT)` non vide, hashée et inspectée automatiquement ;
- défaut de glyphes tofu conservé comme observation, sans masquer la limite visuelle.

### Non prouvé ou différé

- Edge desktop sur le bundle `7286740c` : `NOT RUN` ;
- revue humaine des captures et parcours Gate 0 : différée selon ADR-0005 ;
- CI distante Windows/Linux/WASM/supply-chain : `NOT RUN` selon ADR-0006 ;
- performance représentative du navigateur : non mesurée dans cette session throttlée ;
- vrais draw calls GPU : non fournis par cette preuve navigateur.

Verdict : **preuves automatisées Chrome 728 positive/négative et Tauri a7c506f acquises ; Edge 728 non
exécuté ; revues humaines différées ; CI distante NOT RUN.** Cette page ne déclare ni une CI verte,
ni une inspection humaine, ni une performance représentative au-delà des mesures consignées.
