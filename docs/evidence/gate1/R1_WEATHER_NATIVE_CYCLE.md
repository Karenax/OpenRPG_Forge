# Cycle météo natif — 7 septembre 2026

**Cycle fonctionnel natif exécuté, trois captures inspectées ; Gate 1 reste ouverte.**
Suite de `R1_WEATHER_INPUT_NATIVE.md`. Aucun profil canonique modifié, aucun preset promu.

## Implémentation

Le runtime accepte `--weather-cycle-proof <fresh-directory>` avec un profil externe
clair initial et `sunlight` opt-in. Il réutilise le harness de captures existant et
l'API `WeatherRuntimeState::request_state`, pas un accès aux champs privés du moteur.
Le contrôleur observe après les projections PostUpdate, avant le harness en Last.
Il exige des valeurs lumineuses intermédiaires dans les deux sens et les extrémités
conformes au contrat partagé, ainsi que la restitution de l'éclairage avant séchage.
Les hashes météo/environnement sont surveillés ; la source ne change pas pendant le cycle.
Les uniformes terrain/végétation et la visibilité pluie doivent correspondre à l'état.

Chaque extrémité attend 180 observations concordantes avant capture, puis une nouvelle
callback et un fichier non vide. Le répertoire doit être neuf ; timeout réel 120 s et
fermeture prématurée sont des échecs. Les autres cycles, probes et automatismes sont
refusés, ainsi que les overrides météo et l'adaptation. Le mode normal est inchangé.
Le champ `tier=High` des événements hérités est ici un placeholder de harness : il ne
qualifie pas un preset. La preuve météo n'utilise pas le verdict GPU du cycle Global.

## Validations exécutées

- `cargo test -p forge_runtime` : **72 unitaires + 1 intégration PASS**.
- `cargo clippy -p forge_runtime --all-targets --all-features -- -D warnings` : PASS.
- `cargo build -p forge_runtime`, fmt et diff check : PASS.
- Deux tests ajoutés : portée CLI et conflits ; profil sans option/refus initial pluie,
  activation explicite et trois étapes. Les tests existants du harness couvrent aussi
  la sortie incomplète. Le parcours natif ci-dessous est la preuve intégrée positive.
- Première compilation : Option de lux traitée comme scalaire, corrigée en présence
  obligatoire. Premier Clippy : placement du module de tests corrigé, sans waiver.

Pas de changement du domaine ou du renderer partagé : tests workspace/WASM du lot
moteur précédent non rejoués ni annoncés comme nouvelles validations.

## Exécution et observations

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-weather-cycle-20260907`.
`Replay.ps1` conserve les arguments exacts. Même WorldSketch, pose `keep_landmark`, kit
et catalogue prototypes que le lot d'entrée météo ; `--quality high` et aperçu forêt,
1920 × 1080, Windows/RX 7800 XT/Vulkan, VSync par défaut. Profil candidat revision 3,
facteur pluie **0,65**, transition **4 s**, choisi pour éviter l'assombrissement excessif
du premier essai à 0,25. Cette valeur reste candidate, pas une décision artistique.

Une seule exécution, **exit 0**, zéro événement ERROR, `quality_cycle_completed` à
01:53:02 UTC, trois callbacks/fichiers frais. Les journaux établissent :

| Étape | Lux | Mouillure | Preuve |
|---|---:|---:|---|
| Clair initial | 55000 | 0 | stage-0.png |
| Transition vers pluie | 54843,98 | non relevée dans cet événement | blend 0,008105 |
| Pluie établie | 35750 | 0,78 | stage-1.png |
| Transition vers clair | 35906,40 | non relevée dans cet événement | blend 0,991875 |
| Soleil restauré | 55000 | 0,499451 | événement avant fin du séchage |
| Clair sec final | 55000 | 0 | stage-2.png |

Les trois PNG ont été ouverts et inspectés : pluie visible au milieu, retour à un
rendu clair proche de l'initial à la fin, même architecture et cadrage. À 0,65, les
silhouettes sont moins écrasées qu'à 0,25 ; les reflets mouillés et ombres très dures
restent présents. Pas de simulation de ciel couvert ni d'acceptation artistique.

Les valeurs intermédiaires sont des observations CPU pendant l'exécution graphique,
pas des captures GPU intermédiaires. Trois images terminales ne qualifient pas la
fluidité de la transition : aucune vidéo ni synchronisation pixel à pixel. Le vent,
l'eau et le bruit temporel continuent, les captures initiale/finale ne sont donc pas
identiques octet pour octet. Aucun benchmark : l'overlay initial inclut le démarrage
(moyenne glissante 20,56 ms, 1 % low 1,6 FPS), pas une performance interactive qualifiée.
Le cycle ne surveille que les sources météo/environnement, pas tous les IDs de meshes.

Météo inchangée durant ce run :
`1e09a89de24ebc130f8b6a6481a578f199063578ef9db073723f669f8da46c27`.
Environnement : `845ae5b80b6cb1ef2390b0cc79efb121a5b8ffbd2e911f67f7a5b5ce38f00f0f`.

## Artefacts et limites de livraison

Branche Rust, base HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié
préservé. Cache Cargo `terrain-water-20260906`, incrémental désactivé,
`RUSTFLAGS=-C debuginfo=0`, TEMP/TMP `terrain-water-temp-20260906`, sous
`E:/OpenRPG_Forge/.gate1-build-cache`. Pas de commit/push ou nettoyage.

| Artefact | SHA-256 |
|---|---|
| forge_runtime.exe | 6802A25090F75D5036D7A44E50073879FD463AA998554650BB2D7ABC4547B86F |
| candidate.json | 1DF4D7317B77EB02727448F72F644DA3CF410B407D8163D0CFEE9268387505AF |
| stderr.log | 1B3B99704A4BA2931334510ADCC04F9F97592E0F34140A9CD9B30D056A67B3BC |
| tests.log | 35D0F9E87E984FB684B50E800524E47BE0A3A066A3D14C83F52E4B99E116E921 |
| clippy.log | 7A78EE49A19CDBCA8FF24648CF66CC64A20711A01368CDB804044FF369FBA919 |
| cycle/stage-0.png | 8EB69E2DE5A76CFD2A06439CA18F4776DF9DDA019B5D4F217A6E6455F9EA0486 |
| cycle/stage-1.png | DCB879C9F2952528ACE6D24198A7DAF11328B586696EC1475C5A00D32257B80C |
| cycle/stage-2.png | 5F8280EA1AD0958F0343E8F06BB0A01B1978A4BA86BCFB68466CA855C35AF7CC |

Suite utile : rendre le profil candidat accessible au viewport Studio sans modifier la
source canonique, puis vérifier le cycle par les contrôles météo existants. Web/Tauri,
fluidité, qualité artistique et matériel cible restent non qualifiés. Ce lot n'ouvre
pas Gate 2 et ne copie aucun asset Cadle. CI distante NOT RUN ; revues humaines requises.

Raccord Studio suivant exécuté : `R1_STUDIO_WEATHER_INPUT.md` conserve le chargement
candidat, le cycle par boutons et le retour à l'entrée embarquée. Réponse CPU et parité
Web vérifiées ; vidéo, qualification optique complète et Tauri restent dus.
