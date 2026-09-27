# Gate 1 — Build local du candidat Tauri

8 septembre 2026 (Europe/Paris), branche Rust, worktree modifié préservé.
Aucun commit, push, asset Cadle, lancement ni verdict de passage.

## Implémenté

`scripts/Build-StudioCandidate.ps1` remplace le besoin d'adapter le script temporaire
pour chaque dossier de preuve. Entrées explicites : frontend existant, SHA-256 WASM
attendu, sortie neuve sur E:. Copie et pins de tous les fichiers, overlay relatif
depuis le Studio (pas d'URL Windows), compilation Tauri debug offline/locked, copie
du binaire déclaré `forge-studio`, recontrôle des pins et restauration des variables
d'environnement. Aucun ancien candidat n'est écrasé. Le ledger déclare `BUILD_ONLY`,
`runtime_verified=false`, `gate_qualified=false`.

## Réellement exécuté

- Validation sans écriture : 14 fichiers, `INPUTS_VALID`.
- Trois refus vérifiés : sortie C:, sortie déjà existante, mauvais SHA-256 WASM.
- Build réel : code 0, compilation Cargo 28,00 s, log conservé.
- Les 14 fichiers source et copiés ont les mêmes hashes après compilation.
- `git diff --check` PASS ; pas de suite Rust relancée pour ce script de build.

Commande depuis le dépôt Rust :

```powershell
./scripts/Build-StudioCandidate.ps1 `
  -Frontend E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-road-studio-20260907/web `
  -ExpectedWasmSha256 10726ed3e97520e788fd4f1fbe3ae00f1d584d210d320b68d97c4827d5e9a2e5 `
  -Output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-repro-20260908
```

La sortie contient `build.log`, `build-proof.json`, `tauri-candidate.conf.json`,
`web/` et `forge-studio-candidate.exe`. SHA-256 exécutable :
`E7A8CB052EAC77B792FE01EEA35D3D42B92FD142FB06D71A79BA149174EFA8E3`.
Horodatage ledger UTC : `2026-09-07T22:29:13.1235288Z`.

## Contrôle direct effectué — 8 septembre, 09:16 UTC

La limitation annoncée dans la remise historique ci-dessous était trop générale :
`@oai/sky` via Computer Use fonctionne dans cette session. L'agent a ouvert le
candidat, activé sa fenêtre initialement minimisée, capturé et inspecté son rendu.
L'absence de pilotage natif annoncée n'est plus un blocage de cet essai.

Preuves sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-window-20260908/` :

- `studio-window.jpg` : première observation, éclairage très sombre ;
- `studio-window-loaded.jpg` : observation ultérieure, éclairage amélioré ;
- `viewport.png` : export GPU reçu, 1408 × 966, inspecté ; SHA-256
  `CDFF7E5395A8695FA66ECE5E08EBE63401A73F6A74CB872D967F18323A15B9E1` ;
- `diagnostic.json` : export reçu à 09:16:03 UTC ; SHA-256
  `7D50DB0D98A9065496B5AE178685A5E5E3CDCB3FCF2B16DA4E1B27361F872F99`.

Interface : kit 20/20, 36 colliders, forêt 650 arbres / 7 800 primitives, une
initialisation. Diagnostic `target=tauri_studio`. Scène visible dans Tauri, pas
Chrome. L'image ultérieure interdit de qualifier la première observation sombre
de défaut permanent sans diagnostic supplémentaire. Ciel/silhouettes provisoires.
Télémétrie courte (120 échantillons) : moyenne 26,77 ms, p95 31,4 ms ; ce ne sont
pas des timestamps GPU ni un benchmark release de qualification. JSON et PNG
non atomiques, `gate_passed=false`. Aucun verdict de parité exhaustive ou artistique.
Instance fermée par Alt+F4, puis zéro processus candidat au contrôle système.
Originaux téléchargés conservés, aucun navigateur de test ouvert.

## Limites et remise historiques — antérieures au contrôle direct

### Remise pour contrôle WebView2 — 8 septembre

Après les deux acquisitions RenderDoc natives, intégrité de ce candidat recontrôlée :
SHA-256 exécutable identique au ledger et 14/14 fichiers frontend conformes.
Ce contrôle ne lance aucun processus et ne vérifie pas le rendu embarqué.
Les outils de cette session ne permettent pas de piloter les fenêtres natives ;
ne pas substituer une nouvelle preuve Chrome à l'exécution Tauri manquante.

Prochaine preuve attendue : ouvrir `forge-studio-candidate.exe` dans le dossier
ci-dessus, attendre le chargement du kit et de la forêt, transmettre une capture
de la fenêtre ainsi que le diagnostic exporté, puis fermer cette instance après
l'essai. Vérifier ensuite le backend observé, les états de chargement et les
erreurs avant tout verdict. Cette remise n'est ni une demande de passage de gate,
ni une acceptation artistique implicite. Aucun lancement supplémentaire effectué.

Le frontend est le bundle identifié du 7 septembre, pas un nouveau build WASM
des derniers essais nuageux. Le shell est compilé depuis le worktree courant ;
ceci ne constitue pas une qualification sur arbre propre ADR-0006 ni une preuve
de reproductibilité bit à bit de l'ensemble des sources. Les contrôles de kit au
runtime restent requis. Ce candidat n'a pas été lancé ou inspecté dans WebView2 :
la confirmation utilisateur de l'ancienne v2 ne s'étend pas à ce nouveau binaire.
Gate 1 ouverte ; aucune preuve Chrome renommée en preuve Tauri.
