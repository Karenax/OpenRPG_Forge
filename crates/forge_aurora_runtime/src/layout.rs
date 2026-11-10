//! Typed view over the area v1 documents carried by a module bundle.

use forge_core::Vec3;
use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AreaDocument {
    pub resref: String,
    pub name: String,
    pub dimensions: [u32; 2],
    #[serde(default = "default_grid_size")]
    pub grid_size_meters: f32,
    #[serde(default)]
    pub tileset: Option<String>,
    pub tiles: Vec<AreaTile>,
    #[serde(default)]
    pub instances: Vec<AreaInstance>,
    #[serde(default)]
    pub assets: Vec<AreaAsset>,
}

const fn default_grid_size() -> f32 {
    10.0
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AreaTile {
    pub id: String,
    pub source: TileSource,
    pub transform: AreaTransform,
    pub model_resref: Option<String>,
    #[serde(default)]
    pub model_asset: Option<String>,
    #[serde(default)]
    pub status: String,
}

#[derive(Debug, Clone, Copy, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TileSource {
    pub x: u32,
    pub y: u32,
    pub tile_id: u32,
    pub orientation: u32,
    #[serde(default)]
    pub height: i32,
}

#[derive(Debug, Clone, Copy, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AreaTransform {
    pub position: [f32; 3],
    pub rotation: [f32; 4],
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AreaInstance {
    pub id: String,
    pub source: InstanceSource,
    pub transform: AreaTransform,
    #[serde(default)]
    pub model_assets: Vec<String>,
    #[serde(default)]
    pub classification: String,
    #[serde(default)]
    pub status: String,
    #[serde(default)]
    pub status_reason: String,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InstanceSource {
    pub id: String,
    pub category: String,
    #[serde(default)]
    pub tag: Option<String>,
    #[serde(default)]
    pub template_resref: Option<String>,
    #[serde(default)]
    pub x: f32,
    #[serde(default)]
    pub y: f32,
    #[serde(default)]
    pub z: f32,
    #[serde(default)]
    pub bearing: Option<f32>,
    #[serde(default)]
    pub x_orientation: Option<f32>,
    #[serde(default)]
    pub y_orientation: Option<f32>,
    #[serde(default)]
    pub appearance: Option<u32>,
    #[serde(default)]
    pub transition_destination: Option<String>,
    #[serde(default)]
    pub transition_flags: Option<u32>,
    #[serde(default)]
    pub load_screen_id: Option<u32>,
    #[serde(default)]
    pub source_path: String,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AreaAsset {
    pub id: String,
    pub kind: String,
    #[serde(default)]
    pub resource_keys: Vec<String>,
    pub path: String,
    #[serde(default)]
    pub size_bytes: u64,
    #[serde(default)]
    pub sha256: String,
    #[serde(default)]
    pub status: String,
    #[serde(default)]
    pub texture_paths: Vec<String>,
    #[serde(default)]
    pub navigation_paths: Vec<String>,
    #[serde(default)]
    pub surface_ids: Vec<u32>,
}

impl AreaDocument {
    pub fn parse(value: &serde_json::Value) -> Result<Self, serde_json::Error> {
        serde_json::from_value(value.clone())
    }

    pub fn dimensions_m(&self) -> Vec3 {
        Vec3::new(
            self.dimensions[0] as f32 * self.grid_size_meters,
            0.0,
            self.dimensions[1] as f32 * self.grid_size_meters,
        )
    }

    pub fn offset(&self) -> Vec3 {
        Vec3::new(
            -(self.dimensions[0] as f32) * self.grid_size_meters * 0.5,
            0.0,
            self.dimensions[1] as f32 * self.grid_size_meters * 0.5,
        )
    }

    pub fn navigation_path_for_model(&self, model_resref: &str) -> Option<&str> {
        let key = format!("{}.mdl", model_resref.to_ascii_lowercase());
        self.assets
            .iter()
            .find(|asset| {
                asset.kind == "model"
                    && asset
                        .resource_keys
                        .iter()
                        .any(|candidate| candidate.eq_ignore_ascii_case(&key))
            })
            .and_then(|asset| {
                asset
                    .navigation_paths
                    .iter()
                    .find(|path| path.to_ascii_lowercase().ends_with(".wok"))
            })
            .map(String::as_str)
    }

    pub fn asset_by_path(&self, path: &str) -> Option<&AreaAsset> {
        self.assets.iter().find(|asset| asset.path == path)
    }

    pub fn canonical_instance_position(&self, instance: &AreaInstance) -> Option<Vec3> {
        let position = instance.transform.position;
        if position.iter().all(|value| value.is_finite()) {
            Some(Vec3::new(position[0], position[1], position[2]))
        } else {
            let source = [instance.source.x, instance.source.y, instance.source.z];
            source
                .iter()
                .all(|value| value.is_finite())
                .then_some(Vec3::new(source[0], source[2], -source[1]))
        }
    }

    pub fn canonical_tile_position(&self, tile: &AreaTile) -> Vec3 {
        let position = tile.transform.position;
        Vec3::new(position[0], position[1], position[2])
    }

    pub fn shifted(&self, point: Vec3) -> Vec3 {
        let offset = self.offset();
        Vec3::new(point.x + offset.x, point.y + offset.y, point.z + offset.z)
    }
}
