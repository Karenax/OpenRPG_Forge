// SPDX-License-Identifier: MIT
// View adaptation from Tidewater, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE; upstream 1438b1abfcaee3267092b75573014f4d9b4a983c.
//! Native scene adapter for shared terrain rendering.
use super::Probe;
use bevy::{
    math::{DMat4, DQuat, DVec3, DVec4},
    prelude::*,
};
pub use forge_engine::tidewater_terrain::lod::{LodMaterial, install_shader};
use forge_engine::tidewater_terrain::{
    TerrainSettings,
    scene::{Bindings, PreparedTerrain},
};
use forge_world::cdlod::CdlodView;

/// Convert the already verified native fixture into the same bounded package used by Studio.
pub fn package(
    files: &std::collections::BTreeMap<String, Vec<u8>>,
) -> Result<forge_assets::tidewater_terrain::TerrainPackage, String> {
    let mut entries = serde_json::Map::new();
    for name in [
        "terrain.wgsl",
        "terrain-lod.wgsl",
        "terrain-normal.bin",
        "terrain-splat.bin",
        "terrain-detail.bin",
        "LICENSE-Tidewater.txt",
    ] {
        let bytes = files
            .get(name)
            .ok_or_else(|| format!("Missing terrain resource {name}"))?;
        entries.insert(
            name.into(),
            serde_json::json!({"sha256":forge_core::bytes_sha256(bytes), "bytes":bytes.len()}),
        );
    }
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"forge.tidewater.terrain-package", "version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c", "files":entries
    }))
    .map_err(|e| e.to_string())?;
    forge_assets::tidewater_terrain::TerrainPackage::load(
        &manifest,
        forge_core::bytes_sha256(&manifest),
        |name, _| {
            files
                .get(name)
                .cloned()
                .ok_or_else(|| format!("Missing terrain resource {name}"))
        },
    )
}

pub fn setup(world: &mut World) {
    let (prepared, coverage) = {
        let probe = world.resource::<Probe>();
        if matches!(
            probe.mode,
            super::ProbeMode::VillageAlbedo
                | super::ProbeMode::Silhouette
                | super::ProbeMode::VegetationDirect
                | super::ProbeMode::VegetationEnvironment
                | super::ProbeMode::GrassCoverage
        ) {
            return;
        }
        let Some(bytes) = &probe.lod_heights else {
            return;
        };
        let plan = probe.terrain.as_ref().expect("validated LOD terrain");
        let package = probe
            .terrain_package
            .as_ref()
            .expect("verified terrain package");
        let base = Vec3::new(probe.instance.x, probe.instance.y, probe.instance.z);
        let transform = super::camera_transform(probe, base);
        let Projection::Perspective(projection) = super::camera_projection(probe) else {
            unreachable!()
        };
        (
            PreparedTerrain::new(plan, bytes, package, source_view(&transform, &projection))
                .expect("verified authored terrain inputs"),
            probe.mode == super::ProbeMode::TerrainCoverage,
        )
    };
    let placeholder = world.resource_mut::<Assets<Image>>().add(Image::default());
    let object = world.resource::<super::object_shadow::Target>();
    let bindings = Bindings {
        environment: world.resource::<super::ProbeEnvironment>().0.clone(),
        hill_shadow: world
            .resource::<super::hill_shadow::Images>()
            .output
            .clone(),
        object_shadow: object.image.clone(),
        object_settings: object.settings.clone(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        shore_state: placeholder.clone(),
        shore_lace: placeholder,
        shore_params: [UVec4::ZERO; 3],
        caustics: world
            .get_resource::<forge_engine::tidewater_caustics::CausticMaterialInputs>()
            .cloned(),
        settings: TerrainSettings {
            wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
            sun: Vec3::new(0.3, 0.6, -0.7).normalize().extend(0.0),
            domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
            flags: Vec4::new(0.0, 0.0, if coverage { 1.0 } else { 0.0 }, 0.0),
        },
    };
    prepared
        .stage(world, bindings)
        .expect("registered native terrain renderer");
}

/// Source finite-far reversed-Z frustum, derived independently from the native pose.
pub(super) fn source_view(transform: &Transform, projection: &PerspectiveProjection) -> CdlodView {
    let near = f64::from(projection.near);
    let far = f64::from(projection.far);
    let y = 1.0 / (f64::from(projection.fov) * 0.5).tan();
    let clip = DMat4::from_cols(
        DVec4::new(y / f64::from(projection.aspect_ratio), 0.0, 0.0, 0.0),
        DVec4::new(0.0, y, 0.0, 0.0),
        DVec4::new(0.0, 0.0, near / (far - near), -1.0),
        DVec4::new(0.0, 0.0, near * far / (far - near), 0.0),
    );
    let position = DVec3::from_array(transform.translation.to_array().map(f64::from));
    let world = DMat4::from_rotation_translation(
        DQuat::from_array(transform.rotation.to_array().map(f64::from)).normalize(),
        position,
    );
    let rows = (clip * world.inverse())
        .transpose()
        .to_cols_array_2d()
        .map(DVec4::from_array);
    CdlodView {
        position: position.to_array(),
        planes: [
            rows[3] + rows[0],
            rows[3] - rows[0],
            rows[3] + rows[1],
            rows[3] - rows[1],
            rows[3] - rows[2],
            rows[2],
        ]
        .map(|p| (p / p.truncate().length()).to_array()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn contains(view: CdlodView, point: DVec3) -> bool {
        view.planes
            .into_iter()
            .all(|plane| DVec4::from_array(plane).dot(point.extend(1.0)) >= -1e-8)
    }

    #[test]
    fn wide_lens_keeps_visible_side_geometry() {
        let transform = Transform::from_xyz(0.0, 120.0, 0.0);
        let mut lens = PerspectiveProjection {
            fov: 60_f32.to_radians(),
            near: 0.05,
            far: 60_000.0,
            aspect_ratio: 3440.0 / 1440.0,
            ..default()
        };
        let edge = DVec3::new(125.0, 120.0, -100.0);
        assert!(contains(source_view(&transform, &lens), edge));
        lens.aspect_ratio = 1920.0 / 1080.0;
        assert!(!contains(source_view(&transform, &lens), edge));
    }

    #[test]
    fn portrait_rotated_lens_respects_sides_and_finite_depth() {
        let transform =
            Transform::from_xyz(300.0, 140.0, -90.0).with_rotation(Quat::from_rotation_y(0.8));
        let lens = PerspectiveProjection {
            fov: 60_f32.to_radians(),
            near: 0.1,
            far: 1000.0,
            aspect_ratio: 0.75,
            ..default()
        };
        let view = source_view(&transform, &lens);
        let world = |point: Vec3| DVec3::from(transform.transform_point(point));
        assert!(contains(view, world(Vec3::new(0.0, 0.0, -10.0))));
        assert!(!contains(view, world(Vec3::new(8.0, 0.0, -10.0))));
        assert!(!contains(view, world(Vec3::new(0.0, 0.0, -0.02))));
        assert!(!contains(view, world(Vec3::new(0.0, 0.0, -1100.0))));
    }
}
