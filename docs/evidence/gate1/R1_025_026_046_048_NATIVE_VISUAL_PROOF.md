# Gate 1 — preuve visuelle native temporelle R1-025, R1-026 et R1-046 à R1-048

Date : **2026-09-01**

Verdict de la campagne initiale `a7c9b1e1` : **ACQUISITION AUTOMATISÉE PASS — QUALIFICATION
VISUELLE REFUSÉE — ROUTES OUVERTES**

Mise à jour : le correctif `6a24cabe` supprime le défaut depth-only majeur et réacquiert en Release
29 PNG ainsi qu'un MP4, avec une réduction de 98,88 à 100 % du masque bleu-noir sur les six vues
diagnostiques. Cette amélioration et ses artefacts sont documentés dans
[`R1_046_048_VEGETATION_PREPASS_CORRECTION.md`](R1_046_048_VEGETATION_PREPASS_CORRECTION.md).
La revue humaine comparative, l'exécution GPU météo, le parcours gameplay et le GPU cible restent dus ;
les routes et Gate 1 restent ouvertes.

Cette campagne ajoute un harness natif reproductible pour produire, dans un même processus, une
séquence clair → pluie → clair, deux cadrages de composition, une vue eau et un parcours vidéo. Elle
ne transforme pas la présence de fichiers en acceptation artistique : les captures révèlent encore
des défauts importants, la caméra vidéo est une caméra de preuve et non le contrôleur de gameplay, et
le ledger ne revendique aucune observation d'exécution GPU de la météo.

## Identité exacte

| Élément | Valeur |
|---|---|
| commit source | `a7c9b1e1d5e32f826b606778434a5fae91cc947d` |
| arbre source | `2066fce699e4dd178c3d679cf4b0168d2e727d3d` |
| binaire Release | 81 944 064 octets |
| SHA-256 du binaire | `5047b8e8016f1897882fc55ef9c78a03bdc25c4aabe4d94a7ebd968b93b07a4c` |
| GPU / backend | AMD Radeon RX 7800 XT / Vulkan |
| pilote | AMD proprietary driver |
| résolution / présentation | 1920 × 1080 / `auto_no_vsync` / VSync faux |
| assets Cadle | aucun |

Le binaire a été construit depuis le commit propre en embarquant explicitement son identité :

```powershell
$env:FORGE_SOURCE_COMMIT = (git rev-parse HEAD)
cargo build --release -p forge_runtime --locked -j1
Remove-Item Env:FORGE_SOURCE_COMMIT
```

L'acquisition a ensuite été lancée avec :

```powershell
pwsh -NoProfile -File scripts\Test-Gate1VisualProof.ps1 `
  -EvidenceRoot E:\OpenRPG_Forge\.gate1-visual-artifacts\a7c9b1e1d5e32f826b606778434a5fae91cc947d-run1 `
  -RuntimePath .\target\release\forge_runtime.exe `
  -Quality high `
  -NoVsync
```

Le validateur refuse un worktree sale, une sortie dans le dépôt, un binaire dont le commit embarqué
diffère de `HEAD`, une résolution différente, VSync actif, une identité GPU absente ou un log contenant
warning, erreur ou panic.

## Résultats automatisés

| Contrôle | Résultat |
|---|---|
| harness PowerShell parsable | **PASS** |
| tests ciblés `forge_runtime` | **37/37 PASS** |
| `cargo fmt --all -- --check` | **PASS** |
| `cargo clippy -p forge_runtime --all-targets --locked -- -D warnings` | **PASS** |
| worktree propre avant/après, commit et arbre inchangés | **PASS** |
| 29 PNG physiques 1920 × 1080, non uniformes | **PASS** |
| séquence même processus | **PASS — clear → rain → clear** |
| wetness exacte | **PASS CPU/ECS — 0 → 0,78 → 0** |
| qualité fixe | **PASS — High** |
| Bloom demandé/composant présent | **PASS borné — paramètres numériques et exécution GPU non observés** |
| logs runtime | **PASS — stdout/stderr vides, 0 warning/erreur/panic** |
| vidéo | **PASS fichier — H.264, 1920 × 1080, 24 frames, 8 FPS, 3 s** |
| observation GPU météo | **NON ACQUISE** |
| acceptation humaine | **NON ACQUISE** |
| routes Gate 1 qualifiées | **NON** |

La comparaison SSIM entre `water-clear.png` et `water-restored.png` vaut **0,946502**. La projection
CPU/ECS revient exactement au profil clair ; les images ne sont pas déclarées identiques, car la scène
et la capture restent temporelles. Ce score n'est donc pas une preuve d'identité GPU.

## Inspection visuelle bornée

L'inspection à la résolution originale a porté sur les cinq vues fixes et les frames 000, 012 et 023
du mouvement. Elle constitue un contrôle de défauts par l'agent, pas la revue humaine comparative
exigée pour passer Gate 1.

| Artefact | Observation | Décision |
|---|---|---|
| `human-clear.png` | cadrage à hauteur humaine présent ; stipple noir et crénelage sévères sur herbe et arbres, silhouettes lointaines en blocs | non accepté |
| `distant-clear.png` | vallée et rivière cadrées ; végétation lointaine très bruitée, rivière/berges réduites à des rubans sombres | non accepté |
| `water-clear.png` | l'eau n'occupe qu'une bande étroite, largement masquée par des berges sombres | impropre à qualifier réflexion/réfraction |
| `water-rain.png` | pluie et changement de wetness visibles ; zones crème très lumineuses et défauts des berges persistants | non accepté |
| `water-restored.png` | palette claire approximativement restaurée ; eau toujours mal isolée | non accepté |
| frames `000`, `012`, `023` | déplacement de caméra effectif ; grands aplats verts, herbe striée/noire et silhouettes lointaines instables | non accepté |

## Cartographie conservatrice des routes

| Route | Sous-preuve acquise | Ce qui manque | État |
|---|---|---|---|
| `R1-023` réflexion/réfraction | trois PNG eau clear/rain/restored | eau isolable et effets optiques inspectables selon profil | **OUVERTE** |
| `R1-025` wetness réversible | endpoints CPU/ECS exacts dans un même processus | exécution GPU et acceptation visuelle dry → wet → dry | **OUVERTE** |
| `R1-026` bloom/grade bornés | endpoints de grade et présence du composant Bloom | paramètres numériques, exécution GPU et rendu accepté | **OUVERTE** |
| `R1-046` hauteur humaine | PNG 1920 × 1080 acquis | qualité visuelle et revue humaine acceptées | **OUVERTE** |
| `R1-047` vue lointaine | PNG 1920 × 1080 acquis | qualité de silhouettes/LOD acceptée | **OUVERTE** |
| `R1-048` vidéo courte | MP4 3 s acquis | parcours gameplay représentatif, stutter et revue humaine | **OUVERTE** |

## Artefacts versionnés

Le run local contient 34 fichiers et 62 687 771 octets, dont 29 PNG. Le lot Git conserve les cinq
captures fixes, la vidéo, les frames de mouvement 000/012/023, les deux ledgers et les deux logs :
13 artefacts, 18 444 234 octets. Les 21 PNG intermédiaires ne sont pas dupliqués dans l'historique ;
leurs tailles et SHA-256 restent inscrits dans
`gate1-visual-proof.validation.json`.

Le lot et son manifeste sont sous
[`native/a7c9b1e1d5e32f826b606778434a5fae91cc947d/visual-proof`](native/a7c9b1e1d5e32f826b606778434a5fae91cc947d/visual-proof).

## Conclusion

Le harness rend l'acquisition reproductible et empêche une preuve de provenance ambiguë. La campagne
historique établit le refus initial ; la campagne corrective `6a24cabe` établit ensuite la disparition
mesurée du principal défaut bleu-noir sans se substituer à la revue humaine. `R1-023`, `R1-025`,
`R1-026` et `R1-046` à `R1-048` restent donc volontairement décochées. Gate 1 reste **OUVERTE** et
Gate 2 reste interdite.
