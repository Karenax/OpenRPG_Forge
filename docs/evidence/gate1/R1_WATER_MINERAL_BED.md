# R1-022/023 — continuité minérale du lit

9 septembre 2026, Rust. Suite de `R1_STUDIO_WATER_CLOSE.md`.

## Cause et correction implémentée

`build_river_bank_geometry` ne construisait que les bandes [0,1] et [2,3].
Les deux sommets intérieurs portent une pondération terre de 1, mais aucune
surface ne les reliait : le terrain central, classé principalement herbe selon
sa pente/altitude, devenait visible avec la transmission du candidat.

Ajout de la bande [1,2] au même maillage de revêtement. Le découpage existant
sur les triangles du terrain est conservé : relief et collisions inchangés,
aucune modification du document de carte ni du shader d'eau. Le lit rejoint
la terre des berges ; le bord externe conserve son mélange vers le terrain.
Règle commune à toutes les rivières passant par cet adapter, pas test de slug.

Le nombre de triangles du ruban avant découpage passe de 4 à 6 par paire de
sections ; budgets existants inchangés. Coût de découpage et géométrie finale
augmentent : aucun gain de performance ni coût équivalent revendiqué.

## Vérifié

`cargo test -p forge_engine water:: --lib` : **36 PASS**, zéro échec, 236 filtrés.
Nouveau test synthétique : les 64 m² du lit sont couverts, poids herbe nul et
terre unitaire, hauteur égale au terrain plus le dégagement existant de 15 mm.
Tests existants de conformité au terrain, winding, identité, optique et profils
PASS. Clippy forge_engine tous targets/features strict PASS. Format exécuté.

Build natif PASS. Racine locale `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-bed-20260909/`.
EXE SHA-256 `D8A2FD358D3060589352B7331CC1C58CBAF3A45CABF7606E95C139D6A09AC908`.

Première tentative avec la caméra candidate refusée : le kit natif exige le
hash WorldSketch original, qui inclut les caméras. Refus conservé dans
`refused-candidate.stderr.log`, PID 43408 terminé, garde inchangé.
Rejeu avec la caméra embarquée `human_bridge` : capture frame 300 vérifiée,
sortie 0, aucun événement ERROR, PID 8156 terminé. PNG nommé `water_close.png`
par le script, mais **cadrage réel human_bridge**, SHA-256
`E4C941B7E30FBD0A36A2999EBC34B60FCA338C8D891910091ABAF54F0A7AC79E`.

Capture inspectée : scène affichée, observation eau complète, mais le lit est
trop éloigné pour confirmer la disparition du défaut rapproché. 70 995 triangles
eau/berges affichés ; moyenne indicative 19,02 ms, p95 24,64 ms, hors benchmark.
Ne pas qualifier le changement avec ce cadrage. Le bundle Studio doit être
reconstruit puis observé sur la même vue rapprochée que la preuve précédente.
Rejeu global ensuite terminé : **645 tests workspace PASS, zéro échec, un ignoré**,
log `workspace-tests.log`. Clippy Studio WASM strict PASS (`web-clippy.log`) ;
warning préexistant de compatibilité future `proc-macro-error2` conservé.
Nouveau bundle Studio en construction via `BuildStudio.ps1`, même fichier de
vue candidate que la preuve rapprochée antérieure, sans resérialiser sa graine.
Gate 1 ouverte.

## Confirmation rapprochée Studio

`BuildStudio.ps1` terminé, Trunk release puis Tauri PASS. Exécutable
`candidate/forge-studio-candidate.exe`, SHA-256
`02148004D1A833D89F93B85EA8DCC3B2C398930EAF942AD5836C5838699554AF` ; WASM
`6EB8E21A8443F5053030BE2D8DDEB65133F08DCDD46A9EEF57F56AFD6AC8E1AA`.
Les pins kit/caméra/JS/CSS sont identiques au bundle rapproché précédent ;
seuls WASM et index.html diffèrent dans le manifeste frontend.

Studio lancé, vue `water_close`, 1408 × 966, temps virtuel 0 s. Exports réels
copiés dans la racine locale, originaux Downloads conservés :

- `forge-diagnostic-1788906813528.json`, SHA-256
  `9375A5AE64884130DC207D608C6221A052D871F701364E3EF7C50AA484389311` ;
- `forge-viewport-1788906825872.png`, SHA-256
  `076038B927EC6198DE87EA59C09EA1C33C5D063AFA0A7EC16331F78FFCB7F9DA`.

Inspection face au PNG à temps zéro de `R1_STUDIO_WATER_CLOSE.md` : la zone
verte centrale et ses frontières franches avec les bandes brunes disparaissent,
le revêtement minéral est continu. Correction de ce défaut précis confirmée.
Rides régulières et rupture sombre sous le pont encore présentes ; pas de
qualification artistique globale, de mouvement ni de profils.

JSON : candidat optique actif, observation main-world complète, SSR=0 ;
1 380 triangles d'eau inchangés, 69 615 triangles de revêtement contre 40 369
dans la preuve précédente. Surcoût géométrique explicite, pas de benchmark
comparatif. Préchauffage : 74 pipelines, zéro erreur/en attente finale,
pic transitoire de 18 erreurs conservé ; cela ne qualifie pas le démarrage.

PID Studio 11792 fermé après vérification de son chemin ; terminaison et absence
d'enfants directs vérifiées. Ancienne session utilisateur préservée. Aucun push.
