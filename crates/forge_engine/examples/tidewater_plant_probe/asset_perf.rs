//! Opt-in serial preparation bands and extracted-asset counts for CPU attribution.
use bevy::{
    pbr::{Material, MeshMaterial3d},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        erased_render_asset::{ExtractedAssets, prepare_erased_assets},
        render_asset::prepare_assets,
        storage::GpuShaderBuffer,
        texture::GpuImage,
    },
};
use std::{collections::BTreeMap, hash::Hash, time::Instant};

const NAMES: [&str; 15] = [
    "images",
    "shader_buffers",
    "standard",
    "plants",
    "terrain",
    "terrain_lod",
    "village",
    "water",
    "ocean_geometry",
    "spray",
    "breaker_lip",
    "sky",
    "fish",
    "nature",
    "scanned",
];
#[derive(Resource, Default)]
pub(super) struct Stats {
    last: Option<Instant>,
    pub bands_ms: BTreeMap<&'static str, f64>,
    pub extracted: BTreeMap<&'static str, [usize; 2]>,
}
fn begin(mut stats: ResMut<Stats>) {
    stats.last = Some(Instant::now());
    stats.bands_ms.clear();
    stats.extracted.clear();
}
fn mark<const N: usize>(mut stats: ResMut<Stats>) {
    let now = Instant::now();
    let last = stats.last.replace(now).expect("preparation begin");
    stats
        .bands_ms
        .insert(NAMES[N], now.duration_since(last).as_secs_f64() * 1000.0);
}
fn count<M: Material, const N: usize>(
    assets: Option<Res<ExtractedAssets<MeshMaterial3d<M>>>>,
    mut stats: ResMut<Stats>,
) where
    M::Data: Eq + Hash + Clone,
{
    if let Some(assets) = assets {
        stats
            .extracted
            .insert(NAMES[N], [assets.extracted.len(), assets.modified.len()]);
    }
}
pub(super) fn install(app: &mut App) {
    if std::env::var("FORGE_TIDEWATER_ASSET_PERF").as_deref() != Ok("1") {
        return;
    }
    let render = app.sub_app_mut(RenderApp);
    render.init_resource::<Stats>().add_systems(
        Render,
        (
            begin
                .in_set(RenderSystems::PrepareAssets)
                .before(prepare_assets::<GpuImage>),
            mark::<0>
                .after(prepare_assets::<GpuImage>)
                .before(prepare_assets::<GpuShaderBuffer>),
            mark::<1>.after(prepare_assets::<GpuShaderBuffer>),
        )
            .in_set(RenderSystems::PrepareAssets),
    );
    macro_rules! material {
        ($kind:ty, $n:literal, $prior:literal) => {
            render.add_systems(
                Render,
                (
                    count::<$kind, $n>
                        .after(mark::<$prior>)
                        .before(prepare_erased_assets::<MeshMaterial3d<$kind>>),
                    mark::<$n>.after(prepare_erased_assets::<MeshMaterial3d<$kind>>),
                )
                    .in_set(RenderSystems::PrepareAssets),
            );
        };
    }
    material!(StandardMaterial, 2, 1);
    material!(crate::PlantMaterial, 3, 2);
    material!(crate::terrain_surface::TerrainMaterial, 4, 3);
    material!(crate::terrain_lod::LodMaterial, 5, 4);
    material!(crate::village::VillageMaterial, 6, 5);
    material!(crate::water_optics::WaterMaterial, 7, 6);
    material!(crate::ocean_geometry::OceanGeometryMaterial, 8, 7);
    material!(crate::spray_sprites::SprayMaterial, 9, 8);
    material!(crate::breaker_lip::LipMaterial, 10, 9);
    material!(crate::sky::SkyMaterial, 11, 10);
    material!(
        forge_engine::tidewater_fish_props::material::FishMaterial,
        12,
        11
    );
    material!(
        forge_engine::tidewater_debris::nature::NatureMaterial,
        13,
        12
    );
    material!(
        forge_engine::tidewater_debris::scanned_material::ScannedMaterial,
        14,
        13
    );
}
