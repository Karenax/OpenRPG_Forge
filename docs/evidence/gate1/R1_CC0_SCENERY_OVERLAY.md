# Gate 1 — sous-ensemble CC0 et overlay de décor de Visual Valley

Date de qualification locale : **2026-09-01**

Base exacte du worktree isolé : **`02183f5972a9437630c18e71d4c5e7601af73b66`**

Commit du lot d'origine : **`e1b5ebaf7d0de18429dc1160cb955aadb54fb9b1`**

Candidat final de provenance : **`92286910a9bab622d5d54905551c0f03172e98e7`**, tree
**`6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe`**

Verdict de provenance CC0 au candidat final : **PASS AUTOMATISÉ LOCAL**

Verdict visuel, artistique, GPU/WebGPU et benchmark : **OUVERT — NON QUALIFIÉ PAR CET AUDIT**

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

## Rejeu final de provenance sur `92286910`

Depuis le worktree propre `E:\OpenRPG_Forge\.gate1-candidate` :

```powershell
& '.\scripts\Test-Gate1Provenance.ps1' -Mode SelfTest

& '.\scripts\Test-Gate1Provenance.ps1' -Mode Audit `
  -RequireCleanTree `
  -RequireAssetManifest `
  -CadleReferenceRoot 'E:\OpenRPG_Forge\OpenRPG_Forge-Rust\upstream\cadle'
```

Résultats exacts :

```text
FORGE_GATE1_PROVENANCE_SELF_TEST_PASS cases=7
FORGE_GATE1_PROVENANCE_AUDIT_PASS manifests=1 files=27 models=9
```

Le self-test retourne le code `0` en **5,22 s** et l'audit strict le code `0` en **12,00 s**.
L'arbre Forge reste propre avant et après. Aucun `CargoCheck` n'est relancé par cette qualification de
provenance.

| Objet final | Valeur recalculée ou recoupée |
|---|---|
| manifeste | SHA-256 `c86c4454b3b0ae4ca0bb546d22b29f3e79a0bfd8107ef63b8af1f0ebbffd6677` |
| archive source hors Forge | 104 088 529 octets ; SHA-256 `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9` |
| métadonnées normalisées | SHA-256 `e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d` |
| fichiers / modèles / octets suivis par le manifeste | **27 / 9 / 16 652 493** |
| transformations | **27/27 `byte-for-byte-copy`** |
| licence | `CC0-1.0`, vérifiée, attribution non requise |
| notice | `License_Standard.txt`, hash déclaré et réel `120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58` |

Le clone Cadle de comparaison reste extérieur au candidat, détaché et propre au commit
`ea0da62549980816a6a27e08b75808b1f56d70e7` et au tree
`f0d52e434454fe302052d22eafde203bc361cc1f`. L'audit hache ses **507** fichiers et compare **39**
fichiers canoniques Forge : **0** copie exacte, **0** ligne normalisée commune d'au moins 40
caractères, **0** payload `upstream/cadle/**` suivi et **0** marqueur de réutilisation déclaré. Le
candidat ne contient aucun dossier `upstream/cadle`, seulement le lock de gouvernance versionné.

Ce PASS établit la provenance, la licence, l'intégrité et l'absence de copie Cadle. Il ne démontre
pas que les modèles sont beaux, bien cadrés, sans artefact GPU ou supérieurs à la référence ; ces
questions restent réservées aux captures natives/WebGPU et à la revue humaine comparative.

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
mais le lot d'origine n'avait construit aucun bundle : il s'agissait alors d'une préparation du
packaging Web, pas d'une preuve WebGPU. Dans le candidat final, le chemin Visual Valley du Studio
instancie `VisualValleySceneryPlugin` et expose son observation dans le ledger de scène. Le smoke
Browser final `a34596bd…` active **9/9** modèles, charge les **26** payloads glTF/bin/PNG avec HTTP
200 et ne produit ni requête `.meta`, ni échec réseau, ni entrée console. Voir
[`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md).

### API d'observation pour le ledger Studio

`SceneryProjectionStats` est une ressource Bevy publique, stable, clonable et sérialisable, insérée
par `VisualValleySceneryPlugin::build` avant les systèmes `Startup` et `Update`. Le raccord Studio
doit demander `Option<Res<SceneryProjectionStats>>` dans son collecteur puis consigner au minimum :

- `status` : `pending`, `activated` ou `procedural_fallback_load_failure` ;
- `loaded_models`, `failed_models` et `hidden_procedural_entities` ;
- `requested_instances`, `replacement_instances`, `detail_instances` et `selected_by_kind` ;
- `manifest_hash`, `tracked_files` et `tracked_bytes` ;
- `replacement_lod_start_m`, `replacement_lod_end_m` et `detail_cull_distance_m`.

Le ledger du candidat final ne peut conclure à l'activation que si `status == activated`, les neuf
modèles sont chargés, aucun modèle n'est en échec et les quatre comptes dérivés correspondent
exactement à `selected_by_kind`. Le contrat courant est : remplacements = arbres + arbustes +
fougères ; détails = rochers + détails ; demandées = remplacements + détails ; procéduraux cachés =
`2 × arbres + arbustes + fougères`, car un arbre procédural possède deux entités LOD. Le statut de
repli est conservé tel quel et ne peut jamais être présenté comme une activation réussie.

## Validation locale

| Contrôle | Résultat observé |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `cargo check -j1 -p forge_engine --all-features --all-targets` | **PASS** |
| tests ciblés engine `scenery::tests::` finaux | **PASS — 9/9, 0 échec** |
| tests ciblés runtime `visual_valley` | **PASS — 3/3, 0 échec, 23 filtrés** |
| Clippy strict moteur + runtime | **PASS — tous targets/features, `-D warnings`** |
| check moteur `wasm32-unknown-unknown` feature `web` | **PASS — sans features natives par défaut** |
| smoke Browser clear/rain High | **PASS AUTOMATISÉ BORNÉ à `a34596bd…`** — 24/24 contrôles chacun, activation 9/9, 26 assets HTTP 200 |
| `git diff --cached --check` et audit du diff | **PASS — 37 fichiers code/assets, aucun écart d'espace ; 27/27 blobs source exacts** |

Les tests ciblés refusent un manifeste non CC0, un chemin absolu ou traversant, une identité Cadle,
un modèle manquant, une cardinalité différente ou une empreinte de provenance non épinglée. Ils
recalculent aussi les 27 tailles et SHA-256 du dépôt, vérifient les dépendances URI des neuf glTF,
refusent tout fichier non déclaré dans le répertoire et rejouent deux fois la sélection.

## Qualification Browser acquise, qualification artistique ouverte

Les deux exécutions Browser clear/rain High passent chacune 24/24 contrôles structurés. Le scene
ledger observe `status=activated`, 9 modèles chargés, 0 échec, 125 instances demandées, 99
remplacements, 26 détails et 147 entités procédurales cachées. La parité
`source_manifest_matched` est interne au Studio ; `gpu_presentation=not_observed`. Les temps clear
(moyenne 10,7283 ms, p95 13,4 ms, 1 % low 54,945 FPS) et rain (9,1683 ms, 10,1 ms, 84,034 FPS)
mesurent la boucle WASM et non le GPU.

Les captures 1920 × 1080 sont versionnées et inspectées. Elles montrent bien le décor actif et la
pluie, mais aussi une palette criarde, des noirs dominants et un overlay envahissant. L'activation
fonctionnelle est acquise ; la revue artistique humaine, la comparaison A/B Cadle, le GPU cible et
la présentation GPU restent ouverts.

## Frontières de preuve

- Les assets CC0 sont statiques : ils ne consomment pas encore le shader de vent procédural.
- Le sous-ensemble ne crée pas de nouvelles collisions ; les collisions procédurales existantes
  restent la source runtime.
- Le smoke natif de développement n'est pas le benchmark Gate 1 sur RTX 3060/RX 6600.
- Le smoke Browser et ses pixels ne remplacent pas une sonde de présentation GPU ; le ledger conserve
  honnêtement `gpu_presentation=not_observed`.
- Aucun contenu Cadle, aucune capture Cadle et aucun asset Cadle n'est présent. Le terme apparaît
  uniquement dans les garde-fous négatifs et dans cette déclaration de provenance.
