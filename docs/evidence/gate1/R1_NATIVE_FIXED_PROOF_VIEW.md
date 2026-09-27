# Vue authorée figée en natif — préparation R1-037

## Disponibilité combinée avant capture — 8 septembre

En mode figé, `capture_readiness` exige maintenant la vue installée/conforme sans
erreur, au moins 30 observations main-world stables, pause virtuelle et concordance
des temps virtuel/fixe avec leurs valeurs courantes. La série de pipelines prêts
ne progresse que tant que ces conditions tiennent ; le seuil passe alors de huit
à **30 observations RenderApp distinctes**. Hors vue figée, le seuil reste huit.
Ni le statut Unqualified de la forêt ni les décisions de qualité ne sont changés.

Test ajouté : 29 observations refusées, répétition d'une observation sans progrès,
30e acceptée, perte de disponibilité puis nouvelle série complète requise.
Quatre tests ciblés de capture PASS ; contrôle final workspace **615 PASS, 0 FAIL,
1 timing ignoré**. Clippy strict runtime/all-targets, check WASM et build natif PASS.
Warning futur proc-macro-error2 conservé. Aucun bundle Studio reconstruit.

Rejeu natif réel du candidat complet : dossier externe
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fixed-gpu-settle-20260908`.
Caméra `human_bridge`, WorldSketch et kit rev.4 précédents, forêt batches, clair/High,
capture demandée à partir de 300, sortie 420. Une série CPU stable sans invalidation ;
capture effectivement émise à **349**, **113 pipelines prêts**, **30 observations**
pour un seuil requis de 30. Readback et sortie 0 acquis ; onze pins inchangés.
SHA-256 exécutable : `281218D783D8BDA33D481E8D8AB214C4F2D92DE7A42A1EF3316642B31CC65423`.

Ce contrôle vérifie le déclenchement du screenshot, pas une nouvelle revue artistique.
Les observations PipelineCache ne certifient pas l'identité exhaustive de toutes les
ressources GPU, et aucun `.rdc` n'a été acquis. Reçu atomique lié au nonce/sources,
déclenchement externe et contrôles durant la capture restent à raccorder. R1-037
et Gate 1 restent ouverts.

## Observation continue du gel — 8 septembre

Le plugin partagé observe désormais la vue authorée, l'unicité de la caméra active,
la pause virtuelle et les temps écoulés virtuel/fixe dans `Last`. Toute divergence
remet la série à zéro ; un nouveau temps stable commence une nouvelle série. Les
compteurs sont explicitement des **frames main-world**, pas des frames RenderApp
ni une preuve de ressources GPU prêtes. Aucune correction de caméra n'est imposée
par cet observateur et aucun verdict de qualité n'est promu.

Test ECS existant étendu : quatre frames stables, déplacement de caméra, remise
à zéro, horloges inchangées. Workspace : **614 PASS, 0 FAIL, 1 timing ignoré**.
Clippy strict runtime/all-targets PASS après correction d'une balise de documentation ;
check frontend WASM PASS (warning futur proc-macro-error2 conservé), build natif PASS.

Premier lancement : capture/runtime exit 0, mais collecte de l'état après `app.run()`
sans événement attendu. **Contrôle de collecte FAIL**, artefacts conservés sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fixed-stability-20260908`.
Correction limitée à l'émission : signal produit pendant l'exécution à 30 observations,
ainsi qu'à la perte d'une série déjà stable. Pas d'état post-run présumé disponible.
Clippy/build rejoués PASS après cette correction de collecte.

Rejeu réel dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fixed-stability-20260908-b` :
WorldSketch/kit/forêt et paramètres du smoke précédent, sortie 420, capture à partir
de 300. Une observation `fixed_view_stability_observed`, caméra `human_bridge`,
`stable_main_frames=30`, `current_view_matches=true`, temps virtuel et fixe à zéro ;
aucune invalidation ultérieure dans ce run. Sortie 0 et onze pins conservés.
SHA-256 exécutable : `AED90B6569DDE911E491666900728D50921C015380858BD5A55E5A2AA8846F09`.
Les logs et pins sont conservés avec le PNG, sans nouvelle revue artistique revendiquée.

Le signal garde `gpu_readiness_verified=false`. Ce n'est toujours pas le reçu atomique
complet RenderDoc : la disponibilité GPU, les identités de ressources/sources, le nonce
du contrôleur et la stabilité entre déclenchement et capture restent à relier. Il ne
faut pas déclencher sur ces seules 30 observations CPU.

8 septembre 2026, Rust, aucune publication, source authorée inchangée.

## Implémenté

Option `--fixed-proof-view <camera-id>` raccordée au plugin moteur existant partagé
avec Studio. Caméra validée dans le WorldSketch chargé avant lancement de Bevy ;
gel du temps virtuel depuis Startup. Aucun second modèle de caméra ni nouveau shader.
Ce mode exige capture PNG et sortie bornée, interdit métriques exportées, mouvement,
adaptation, resize et probes de cycle. Le mode normal demeure inchangé.

## Vérifié

- 75 tests unitaires runtime et un test d'intégration PASS, avant et après extraction
  du parseur de l'option ; nouveau test de combinaison/refus inclus.
- Clippy strict runtime/all-targets PASS après correction de la limite de longueur
  de `Options::parse` par extraction d'un helper, sans désactivation du lint.
- Build natif offline/locked PASS ; `git diff --check` PASS.
- Lancement réel du candidat rev.4, caméra `human_bridge`, High/clair, forêt batches,
  WorldSketch externe identifié, sans aperçu nuages. Sortie 0, aucune erreur runtime.
- Capture demandée à partir de frame 300, émise à 329 après 113 pipelines prêts et
  huit observations stables ; readback reçu, sortie normale à 420.
- PNG 1920 × 1080 ouvert/inspecté : pont, route, eau et forêt gouvernée visibles.
  Aucun jugement d'acceptation artistique ; avertissements wgpu `@invariant` conservés.
- Caméra inconnue refusée par le binaire, sortie 2 avant démarrage ; aucun PNG créé.
- Onze pins (WorldSketch, trois documents kit, six GLB et exécutable) conservés.

Dossier : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-native-fixed-view-20260908`.
`pins.json`, `runtime.log`, `view.png` et copie de l'exécutable y sont conservés.

SHA-256 exécutable : `7112040D0B733201246071D775D9B51CEEB807F313D2058DDDE735E1C24D93DF`.
SHA-256 PNG : `F3F8C62815FEBFD70A8D9A401D955F6845CB90A9198B58067DCF955E17443796`.

## Limites et suite

Il s'agit de la disponibilité pour screenshot existante (huit observations), pas
du futur contrat RenderDoc de gel/settle et de vérification des sources par frame.
Aucun reçu externe atomique ni capture `.rdc` acquis. La physique au démarrage,
les horloges fixes, les sources complètes, la stabilité entre reçu et capture et
le retour de capture RenderDoc restent à contrôler. L'overlay de timings en mode
figé n'est pas un benchmark. Aucun bundle Web remplacé et aucune parité optique
globale, qualification forêt ou Gate 1 acquise par cette image.
