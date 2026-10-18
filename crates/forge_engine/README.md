# forge_engine

Adaptateur générique des documents Forge vers Bevy, le rendu et Rapier.
Cette crate ne contient ni carte intégrée, ni acquisition de carte, ni shader
porté d'un contenu externe. Ses dépendances de production n'incluent aucun
composant de carte. Elle compile sans composant de carte, en natif et en WASM.

Les documents restent la source de vérité. Terrain, végétation, eau, physique,
navigation, contrôleur, gameplay et programmes de matériaux admis sont projetés
par les API de cette crate. Les instances ECS et ressources GPU sont dérivées.

Les extensions de locomotion publient `player_motion_extension::PlayerMotionExtension`
avant la caméra et les observations de gameplay. Le moteur ne connaît que ce
contrat : point de vue, synchronisation du collider et contact humide local.
L'absence de cette ressource conserve le contrôleur standard.

Les caméras temporaires portent `scene_camera::OffscreenSceneCamera` pour être
exclues des outils interactifs de terrain.

```powershell
cargo check -p forge_engine --lib --locked --offline
cargo test -p forge_engine --lib --tests --locked --offline -- --test-threads=1
cargo check -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --lib --locked --offline
```
