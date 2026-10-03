# forge_navigation_benchmark

Outil isolé de sélection du fournisseur de navigation. Il consomme les documents Forge et mesure
des crates candidates sans les introduire dans `forge_engine` ou `forge_runtime`.

Le benchmark doit rester plus strict qu'une simple compilation :

- trois générations déterministes de la vallée sculptée et du pont ;
- requête de chemin sur le franchissement route/rivière réel ;
- intérieur avec porte ouverte, porte fermée puis réouverte ;
- profils d'agent de tailles différentes ;
- coûts de zones, lien hors maillage et évitement local ;
- compilation native et `wasm32-unknown-unknown` ;
- rapport explicite des fonctions documentées mais absentes chez le fournisseur.

L'outil n'est pas un moteur de navigation Forge. Il appelle les algorithmes des fournisseurs et
produit uniquement une preuve dérivée.

## Pile candidate épinglée

- `rerecast 0.4.0` / `bevy_rerecast 0.5.0`, feature déterministe `libm` ;
- `landmass 0.9.2` / `bevy_landmass 0.12.0` ;
- `landmass_rerecast 0.3.0` ;
- `rapier3d 0.35.0-glamx0.2`, utilisé uniquement par le probe d'adaptation physique du labo ;
- `getrandom 0.3.4` avec `wasm_js` uniquement pour `wasm32`.

Ces dépendances restent isolées dans ce laboratoire Cargo autonome, explicitement exclu du workspace
produit. Elles ne sont donc présentes ni dans le lockfile produit, ni dans `forge_engine`, ni dans
`forge_runtime`.

## Exécution

Depuis la racine du dépôt, avec un chemin de rapport qui n'existe pas encore :

```powershell
cargo run --manifest-path tools/forge_navigation_benchmark/Cargo.toml --locked -- `
  examples/visual_valley/world-sketch.v1.json `
  E:\OpenRPG_Forge\.gate1-evidence-runtime\navigation-benchmark-new\navigation-provider-benchmark.v1.json
```

L'écriture utilise `create_new` et refuse donc d'écraser une preuve précédente. Le statut de
sélection se trouve dans `selection.status` ; une exécution réussie du programme ne signifie pas que
le fournisseur a passé le contrat.

## Vérifications

```powershell
cargo fmt --manifest-path tools/forge_navigation_benchmark/Cargo.toml -- --check
cargo clippy --manifest-path tools/forge_navigation_benchmark/Cargo.toml --all-targets --locked -- -D warnings
cargo test --manifest-path tools/forge_navigation_benchmark/Cargo.toml --locked
cargo check --manifest-path tools/forge_navigation_benchmark/Cargo.toml --target wasm32-unknown-unknown --locked
cargo deny --manifest-path tools/forge_navigation_benchmark/Cargo.toml check
```

Au 1er septembre 2026, la pile est `blocked_pending_contract_and_governance_gaps`. Le crop extérieur,
les portes, profils d'agents, coûts, liens et l'évitement passent. La vallée brute est rejetée par
Landmass sur le winding du polygone 2 819 ; les tuiles, la reconstruction partielle, le carving
d'obstacle dynamique et un backend Rapier maintenu manquent ; `cargo-deny` refuse aussi
`bincode 2.0.1` via `RUSTSEC-2025-0141`.

Le rapport contient une normalisation diagnostique séparée. Elle ne doit jamais être activée
silencieusement : `winding_normalization_accepted` reste faux jusqu'à une décision architecturale et
une correction qualifiée.

## Probe de terrain canonique importé

Le binaire `canonical_terrain_probe` ouvre une copie de workspace par l'hôte
Forge et authentifie ses blocs de raster. Il génère un crop à pas unitaire par
`SampledTerrainPlan`, identique à l'échantillonnage de collision, puis mesure
trois générations et requêtes Rerecast/Landmass brutes. Il refuse un crop de
plus de 65 536 cellules et des endpoints hors crop. Les triangles de bord
sont conservés par chevauchement ; l'enveloppe réelle est rapportée.

```powershell
cargo run --manifest-path tools/forge_navigation_benchmark/Cargo.toml --features native-probe --bin canonical_terrain_probe --locked --offline -- `
  <copie-authoring-root> <area-uuid> -10 75 -315 -60 40 -118 16 -270 <nouveau-rapport.json>
```

La racine passée au binaire doit contenir `workspace-v1/` et les archives de zone
de la copie authoring ; le binaire résout ensuite le workspace géré sous cette racine.

Ce probe ne comprend pas encore colliders statiques, exclusions d'eau,
liens entre tuiles, obstacles dynamiques ou navigateur réel. Le rapport v2 mesure
également jusqu'à trois bandes contiguës de la grille canonique ; chaque bande
est régénérée trois fois et soumise à la conversion/path query Landmass. Cette
mesure de bandes ne vaut pas une tile-cache native ni une reconstruction
partielle. Son statut reste `selection.status=blocked_pending_full_world_contract`,
même si le chemin terrain est trouvé. Il n'autorise pas une sélection du fournisseur.
La feature `native-probe` est réservée à l'hôte natif ; le contrôle WASM
habituel du laboratoire continue à compiler ses probes fournisseur indépendants.

### Replay navigateur de la campagne 002

La campagne `navigation-canonical-002` conserve le rapport natif complet et ses
triangles canoniques dans
`.gate3-evidence/20261002/navigation-canonical-002/`. La librairie du labo est
compilée pour `wasm32-unknown-unknown`, puis `wasm-bindgen 0.2.127 --target
web` produit `browser-dist/canonical_browser_probe.{js,html}` et le fichier
WASM associé. La page charge exactement la copie du rapport v2, vérifie ses
hashes et rejoue Rerecast/Landmass sans normaliser le winding.

Depuis le dossier `browser-dist`, servir les fichiers avec un serveur HTTP
local, par exemple :

```powershell
python -m http.server 8042 --bind 127.0.0.1 --directory `
  E:\OpenRPG_Forge\.gate3-evidence\20261002\navigation-canonical-002\browser-dist
```

Puis ouvrir `http://127.0.0.1:8042/canonical_browser_probe.html`. Le manifeste
`browser-bundle-manifest.json` épingle les hashes du bundle, de l'entrée HTML
et de la copie du rapport. La génération du bundle ne constitue pas une
acceptation navigateur : le rapport garde `selection.status` à
`blocked_pending_full_world_contract` et les réserves concernant les liens
inter-tuiles, les colliders/eaux, les obstacles dynamiques, les NPC, la
gouvernance des dépendances et l'acceptation ADR restent ouvertes.

### Extraction contrôlée des colliders Tidewater

La sonde de laboratoire `scripts/extract_static_colliders.py` vérifie les hashes du package
Tidewater, décode uniquement les payloads `pos` et `indices` des bâtiments sélectionnés, puis
applique le transform de placement Forge en coordonnées `f32`. Elle peut aussi ajouter les huit
boîtes exactes de `forge_world::building_layout::RoomLayout`, la porte fermée et le support statique
du fixture `base009`. La charge portée est conservée comme source exclue, car elle est dynamique.

Le rapport produit est une nouvelle entrée canonique; les triangles terrain restent en tête et les
triangles de colliders sont ajoutés sans normaliser leur winding. Un rejet Rerecast est une preuve
de la limite du provider pour cette géométrie, jamais un remplacement par une boîte ou un chemin
de secours. Les hashes package, descripteurs, payloads, transforms et sources authoring sont
conservés dans le rapport d'évidence.

### Probe d'adaptation Rapier et régressions de triangulation

`canonical_rapier_dynamic_probe` lit le rapport canonique `support.v1.json`, vérifie son hash de
géométrie, puis transmet sans réécriture les huit sommets et douze triangles du collider authored
à `rapier3d::ColliderBuilder::trimesh`. Il insère ce collider fixe après 30 pas de chute, exige un
`CollisionEvent::Started` réel, le retire avec `PhysicsWorld::remove_collider`, exige
`Stopped(REMOVED)` et vérifie que l'acteur reprend sa chute. Deux répétitions identiques sont
enregistrées dans `navigation-rapier-dynamic-009`.

```powershell
cargo run --manifest-path tools/forge_navigation_benchmark/Cargo.toml --features native-probe `
  --bin canonical_rapier_dynamic_probe --locked --offline -- `
  <support.v1.json> <nouveau-rapport.json>
```

Cette preuve couvre la frontière de collision Rapier et conserve `no_go=true` pour le cache de
tuiles, la reconstruction partielle du navmesh, les seams Landmass, l'eau, le déplacement NPC et
l'acceptation ADR. `tests/canonical_triangulation_regressions.rs` couvre les contours CW/CCW,
concaves, zéro aire et deux boucles déterministes. Les tests unitaires du fork Rerecast vérifient
en plus que l'inversion locale ne mute ni les tuples de métadonnées ni les indices hors bornes.

### Campagnes physiques et validation brute du 2 octobre 2026

`canonical_uniform_sweep` valide chaque sortie brute dans Landmass et quitte avec un code non nul
si un cas est rejeté. Les sept valeurs de simplification sont appliquées uniformément aux neuf
fixtures canoniques ; aucune normalisation du winding ni exclusion silencieuse n'est utilisée.
Le profil par défaut est rayon 0,35 m / hauteur 1,80 m. `FORGE_AGENT_HEIGHT_M=1.9` rejoue les mêmes
cas avec le gabarit NPC. Le probe WASM expose aussi `run_canonical_browser_profile_probe` pour
déclarer explicitement ce gabarit dans les preuves Node et Edge.

`canonical_navigation_runtime_campaign` partage les 51 102 triangles canoniques terrain et
colliders entre Rerecast et Rapier. Son acteur capsule de hauteur 1,90 m et rayon 0,35 m applique
uniquement la translation résolue par `KinematicCharacterController::move_shape`, puis renvoie
sa position de pied réelle à Landmass. Le support initial exige un contact KCC. Un contrôle
physique séparé exige le blocage par le même obstacle canonique et la reprise après son retrait.
Les trois bandes utilisent une reconstruction partielle explicite et un lien de seam parcouru
avec le même contrôleur, sans téléportation. La vitesse maximale de la sonde est 4 m/s ; le NPC
produit utilise actuellement 2 m/s et demande une qualification dans son ordonnanceur réel.

Les campagnes archivées restent des preuves distinctes et ne sont pas réécrites. Le dossier
016 associe archive source, exécutables, entrées et rapports actuels. Un succès sur ces fixtures
ne démontre ni le trajet intérieur complet, ni le contrat eau, ni le déplacement KCC dans un
navigateur. Les forks locaux, leur maintenance et la sélection du provider demandent une
acceptation explicite ADR0009 avant toute intégration produit.

Le binaire runtime exporte `run_canonical_navigation_runtime` pour WASM : les trois documents
JSON d'entrée sont désérialisés et vérifiés, puis les mêmes générations, mises à jour Landmass,
requêtes KCC Rapier et contrôle obstacle sont recalculés. Le navigateur fournit seulement une
horloge monotone pour les mesures ; il ne charge aucune trace précalculée ni état d'acteur.
La campagne 017 conserve aussi la correction du retrait du premier sommet de bord Rerecast,
qui pouvait soustraire à l'indice `usize` zéro dans le benchmark VisualValley global.
Le diagnostic a ensuite isolé des cordes simplifiées qui se croisent. Le fork vérifie les deux
extrémités des diagonales et conserve davantage de sommets exacts du contour brut pour supprimer
ces croisements. Les contours toujours invalides sont rejetés ; aucune sortie invalide n'est
retournée ou normalisée. Les régressions couvrent le contour fautif et cette conservation bornée.

`canonical_navigation_runtime_campaign --visual-valley-partial <world-sketch.v1.json> <nouveau-rapport.json>`
reconstruit les vrais triangles du benchmark VisualValley 512 m, trois bandes déterministes,
puis uniquement la bande 1 après insertion/retrait d’une boîte LAB explicitement placée au
milieu du deck canonique. Les hashes des bandes intactes et restaurées, chemins, temps et
contacts KCC au sol sur le même trimesh Rapier sont conservés. Cette source procédurale est
distincte du corridor Tidewater importé et de son obstacle authored.
