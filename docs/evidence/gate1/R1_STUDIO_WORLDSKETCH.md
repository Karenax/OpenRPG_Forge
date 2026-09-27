# Gate 1 — WorldSketch verrouillé dans Studio

2026-09-05, branche Rust, local non publié ; Gate 1 ouverte.

## Implémenté

Le choix explicite `?forge_scene=world-sketch-visual-valley` compile le WorldSketch embarqué
verrouillé, sans remplacer `visual-valley` ni la scène Gate 0 par défaut. Le Studio utilise
`forge_world::compile_world_sketch`, les sorties terrain sculpté/végétation et la même
conversion de l'unique rivière principale en `WaterDocument` que le runtime natif.
Les profils environnement, joueur et météo embarqués restent communs. Les contrats d'identité
et capacités des documents sont validés avant création du viewport.

Le chargement préparé est désormais transmis au lancement Bevy : pas de seconde compilation
WorldSketch au démarrage. Le manifeste expose les six hashes dérivés ; les six hashes observés
par les ressources moteur doivent correspondre. Le nom et l'enum de scène distinguent WorldSketch,
avec le mode général `gate1_visual_valley`. Les anciens ledgers restent lisibles ; aucune
compatibilité d'un ancien lecteur avec la nouvelle valeur d'enum n'est revendiquée.

Les contrôles joueur et l'aperçu météo s'appliquent aussi à cette variante. Le kit Blender
corrigé et le pont externe ne sont PAS importés dans ce chemin : la comparaison porte sur
`forge_runtime --world-sketch-visual-valley --player-camera`, sans ces options additionnelles.
Ni le WorldSketch source, ni son budget de carving, ni les assets ne sont modifiés.

## Vérifié

- `cargo test --workspace --locked -q` : **307 PASS**. Nouveau test : choix explicite,
  défaut Gate 0 inchangé, pluie acceptée, outputs/hashes du compilateur, différence avec la
  vallée embarquée, source intacte, six hashes et round-trip du manifeste.
- Clippy strict workspace et Studio WASM : PASS.
- Build Trunk : PASS ; warning de compatibilité future `proc-macro-error2 2.0.1` conservé.
- Format et diff : PASS.
- Run natif du binaire existant `f6e1d1c329bf99a0be0bdf82f477eca614db291ea6cdca5dc77fb680c05f837a` :
  `--world-sketch-visual-valley --player-camera --metrics native-metrics.json
  --metrics-warmup-frames 60 --exit-after-frames 240`, code 0 à
  `2026-09-05T14:32:10.937239Z`, aucun ERROR dans le JSONL parsé.

Dossier externe `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-world-sketch-20260905` :
`dist/forge_studio_bg.wasm`, `native-metrics.json`, `native-run.jsonl`.
Hashes et observations extraites conservés dans `R1_STUDIO_WORLDSKETCH_PARITY.json`.
Target/variables Cargo identiques aux lots Studio précédents ; aucune dépendance ajoutée.

Chrome réellement lancé, onglet au premier plan : un démarrage, une caméra active,
source sélectionnée/runtime alignée, CSS et buffer 2048 × 1101. Le manifeste navigateur
observe ses six hashes ; les métriques natives exposent cinq hashes communs qui sont tous
identiques. Le profil joueur est observé côté navigateur et commun par chargement/test,
mais pas exposé comme sixième hash dans le fichier de métriques natif de ce run.
Première frame CPU à 3278,60 ms, contrat prêt à 3278,70 ms depuis navigation.

Capture inspectée dans la conversation : relief/carving différents de la vallée embarquée,
terrain et végétation visibles, larges surfaces problématiques au voisinage de la rivière.
**Cette image n'est pas une acceptation artistique.** Les métriques natives confirment
le défaut connu : minimum eau/terrain -28,309752 m. La copie de référence à 5 m de carving
reste donc non qualifiée, même si elle se charge correctement. Le candidat externe à
40 m n'a pas été promu et nécessite toujours l'autorisation et la revue prévues.

La capture navigateur n'est pas archivée comme fichier durable. La sonde GPU demeure
`not_observed` et l'inspection externe ne réécrit pas ce statut. Aucun log erreur/avertissement
retourné par la consultation Chrome ; pas une garantie exhaustive.

Le court relevé de démarrage (120 frames sur 1,18 s, moyenne 9,833 ms, p95 11,5 ms)
n'est pas un benchmark de qualification : vue incomplète et non acceptée, aucune traversée,
résolution différente de 1080p, matériel de développement. Ne pas cocher la performance Gate 1.

## Reste ouvert

Réparation autorisée de la rivière, qualité/composition, kit/pont dans Studio, revue humaine,
vidéo et captures normalisées, performance sur scène acceptée, WebView2. Aucun éditeur Gate 2
ou système de sauvegarde introduit. CI distante NOT RUN ; aucun commit/push ni asset Cadle.
