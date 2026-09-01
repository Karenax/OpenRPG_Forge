# Gate 1 — sous-ensemble CC0 et overlay de décor de Visual Valley

Date de qualification locale : **2026-09-01**

Base exacte du worktree isolé : **`02183f5972a9437630c18e71d4c5e7601af73b66`**

Commit du lot : **`e1b5ebaf7d0de18429dc1160cb955aadb54fb9b1`**

Verdict borné : **PASS AUTOMATISÉ LOCAL NATIF + COMPILATION WASM — GPU/VISUEL/WEBGPU NOT RUN**

Ce lot ajoute une couche de présentation optionnelle à Visual Valley. Il ne modifie ni le document
terrain, ni le document végétation, ni leurs placements déterministes, ni les collisions. Il ne
ferme pas Gate 1 et ne vaut pas revue artistique humaine comparative.

## Provenance et sélection minimale

Source locale auditée :
`E:\OpenRPG_AssetVault\normalized\quaternius\stylized-nature-megakit-standard-1.0`.
Route d'archive épinglée par sa métadonnée :
<https://store.godotengine.org/asset/quaternius/stylized-nature-megakit/download/31/>.

| Preuve | Valeur vérifiée |
|---|---|
| pack | Quaternius — Stylized Nature MegaKit, Standard 1.0 |
| licence | `CC0-1.0`, vérifiée, attribution non requise |
| modèles retenus / modèles glTF disponibles | **9 / 68** |
| fichiers distribués | **27** fichiers source/licence, plus un manifeste Forge |
| taille des 27 fichiers | **16 652 493 octets** |
| géométrie `.bin` | 9 fichiers, 1 359 988 octets |
| descriptions `.gltf` | 9 fichiers, 26 419 octets |
| textures `.png` partagées | 8 fichiers, 15 265 234 octets |
| notice originale | 1 fichier, 852 octets |
| géométrie unique déclarée par les neuf glTF | 26 881 sommets, 20 844 triangles |
| archive source originale | 104 088 529 octets ; non copiée dans Forge |

Modèles retenus : `CommonTree_1`, `CommonTree_3`, `Pine_2`, `Bush_Common_Flowers`, `Fern_1`,
`Rock_Medium_1`, `Rock_Medium_3`, `Mushroom_Laetiporus` et `Flower_4_Group`. Seuls leurs neuf
descripteurs, neuf buffers, huit textures effectivement référencées et la notice originale sont
présents. Aucun projet moteur, aperçu, média promotionnel, archive ou modèle non sélectionné n'est
importé.

| Objet de preuve hors dépôt | SHA-256 |
|---|---|
| archive originale `Stylized_Nature_MegaKitStandard.zip` | `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9` |
| métadonnée normalisée du coffre | `e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d` |
| notice `License_Standard.txt` | `120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58` |

Le manifeste
`assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json`
porte chaque chemin source/destination, taille, SHA-256 et la transformation
`byte-for-byte-copy`. Le contrôle local du coffre relit les 27 sources et les 27 copies : **27/27
identiques, 0 divergence**. `THIRD_PARTY_NOTICES.md` conserve la provenance malgré l'absence
d'obligation d'attribution en CC0. La notice conserve ses octets originaux via un attribut Git
`binary`; les blobs indexés des 27 fichiers correspondent aux fichiers qualifiés (**27/27**).

## Projection déterministe et repli

`VisualValleySceneryPlugin` régénère les placements à partir de la paire canonique
terrain/végétation. Un sélecteur stable dérivé de chaque identifiant de placement choisit un nombre
borné de remplacements et détails autour du point joueur `(16, 8)`. Le plafond dur est de 512
instances ; aucune position aléatoire runtime n'est introduite.

Les racines d'instance liées aux neuf scènes glTF sont initialement `Hidden`. Le plugin attend l'état
`Loaded` de chaque scène, de ses dépendances directes et récursives. Il révèle toutes les racines CC0
en une seule transition, puis
masque uniquement les représentations procédurales portant les identifiants remplacés. Les collisions
et le cache logique restent présents. Si une scène, un buffer ou une texture échoue, aucune racine
CC0 n'est révélée et la végétation procédurale reste visible ; le statut explicite devient
`procedural_fallback_load_failure`.

Après activation, la visibilité héritée de chaque racine est pilotée par la caméra 3D active. Les
remplacements reçoivent un seuil stable réparti dans la transition HLOD canonique **72–180 m** : la
population glTF décroît progressivement pendant que le batch HLOD procédural apparaît, puis aucun
mesh glTF de remplacement ne subsiste au-delà de 180 m. Les rochers, fleurs et champignons, qui n'ont
pas de proxy HLOD, sont bornés à 120 m. Le seuil est dérivé de l'identifiant canonique et ne varie pas
d'une exécution à l'autre.

La même logique Rust compile en natif et pour `wasm32-unknown-unknown`. Trunk copie `assets/` dans le
bundle Studio afin que les URI AssetServer restent identiques. La configuration Trunk a été parsée,
mais aucun bundle n'a été construit dans ce lot : il s'agit donc d'une préparation du packaging Web,
pas d'une preuve que les 27 fichiers ont été émis ni d'une qualification WebGPU. Sur la base isolée
de ce lot, le Studio n'instancie pas encore Visual Valley. L'intégration avec le lot Studio Visual
Valley doit ajouter explicitement
`VisualValleySceneryPlugin` au même chemin de scène avant le smoke navigateur.

### API d'observation pour le ledger Studio

`SceneryProjectionStats` est une ressource Bevy publique, stable, clonable et sérialisable, insérée
par `VisualValleySceneryPlugin::build` avant les systèmes `Startup` et `Update`. Le raccord Studio
doit demander `Option<Res<SceneryProjectionStats>>` dans son collecteur puis consigner au minimum :

- `status` : `pending`, `activated` ou `procedural_fallback_load_failure` ;
- `loaded_models`, `failed_models` et `hidden_procedural_entities` ;
- `replacement_instances`, `detail_instances`, `manifest_hash`, `tracked_files` et `tracked_bytes` ;
- `replacement_lod_start_m`, `replacement_lod_end_m` et `detail_cull_distance_m`.

Le commit Studio Visual Valley étant un lot parallèle absent de cette base exacte, ce rapport ne
revendique pas que son ledger observe déjà la ressource. Après assemblage, le ledger ne pourra
conclure à l'activation que si `status == activated`, les neuf modèles sont chargés, aucun modèle
n'est en échec et le nombre de procéduraux cachés correspond aux remplacements déclarés. Le statut
de repli doit être conservé tel quel et ne doit jamais être présenté comme une activation réussie.

## Validation locale

| Contrôle | Résultat observé |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `cargo check -j1 -p forge_engine --all-features --all-targets` | **PASS** |
| tests ciblés `scenery::tests::` | **PASS — 6/6, 0 échec, 76 filtrés** |
| tests ciblés runtime `visual_valley` | **PASS — 3/3, 0 échec, 23 filtrés** |
| Clippy strict moteur + runtime | **PASS — tous targets/features, `-D warnings`** |
| check moteur `wasm32-unknown-unknown` feature `web` | **PASS — sans features natives par défaut** |
| configuration Trunk Studio | **PASS — manifeste parsé ; bundle et exécution WebGPU NOT RUN** |
| `git diff --cached --check` et audit du diff | **PASS — 37 fichiers code/assets, aucun écart d'espace ; 27/27 blobs source exacts** |

Les tests ciblés refusent un manifeste non CC0, un chemin absolu ou traversant, une identité Cadle,
un modèle manquant, une cardinalité différente ou une empreinte de provenance non épinglée. Ils
recalculent aussi les 27 tailles et SHA-256 du dépôt, vérifient les dépendances URI des neuf glTF,
refusent tout fichier non déclaré dans le répertoire et rejouent deux fois la sélection.

## Qualification GPU et visuelle différée

**NOT RUN dans ce lot autonome.** À la demande du responsable d'intégration, la capture doit attendre
le runtime combiné de Gate 1 (décor, eau, météo et viewport Studio) afin de ne pas produire une preuve
intermédiaire déjà obsolète. En conséquence, ce rapport ne prétend ni activation AssetServer
observée, ni amélioration visuelle constatée, ni absence d'artefact GPU.

Commande qualifiante prévue après intégration :

```powershell
$env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.cargo-target-cc0-scenery'
cargo run -j1 -p forge_runtime --locked -- --visual-valley --player-camera `
  --capture 'E:\OpenRPG_Forge\.gate1-cc0-scenery-artifacts\visual-valley-cc0-player.png' `
  --capture-frame 240 `
  --metrics 'E:\OpenRPG_Forge\.gate1-cc0-scenery-artifacts\visual-valley-cc0-metrics.json' `
  --metrics-warmup-frames 180 --exit-after-frames 300 --no-vsync 2>&1 |
  Tee-Object 'E:\OpenRPG_Forge\.gate1-cc0-scenery-artifacts\visual-valley-cc0-runtime.log'
```

La future qualification doit conserver et hasher la capture, le JSON métriques et le log contenant
l'événement `visual_valley_scenery_activated`. La caméra `--player-camera` est obligatoire : la vue
overview est normalement au-delà de la transition 180 m et montre le HLOD, pas les détails CC0.

## Frontières de preuve

- Les assets CC0 sont statiques : ils ne consomment pas encore le shader de vent procédural.
- Le sous-ensemble ne crée pas de nouvelles collisions ; les collisions procédurales existantes
  restent la source runtime.
- Le smoke natif de développement n'est pas le benchmark Gate 1 sur RTX 3060/RX 6600.
- La compilation WASM et le bundle Trunk ne prouvent pas une exécution du pipeline sous WebGPU.
- Aucun contenu Cadle, aucune capture Cadle et aucun asset Cadle n'est présent. Le terme apparaît
  uniquement dans les garde-fous négatifs et dans cette déclaration de provenance.
