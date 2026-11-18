//! The resident GLB and its exact repeating animation are shared by all copies.
use crate::source_shadows::casters::scene::NativeSkinnedShadowRoot;
use bevy::{
    asset::RecursiveDependencyLoadState,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstanceReady},
};
use forge_project::module_package::ModuleStallAnimatedModel;

pub(super) struct Animated {
    model: Handle<WorldAsset>,
    clip: Handle<AnimationClip>,
    pose: Transform,
    playback: Playback,
}

#[derive(Component, Clone)]
struct Playback {
    graph: Handle<AnimationGraph>,
    node: AnimationNodeIndex,
    start_seconds: f32,
}

#[derive(Component)]
struct Started(bool);

impl Animated {
    pub(super) fn load(world: &mut World, input: ModuleStallAnimatedModel) -> Self {
        let server = world.resource::<AssetServer>();
        let model = server.load(
            GltfAssetLabel::Scene(input.scene_index as usize).from_asset(input.model.path.clone()),
        );
        let clip = server.load(
            GltfAssetLabel::Animation(input.clip_index as usize).from_asset(input.model.path),
        );
        let (graph, node) = AnimationGraph::from_clip(clip.clone());
        Self {
            model,
            clip,
            pose: crate::domain_transform_to_bevy(input.local_transform),
            playback: Playback {
                graph: world.resource_mut::<Assets<AnimationGraph>>().add(graph),
                node,
                start_seconds: input.start_seconds,
            },
        }
    }

    pub(super) fn spawn(&self, world: &mut World, parent: Entity) -> Entity {
        world
            .spawn((
                WorldAssetRoot(self.model.clone()),
                self.pose,
                Visibility::Inherited,
                self.playback.clone(),
                NativeSkinnedShadowRoot,
                ChildOf(parent),
            ))
            .observe(start)
            .id()
    }

    pub(super) fn ready(&self, world: &World, actors: &[Entity]) -> Result<bool, String> {
        let server = world.resource::<AssetServer>();
        if server.load_state(self.model.id()).is_failed()
            || server.load_state(self.clip.id()).is_failed()
            || matches!(
                server.get_recursive_dependency_load_state(self.model.id()),
                Some(RecursiveDependencyLoadState::Failed(_))
            )
        {
            return Err("Stall model or animation could not be loaded".into());
        }
        let clips = world.resource::<Assets<AnimationClip>>();
        let Some(clip) = clips.get(&self.clip) else {
            return Ok(false);
        };
        if clip.duration() <= self.playback.start_seconds {
            return Err("Stall model animation starts outside the clip".into());
        }
        let mut current = true;
        for &actor in actors {
            match world.get::<Started>(actor) {
                Some(Started(true)) => (),
                Some(Started(false)) => return Err("Stall model animation has no player".into()),
                None => current = false,
            }
        }
        Ok(current)
    }
}

#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy observer systems receive the event wrapper by value"
)]
fn start(
    ready: On<WorldInstanceReady>,
    mut commands: Commands,
    roots: Query<&Playback>,
    children: Query<&Children>,
    mut players: Query<&mut AnimationPlayer>,
) {
    let Ok(playback) = roots.get(ready.entity) else {
        return;
    };
    let mut assigned = false;
    for child in children.iter_descendants(ready.entity) {
        if let Ok(mut player) = players.get_mut(child) {
            player
                .play(playback.node)
                .repeat()
                .seek_to(playback.start_seconds);
            commands
                .entity(child)
                .insert(AnimationGraphHandle(playback.graph.clone()));
            assigned = true;
        }
    }
    commands.entity(ready.entity).insert(Started(assigned));
}
