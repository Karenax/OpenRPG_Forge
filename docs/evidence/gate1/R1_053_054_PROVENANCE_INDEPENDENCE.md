# Gate 1 — R1-053 / R1-054 — provenance et indépendance Cadle

Date de qualification locale : **2026-09-01**

Branche de travail : **worktree détaché depuis `d1f73e62e054695bdbf069a96bf83e7afd5edef1`**

Commit technique qualifié : **`03a5ecb5d486f242488363a7aea6e961bb75dc69`**

Tree Git qualifié : **`1738e3906e892e469dfa8fe3125a7f946b3fbc0d`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL DU CONTRAT D'AUDIT — REJEU OBLIGATOIRE SUR LE
CANDIDAT FINAL CONTENANT LES ASSETS**

Ce lot ajoute un audit reproductible, sans importer de contenu Cadle et sans modifier la roadmap ou
`PROJECT_CONTEXT.md`. Il qualifie l'état exact ci-dessus et rend vérifiable le futur lot d'assets
Gate 1. Il ne ferme pas encore `R1-053` ou `R1-054` dans la roadmap : ce commit ne contient aucun
asset externe sous `assets/visual_valley/`, et le candidat final évoluera après son intégration.

## Résultat observé

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

## Commandes de reproduction

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

Après intégration des assets CC0, les deux dernières commandes doivent ajouter
`-RequireAssetManifest`. Le résultat attendu doit alors avoir un nombre de manifestes et de fichiers
strictement positif, avec toutes les licences et empreintes validées.

## Qualification exacte de R1-053 et R1-054

| Item | Preuve acquise à ce commit | Statut de transition |
|---|---|---|
| `R1-053` | zéro payload Cadle suivi, zéro identité interdite, zéro copie exacte/ligne longue commune avec le snapshot épinglé ; audit strict des futurs manifestes testé | **Avancé, non fermé** : aucun asset externe n'est encore présent ; rejeu `-RequireAssetManifest` obligatoire après intégration |
| `R1-054` | checkout autonome propre, zéro remote, zéro ref Cadle, dossier `upstream/cadle` absent, audit et `cargo check` ciblé PASS | **Avancé, non fermé** : preuve valide pour ce commit ; rejeu obligatoire sur le candidat Gate 1 final |

## Limites et validations différées

- La comparaison exacte et lexicale détecte une copie textuelle ou binaire évidente ; elle ne prouve
  pas juridiquement l'absence de dérivation algorithmique ou sémantique.
- Le check ciblé couvre `forge_core`, `forge_project` et `forge_world`. Le workspace complet, le
  moteur de rendu, le runtime natif, WASM/WebGPU, Tauri et le Studio n'ont pas été recompilés par ce
  lot afin de ne pas dupliquer les compilations Gate 1 déjà en cours.
- Aucun asset Cadle n'a été copié, généré ou déplacé. Le checkout de référence est resté propre et
  n'a servi qu'à la comparaison locale en lecture seule.
- Le checkout autonome est produit depuis les fichiers suivis du commit. Il conserve donc les
  métadonnées de gouvernance `upstream/cadle.lock.json`, mais aucun clone, remote, ref, chemin de
  dépendance ou payload `upstream/cadle/`.
- La roadmap et `PROJECT_CONTEXT.md` doivent être mis à jour seulement lors de l'intégration dans le
  candidat Gate 1, après le rejeu strict sur les assets réellement retenus.
