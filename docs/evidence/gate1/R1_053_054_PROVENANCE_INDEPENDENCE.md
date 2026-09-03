# Gate 1 — R1-053 / R1-054 — provenance et indépendance Cadle

Date du rejeu qualifiant local : **2026-09-03**

Commit candidat qualifié : **`4e476b31ee33533b6f4023804d8ecd1be02f9c55`**

Tree Git qualifié : **`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`**

Worktree qualifié : **`E:\OpenRPG_Forge\.gate1-qualification-055cd36a`**, branche `Rust`, propre,
aligné avec `origin/Rust` à divergence `0/0` avant et après l'audit.

Référence Cadle extérieure : commit **`ea0da62549980816a6a27e08b75808b1f56d70e7`**, tree
**`f0d52e434454fe302052d22eafde203bc361cc1f`**, HEAD détaché et arbre propre dans le seul
checkout ignoré `upstream/cadle` ; aucun de ses fichiers n'est suivi par Forge.

Verdict de provenance et d'indépendance source : **PASS AUTOMATISÉ LOCAL**.

Verdict visuel, artistique, WebGPU navigateur et benchmark matériel : **OUVERT — NON QUALIFIÉ PAR CE
RAPPORT**.

Le candidat logiciel qualifié contient le sous-ensemble CC0 gouverné. L'audit strict prouve ses tailles, hashes,
licence et suivi Git, ainsi que l'absence de payload, dépendance ou copie Cadle. Ce PASS de provenance
ne ferme aucune validation visuelle de Gate 1.

## Rejeu qualifiant sur `4e476b31`

Commandes réellement exécutées depuis `E:\OpenRPG_Forge\.gate1-qualification-055cd36a` :

```powershell
$Forge = 'E:\OpenRPG_Forge\.gate1-qualification-055cd36a'
$Cadle = Join-Path $Forge 'upstream\cadle'
$Out = 'E:\OpenRPG_Forge\.gate1-nongpu-proof-4e476b31\gate1-provenance.json'

pwsh -NoProfile -File "$Forge\scripts\Get-Cadle.ps1" -RepositoryRoot $Forge
pwsh -NoProfile -File "$Forge\scripts\Test-CadleGateC.ps1" `
  -RepositoryRoot $Forge -RequireCheckout
pwsh -NoProfile -File "$Forge\scripts\Test-Gate1Provenance.ps1" `
  -Mode SelfTest -RepositoryRoot $Forge

pwsh -NoProfile -File "$Forge\scripts\Test-Gate1Provenance.ps1" -Mode Audit `
  -RepositoryRoot $Forge `
  -RequireCleanTree `
  -RequireAssetManifest `
  -CadleReferenceRoot $Cadle `
  -OutputPath $Out

git status --short
git diff --check
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'
```

Sorties exactes :

```text
CADLE_ACQUISITION_PASS
CADLE_GATE_C_BOUNDARY_PASS tracked=396 checkout_present=True
FORGE_GATE1_PROVENANCE_SELF_TEST_PASS cases=7
FORGE_GATE1_PROVENANCE_AUDIT_PASS manifests=1 files=27 models=9
4e476b31ee33533b6f4023804d8ecd1be02f9c55
cc6efae0289b10dcf12f97e6bb4d97b3ead29f90
```

`git status --short` et `git diff --check` ne produisent aucune ligne. Tous les scripts retournent le
code `0`. Le JSON externe mesure **915 octets** et porte le SHA-256
`7d28d2df849a3243773b92993f4876280088c4fdb967fd33923a5255374923c6`. Aucun `CargoCheck` n'a
été demandé pendant ce rejeu ; la qualification Cargo complète du même commit est consignée dans le
rapport Gate 1.

| Contrôle final | Résultat observé |
|---|---:|
| fichiers Forge suivis | **396** |
| fichiers produit dépendance/configuration inspectés | **74** |
| fichiers canoniques Visual Valley comparés | **39** |
| manifestes / fichiers / modèles validés | **1 / 27 / 9** |
| fichiers suivis sous `upstream/cadle/**` | **0** |
| checkout `upstream/cadle` dans le worktree | **présent, ignoré et non suivi** |
| marqueurs de réutilisation Cadle | **0** |
| fichiers Cadle hachés en lecture seule | **507** |
| fichiers exactement identiques à Cadle | **0** |
| lignes normalisées communes d'au moins 40 caractères | **0** |
| arbres Forge et Cadle | **propres** |

Le lock v2 `upstream/cadle.lock.json` porte le SHA-256
`6e0608b93da299285304996a7d5769de2dce1581ed86f3e86c0b51a40b546630`. Il épingle l'URL
`https://github.com/Humpalumps/Cadle.git`, le commit et le tree ci-dessus ; le checkout local reste
ignoré, hors arbre Git et utilisé uniquement en lecture seule.

Le manifeste
`assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json`
porte le SHA-256 `c86c4454b3b0ae4ca0bb546d22b29f3e79a0bfd8107ef63b8af1f0ebbffd6677`.
Il valide :

- subset `visual-valley-quaternius-stylized-nature-standard-1.0` et pack
  `quaternius:stylized-nature-megakit-standard-1.0` ;
- archive de **104 088 529 octets**, SHA-256
  `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9` ;
- métadonnées normalisées SHA-256
  `e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d` ;
- **27 fichiers**, **9 modèles**, **16 652 493 octets**, tous `byte-for-byte-copy` ;
- licence `CC0-1.0`, `verified=true`, attribution non requise ;
- `License_Standard.txt`, hash déclaré et recalculé
  `120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58`.

## Historique du lot d'outil `03a5ecb5`

Le lot initial avait qualifié le mécanisme d'audit avant l'arrivée des assets. Les mesures ci-dessous
sont conservées comme historique de construction de l'outil ; elles ne remplacent pas le rejeu qualifiant
ci-dessus.

## Résultat historique observé

Le script [`scripts/Test-Gate1Provenance.ps1`](../../../scripts/Test-Gate1Provenance.ps1) a été ajouté
avec trois modes :

- `SelfTest` construit une fixture Git synthétique et vérifie sept cas positifs/négatifs ;
- `Audit` contrôle les chemins et dépendances produit, les identités interdites, les manifestes
  d'assets, les licences, tailles et SHA-256, puis compare facultativement le contenu canonique au
  snapshot Cadle épinglé ;
- `IsolatedCheck` exporte `HEAD` par `git archive`, réinitialise un dépôt temporaire autonome, vérifie
  qu'il ne contient aucun remote, aucune ref Cadle et aucun dossier `upstream/cadle`, puis relance
  l'audit et le niveau de `cargo check` demandé.

Le script qualifié mesure **45 919 octets** et son SHA-256 est
`a14534f6bffcfb01ddf8594188b08aefbfd159801e83c3f13f442025dddbb1ff`.

| Contrôle | Résultat observé |
|---|---:|
| parse PowerShell sans erreur | PASS |
| `git diff --check` du commit technique | PASS |
| self-tests synthétiques | **7/7 PASS** |
| snapshot Cadle | commit `ea0da62549980816a6a27e08b75808b1f56d70e7`, tree `f0d52e434454fe302052d22eafde203bc361cc1f` |
| fichiers Cadle hachés | **507** |
| fichiers canoniques Visual Valley comparés | **11** |
| fichiers exactement identiques à Cadle | **0** |
| lignes normalisées communes d'au moins 40 caractères | **0** |
| fichiers produit dépendance/configuration inspectés | **65** |
| fichiers suivis sous `upstream/cadle/**` | **0** |
| marqueurs de réutilisation Cadle déclarés dans Visual Valley | **0** |
| remotes dans le checkout autonome | **0** |
| refs contenant `cadle` dans le checkout autonome | **0** |
| dossier `upstream/cadle` dans le checkout autonome | absent |
| arbre du checkout autonome | propre |
| `cargo check --locked -j 1 -p forge_core -p forge_project -p forge_world` | PASS |

Le ledger local ignoré `target/gate1-provenance/isolated-03a5ecb.json` mesure **1 696 octets** et
porte le SHA-256 `26ec148eb3641adc3ef9c579f8465b544a8c32534c67252adf14fabf001a02dc`.
Il relie le replay au commit et au tree ci-dessus ; il ne s'agit pas d'un artefact à publier.

## Cas négatifs réellement exercés

La fixture synthétique ne se limite pas au chemin heureux. Le self-test injecte successivement :

1. un fichier dont le contenu ne correspond plus au SHA-256 déclaré ;
2. un asset suivi absent de `manifest.files` ;
3. l'identité interdite `Whisperwood` dans le document Visual Valley ;
4. une dépendance `cadle` pointant vers `upstream/cadle` ;
5. une copie exacte et une ligne normalisée communes avec une référence épinglée synthétique.

Chaque mutation est refusée. Les deux autres cas sont les fixtures positives de manifeste et de
comparaison épinglée.

Un essai séparé avec `-RequireAssetManifest` sur ce commit sans asset échoue volontairement avec :

```text
At least one validated Visual Valley asset subset manifest is required.
```

Cette option doit être obligatoire lors du rejeu sur le candidat contenant le décor CC0. Elle évite
qu'une qualification finale passe simplement parce que les assets attendus ont été omis.

## Contrat de provenance des futurs assets

Tout fichier suivi sous `assets/visual_valley/` doit appartenir exactement à un
`subset.manifest.json` de schéma `forge.asset-subset-manifest` version 1. L'audit exige notamment :

- source, auteur, version, pages de source et de distribution HTTPS, URL d'archive, date de
  récupération, taille et SHA-256 de l'archive, empreinte des métadonnées normalisées ;
- identifiant SPDX reconnu, URL de licence HTTPS, validation explicite et fichier de preuve de
  licence lui-même déclaré et haché ;
- chemin portable, taille et SHA-256 exacts pour chaque fichier ; aucun fichier suivi orphelin et
  aucun fichier revendiqué par deux manifestes ;
- chaque scène modèle reliée à son fichier déclaré, son chemin source et une échelle positive ;
- aucune identité Cadle dans les chemins ou contenus textuels canoniques.

Le manifeste prouve l'acquisition déclarée et l'intégrité des fichiers présents. Il ne remplace pas
une expertise juridique de l'historique d'un tiers ni une revue humaine de la fidélité des
métadonnées publiées par ce tiers.

## Commandes historiques de construction

Le premier chemin ci-dessous désigne le checkout Forge à qualifier. Le second est uniquement la
référence Cadle locale épinglée ; l'étape autonome prouve ensuite que Forge n'en dépend pas.

```powershell
$Forge = 'E:\OpenRPG_Forge\.gate1-provenance'
$Cadle = 'E:\OpenRPG_Forge\OpenRPG_Forge-Rust\upstream\cadle'
$Target = "$Forge\target\gate1-provenance-cargo"

pwsh -NoLogo -NoProfile -File "$Forge\scripts\Test-Gate1Provenance.ps1" `
  -Mode SelfTest

pwsh -NoLogo -NoProfile -File "$Forge\scripts\Test-Gate1Provenance.ps1" `
  -Mode Audit -RequireCleanTree -CadleReferenceRoot $Cadle

pwsh -NoLogo -NoProfile -File "$Forge\scripts\Test-Gate1Provenance.ps1" `
  -Mode IsolatedCheck -CargoCheck Targeted -CargoTargetDirectory $Target `
  -CadleReferenceRoot $Cadle `
  -OutputPath "$Forge\target\gate1-provenance\isolated.json"
```

Ces commandes décrivent le lot d'outil antérieur. La reproduction du candidat qualifié doit utiliser les
commandes `SelfTest` et `Audit -RequireCleanTree -RequireAssetManifest` données en tête de ce rapport.
Le rejeu qualifiant a volontairement omis `IsolatedCheck` et Cargo ; il ne faut donc pas attribuer ces
étapes à l'audit de provenance de `4e476b31` dans cette preuve. Le portail Cargo complet du même
commit est une preuve séparée.

## Qualification exacte de R1-053 et R1-054

| Item | Preuve acquise au candidat `4e476b31` | Verdict borné |
|---|---|---|
| `R1-053` | manifeste CC0 strict, licence/hash/tailles exacts, zéro payload Cadle, zéro copie exacte et zéro ligne longue commune | **PASS automatisé de provenance** |
| `R1-054` | aucun clone ni payload Cadle suivi, aucun chemin de dépendance produit, lock seulement ; checkout de comparaison présent dans le worktree, mais ignoré, propre et hors arbre Git | **PASS automatisé d'indépendance source** ; aucun Cargo n'a été relancé par cet audit qualifiant |

## Limites et validations différées

- La comparaison exacte et lexicale détecte une copie textuelle ou binaire évidente ; elle ne prouve
  pas juridiquement l'absence de dérivation algorithmique ou sémantique.
- L'ancien check ciblé couvrait `forge_core`, `forge_project` et `forge_world`. Le rejeu qualifiant de
  provenance n'a lancé aucun Cargo afin de ne pas dupliquer les qualifications du candidat.
- Aucun asset Cadle n'a été copié, généré ou déplacé. Le checkout de référence est resté propre et
  n'a servi qu'à la comparaison locale en lecture seule.
- Le checkout autonome est produit depuis les fichiers suivis du commit. Il conserve donc les
  métadonnées de gouvernance `upstream/cadle.lock.json`, mais aucun clone, remote, ref, chemin de
  dépendance ou payload `upstream/cadle/`.
- La provenance PASS ne vaut ni revue artistique, ni inspection GPU, ni benchmark. Ces critères
  restent ouverts et sont traités dans des preuves séparées.
