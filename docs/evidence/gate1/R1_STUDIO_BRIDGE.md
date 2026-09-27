# Gate 1 — Pont dérivé chargé dans le Studio

2026-09-05, Rust local non publié. Gate 1 reste ouverte.

## Fonction intégrée

Le mode `?forge_scene=world-sketch-visual-valley` conserve le document de pont retourné par la
compilation déjà effectuée pour son terrain. Il installe `WorldSketchBridgePlugin` avec les mêmes
validations de hash terrain et les mêmes meshes/colliders Bevy/Rapier que le natif. Aucun chemin de
fichier supplémentaire, seconde compilation, caméra d'aperçu ou seconde eau n'est nécessaire.
Gate 0 et le mode Visual Valley historique n'ajoutent pas de pont.

L'inspecteur affiche « Pont et colliders créés · passage Web non vérifié » après observation des
compteurs du moteur. Le ledger `studio-bridge-projection-ledger` contient le hash observé, les
compteurs et le document compilé de diagnostic. C'est une copie transitoire en lecture seule,
pas une nouvelle source canonique. Les six hashes du manifeste de scène ne sont pas artificiellement
présentés comme couvrant le pont ; sa preuve reste séparée.

## Écart trouvé et corrigé

Le premier Chrome réel créait bien le pont, mais son hash était
`885ce0168fdcd1364f8c6e16a35ea9768efb58b2aa442e492b50f85d3ac850a3`, contre
`f0e44ba91e65dba028e80a27f56d5cd30b86c23e3f3b3909df2098dd81e5c8db` en natif.
Le document diagnostique a isolé une différence d'un ULP sur la pente calculée par `atan2f` :
0,21011888980865479° contre 0,21011890470981598°. Les hashes WorldSketch, terrain et audit
étaient identiques, ainsi que les positions du pont.

Le compilateur arrondit désormais la pente diagnostique persistée à **0,0001 degré**, après le
contrôle de la pente brute contre le contrat joueur. Ni seuil, ni positions, ni colliders ne sont
assouplis ou arrondis. Un test couvre ces deux valeurs et l'erreur bornée ; voir ADR-0008.
Les anciens documents restent lisibles. Ce résultat prouve la parité de cette source de test,
pas l'absence universelle de toute différence flottante sur tous les futurs mondes.

## Preuve Chrome finale

Racine : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-bridge-20260905`.
Bundle `final-dist/`, URL locale port 14623. Relevé du **18:39:40.097 UTC** :

- hash du pont observé : **`6295913b59a4627d8691bb9da5ad6ed946056b0f060c20fbddcba34eb4bf0936`** ;
- un placement, 201 instances de mesh procédural ;
- un collider de tablier, deux de parapet, deux de support ;
- 72 pavés, 18 potelets, 102 voussoirs ;
- aucune eau d'aperçu ; une caméra active et manifeste de scène aligné ;
- aucun avertissement/erreur retourné par le lecteur de logs ; seul le favicon manque côté serveur.

Le viewport et l'inspecteur ont été capturés dans la conversation lors du premier passage ; la
vue de départ ne cadre pas le pont. Cette capture n'est donc pas une inspection visuelle rapprochée
du pont. Le dernier passage compare les hashes/documents et la caméra, sans multiplier les captures.
Les onglets et les trois serveurs successifs de preuve ont été arrêtés.

Les compteurs sont ceux des créations CPU du moteur : ils ne prouvent pas à eux seuls que tous
les handles physiques sont actifs ni qu'un joueur a traversé le pont dans Chrome.
`browser_crossing_verified=false` reste affiché. L'extraction des compteurs est conservée dans
`R1_STUDIO_BRIDGE_PARITY.json` ; les documents complets lus par l'outil restent dans la conversation.

## Probe natif sur le document final

Le CLI recompilé génère `native-compiled/`. Son hash de pont est identique au hash Chrome final.
Le runtime natif reconstruit par la suite workspace charge ce nouveau document via
`--terrain <native-compiled>/terrain-carved.v2.json --bridge-placement
<native-compiled>/bridge-placements.v1.json --bridge-crossing-probe` : cette voie teste les
documents matérialisés par le CLI.

Événement `bridge_crossing_probe_completed` du **18:38:11.966758 UTC**, code 0 :

- 42,5282 m parcourus, cible 42,49999 m ;
- 518/518 ticks au sol, zéro perte de contact, 435 ticks sur le tablier ;
- pente diagnostique 0,2101°, hauteur maximale au-dessus du terrain 3,90455 m ;
- bindings terrain/joueur/pont conservés, état final au sol ; navigation non qualifiée.

Le terrain reste `c9000f6ab51b7d716511dd718924f5cd6403c749456620bef86bb2120f364930` et le
WorldSketch `9841898bfa77367b04d080e52e1b8ead6f0cfdb8edee3e17e5b57a3d6360f8d3`.

## Validations et artefacts

- 315 tests workspace PASS : Assets 14, CLI 7, Core 11, Engine 102, Project 45,
  Runtime 45, Studio 25, World 66.
- Clippy workspace toutes cibles/features et Clippy Studio web/WASM strict : PASS.
- Trunk final, CLI, formatage et `git diff --check` : PASS.
- Avertissement amont conservé : future incompatibilité de `proc-macro-error2 2.0.1`.
- Un échec intermédiaire de dérivation `Resource` a été corrigé avec l'alias local `bevy::ecs`,
  sans ajouter une nouvelle dépendance ; le bundle final a été reconstruit et exécuté.

SHA-256 octets :

- WASM final : `5bbe2af1fa773cc56531253e362ca8115d21af828a6353f2cdc07884112de999` ;
- CLI producteur : `cbbd23f765cfaeb03b911d5f5a6ed655d16d288bae83fa27bb04f8c1cba27e76` ;
- probe natif JSONL : `5bc332784065b244ad58860da421a4d7b299baad82eff51e8c92901487ea1a0b` ;
- runtime lecteur : `f393e1aeeb02b6ecf41e71e4856f0f8f31a3aba4a8f36487138fa7074f3f7132`.

Le pont est raccordé au Studio, avec une preuve technique distincte de son acceptation humaine.
Le kit Blender externe n'est pas ajouté au navigateur, aucun Gate 2+, commit, push ou asset Cadle.
