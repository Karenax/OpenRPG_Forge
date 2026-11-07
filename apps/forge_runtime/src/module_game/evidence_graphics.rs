//! Portable receipts, with no renderer dependency. Pixels and timing remain separate evidence.
use forge_core::{ContentHash, EntityId, canonical_json_sha256};
use forge_project::VisualQualityTier;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct GraphicsIdentity {
    pub head: ContentHash,
    pub policy: ContentHash,
    #[serde(default, skip_serializing_if = "is_false")]
    pub water: bool,
}
#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "Serde passes fields by reference"
)]
const fn is_false(value: &bool) -> bool {
    !*value
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct GraphicsObservation {
    pub identity: GraphicsIdentity,
    pub requested: VisualQualityTier,
    pub applied: Option<VisualQualityTier>,
    pub persisted: Option<VisualQualityTier>,
    pub request_sequence: u64,
    pub generation: u64,
    pub application_frame: u32,
    pub cpu_ready: bool,
    pub ready: bool,
    pub error: Option<String>,
    pub preference_error: Option<String>,
    pub scopes: Vec<GraphicsScope>,
    pub plant_completed_frame: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_surfaces: Option<super::SurfaceStatus>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_terrain: Option<SourceTerrainObservation>,
}
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct SourceTerrainObservation {
    pub document: Option<ContentHash>,
    pub package: Option<ContentHash>,
    pub pending: bool,
    pub active_tiles: usize,
    pub cached_tiles: usize,
    pub normals_ready: bool,
    pub staged_frame: Option<u32>,
    pub ready: bool,
    pub error: Option<String>,
}
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct GraphicsScope {
    pub name: String,
    pub owner: Option<EntityId>,
    pub identity: Option<ContentHash>,
    pub generation: Option<u64>,
    pub epoch: u64,
    pub observed: u32,
    pub completed: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub required_frame: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub publication: Option<GraphicsPublication>,
    pub token_current: bool,
    pub meshes: [usize; 2],
    pub materials: [usize; 2],
    pub images: [usize; 2],
    pub buffers: [usize; 2],
    pub pipelines: [usize; 3],
    pub effect_groups: [usize; 2],
    pub effects_ready: bool,
    pub camera_ready: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct GraphicsPublication {
    pub frame: u32,
    pub sources: ContentHash,
    pub seal: ContentHash,
}

pub(super) fn verify(
    expected: Option<&GraphicsIdentity>,
    observed: Option<&GraphicsObservation>,
    frame: u32,
    plants: bool,
    fish: bool,
) -> Result<(), String> {
    let (expected, actual) = match (expected, observed) {
        (None, None) if !fish => return Ok(()),
        (Some(expected), Some(actual)) => (expected, actual),
        _ => return Err("Module graphics observation/identity absent or unexpected".into()),
    };
    if expected != &actual.identity
        || actual.generation == 0
        || !actual.ready
        || !actual.cpu_ready
        || actual.applied != Some(actual.requested)
        || actual.error.is_some()
    {
        return Err("Module graphics require the exact policy and observed CPU application".into());
    }
    if actual.source_surfaces.as_ref().is_some_and(|source| {
        !source.active
            || !source.ready
            || !source.motion_matches
            || source.generation == 0
            || source.world_receivers == 0
            || source.model_receivers.checked_add(source.terrain_receivers)
                != Some(source.world_receivers)
            || source.encoded_frames == 0
            || source
                .encoded_frame
                .zip(source.gpu_completed)
                .is_none_or(|(encoded, completed)| completed < encoded)
    }) {
        return Err(
            "Source surfaces require their real common scene and dated GPU completion".into(),
        );
    }
    verify_source_terrain(actual)?;
    // A failed preference write remains visible but does not claim rendering failed.
    // Persistence qualification additionally requires no preference_error and the saved tier.
    verify_scopes(actual, expected, frame, plants, expected.water, fish)
}

fn verify_source_terrain(actual: &GraphicsObservation) -> Result<(), String> {
    if actual.source_terrain.as_ref().is_some_and(|source| {
        source.document.is_none()
            || source.package.is_none()
            || source.pending
            || !source.normals_ready
            || !source.ready
            || source.error.is_some()
            || source.staged_frame.is_none()
            || source.cached_tiles < source.active_tiles
            || source.cached_tiles > source.active_tiles.max(256)
            || !actual.scopes.iter().any(|scope| {
                scope.name == "terrain"
                    && scope.buffers[0] >= 5
                    && scope.images[0] >= 8
                    && source
                        .staged_frame
                        .zip(scope.completed)
                        .is_some_and(|(staged, done)| done >= staged)
            })
    }) {
        return Err(
            "Source terrain requires its owned tiles, normals, resources and completed fence"
                .into(),
        );
    }
    Ok(())
}

const GRAPHICS_SCOPE_NAMES: [&str; 11] = [
    "terrain",
    "models",
    "vegetation",
    "objects",
    "source_fragments",
    "fish",
    "water",
    "breaker_lip",
    "spray",
    "source_graphs",
    "source_graph_unlit",
];

fn verify_scopes(
    actual: &GraphicsObservation,
    expected: &GraphicsIdentity,
    frame: u32,
    plants: bool,
    water: bool,
    fish: bool,
) -> Result<(), String> {
    let mut names = std::collections::BTreeSet::new();
    if actual.scopes.is_empty() || actual.scopes.len() > GRAPHICS_SCOPE_NAMES.len() {
        return Err("Graphics scopes outside the module budget".into());
    }
    for scope in &actual.scopes {
        let count = |values: [usize; 2]| values[0] == values[1];
        let generation = scope.generation.ok_or("Graphics scope generation absent")?;
        let required_frame = scope.required_frame.unwrap_or(actual.application_frame);
        let identity = if scope.required_frame.is_some() {
            canonical_json_sha256(&(
                expected.head,
                expected.policy,
                actual.requested,
                generation,
                required_frame,
            ))
        } else {
            canonical_json_sha256(&(expected.head, expected.policy, actual.requested, generation))
        }
        .map_err(|error| error.to_string())?;
        let publication_frame = if let Some(publication) = &scope.publication {
            if scope.required_frame.is_none() {
                return Err("Graphics publication requires its registered source frame".into());
            }
            let seal = canonical_json_sha256(&(identity, publication.frame, publication.sources))
                .map_err(|error| error.to_string())?;
            if publication.seal != seal || publication.frame < required_frame {
                return Err("Graphics publication identity or frame is invalid".into());
            }
            publication.frame
        } else {
            required_frame
        };
        let primary_owner = EntityId::deterministic(format!("module-graphics/{}", scope.name));
        let prepared_owner =
            EntityId::deterministic(format!("module-graphics/{}/prepared", scope.name));
        let owned = scope.owner == Some(primary_owner)
            || (matches!(scope.name.as_str(), "terrain" | "water")
                && scope.required_frame.is_some()
                && scope.publication.is_some()
                && scope.owner == Some(prepared_owner));
        if !GRAPHICS_SCOPE_NAMES.contains(&scope.name.as_str())
            || !names.insert(scope.name.as_str())
            || !owned
            || scope.identity != Some(identity)
            || generation == 0
            || generation > actual.generation
            || (scope.required_frame.is_none() && generation != actual.generation)
            || required_frame < actual.application_frame
            || required_frame > scope.observed
            || publication_frame > scope.observed
            || scope.epoch == 0
            || !scope.token_current
            || !scope.camera_ready
            || !scope.effects_ready
            || scope.meshes[0] == 0
            || scope.materials[0] == 0
            || ![scope.meshes, scope.materials, scope.images, scope.buffers]
                .into_iter()
                .all(count)
            || scope.pipelines[0] == 0
            || scope.pipelines[1..] != [0, 0]
            || scope.effect_groups[0] == 0
            || !count(scope.effect_groups)
            || frame.checked_sub(scope.observed).is_none_or(|age| age > 2)
            || !scope.completed.is_some_and(|completed| {
                completed >= publication_frame
                    && scope
                        .observed
                        .checked_sub(completed)
                        .is_some_and(|age| age <= 64)
            })
        {
            return Err("Graphics require fresh, complete typed GPU inventories, effects and queue completion".into());
        }
    }
    if names.contains("water") != water {
        return Err("Graphics require exactly the water inventory declared by the area".into());
    }
    if names.contains("fish") != fish {
        return Err("Graphics require exactly the inventory for admitted fish placements".into());
    }
    if (names.contains("breaker_lip") || names.contains("spray")) && !water {
        return Err("Breaking effects require the area's water inventory".into());
    }
    if names.contains("breaker_lip") && !names.contains("spray") {
        return Err("A breaker lip requires its coupled spray inventory".into());
    }
    if !names.contains("terrain")
        || plants
            && actual
                .plant_completed_frame
                .is_none_or(|frame| frame < actual.application_frame)
    {
        return Err("Graphics require terrain and the independent plant publication".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn graphics_receipts_refuse_stale_partial_foreign_or_missing_fences() {
        let expected = GraphicsIdentity {
            head: ContentHash::from_bytes([1; 32]),
            policy: ContentHash::from_bytes([2; 32]),
            water: false,
        };
        let tier = VisualQualityTier::High;
        let identity =
            canonical_json_sha256(&(expected.head, expected.policy, tier, 3_u64)).unwrap();
        let scope = GraphicsScope {
            name: "terrain".into(),
            owner: Some(EntityId::deterministic("module-graphics/terrain")),
            identity: Some(identity),
            generation: Some(3),
            epoch: 1,
            observed: 50,
            completed: Some(48),
            required_frame: None,
            publication: None,
            token_current: true,
            meshes: [280, 280],
            materials: [1, 1],
            images: [0, 0],
            buffers: [0, 0],
            pipelines: [32, 0, 0],
            effect_groups: [4, 4],
            effects_ready: true,
            camera_ready: true,
        };
        let valid = GraphicsObservation {
            identity: expected.clone(),
            requested: tier,
            applied: Some(tier),
            persisted: Some(tier),
            request_sequence: 4,
            generation: 3,
            application_frame: 46,
            cpu_ready: true,
            ready: true,
            error: None,
            preference_error: None,
            scopes: vec![scope],
            plant_completed_frame: Some(48),
            source_surfaces: None,
            source_terrain: None,
        };
        assert!(verify(Some(&expected), Some(&valid), 51, true, false).is_ok());
        assert!(verify(Some(&expected), None, 51, true, false).is_err());
        assert!(verify(None, Some(&valid), 51, true, false).is_err());
        for failure in 0..17 {
            let mut invalid = valid.clone();
            match failure {
                0 => invalid.identity.policy = ContentHash::from_bytes([9; 32]),
                1 => invalid.generation = 0,
                2 => invalid.applied = Some(VisualQualityTier::Low),
                3 => invalid.cpu_ready = false,
                4 => invalid.scopes.clear(),
                5 => invalid.scopes[0].token_current = false,
                6 => invalid.scopes[0].observed = 48,
                7 => invalid.scopes[0].completed = Some(45),
                8 => invalid.scopes[0].pipelines[1] = 1,
                9 => invalid.scopes[0].effects_ready = false,
                10 => invalid.scopes[0].effect_groups[1] = 3,
                11 => invalid.scopes[0].meshes[1] = 279,
                12 => invalid.scopes[0].generation = Some(2),
                13 => invalid.scopes[0].owner = Some(EntityId::deterministic("foreign")),
                14 => invalid.plant_completed_frame = None,
                15 => invalid.scopes.push(invalid.scopes[0].clone()),
                _ => invalid.error = Some("refused".into()),
            }
            assert!(
                verify(Some(&expected), Some(&invalid), 51, true, false).is_err(),
                "failure {failure}"
            );
        }
        verify_independent_scope_revisions(&expected, &valid);
        verify_common_surfaces_without_plants(&expected, &valid);
        verify_source_terrain_contract(&expected, &valid);
        verify_declared_water_scope(&expected, &valid);
        verify_source_fragment_scope(&expected, &valid);
        verify_source_graph_scopes(&expected, &valid);
        verify_fish_scope(&expected, &valid);
        let mut save_failed = valid;
        save_failed.persisted = None;
        save_failed.preference_error = Some("write refused".into());
        assert!(
            verify(Some(&expected), Some(&save_failed), 51, true, false).is_ok(),
            "Rendering readiness is distinct from preference persistence"
        );
    }

    fn verify_fish_scope(expected: &GraphicsIdentity, valid: &GraphicsObservation) {
        assert!(verify(None, None, 51, false, true).is_err());
        assert!(verify(Some(expected), Some(valid), 51, false, true).is_err());
        let mut with_fish = valid.clone();
        let mut fish = valid.scopes[0].clone();
        fish.name = "fish".into();
        fish.owner = Some(EntityId::deterministic("module-graphics/fish"));
        fish.meshes = [10, 10];
        fish.materials = [10, 10];
        fish.images = [6, 6];
        fish.buffers = [6, 6];
        with_fish.scopes.push(fish);
        assert!(verify(Some(expected), Some(&with_fish), 51, false, true).is_ok());
        assert!(verify(Some(expected), Some(&with_fish), 51, false, false).is_err());
        for failure in 0..4 {
            let mut invalid = with_fish.clone();
            let scope = &mut invalid.scopes[1];
            match failure {
                0 => scope.token_current = false,
                1 => scope.owner = Some(EntityId::deterministic("module-graphics/models")),
                2 => scope.buffers[1] -= 1,
                _ => scope.completed = None,
            }
            assert!(
                verify(Some(expected), Some(&invalid), 51, false, true).is_err(),
                "fish scope failure {failure}"
            );
        }
    }

    fn verify_source_fragment_scope(expected: &GraphicsIdentity, valid: &GraphicsObservation) {
        let mut with_fragment = valid.clone();
        let mut fragment = with_fragment.scopes[0].clone();
        fragment.name = "source_fragments".into();
        fragment.owner = Some(EntityId::deterministic("module-graphics/source_fragments"));
        with_fragment.scopes.push(fragment);
        assert!(verify(Some(expected), Some(&with_fragment), 51, false, false).is_ok());
        let mut stale = with_fragment.clone();
        stale.scopes[1].token_current = false;
        assert!(verify(Some(expected), Some(&stale), 51, false, false).is_err());
        let mut foreign = with_fragment;
        foreign.scopes[1].owner = Some(EntityId::deterministic("module-graphics/models"));
        assert!(verify(Some(expected), Some(&foreign), 51, false, false).is_err());
    }

    fn verify_source_graph_scopes(expected: &GraphicsIdentity, valid: &GraphicsObservation) {
        let mut with_graphs = valid.clone();
        for name in ["source_graphs", "source_graph_unlit"] {
            let mut scope = valid.scopes[0].clone();
            scope.name = name.into();
            scope.owner = Some(EntityId::deterministic(format!("module-graphics/{name}")));
            with_graphs.scopes.push(scope);
            assert!(verify(Some(expected), Some(&with_graphs), 51, false, false).is_ok());
            let mut stale = with_graphs.clone();
            stale.scopes.last_mut().unwrap().token_current = false;
            assert!(verify(Some(expected), Some(&stale), 51, false, false).is_err());
            let mut foreign = with_graphs.clone();
            foreign.scopes.last_mut().unwrap().owner =
                Some(EntityId::deterministic("module-graphics/models"));
            assert!(verify(Some(expected), Some(&foreign), 51, false, false).is_err());
        }
        let mut duplicate = with_graphs.clone();
        duplicate.scopes.push(with_graphs.scopes[1].clone());
        assert!(verify(Some(expected), Some(&duplicate), 51, false, false).is_err());
        let mut unknown = with_graphs;
        unknown.scopes[1].name = "source_graph_unknown".into();
        unknown.scopes[1].owner = Some(EntityId::deterministic(
            "module-graphics/source_graph_unknown",
        ));
        assert!(verify(Some(expected), Some(&unknown), 51, false, false).is_err());
    }
    fn verify_independent_scope_revisions(
        expected: &GraphicsIdentity,
        valid: &GraphicsObservation,
    ) {
        let mut independent = valid.clone();
        let mut old = independent.scopes[0].clone();
        old.name = "models".into();
        old.owner = Some(EntityId::deterministic("module-graphics/models"));
        old.generation = Some(2);
        old.identity = Some(
            canonical_json_sha256(&(
                expected.head,
                expected.policy,
                valid.requested,
                2_u64,
                47_u32,
            ))
            .unwrap(),
        );
        old.required_frame = Some(47);
        let sources = ContentHash::from_bytes([11; 32]);
        old.publication = Some(GraphicsPublication {
            frame: 48,
            sources,
            seal: canonical_json_sha256(&(old.identity.unwrap(), 48_u32, sources)).unwrap(),
        });
        independent.scopes.push(old);
        assert!(verify(Some(expected), Some(&independent), 51, false, false).is_ok());
        let mut prepared = independent.clone();
        prepared.scopes[0].required_frame = Some(47);
        let identity = canonical_json_sha256(&(
            expected.head,
            expected.policy,
            valid.requested,
            3_u64,
            47_u32,
        ))
        .unwrap();
        let sources = ContentHash::from_bytes([11; 32]);
        prepared.scopes[0].identity = Some(identity);
        prepared.scopes[0].owner =
            Some(EntityId::deterministic("module-graphics/terrain/prepared"));
        prepared.scopes[0].publication = Some(GraphicsPublication {
            frame: 48,
            sources,
            seal: canonical_json_sha256(&(identity, 48_u32, sources)).unwrap(),
        });
        assert!(verify(Some(expected), Some(&prepared), 51, false, false).is_ok());
        let mut foreign = prepared.clone();
        foreign.scopes[0].owner = Some(EntityId::deterministic("module-graphics/water/prepared"));
        assert!(verify(Some(expected), Some(&foreign), 51, false, false).is_err());
        let mut undated = prepared.clone();
        undated.scopes[0].publication = None;
        assert!(verify(Some(expected), Some(&undated), 51, false, false).is_err());
        let mut unsupported = prepared.clone();
        unsupported.scopes[1].owner =
            Some(EntityId::deterministic("module-graphics/models/prepared"));
        assert!(verify(Some(expected), Some(&unsupported), 51, false, false).is_err());
        for failure in 0..6 {
            let mut invalid = independent.clone();
            let scope = &mut invalid.scopes[1];
            match failure {
                0 => scope.required_frame = Some(49),
                1 => scope.required_frame = Some(45),
                2 => scope.required_frame = None,
                3 => scope.generation = Some(4),
                4 => scope.required_frame = Some(46),
                _ => scope.identity = Some(ContentHash::from_bytes([7; 32])),
            }
            assert!(
                verify(Some(expected), Some(&invalid), 51, false, false).is_err(),
                "independent scope failure {failure}"
            );
        }
        for failure in 0..5 {
            let mut invalid = independent.clone();
            let identity = invalid.scopes[1].identity.unwrap();
            let publication = invalid.scopes[1].publication.as_mut().unwrap();
            match failure {
                0 => publication.frame = 49,
                1 => publication.sources = ContentHash::from_bytes([12; 32]),
                2 => publication.seal = ContentHash::from_bytes([13; 32]),
                3 => {
                    publication.frame = 46;
                    publication.seal =
                        canonical_json_sha256(&(identity, 46_u32, publication.sources)).unwrap();
                }
                _ => {
                    publication.frame = 49;
                    publication.seal =
                        canonical_json_sha256(&(identity, 49_u32, publication.sources)).unwrap();
                }
            }
            assert!(
                verify(Some(expected), Some(&invalid), 51, false, false).is_err(),
                "publication failure {failure}"
            );
        }
    }

    fn verify_declared_water_scope(expected: &GraphicsIdentity, valid: &GraphicsObservation) {
        assert!(
            serde_json::to_value(expected)
                .unwrap()
                .get("water")
                .is_none()
        );
        let mut requested = expected.clone();
        requested.water = true;
        let mut actual = valid.clone();
        actual.identity.water = true;
        assert!(verify(Some(&requested), Some(&actual), 51, false, false).is_err());
        let mut water = actual.scopes[0].clone();
        water.name = "water".into();
        water.owner = Some(EntityId::deterministic("module-graphics/water"));
        water.images = [21, 21];
        water.buffers = [2, 2];
        actual.scopes.push(water);
        assert!(verify(Some(&requested), Some(&actual), 51, false, false).is_ok());
        let mut with_effects = actual.clone();
        for name in ["breaker_lip", "spray"] {
            let mut effect = actual.scopes[0].clone();
            effect.name = name.into();
            effect.owner = Some(EntityId::deterministic(format!("module-graphics/{name}")));
            with_effects.scopes.push(effect);
        }
        assert!(verify(Some(&requested), Some(&with_effects), 51, false, false).is_ok());
        let mut missing_spray = with_effects.clone();
        missing_spray.scopes.pop();
        assert!(verify(Some(&requested), Some(&missing_spray), 51, false, false).is_err());
        let mut stale_effect = with_effects;
        stale_effect.scopes.last_mut().unwrap().token_current = false;
        assert!(verify(Some(&requested), Some(&stale_effect), 51, false, false).is_err());
        let mut stale = actual.clone();
        stale.scopes[1].completed = Some(45);
        assert!(verify(Some(&requested), Some(&stale), 51, false, false).is_err());
        let mut unexpected = actual;
        unexpected.identity.water = false;
        assert!(verify(Some(expected), Some(&unexpected), 51, false, false).is_err());
    }
    fn verify_common_surfaces_without_plants(
        expected: &GraphicsIdentity,
        valid: &GraphicsObservation,
    ) {
        let mut graphics = valid.clone();
        graphics.plant_completed_frame = None;
        graphics.source_surfaces = Some(super::super::SurfaceStatus {
            active: true,
            ready: true,
            motion_matches: true,
            generation: 1,
            world_receivers: 2,
            model_receivers: 1,
            terrain_receivers: 1,
            opaque_casters: 2,
            plant_batches: 0,
            encoded_frames: 2,
            cascade_layers: [2, 1, 1],
            encoded_frame: Some(44),
            gpu_completed: Some(48),
        });
        assert!(verify(Some(expected), Some(&graphics), 51, false, false).is_ok());
        for failure in 0..8 {
            let mut invalid = graphics.clone();
            let source = invalid.source_surfaces.as_mut().unwrap();
            match failure {
                0 => source.active = false,
                1 => source.ready = false,
                2 => source.motion_matches = false,
                3 => source.generation = 0,
                4 => source.encoded_frames = 0,
                5 => source.gpu_completed = Some(43),
                6 => source.encoded_frame = None,
                _ => source.world_receivers = 3,
            }
            assert!(
                verify(Some(expected), Some(&invalid), 51, false, false).is_err(),
                "failure {failure}"
            );
        }
    }
    fn verify_source_terrain_contract(expected: &GraphicsIdentity, valid: &GraphicsObservation) {
        let mut graphics = valid.clone();
        graphics.scopes[0].buffers = [5, 5];
        graphics.scopes[0].images = [8, 8];
        graphics.source_terrain = Some(SourceTerrainObservation {
            document: Some(ContentHash::from_bytes([7; 32])),
            package: Some(ContentHash::from_bytes([8; 32])),
            pending: false,
            active_tiles: 2,
            cached_tiles: 3,
            normals_ready: true,
            staged_frame: Some(44),
            ready: true,
            error: None,
        });
        assert!(verify(Some(expected), Some(&graphics), 51, false, false).is_ok());
        for failure in 0..8 {
            let mut invalid = graphics.clone();
            let source = invalid.source_terrain.as_mut().unwrap();
            match failure {
                0 => source.pending = true,
                1 => source.ready = false,
                2 => source.normals_ready = false,
                3 => source.staged_frame = Some(49),
                4 => source.package = None,
                5 => source.cached_tiles = 257,
                6 => invalid.scopes[0].buffers = [4, 4],
                _ => invalid.scopes[0].images = [7, 7],
            }
            assert!(
                verify(Some(expected), Some(&invalid), 51, false, false).is_err(),
                "terrain failure {failure}"
            );
        }
    }
}
