# Ports de référence hors du moteur — séparation du 18 octobre 2026

Ces API résident maintenant dans `forge_tidewater`. Les contrats neutres `SceneSun` et `water_medium` restent dans `forge_engine`.

# Entrées de scène communes

Ces types sont des entrées dérivées du renderer et du runtime. Les documents
Forge demeurent la source de vérité persistante ; aucun de ces exemples ne crée
un deuxième format de carte.

## Animation et locomotion

`scene_wind::WindFrame::new(time, wind)` conserve le pas maximal historique de
0,1 s. `with_max_delta` rend cette politique explicite pour une autre scène.
Une modification de vent conserve la politique et l’horloge existantes. La borne
concerne ce clock ; l’intégrateur de pendules garde sa propre borne de stabilité.
Les scènes doivent fournir des deltas cohérents à leurs consommateurs.

`scene_wind::AcquiredFrame::from_json(bytes, frame)` reçoit les horloges déjà
sélectionnées et authentifiées par l’adaptateur d’import. Le moteur n’inclut plus
la capture Tidewater. Sa lecture et son contrôle de provenance restent dans
`forge_tidewater_assets::tidewater_environment::animation_reference`.

```rust
use forge_engine::amphibious_locomotion::MotionProfile;
let profile = MotionProfile {
    walk_speed_mps: 2.0,
    gravity_magnitude_mps2: 3.5,
    ..Default::default()
}.validate()?;
// Le plugin et State acceptent uniquement le type de profil validé.
let plugin = plugin.with_motion_profile(profile);
```

La gravité ci-dessus est une magnitude descendante positive. Le profil canonique
`PlayerControllerProfileDocument` conserve sa convention existante ; un adaptateur
doit convertir les unités/conventions explicitement. Les vitesses de réponse
exponentielle ne sont pas assimilées aux accélérations du document canonique.

## Optique

`film_grain`, `ambient_occlusion` et `lens_flare` possèdent les plugins et
composants communs. Les anciennes API moteur préfixées ne sont pas des alias.
Les UUID et les noyaux WGSL des effets sont conservés. AO et flare déclarent aussi
leur besoin de profondeur échantillonnable ; le renderer ajoute ce droit avant
la préparation Bevy. Une caméra standard n’a pas besoin de reprendre un réglage
caché de la scène importée.
Le flare s'ordonne explicitement avant l'upscaling et l'UI, même lorsque le set de
haze n'a aucun consommateur. Cet ordre fait partie du contrat du plugin commun.

`lens_flare_sun::SceneSun::new(direction, radiance)` valide une direction unitaire
et une radiance linéaire non négative. La scène le publie explicitement ; en son
absence, le flare utilise les lumières directionnelles Bevy. Il ne consulte pas
les assets de bâtiments. L’adaptateur de matériaux importés peut installer
`tidewater_material::ImportedSunRelayPlugin` pour reprendre l’éclairage existant.
Ce relay n’est pas installé par le plugin optique commun.

`water_medium` expose l’observation d’immersion et son set d’ordonnancement.
`atmospheric_haze::LdrHazePass` délimite le haze en espace d’affichage, afin de
conserver l’ordre des effets sans appeler une fonction privée du backend.

## Vérification et limites

`cargo test -p forge_engine --test generic_scene_inputs` exerce des scènes et
profils CPU indépendants. `scripts/Test-GenericEngine.ps1 -PrepareBrowser` prépare
les contrôles, le probe optique natif et son équivalent WebGPU. Le navigateur doit
encore exécuter le probe et effectuer les readbacks : le build seul ne prouve pas
le rendu WebGPU. Les comparaisons GPU portent sur grain, flare et AO, sans asset
importé ; elles ne qualifient pas toutes les autres familles du moteur.
Les cibles du probe sont sRGB LDR : la branche temporelle HDR et le relay des
matériaux importés exigent leurs propres replays de régression.
Le probe utilise aussi `Msaa::Off` et `DepthPrepass` ; il ne qualifie pas les
variantes multisamplées de ces effets.

L’ADR-0025 et le bilan d’architecture décrivent la migration restante. Les noms
et recettes encore spécifiques ailleurs dans le moteur ne sont pas masqués par
ces extractions.
