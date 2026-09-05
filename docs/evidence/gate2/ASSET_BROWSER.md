# Gate 2 — navigateur d’assets vérifiés

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `7dafbe2dd13f2bc40516fab4e891a063b285aa88`

Arbre source : `f201b89d20222f4e725a0f746fc99ce54ad79060`

Parent source : `1997d838a2b342ed08f15ce6a3018fc9cd80185f`

**R2-026 est `PASS` sur qualification fonctionnelle automatisée d’un navigateur local d’assets
vérifiés, borné et sans persistance. Gate 2 atteint 25/43 et reste ouverte.** Ce verdict ne qualifie
ni l’UI native, ni un build natif autonome ou Release, ni le GPU, la performance, PresentMon ou la
CI distante.

Le manifeste des reçus est
[`ASSET_BROWSER_ARTIFACTS.json`](ASSET_BROWSER_ARTIFACTS.json). Aucun journal brut n’a été capturé :
`artifacts` reste volontairement vide. `Test-Gate2AssetBrowserEvidence.ps1` relie cette preuve au
commit, à son arbre, à son parent, à l’inventaire exact des 14 fichiers source et au manifeste
d’assets immuable, sans relancer de build ni scanner le système de fichiers de production.

## Snapshot dérivé et registre gouverné

`forge_assets::browser` expose sur natif et WASM les DTO sérialisables du snapshot dérivé
`forge.asset-browser-snapshot` v1. Le scanner filesystem est exclusivement natif, hors ligne et non
persistant. Il ne désérialise que les fichiers nommés exactement `subset.manifest.json`, refuse une
racine sans manifeste et publie des entrées triées par `AssetId`, avec catégories, chemins, tailles,
SHA-256, licence et provenance.

Le registre Forge indépendant n’autorise pour cette tranche qu’un sous-ensemble :

- sous-ensemble `visual-valley-quaternius-stylized-nature-standard-1.0` ;
- pack `quaternius:stylized-nature-megakit-standard-1.0` ;
- manifeste SHA-256 `c86c4454b3b0ae4ca0bb546d22b29f3e79a0bfd8107ef63b8af1f0ebbffd6677` ;
- archive SHA-256 `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9` ;
- métadonnées normalisées SHA-256
  `e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d` ;
- licence `CC0-1.0`, vérifiée, sans attribution requise, prouvée par `License_Standard.txt` au
  SHA-256 `120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58`.

Le manifeste v1 lie exactement **1 manifeste, 9 modèles et 27 fichiers déclarés** : 26 fichiers de
contenu plus une preuve de licence. L’identité stable est gouvernée par
`lowercase(source.pack_id/model.id)`, jamais par le nom local `subset_id`. Changer seulement la casse
conserve donc l’`AssetId`, tandis que l’empreinte du snapshot lie le contenu canonique complet.

## Scanner strict et budgets

La collecte `read_dir` est arrêtée en streaming avant tri ou allocation non bornée. Les limites sont
6 niveaux, 256 répertoires, 32 manifestes, 512 modèles, 4 096 fichiers et un plafond global égal à
la somme répertoires + manifestes + fichiers. Un manifeste vaut au plus 256 Kio et tous les
manifestes 4 Mio ; un fichier vaut au plus 128 Mio et tous les fichiers 512 Mio ; un chemin portable
vaut au plus 256 octets.

Chaque scène `.gltf` JSON est elle-même bornée à 8 Mio, 32 Mio cumulés, 512 URI par scène et 4 096 URI
au total. Les buffers et images doivent résoudre vers un chemin local déclaré, relativement au
dossier de la scène ; un sous-chemin local est accepté. Sont
refusés : chemins absolus, parent `..`, composant `.`, antislash, lien symbolique ou reparse point,
composant Cadle, doublon, fichier absent/non déclaré/hors dossier, URI `file:`, `http:`, `https:` ou
`data:`. Le format GLB est refusé explicitement en v1 plutôt que partiellement inspecté.

Le scanner reçoit la racine d’assets compilée du checkout et suppose cette racine fiable et stable
pendant l’appel. Il ne prétend pas constituer une frontière sûre contre une mutation concurrente
hostile (TOCTOU). Une racine d’installation packagée appartient à Gate 4.

## Studio, IPC et cache

La source Studio ajoute `Assets` comme quatrième fenêtre, entrée de dock et menu supérieur. Le
panneau permet consultation, recherche, filtre par catégorie, sélection et détail de provenance. Il
ne demande le catalogue qu’à la première visibilité réelle de la fenêtre. Sans backend Tauri, le
navigateur web affiche explicitement « Backend Assets absent », n’invente aucun catalogue et
n’accède pas au filesystem.

`BrowseAssetsRequest` v1 est strict, ne contient aucune racine choisie par l’utilisateur et utilise
un `request_serial` client uniquement pour la corrélation ; toute valeur supérieure à zéro est
acceptée, y compris 1 après rechargement ou une valeur élevée. Le service conserve un snapshot
valide atomique, différencie premier scan, hit et refresh, et exécute le scan natif via
`spawn_blocking`.

Un ticket opaque serveur autorise exactement un scan requis en vol. Une seconde demande nécessitant
un scan est rejetée sans lancer un second worker ; un hit sur cache chaud reste disponible. Succès,
erreur de scan, erreur de validation ou erreur de join libèrent uniquement le ticket exact. Un
résultat périmé ne peut ni libérer le scan courant ni remplacer le cache ; un refresh échoué conserve
le snapshot valide précédent et permet un retry.

## Compatibilité, dépendances et frontière produit

Le lot modifie exactement 14 fichiers source, dont exactement trois métadonnées Cargo :
`Cargo.lock`, `apps/forge_studio/Cargo.toml` et `crates/forge_assets/Cargo.toml`. Aucun nouveau package
tiers ni aucune nouvelle version n’est ajouté. Le lockfile matérialise seulement trois arêtes déjà
épinglées dans le workspace : Studio vers `forge_assets`, puis `forge_assets` natif vers
`serde_json` et `sha2`.

Aucun asset existant n’est modifié ; le sous-ensemble gouverné est seulement lu. Aucun contenu ou
chemin Cadle n’est ajouté. Les contrats `gate0_procedural` et `validate_procedural_only` restent
sémantiquement inchangés. R2-026 ne place, n’importe et ne génère aucun thumbnail ou nouvel asset ;
le drag/drop et le placement commencent en R2-027.

## Preuves automatisées acquises

Une campagne verrouillée unique totalise **640 tests de bibliothèque PASS** :

`cargo test -p forge_assets -p forge_project -p forge_world -p forge_engine -p forge_studio --lib
--locked`

- `forge_assets` : **21/21 PASS** ;
- `forge_project` : **149/149 PASS** ;
- `forge_world` : **133/133 PASS** ;
- `forge_engine` : **201/201 PASS** ;
- `forge_studio` : **136/136 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio --locked` : **6/6 PASS**, hors du
total des bibliothèques. Les trois tests unitaires du DTO/filtrage/enveloppe IPC Assets repassent
également **3/3 PASS** après leur adaptation cross-platform avec un snapshot synthétique ; la
campagne de 640 les couvre dans son état final.

Les contrôles acquis sont :

- Clippy natif strict combiné Assets + Studio `--all-targets --locked` : **PASS** ;
- après les trois correctifs natifs de concurrence/cache, Clippy Studio final
  `--all-targets --all-features` strict : **PASS** ;
- check Studio WASM web `--locked` : **PASS** ;
- Clippy Assets WASM `--locked` strict et Studio WASM web `--locked` strict : **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true`, profil debug : **PASS** ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Le premier lancement de la campagne intégrée a échoué avec `LNK1318: Erreur PDB inattendue; LIMIT
(12)` alors que le volume E: ne disposait plus que de 33 Mio : la faible place est un contexte
plausible, pas une cause prouvée de cette erreur linker. Le premier build Trunk réel a, lui, échoué
explicitement par `no space on device`. `cargo clean -p forge_studio` a récupéré 3,1 Gio, puis des
nettoyages ciblés des packages du workspace ont récupéré 16,9 Gio supplémentaires ; seuls des
artefacts de compilation régénérables ont été supprimés, aucun fichier source ni asset. Les
exécutions finales ci-dessus sont vertes. Aucun journal brut n’est revendiqué.

Un diagnostic supplémentaire `cargo check ... --target wasm32-unknown-unknown ... --tests` reste
bloqué par l’import natif préexistant `area_preview -> workspace_service`. Ce diagnostic hors route
ne remet pas en cause le check WASM produit standard, relancé avec succès, ni le test IPC désormais
cross-platform ; il ne constitue pas non plus un faux PASS des harnais WASM complets.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN** ;
- build natif autonome et builds Release : **NOT RUN** ;
- GPU, performance et PresentMon : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- racine packagée autonome : différée à Gate 4 ;
- placement, import, thumbnail, drag/drop et picking : non livrés ;
- aucun asset ni contenu Cadle copié, créé ou modifié.

La prochaine tranche fonctionnelle est `R2-027`, drag/drop.
