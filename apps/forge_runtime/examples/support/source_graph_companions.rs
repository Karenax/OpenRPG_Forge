//! Original, synthetic API 13 placement inputs for a local qualification only.
//! This does not claim a Vault asset, a native source scene, or GPU admission.
use forge_assets::{
    governed_import::vault_asset_id,
    source_graph_bundle::SOURCE_GRAPH_STATE_BUFFER_DECLARATION,
    source_graph_glb::SourceGraphGlbReadBudget,
    source_particle_emission::SourceSingleBurstLoopParameters,
    source_particle_package::SourceParticlePackage,
    source_particle_transport::{
        SourceParticleBlob, SourceParticleControlLayout, SourceParticleFrameLayout,
        SourceParticleOwnerTransport, SourceParticleProgram, SourceParticleTransport,
        SourceParticleVertexTransport,
    },
    source_physics_package::SourcePhysicsPackage,
};
use forge_core::{AssetId, ContentHash, bytes_sha256, canonical_json_sha256};
use forge_delivery::module_placement_companions::export_paths;
use forge_project::{
    module_package::ModulePlacementCompanionKind,
    zone_authoring::{
        physics::PhysicsSceneSettings,
        placements::{ParticlePackageReference, PhysicsPackageReference},
        session::SourceCompanionPin,
    },
};
use serde_json::{Value, json};
use std::collections::BTreeMap;

pub(super) const LIBRARY_ID: &str = "synthetic-source-graph-companions";
const PARTICLE_LIBRARY_ID: &str = "synthetic-source-graph-particles";
const GUID: &str = "00000000000000000000000000000001";
pub(super) const PHYSICS_RECIPE: &[u8] = br#"{"schema":"forge.qualification.source-model","version":1,"mode":"source-graph-physics","generator":"source_fragment_module_fixture"}"#;
pub(super) const PARTICLE_RECIPE: &[u8] = br#"{"schema":"forge.qualification.source-model","version":1,"mode":"source-graph-companions","generator":"source_fragment_module_fixture"}"#;
const COMPUTE: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> input_words: array<vec4<u32>>;
@group(0) @binding(4) var<storage,read_write> output_words: array<vec4<u32>>;
@compute @workgroup_size(1) fn advance() {
    output_words[0] = vec4<u32>(bitcast<u32>(1.0), 0u, 0u, 0u);
}
";
const PROJECT: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> input_words: array<vec4<u32>>;
@group(0) @binding(4) var<storage,read_write> output_words: array<vec4<u32>>;
@compute @workgroup_size(1) fn project() {
    output_words[0] = input_words[0];
    output_words[1] = bank.words[10];
    output_words[2] = bank.words[12];
}
";
const FORWARD: &str = r"
@vertex fn vertex(@location(0) position: vec3<f32>) -> @builtin(position) vec4<f32> {
    return vec4<f32>(position.xy, 0.5, 1.0);
}
@fragment fn fragment() -> @location(0) vec4<f32> {
    let emitted = select(0.0, 1.0, source_state[0].x == bitcast<u32>(1.0));
    let jitter = select(0.0, 1.0, abs(bitcast<f32>(source_state[1].x)) > 0.001);
    let moved = select(0.0, 1.0, abs(bitcast<f32>(source_state[2].x)) > 0.1);
    return vec4<f32>(emitted, jitter, moved, 1.0);
}
";
const PREPASS: &str = r"
@vertex fn vertex(@location(0) position: vec3<f32>) -> @builtin(position) vec4<f32> {
    return vec4<f32>(position.xy, 0.5, 1.0);
}
@fragment fn fragment() {}
";

pub(super) struct Fixture {
    pub model: Vec<u8>,
    pub physics: PhysicsPackageReference,
    pub particles: Option<ParticleFixture>,
    pub physics_settings: PhysicsSceneSettings,
    pub payloads: BTreeMap<String, Vec<u8>>,
}

pub(super) struct ParticleFixture {
    pub asset_id: AssetId,
    pub model: Vec<u8>,
    pub reference: ParticlePackageReference,
}

fn box_observation(file_id: &str, enabled: bool, trigger: bool) -> Value {
    json!({
        "identity":{"guid":GUID,"file_id":file_id,"type":"UnityEngine.BoxCollider"},
        "serialized":[
            {"property_path":"m_GameObject","property_type":"ObjectReference",
                "reference":{"guid":GUID,"file_id":"1"}},
            {"property_path":"m_Center","property_type":"Vector3","vector":[0,0,0]},
            {"property_path":"m_Size","property_type":"Vector3","vector":[2,2,2]},
            {"property_path":"m_Enabled","property_type":"Boolean","boolean":enabled},
            {"property_path":"m_IsTrigger","property_type":"Boolean","boolean":trigger},
            {"property_path":"m_IncludeLayers.m_Bits","property_type":"Integer","number":0},
            {"property_path":"m_ExcludeLayers.m_Bits","property_type":"Integer","number":0},
            {"property_path":"m_LayerOverridePriority","property_type":"Integer","number":0},
            {"property_path":"m_Material","property_type":"ObjectReference",
                "reference":{"guid":"","file_id":"","path":"","type":""}}
        ]
    })
}

fn carrier() -> Result<(Vec<u8>, Vec<Value>), String> {
    let original = super::source_graph_fixture::fixture()?;
    let graph = forge_assets::source_graph_glb::read_source_graph_glb(&original)?
        .ok_or("Synthetic source graph absent")?;
    let mut document = graph.document().clone();
    let json_length = usize::try_from(u32::from_le_bytes(
        original
            .get(12..16)
            .ok_or("Synthetic GLB JSON header absent")?
            .try_into()
            .map_err(|_| "Synthetic GLB JSON header invalid")?,
    ))
    .map_err(|error| error.to_string())?;
    let binary = original
        .get(28 + json_length..)
        .ok_or("Synthetic GLB binary absent")?;
    let observations = vec![
        box_observation("10", true, false),
        box_observation("11", true, true),
        box_observation("12", false, false),
    ];
    let source = &mut document["nodes"][0]["extras"]["unity_source_node"];
    source["gameobject"] = json!({"guid":GUID,"file_id":"1"});
    source["native_collider_observations"] = json!(observations);
    let mut encoded = serde_json::to_vec(&document).map_err(|error| error.to_string())?;
    encoded.resize(encoded.len().next_multiple_of(4), b' ');
    let total = 28usize
        .checked_add(encoded.len())
        .and_then(|length| length.checked_add(binary.len()))
        .ok_or("Synthetic GLB length overflow")?;
    let mut model = b"glTF".to_vec();
    model.extend(2_u32.to_le_bytes());
    model.extend(
        u32::try_from(total)
            .map_err(|error| error.to_string())?
            .to_le_bytes(),
    );
    model.extend(
        u32::try_from(encoded.len())
            .map_err(|error| error.to_string())?
            .to_le_bytes(),
    );
    model.extend(b"JSON");
    model.extend(encoded);
    model.extend(
        u32::try_from(binary.len())
            .map_err(|error| error.to_string())?
            .to_le_bytes(),
    );
    model.extend(b"BIN\0");
    model.extend(binary);
    Ok((model, observations))
}

fn particle_carrier() -> Result<(Vec<u8>, ContentHash), String> {
    let forward = format!("{SOURCE_GRAPH_STATE_BUFFER_DECLARATION}\n{FORWARD}");
    let prepass = format!("{SOURCE_GRAPH_STATE_BUFFER_DECLARATION}\n{PREPASS}");
    Ok((
        super::source_graph_fixture::fixture_with_stages(&forward, &prepass)?,
        bytes_sha256(forward.as_bytes()),
    ))
}

fn physics_package(model: &[u8], observations: &[Value]) -> Result<SourcePhysicsPackage, String> {
    let carrier_sha256 = bytes_sha256(model);
    let components: Vec<_> = observations
        .iter()
        .map(|item| {
            json!({
                "source_binding":{"source_component":item["identity"],"glb_sha256":carrier_sha256,
                    "node_index":0,"node_path":"fixture","kind":"box",
                    "source_serialized_properties":item["serialized"]},
                "backend_policy_qualified":false,"shape_transport":{"kind":"native_box_observation"},
                "native_metrics":{"source_guid":GUID,"source_file_id":item["identity"]["file_id"],
                    "contact_offset":0.0,"game_object_layer":0,
                    "implicit_material":{"original_reference_null":true,
                        "friction_combine":"Average","bounce_combine":"Average",
                        "dynamic_friction":0.5,"static_friction":0.5,"bounciness":0.0}}
            })
        })
        .collect();
    let manifest = serde_json::to_vec(&json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_sha256,"components":components}]
    }))
    .map_err(|error| error.to_string())?;
    Ok(SourcePhysicsPackage {
        carrier_sha256,
        manifest_sha256: bytes_sha256(&manifest),
        manifest,
        candidates: BTreeMap::new(),
    })
}

fn particle_package(
    model: &[u8],
    graph_sha256: ContentHash,
) -> Result<SourceParticlePackage, String> {
    let mut resources = BTreeMap::new();
    let mut blob = |path: &str, bytes: Vec<u8>| {
        let pin = SourceParticleBlob {
            path: path.into(),
            sha256: bytes_sha256(&bytes),
            byte_length: bytes.len(),
        };
        resources.insert(path.into(), bytes);
        pin
    };
    let bank = blob("bank.bin", vec![0; 1024]);
    let owner = SourceParticleOwnerTransport {
        source_component_guid: "a".repeat(32),
        source_component_file_id: "11400000".into(),
        source_node: 0,
        source_primitive: 0,
        source_material: 0,
        compute: SourceParticleProgram {
            source: blob("compute.wgsl", COMPUTE.as_bytes().to_vec()),
            entry_point: "advance".into(),
        },
        curve_atlas: blob("atlas.bin", vec![0; 8]),
        curve_width: 1,
        curve_height: 1,
        curve_bank: bank.clone(),
        initial_state: blob("state.bin", vec![0; 16]),
        state_prefix_bytes: 16,
        workgroups: 1,
        control: SourceParticleControlLayout {
            delta_offset: 1008,
            mode_offset: 1012,
            stamp_offset: 1016,
            count_offset: 1020,
            reset_mode: 3.0,
            allocate_mode: 2.0,
            update_mode: 1.0,
        },
        spawner: SourceSingleBurstLoopParameters {
            loop_duration_seconds: 3.0,
            loop_count: 1,
            burst_delay_seconds: 0.0,
            burst_count: 1.0,
            burst_active: true,
            initial_play: true,
            prewarm_steps: 0,
        },
        projection: SourceParticleVertexTransport {
            program: SourceParticleProgram {
                source: blob("project.wgsl", PROJECT.as_bytes().to_vec()),
                entry_point: "project".into(),
            },
            bank,
            native_vertices: blob("vertices.bin", vec![0; 3 * 64]),
            vertex_count: 3,
            input_stride_bytes: 64,
            output_stride_bytes: 112,
            frame_layout: Some(SourceParticleFrameLayout {
                object_to_world_offset: 0,
                world_to_object_offset: 64,
                relative_world_to_clip_offset: 128,
                camera_position_offset: 192,
                evidence_sha256: bytes_sha256(b"synthetic particle frame layout"),
            }),
        },
    };
    let carrier_sha256 = bytes_sha256(model);
    let transport = SourceParticleTransport {
        schema_version: 1,
        carrier_sha256,
        source_archive_sha256: bytes_sha256(PARTICLE_RECIPE),
        source_graph_sha256: graph_sha256,
        licence_ref: "LicenseRef-local-qualification-fixture".into(),
        owners: vec![owner],
    };
    let manifest = serde_json::to_vec(&transport).map_err(|error| error.to_string())?;
    Ok(SourceParticlePackage {
        carrier_sha256,
        manifest_sha256: bytes_sha256(&manifest),
        manifest,
        resources,
    })
}

fn proof(
    kind: ModulePlacementCompanionKind,
    pin: SourceCompanionPin,
    library_id: &str,
) -> Result<Vec<u8>, String> {
    let field = match kind {
        ModulePlacementCompanionKind::Physics => "source_physics_package",
        ModulePlacementCompanionKind::Particles => "source_particle_package",
    };
    let mut profile = json!({"external_id":library_id,"model_content_sha256":pin.carrier_sha256});
    profile[field] = json!({"carrier_sha256":pin.carrier_sha256,
        "manifest_sha256":pin.manifest_sha256,"sha256":pin.package_sha256});
    let revision = bytes_sha256(b"synthetic qualification registry; no Vault accessed");
    let metadata = json!({
        "schema":"forge.private-vault-package-proof.v1",
        "library_id":library_id,"model_sha256":pin.carrier_sha256,"library_revision":revision,
        "metadata":{"id":library_id,"model_sha256":pin.carrier_sha256},
        "functional_profile":profile,
        "source_catalog_entry":{"id":library_id,"sha256":pin.carrier_sha256}
    });
    serde_json::to_vec(&json!({
        "schema":"forge.private-vault-companion-proof.v1","kind":kind,"asset_id":pin.asset_id,
        "carrier_sha256":pin.carrier_sha256,"manifest_sha256":pin.manifest_sha256,
        "package_sha256":pin.package_sha256,"registry_snapshot_revision":revision,
        "selected_details_sha256":canonical_json_sha256(&metadata["metadata"]).map_err(|e| e.to_string())?,
        "selected_profile_sha256":canonical_json_sha256(&metadata["functional_profile"]).map_err(|e| e.to_string())?,
        "selected_catalog_entry_sha256":canonical_json_sha256(&metadata["source_catalog_entry"]).map_err(|e| e.to_string())?,
        "metadata":metadata
    }))
    .map_err(|error| error.to_string())
}

pub(super) fn fixture(asset_id: AssetId, with_particles: bool) -> Result<Fixture, String> {
    if asset_id != vault_asset_id(LIBRARY_ID) {
        return Err("Synthetic companion identity differs".into());
    }
    let (model, observations) = carrier()?;
    let physics = physics_package(&model, &observations)?;
    let (_, components) =
        physics.resolve_for_carrier(&model, SourceGraphGlbReadBudget::Standard)?;
    let physics_settings = PhysicsSceneSettings {
        schema_version: 1,
        layer_matrix: [u32::MAX; 32],
        contact_skin_m: 0.0,
        evidence: "Synthetic target policy; no original native simulation claimed".into(),
    };
    let contacts = forge_engine::source_physics_contacts::ContactSceneSettings {
        layer_matrix: physics_settings.layer_matrix,
        contact_skin_m: physics_settings.contact_skin_m,
        evidence: physics_settings.evidence.clone(),
    };
    forge_engine::source_physics_contacts::contacts_from_transport(&components, &contacts)?;
    let carrier_sha256 = bytes_sha256(&model);
    let physics_wire = physics.encode()?;
    let physics_pin = SourceCompanionPin {
        asset_id,
        carrier_sha256,
        manifest_sha256: physics.manifest_sha256,
        package_sha256: bytes_sha256(&physics_wire),
    };
    let mut payloads = BTreeMap::new();
    let kind = ModulePlacementCompanionKind::Physics;
    let (package_path, proof_path) = export_paths(kind, physics_pin);
    payloads.insert(package_path, physics_wire);
    payloads.insert(proof_path, proof(kind, physics_pin, LIBRARY_ID)?);
    let particles = if with_particles {
        let (model, graph) = particle_carrier()?;
        let package = particle_package(&model, graph)?;
        let resolved = package.resolve_for_carrier(&model)?;
        for owner in &resolved.contract.owners {
            forge_engine::source_particle_transport::PreparedSourceParticleOwner::prepare(
                owner,
                &resolved.resources,
            )?;
        }
        let wire = package.encode()?;
        let pin = SourceCompanionPin {
            asset_id: vault_asset_id(PARTICLE_LIBRARY_ID),
            carrier_sha256: bytes_sha256(&model),
            manifest_sha256: package.manifest_sha256,
            package_sha256: bytes_sha256(&wire),
        };
        let kind = ModulePlacementCompanionKind::Particles;
        let (package_path, proof_path) = export_paths(kind, pin);
        payloads.insert(package_path, wire);
        payloads.insert(proof_path, proof(kind, pin, PARTICLE_LIBRARY_ID)?);
        Some(ParticleFixture {
            asset_id: pin.asset_id,
            model,
            reference: ParticlePackageReference {
                schema_version: 1,
                carrier_sha256: pin.carrier_sha256,
                manifest_sha256: pin.manifest_sha256,
                package_sha256: pin.package_sha256,
            },
        })
    } else {
        None
    };
    Ok(Fixture {
        model,
        physics: PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256,
            manifest_sha256: physics_pin.manifest_sha256,
            package_sha256: physics_pin.package_sha256,
        },
        particles,
        physics_settings,
        payloads,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn synthetic_companions_resolve_to_declared_collider_states_and_pinned_effect() {
        let asset_id = vault_asset_id(LIBRARY_ID);
        let fixture = fixture(asset_id, true).unwrap();
        let pin = SourceCompanionPin {
            asset_id,
            carrier_sha256: fixture.physics.carrier_sha256,
            manifest_sha256: fixture.physics.manifest_sha256,
            package_sha256: fixture.physics.package_sha256,
        };
        let (path, _) = export_paths(ModulePlacementCompanionKind::Physics, pin);
        let physics = SourcePhysicsPackage::decode_pinned(
            &fixture.payloads[&path],
            pin.carrier_sha256,
            pin.manifest_sha256,
            pin.package_sha256,
        )
        .unwrap();
        let (graph, components) = physics
            .resolve_for_carrier(&fixture.model, SourceGraphGlbReadBudget::Standard)
            .unwrap();
        assert_eq!(components.len(), 3);
        let boxes = forge_engine::source_colliders::prepare(
            graph.document(),
            bevy::prelude::Mat4::IDENTITY,
        )
        .unwrap();
        assert_eq!(
            boxes
                .iter()
                .map(|item| (item.enabled, item.is_trigger))
                .collect::<Vec<_>>(),
            [(true, false), (true, true), (false, false)]
        );
        let effect = fixture.particles.as_ref().unwrap();
        let pin = SourceCompanionPin {
            asset_id: effect.asset_id,
            carrier_sha256: effect.reference.carrier_sha256,
            manifest_sha256: effect.reference.manifest_sha256,
            package_sha256: effect.reference.package_sha256,
        };
        let (path, _) = export_paths(ModulePlacementCompanionKind::Particles, pin);
        let particles = SourceParticlePackage::decode_pinned(
            &fixture.payloads[&path],
            pin.carrier_sha256,
            pin.manifest_sha256,
            pin.package_sha256,
        )
        .unwrap();
        let resolved = particles.resolve_for_carrier(&effect.model).unwrap();
        assert_eq!(resolved.contract.owners.len(), 1);
        assert!(
            resolved.contract.owners[0]
                .projection
                .frame_layout
                .is_some()
        );
        let mut changed = fixture.model.clone();
        changed[0] ^= 1;
        assert!(
            physics
                .resolve_for_carrier(&changed, SourceGraphGlbReadBudget::Standard)
                .is_err()
        );
        let mut changed_effect = effect.model.clone();
        changed_effect[0] ^= 1;
        assert!(particles.resolve_for_carrier(&changed_effect).is_err());
    }

    #[test]
    fn physical_only_reuses_identical_package_without_particle_material_or_payload() {
        let asset_id = vault_asset_id(LIBRARY_ID);
        let physical = fixture(asset_id, false).unwrap();
        let combined = fixture(asset_id, true).unwrap();
        assert!(physical.particles.is_none());
        assert!(combined.particles.is_some());
        assert_eq!(physical.model, combined.model);
        assert_eq!(physical.physics, combined.physics);
        assert_eq!(physical.physics_settings, combined.physics_settings);
        assert_eq!(physical.payloads.len(), 2);
        assert_eq!(combined.payloads.len(), 4);
        for (path, bytes) in &physical.payloads {
            assert_eq!(combined.payloads.get(path), Some(bytes));
        }
        let graph = forge_assets::source_graph_glb::read_source_graph_glb(&physical.model)
            .unwrap()
            .unwrap();
        for (_, material) in &graph.materials {
            for stage in [&material.forward, &material.prepass] {
                let source = std::str::from_utf8(graph.views[stage.buffer_view]).unwrap();
                assert!(
                    !forge_assets::source_graph_bundle::stage_requires_state_buffer(source)
                        .unwrap()
                );
            }
        }
    }
}
