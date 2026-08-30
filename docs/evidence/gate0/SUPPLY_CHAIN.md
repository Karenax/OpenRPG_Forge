# Gate 0 — Audit de la chaîne d’approvisionnement Rust

Date d’exécution : **2026-08-30**
Branche : **`Rust`**
HEAD au moment de l’audit : `7db78a3432bd6836c6daa3911017b30fe2bf6b1a`
Portée : `Cargo.lock` et politique `deny.toml` de Gate 0, toutes features, cibles Windows, Linux et
WASM définies dans `deny.toml`.

## Verdict

Le graphe verrouillé ne contient **aucune vulnérabilité RustSec connue** au snapshot de base de
données audité. Les licences et les sources passent sans erreur ni avertissement. Le replay final de
`cargo deny check` et celui de `cargo audit --no-fetch --json` terminent tous deux avec le code `0`.

Le verdict n’est pas « sans alerte » : 43 groupes de versions transitives dupliquées restent visibles,
19 avis `unmaintained` font l’objet d’exceptions exactes et motivées, et `cargo audit` signale une
unsoundness informationnelle dans `glib 0.18.5`, transitive au shell Linux Tauri. Cette dernière
n’est pas ignorée dans `deny.toml` et doit être réévaluée avant toute qualification de distribution
Linux.

Cette preuve est valide uniquement pour :

- `Cargo.lock` SHA-256
  `ebc7f6e67c0236479b08f5ed36e36681498eb208b80360bf4f73bb9c6b53a69d` ;
- `Cargo.lock` taille exacte : `237166` octets, `9942` lignes ;
- `deny.toml` SHA-256
  `0765300cc47cdac77d5aded29d82a700cdba28266ab1af31008bfbc0933e9202`.

Toute modification de l’un de ces fichiers rouvre cet audit.

## Environnement et outils

| Élément | Valeur vérifiée |
| --- | --- |
| Hôte | Windows `x86_64-pc-windows-msvc` |
| `rustc` | `1.98.0 (88d9e12ae 2026-08-18)` |
| LLVM | `22.1.8` |
| `cargo` | `1.98.0 (797e8a9bc 2026-08-05)` |
| `cargo-deny` | `0.20.2` |
| `cargo-audit` | `0.22.2` |
| RustSec DB | commit `b331df68b3ed0e99594d259040bdcb9de3c7c8a4` |
| RustSec DB date | `2026-08-29T08:11:09+02:00` |
| Format lockfile | Cargo lock version `4` |

## Graphe verrouillé

`cargo metadata --locked --format-version 1 --all-features` et `cargo audit` observent :

| Mesure | Résultat |
| --- | ---: |
| Paquets dans `Cargo.lock` | 871 |
| Paquets crates.io | 863 |
| Membres locaux du workspace | 8 |
| Sources Git | 0 |
| Registres inconnus | 0 |
| Taille de `Cargo.lock` | 237166 octets |
| Lignes de `Cargo.lock` | 9942 |

L’union filtrée de `cargo-deny` couvre 684 crates : 500 sous Windows, 555 sous Linux et 506 sous
WASM, avec chevauchement entre les cibles. `cargo audit` inspecte les 871 entrées du lockfile, y
compris les dépendances conditionnelles qui ne sont pas actives sur l’hôte Windows. Le changement
final du graphe ajoute `serde-wasm-bindgen 0.6.5` sous WASM, licence MIT ;
`wasm-bindgen-futures 0.4.77` était déjà présent transitivement et devient une dépendance directe du
Studio WASM.

## Politique corrigée

Les corrections sont limitées à `deny.toml` :

1. ajout de `MIT-0`, licence permissive OSI rencontrée dans cinq crates ;
2. ajout de `Apache-2.0 WITH LLVM-exception`, expression SPDX rencontrée dans six crates ;
3. retrait de l’autorisation `OpenSSL`, non rencontrée dans le graphe ;
4. ajout de `allow-wildcard-paths = true` tout en conservant `wildcards = "deny"` : seuls les chemins
   privés internes au workspace peuvent omettre une version, jamais une dépendance registry ou Git ;
5. ajout de 19 exceptions RustSec par identifiant, chacune avec raison et origine transitive. Ces avis
   sont tous informationnels et classés `unmaintained`; aucune vulnérabilité n’est ignorée.

Les contrôles restent stricts : dépendance externe wildcard interdite, registre inconnu interdit,
source Git inconnue interdite, crate yanked interdite et liste de licences explicitement bornée.

## Résultats `cargo deny`

Les contrôles détaillés ont été exécutés avec `--locked` :

```powershell
cargo deny --format json --color never --locked check licenses --hide-inclusion-graph --show-stats
cargo deny --format json --color never --locked check bans --hide-inclusion-graph --show-stats
cargo deny --format json --color never --locked check sources --hide-inclusion-graph --show-stats
cargo deny --format json --color never --locked check advisories --hide-inclusion-graph --show-stats
cargo deny --format json --color never --locked check --hide-inclusion-graph --show-stats
```

Après stabilisation du manifeste Studio et du lockfile, deux empreintes prises à dix secondes
d'intervalle sont restées identiques. La dernière commande combinée ci-dessus a alors été rejouée
sur le SHA-256 final `ebc7f6e67c0236479b08f5ed36e36681498eb208b80360bf4f73bb9c6b53a69d` :
code `0`, 0 erreur, 43 avertissements de duplication et 38 notes correspondant aux 19 exceptions
exactes rencontrées.

| Contrôle | Code | Erreurs | Avertissements | Notes/aides |
| --- | ---: | ---: | ---: | ---: |
| Licences | 0 | 0 | 0 | 691 aides de résolution |
| Bans | 0 | 0 | 43 | 0 |
| Sources | 0 | 0 | 0 | 0 |
| Advisories | 0 | 0 | 0 | 38 notes pour 19 exceptions rencontrées |
| Contrôle combiné | 0 | 0 | 43 | 38 notes |

Les premiers passages rouges ont été conservés dans l’analyse : licences, 4 erreurs et 1 avertissement
(`MIT-0`, `Apache-2.0 WITH LLVM-exception`, autorisation `OpenSSL` inutilisée) ; bans, 7 erreurs de
wildcards sur des dépendances `path` internes et 43 avertissements de duplication ; advisories,
19 erreurs informationnelles `unmaintained`. Les corrections ci-dessus ont supprimé uniquement les
erreurs de politique légitimement expliquées.

### Inventaire des licences rencontré

L'inventaire final est celui de `cargo deny list --format json --layout license` sur le lockfile
identifié plus haut : 13 libellés, 1079 associations licence/crate et 0 crate sans licence. Les comptes
se chevauchent lorsqu’une crate publie une expression multiple. Les huit occurrences `Unlicense` sont
des alternatives `Unlicense OR MIT` ou `Unlicense/MIT`; la branche `MIT` autorisée satisfait ces
expressions, donc `Unlicense` n’a pas été ajouté globalement.

| Licence détectée | Crates |
| --- | ---: |
| `0BSD` | 1 |
| `Apache-2.0` | 443 |
| `Apache-2.0 WITH LLVM-exception` | 6 |
| `BSD-2-Clause` | 3 |
| `BSD-3-Clause` | 10 |
| `CC0-1.0` | 4 |
| `ISC` | 3 |
| `MIT` | 554 |
| `MIT-0` | 5 |
| `MPL-2.0` | 5 |
| `Unicode-3.0` | 23 |
| `Unlicense` comme alternative | 8 |
| `Zlib` | 14 |

### Versions transitives dupliquées

`multiple-versions = "warn"` reste volontairement actif. Les 43 alertes ne sont ni supprimées ni
placées dans une liste d’exclusion :

| Crate | Versions verrouillées |
| --- | --- |
| `bit-set` | `0.8.0`, `0.9.1` |
| `bit-vec` | `0.8.0`, `0.9.1` |
| `bitflags` | `1.3.2`, `2.13.1` |
| `block-buffer` | `0.10.4`, `0.12.1` |
| `codespan-reporting` | `0.12.0`, `0.13.1` |
| `convert_case` | `0.6.0`, `0.10.0`, `0.11.0` |
| `cpufeatures` | `0.2.17`, `0.3.1` |
| `crypto-common` | `0.1.7`, `0.2.2` |
| `digest` | `0.10.7`, `0.11.3` |
| `downcast-rs` | `1.2.1`, `2.0.2` |
| `foldhash` | `0.1.5`, `0.2.0` |
| `font-types` | `0.11.3`, `0.12.4` |
| `getrandom` | `0.3.4`, `0.4.3` |
| `hashbrown` | `0.12.3`, `0.15.5`, `0.16.1`, `0.17.1` |
| `heck` | `0.4.1`, `0.5.0` |
| `indexmap` | `1.9.3`, `2.14.1` |
| `linux-raw-sys` | `0.4.15`, `0.12.1` |
| `miniz_oxide` | `0.8.9`, `0.9.1` |
| `png` | `0.17.16`, `0.18.1` |
| `proc-macro-crate` | `1.3.1`, `2.0.2` |
| `read-fonts` | `0.39.2`, `0.41.0` |
| `rustc-hash` | `1.1.0`, `2.1.3` |
| `rustix` | `0.38.44`, `1.1.4` |
| `serde_spanned` | `0.6.9`, `1.1.1` |
| `sha2` | `0.10.9`, `0.11.0` |
| `skrifa` | `0.42.1`, `0.44.0` |
| `syn` | `1.0.109`, `2.0.119`, `3.0.4` |
| `thiserror` | `1.0.69`, `2.0.20` |
| `thiserror-impl` | `1.0.69`, `2.0.20` |
| `toml` | `0.8.2`, `0.9.12+spec-1.1.0`, `1.1.4+spec-1.1.0` |
| `toml_datetime` | `0.6.3`, `0.7.5+spec-1.1.0`, `1.1.1+spec-1.1.0` |
| `toml_edit` | `0.19.15`, `0.20.2`, `0.25.13+spec-1.1.0` |
| `windows` | `0.61.3`, `0.62.2` |
| `windows-collections` | `0.2.0`, `0.3.2` |
| `windows-core` | `0.61.2`, `0.62.2` |
| `windows-future` | `0.2.1`, `0.3.2` |
| `windows-link` | `0.1.3`, `0.2.1` |
| `windows-numerics` | `0.2.0`, `0.3.1` |
| `windows-result` | `0.3.4`, `0.4.1` |
| `windows-strings` | `0.4.2`, `0.5.1` |
| `windows-sys` | `0.52.0`, `0.59.0`, `0.61.2` |
| `windows-threading` | `0.1.0`, `0.2.1` |
| `winnow` | `0.5.40`, `0.7.15`, `1.0.4` |

Ces duplications proviennent principalement de l’agrégation Bevy 0.19.1, Tauri 2.11.5 et Leptos
0.8.20. Elles augmentent taille et surface de maintenance, mais aucune n’est actuellement associée à
une vulnérabilité RustSec dans ce lockfile. Elles doivent être réévaluées lors des mises à niveau de
ces trois dépendances racines.

## Résultat `cargo audit`

L’actualisation initiale et les deux validations finales ont été exécutées :

```powershell
cargo audit --json
cargo audit --no-fetch --json
cargo audit --no-fetch --no-yanked --json
```

Le premier passage a actualisé RustSec et termine avec le code `0`, mais sa requête réseau de statut
`yanked` à crates.io a dépassé son délai. Sur le lock final identifié ci-dessus,
`cargo audit --no-fetch --json` a été rejoué et termine avec le code `0`, y compris son contrôle
yanked. Le passage déterministe qui désactive seulement cette requête réseau confirme le même
résultat de sécurité :

- 871 dépendances inspectées ;
- 0 vulnérabilité ;
- 19 avertissements `unmaintained` ;
- 1 avertissement `unsound` ;
- code de sortie `0`.

Le statut yanked est donc couvert à la fois par le passage final `cargo audit --no-fetch --json` et
par `cargo deny check advisories`, dont la politique `yanked = "deny"` passe. Le CI doit toujours
rejouer `cargo audit` connecté afin de détecter un changement ultérieur du registre.

### Avis RustSec conservés

| Avis | Crate | Catégorie | Origine et disposition |
| --- | --- | --- | --- |
| `RUSTSEC-2024-0411` | `gdkwayland-sys 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0412` | `gdk 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0413` | `atk 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0414` | `gdkx11-sys 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0415` | `gtk 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0416` | `atk-sys 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0417` | `gdkx11 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0418` | `gdk-sys 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0419` | `gtk3-macros 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0420` | `gtk-sys 0.18.2` | unmaintained | GTK3 Linux via Tauri 2.11.5 ; exception exacte, suivre la suppression GTK3 upstream. |
| `RUSTSEC-2024-0370` | `proc-macro-error 1.0.4` | unmaintained | GTK3 macros via Tauri Linux ; exception exacte, aucun upgrade sûr dans le graphe épinglé. |
| `RUSTSEC-2024-0436` | `paste 1.0.15` | unmaintained | Leptos 0.8.20 ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2026-0173` | `proc-macro-error2 2.0.1` | unmaintained | Leptos 0.8.20 ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2026-0192` | `ttf-parser 0.25.1` | unmaintained | Bevy/winit, décoration Linux ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2025-0075` | `unic-char-range 0.9.0` | unmaintained | `urlpattern 0.3.0` via Tauri ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2025-0080` | `unic-common 0.9.0` | unmaintained | `urlpattern 0.3.0` via Tauri ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2025-0081` | `unic-char-property 0.9.0` | unmaintained | `urlpattern 0.3.0` via Tauri ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2025-0098` | `unic-ucd-version 0.9.0` | unmaintained | `urlpattern 0.3.0` via Tauri ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2025-0100` | `unic-ucd-ident 0.9.0` | unmaintained | `urlpattern 0.3.0` via Tauri ; exception exacte, suivre le remplacement upstream. |
| `RUSTSEC-2024-0429` | `glib 0.18.5` | unsound | Tauri Linux ; non ignoré. `VariantStrIter` peut provoquer un comportement indéfini ; corrigé dans `glib >=0.20.0`. |

`RUSTSEC-2024-0429` affecte les implémentations d'itérateur de `glib::VariantStrIter`. Aucun usage
Forge direct de cette API n'a été identifié, mais cela ne constitue pas une preuve d'inaccessibilité
dans Tauri/WebKitGTK. Avant distribution Linux, il faut obtenir un graphe Tauri compatible avec
`glib >=0.20.0` ou produire une qualification ciblée et une décision de risque explicite.

## Politique automatisée des avis informationnels

Le replay a révélé que le script `scripts/Test-Rust.ps1` et le job `supply-chain` de
`.github/workflows/rust-gate.yml` appelaient tous deux :

```text
cargo audit -D warnings
```

Cette commande n'est pas cohérente avec la politique documentée : son replay déterministe
`cargo audit --no-fetch -D warnings` termine avec le code `1` et le message
`20 denied warnings found!` (19 avis `unmaintained` et `RUSTSEC-2024-0429` classé `unsound`). Le
script complet et le CI auraient donc été programmés pour rester rouges sur ce lockfile, alors que
`cargo deny` accepte explicitement les 19 avis `unmaintained` documentés.

La correction a été appliquée ensemble au script et au workflow :

1. conserver `cargo deny check licenses bans sources advisories` comme porte de politique ;
2. remplacer `cargo audit -D warnings` par `cargo audit --json` ;
3. analyser le JSON avec `scripts/Test-RustSec.ps1` et échouer si `vulnerabilities.count` n'est pas
   `0` ou si l'ensemble exact ID/catégorie/crate/version diffère de
   `docs/evidence/gate0/RUSTSEC_POLICY.json` ;
4. imprimer `RUSTSEC-2024-0429` comme blocage de qualification Linux, jamais comme avis ignoré.

Replay déterministe de la nouvelle politique :

```text
WARNING: Distribution blocker retained: RUSTSEC-2024-0429|unsound|glib|0.18.5
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
```

L'automatisation reste verte uniquement pour cet ensemble de risques exactement documenté et
redevient rouge pour toute nouvelle vulnérabilité, disparition ou alerte informationnelle.

## Conditions de maintien de la preuve

1. Rejouer les cinq commandes finales après chaque changement de `Cargo.lock` ou `deny.toml`.
2. Ne jamais remplacer les 19 exceptions exactes par une règle globale masquant les avis
   informationnels.
3. Conserver les 43 duplications en avertissements jusqu’à leur résolution par les dépendances racines.
4. Surveiller en priorité Tauri/GTK3/`glib`, Leptos macros et Bevy/winit.
5. Rejouer le statut yanked en ligne dans CI.
6. Bloquer la qualification de distribution Linux tant que `RUSTSEC-2024-0429` n'a pas une résolution
   technique ou une acceptation de risque documentée.
7. Conserver l'ensemble exact de `RUSTSEC_POLICY.json` : tout nouvel avis, toute disparition ou tout
   changement de crate/version doit rouvrir la revue.

## Frontières vérifiées

- aucune dépendance Git ;
- aucun registre autre que crates.io ;
- aucune dépendance externe wildcard ;
- aucun chemin vers `main` ou `upstream/cadle/` dans le graphe Cargo ;
- aucun asset ou runtime Cadle introduit par cet audit ;
- aucune alerte RustSec de vulnérabilité ignorée.

Conclusion pour `R0-057` : le rapport de licences et de chaîne d'approvisionnement est reproductible
et les deux outils d'audit passent pour le lockfile identifié lorsqu'ils sont invoqués selon la
politique documentée. La chaîne reste **qualifiée avec risques transitifs déclarés**, pas déclarée
exempte de risques. Le script local passe désormais selon une politique exacte ; le workflow distant
reste non prouvé tant qu'aucun run GitHub Actions n'a été observé.
