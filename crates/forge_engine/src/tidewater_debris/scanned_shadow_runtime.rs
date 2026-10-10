//! Ephemeral scanned casters submitted to the existing three-cascade graph.
use super::{scanned_draws, scanned_meshes::PreparedMeshes, scanned_placements::SourcePlacements};
use bevy::{
    mesh::{Indices, VertexAttributeValues},
    prelude::*,
    render::Extract,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::sync::Arc;
#[path = "scanned_shadow/gpu.rs"]
pub mod gpu;

pub struct MeshBytes {
    pub vertices: Vec<u8>,
    pub indices: Vec<u8>,
}
#[derive(Clone, PartialEq)]
pub struct Pose {
    pub asset: u8,
    pub model: Mat4,
    pub data: [f32; 4],
    pub enabled: bool,
}
#[derive(Resource, Clone)]
pub struct Frame {
    pub target: Handle<Image>,
    pub shader: Arc<String>,
    pub meshes: Arc<Vec<MeshBytes>>,
    pub poses: Vec<Pose>,
}

/// Retain only the four acquired near prototypes; share their buffers between owners.
/// # Errors
/// Refuses missing positions/indices or incomplete near-LOD inventory.
pub fn geometry(prepared: &PreparedMeshes) -> Result<Vec<MeshBytes>, String> {
    let mut near: Vec<_> = prepared.prototypes.iter().filter(|p| p.lod == 0).collect();
    near.sort_by_key(|p| p.asset);
    if near.len() != 4
        || near
            .iter()
            .enumerate()
            .any(|(i, p)| usize::from(p.asset) != i)
    {
        return Err("Scanned near caster inventory differs".into());
    }
    near.into_iter()
        .map(|prototype| {
            let Some(VertexAttributeValues::Float32x3(positions)) =
                prototype.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                return Err("Scanned caster positions absent".into());
            };
            let Some(Indices::U32(indices)) = prototype.mesh.indices() else {
                return Err("Scanned caster indices absent".into());
            };
            Ok(MeshBytes {
                vertices: positions
                    .iter()
                    .flatten()
                    .flat_map(|v| v.to_le_bytes())
                    .collect(),
                indices: indices.iter().flat_map(|v| v.to_le_bytes()).collect(),
            })
        })
        .collect()
}

/// Keep an entry per accepted placement, enabling only the source's near-LOD draws.
/// # Errors
/// Refuses absent source variations or malformed per-draw storage references.
pub fn poses(
    source: &SourcePlacements,
    placements: &[ManualPlacement],
    frame: &scanned_draws::Frame,
) -> Result<Vec<Pose>, String> {
    placements
        .iter()
        .map(|placement| {
            let owner = source
                .definitions
                .get(&placement.asset_id)
                .ok_or("Scanned caster variation absent")?;
            let draw = frame
                .draws
                .iter()
                .find(|d| d.placement_id == placement.id && d.lod == 0);
            let data = if let Some(draw) = draw {
                *frame
                    .records
                    .get(usize::try_from(draw.tag).map_err(|e| e.to_string())?)
                    .ok_or("Scanned caster tag out of bounds")?
            } else {
                [f32::from(owner.source.asset), 0.0, 1.0, 1.0]
            };
            Ok(Pose {
                asset: owner.source.asset,
                model: crate::domain_transform_to_bevy(placement.transform).to_matrix(),
                data,
                enabled: draw.is_some(),
            })
        })
        .collect()
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extraction systems require owned Extract and Commands parameter wrappers."
)]
pub(crate) fn extract(mut commands: Commands, frame: Extract<Option<Res<Frame>>>) {
    if let Some(frame) = frame.as_ref() {
        commands.insert_resource((**frame).clone());
    } else {
        commands.remove_resource::<Frame>();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::{ExtractSchedule, MainWorld};

    #[test]
    fn retired_scanned_scene_does_not_leave_a_render_world_frame() {
        let mut main = MainWorld::default();
        let meshes = Arc::new(Vec::new());
        main.insert_resource(Frame {
            target: Handle::default(),
            shader: Arc::new(String::new()),
            meshes: meshes.clone(),
            poses: Vec::new(),
        });
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extract);
        schedule.run(&mut render);
        assert!(Arc::ptr_eq(&render.resource::<Frame>().meshes, &meshes));
        render
            .resource_mut::<MainWorld>()
            .remove_resource::<Frame>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
    }

    #[test]
    #[ignore = "Requires acquired scanned package"]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn near_casters_share_four_prototypes_and_retain_draw_coverage() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let package = forge_assets::tidewater_debris::scanned::ScannedPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
        )
        .unwrap();
        let meshes = super::super::scanned_meshes::prepare(&package).unwrap();
        let near = geometry(&meshes).unwrap();
        assert_eq!(near.len(), 4);
        assert_eq!(
            near.iter().map(|m| m.indices.len() / 12).sum::<usize>(),
            10_598
        );
        let source = super::super::scanned_placements::prepare(&package).unwrap();
        let p = source.placements[0].transform.translation_m;
        let draws = scanned_draws::prepare(
            &source,
            &source.placements,
            [f64::from(p.x), f64::from(p.y), f64::from(p.z)],
            |_, _, _| true,
        )
        .unwrap();
        let casters = poses(&source, &source.placements, &draws).unwrap();
        assert_eq!(casters.len(), 141);
        for (caster, placement) in casters.iter().zip(&source.placements) {
            let draw = draws
                .draws
                .iter()
                .find(|d| d.placement_id == placement.id && d.lod == 0);
            assert_eq!(caster.enabled, draw.is_some());
            if let Some(draw) = draw {
                assert_eq!(
                    caster.data,
                    draws.records[usize::try_from(draw.tag).unwrap()]
                );
            }
        }
        let empty =
            scanned_draws::prepare(&source, &source.placements, [0.0; 3], |_, _, _| false).unwrap();
        assert!(
            poses(&source, &source.placements, &empty)
                .unwrap()
                .iter()
                .all(|p| !p.enabled)
        );
    }
}
