# R1-014 — protocole temporel Browser WebGPU clear/High

Statut : **HARNESS READY — ACQUISITION NOT RUN**

Branche cible : **`Rust`**

Base de conception : commit **`28985328f0d2de1fa79c705d5c3aa1bc522dbe35`**, tree
**`ec71501d231ea6469eb97dc0af3d0230f4daf6eb`**

`R1-014` reste ouvert. Ce lot versionne un harnais reproductible ; il n'a lancé ni Browser, ni Cargo,
ni build Trunk et ne produit donc aucune nouvelle preuve de présentation WebGPU. Aucun asset Cadle
n'est copié dans Forge.

## Contrat préenregistré

La configuration [`scripts/gate1-browser-temporal.v1.json`](../../../scripts/gate1-browser-temporal.v1.json)
fixe avant le premier run :

- la route exacte `forge_scene=visual-valley&forge_weather=clear&forge_quality=high` ;
- l'origine exacte `http://127.0.0.1:1420/` et les bundles `index.html`, `forge_studio.js`,
  `forge_studio_bg.wasm` et `styles.css` ;
- une page **headed**, un viewport émulé `1920 × 1080`, un facteur d'échelle `1` et le canvas
  `#forge-canvas` ;
- le blocage, injecté avant le chargement de Studio, des entrées clavier, souris, tactile et pointeur ;
- le propriétaire caméra `visual_valley_player_controller`, une caméra active, le hash du contrôleur
  joueur et les manifests scène/qualité/météo identiques avant et après les captures ;
- deux captures PNG du canvas séparées par au moins **120 callbacks `requestAnimationFrame`** ;
- trois ROIs végétation, une ROI terrain témoin et trois exclusions explicites overlay/ciel/eau ;
- les seuils numériques et la règle d'acceptation ci-dessous.

Les rectangles sont normalisés dans le canvas. Ils ont été choisis sur la capture historique clear/High
[`visual-valley-browser-webgpu-clear-high-d9e26701.png`](captures/visual-valley-browser-webgpu-clear-high-d9e26701.png),
SHA-256 `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4`.
Cette image sert uniquement à enregistrer la géométrie des ROIs ; une image fixe historique n'est pas
une preuve temporelle.

| Zone | Type | Rectangle `(x, y, largeur, hauteur)` |
|---|---|---|
| `vegetation_left_foreground` | végétation | `(0,02 ; 0,47 ; 0,17 ; 0,20)` |
| `vegetation_right_midfield` | végétation | `(0,69 ; 0,32 ; 0,27 ; 0,22)` |
| `vegetation_right_foreground` | végétation | `(0,66 ; 0,69 ; 0,28 ; 0,20)` |
| `terrain_witness` | témoin stable | `(0,56 ; 0,55 ; 0,08 ; 0,06)` |
| `studio_overlay` | exclusion | `(0,00 ; 0,00 ; 0,57 ; 0,31)` |
| `sky` | exclusion | `(0,57 ; 0,00 ; 0,43 ; 0,28)` |
| `water_guard` | exclusion | `(0,34 ; 0,31 ; 0,30 ; 0,15)` |

Un pixel est changé si l'écart d'au moins un canal RGB atteint **6/255**. Une ROI végétation passe si
son ratio de pixels changés est `>= 0,002` et son écart RGB absolu moyen `>= 0,1`. Au moins **deux des
trois** ROIs végétation doivent passer. Le témoin terrain passe si son ratio est `<= 0,01` et son écart
RGB absolu moyen `<= 0,5`. Tout warning/erreur/assert console ou défaillance réseau fait échouer le verdict
automatisé. Modifier une ROI ou un seuil après observation exige une nouvelle version de protocole :
le run courant ne peut pas être réinterprété avec des seuils ajustés a posteriori.

## Acquisition reproductible

Le harnais réutilise les ledgers DOM existants de Studio : `gate0-telemetry-ledger` et
`forge-scene-telemetry-ledger`. Il pilote Edge ou Chrome par CDP sur une adresse loopback et un profil
temporaire isolé. Le serveur Trunk doit être lancé depuis le même checkout propre.

La découverte CDP normalise explicitement les réponses `/json/list` mono-cible, multi-cibles et
imbriquées avant de sélectionner une unique cible `page`. Cette normalisation empêche PowerShell de
concaténer plusieurs URL WebSocket lorsque Chrome expose aussi des pages d'arrière-plan ; l'URL
retenue doit rester un endpoint `ws://` loopback sur le port réservé.

Terminal A :

```powershell
pwsh -NoProfile -File .\scripts\Invoke-StudioTrunk.ps1 -Action serve -RequireExactCommit
```

Terminal B :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate1BrowserTemporal.ps1
pwsh -NoProfile -File .\scripts\Invoke-Gate1BrowserTemporal.ps1 `
  -BaseUrl 'http://127.0.0.1:1420/'
```

Le dossier de sortie par défaut est voisin du checkout,
`.gate1-evidence-browser-temporal/<commit>-<timestamp>/`, donc hors du dépôt. Le pilote refuse un
worktree sale et refuse d'écrire les preuves dans le worktree. Il enregistre notamment :

- commit et tree Git exacts, route, exécutable/version Browser, viewport et présence de
  `navigator.gpu` ;
- commit exact embarqué dans le ledger Studio v8, identité Git propre avant/après et égalité
  byte-for-byte des quatre bundles servis avec `apps/forge_studio/dist` ;
- ledgers Gate et scène avant/après, propriétaire caméra, caméra active, hash du contrôleur joueur,
  météo et qualité ;
- les deux PNG du canvas, timestamps et nombre de callbacks `requestAnimationFrame` observés ;
- métriques par ROI : rectangle pixel, pixels changés, ratio, écart RGB absolu moyen, RMSE et PSNR ;
- journal console avec zéro warning/erreur/assert, réponses/échecs réseau et ledger
  `served-bundles.json` ;
- taille et SHA-256 de chaque capture, ledger et journal, plus les SHA-256 des fichiers du protocole.

Le ledger principal est `r1-014-browser-temporal-ledger.json` et le manifeste final est
`artifact-manifest.json`. Un résultat automatisé `pass` signifie seulement que le run satisfait les
seuils préenregistrés et les invariants de contexte. Le ledger conserve toujours
`human_temporal_inspection=required_not_performed_by_harness` et `r1_014_claim=not_made_by_harness`.

## Validation statique du lot

Le test statique parse les scripts PowerShell, rejoue la validation de configuration, vérifie que les
mutations `119 RAF`, `ROI dans le ciel` et `seuil modifié sans changement de version` sont rejetées,
teste une matrice synthétique positive puis un échec console, et contrôle que la roadmap laisse
`R1-014` décoché. Le self-test couvre aussi une réponse CDP synthétique composée d'une page
d'arrière-plan, de deux pages et de l'ancienne forme imbriquée. Il ne lance ni Browser ni build :

```powershell
pwsh -NoProfile -File .\scripts\Invoke-Gate1BrowserTemporal.ps1 -ValidateOnly
pwsh -NoProfile -File .\scripts\Invoke-Gate1BrowserTemporal.ps1 -SelfTest
pwsh -NoProfile -File .\scripts\Test-Gate1BrowserTemporal.ps1
cargo fmt --all -- --check
git diff --check
```

## Condition de fermeture ultérieure

Après un vrai run sur le commit Rust à qualifier, les deux captures doivent être inspectées à leur
résolution originale et les ROIs doivent être confirmées hors overlay, ciel et eau. Il faut ensuite
valider les hashes, les quatre égalités bundle servi/dist, le commit embarqué, l'absence de
warning/erreur console ou réseau, la stabilité caméra/joueur et les métriques.
Ce n'est qu'après cette inspection humaine versionnée que `R1-014` pourra être réévalué ; le présent
lot ne revendique ni `R1-014 PASS`, ni Gate 1 PASS.
