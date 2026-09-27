//! Shared far-card mesh proof; spatial selection and authoring remain separate.
use super::{PlantMaterial, Probe, ProbeMode, ProbeParams, read_vectors};
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, mesh::MeshTag, prelude::*,
};
use std::{collections::BTreeMap, error::Error};

pub(super) use forge_engine::tidewater_plants::Record;

pub(super) fn load(
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<Option<Vec<Record>>, Box<dyn Error>> {
    let Some(bytes) = files.get("canopy-instances.bin") else {
        return Ok(None);
    };
    if bytes.is_empty() || bytes.len() > 65_383 * 32 || !bytes.len().is_multiple_of(32) {
        return Err("invalid canopy instance count".into());
    }
    let values = read_vectors::<4>(bytes)?;
    values
        .as_chunks::<2>()
        .0
        .iter()
        .map(|[position, data]| {
            if position[3] <= 0.0 || position[3] > 100.0 {
                return Err("invalid canopy scale".into());
            }
            Ok(Record {
                i_pos: Vec4::from_array(*position),
                i_dat: Vec4::from_array(*data),
            })
        })
        .collect::<Result<Vec<_>, Box<dyn Error>>>()
        .map(Some)
}

pub(super) fn fallback(params: &ProbeParams) -> Vec<Record> {
    vec![Record {
        i_pos: params.i_pos,
        i_dat: params.i_dat,
    }]
}

pub(super) fn spawn(
    commands: &mut Commands,
    probe: &Probe,
    mesh: Handle<Mesh>,
    material: Handle<PlantMaterial>,
) {
    let visibility = if matches!(
        probe.mode,
        ProbeMode::TerrainCoverage | ProbeMode::VillageAlbedo
    ) {
        Visibility::Hidden
    } else {
        Visibility::Visible
    };
    let instance = &probe.instance;
    let transform = if probe.canopy.is_some() {
        // Far source vertices already produce world positions from packed records.
        Transform::IDENTITY
    } else {
        Transform::from_xyz(instance.x, instance.y, instance.z)
            .with_rotation(Quat::from_rotation_y(instance.yaw))
            .with_scale(Vec3::new(
                instance.s,
                instance.s * instance.sy.abs(),
                instance.s,
            ))
    };
    let count = probe.canopy.as_ref().map_or(1, Vec::len);
    for index in 0..count {
        commands.spawn((
            Mesh3d(mesh.clone()),
            MeshMaterial3d(material.clone()),
            MeshTag(u32::try_from(index).expect("bounded canopy count")),
            transform,
            NoFrustumCulling,
            NotShadowCaster,
            visibility,
        ));
    }
    info!(instances = count, "Tidewater shared plant mesh submitted");
}
