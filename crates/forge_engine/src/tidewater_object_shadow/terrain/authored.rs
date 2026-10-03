//! Prepare terrain shadow casters from authored data without filesystem access.
use crate::sun_cascades::CascadeView;
use bevy::{
    math::{DMat4, DVec4},
    prelude::Vec4,
};
use forge_project::TerrainDocument;
use forge_world::{
    cdlod::{CdlodConfig, CdlodRoot, CdlodView, select_nodes},
    sampled_terrain::{RasterLodBounds, SampledTerrainPlan},
};
use std::error::Error;

/// One immutable terrain revision. Camera movement only repeats CDLOD selection.
/// The caller replaces this cache when the document or verified shader changes.
pub struct PreparedSource {
    pub document_hash: forge_core::ContentHash,
    bounds: RasterLodBounds,
    heights: Vec<u8>,
    edit_side: u32,
    edits: Vec<u8>,
    shader: String,
}
impl PreparedSource {
    pub fn new(
        doc: &TerrainDocument,
        heights: Vec<u8>,
        shader: &str,
    ) -> Result<Self, Box<dyn Error>> {
        if shader.is_empty() || shader.len() > 1024 * 1024 {
            return Err("Invalid terrain shadow shader".into());
        }
        let plan = SampledTerrainPlan::from_document(doc, &heights)?;
        let source = doc
            .sampled_source
            .as_ref()
            .ok_or("missing terrain source")?;
        if source.width != 2048
            || source.height != 2048
            || source.sample_spacing_m.to_bits() != 1.0_f64.to_bits()
            || source.origin_xz_m.map(f64::to_bits) != [(-1024.0_f64).to_bits(); 2]
            || source.outside_height_m.to_bits() != (-90.0_f32).to_bits()
        {
            return Err("shadow terrain requires original raster domain".into());
        }
        let bounds = RasterLodBounds::from_plan(&plan)?;
        let (_, functions) = shader
            .split_once("fn terrainHeightAt(")
            .ok_or("missing source terrain functions")?;
        let shader = format!("{HEADER}\nfn terrainHeightAt({functions}");
        let corrections = crate::tidewater_terrain_edits::Corrections::from_document(doc)?;
        let shader = corrections.shader(&shader, "0", 2)?;
        Ok(Self {
            document_hash: plan.document_hash(),
            bounds,
            heights,
            edit_side: corrections.side,
            edits: corrections.bytes,
            shader,
        })
    }
    #[allow(clippy::cast_possible_truncation)] // Fixed source domain and GPU f32 fields.
    pub fn select(&self, view: CascadeView) -> Result<super::Stream, Box<dyn Error>> {
        validate_view(view)?;
        let config = source_lod_config();
        let y = 1.0 / (view.fov_degrees * std::f64::consts::PI / 360.0).tan();
        let near = view.near;
        let far = 60000.0;
        let clip = DMat4::from_cols(
            DVec4::new(y / view.aspect, 0.0, 0.0, 0.0),
            DVec4::new(0.0, y, 0.0, 0.0),
            DVec4::new(0.0, 0.0, near / (far - near), -1.0),
            DVec4::new(0.0, 0.0, near * far / (far - near), 0.0),
        );
        let rows = (clip * view.world_from_view.inverse())
            .transpose()
            .to_cols_array_2d()
            .map(DVec4::from_array);
        let position = view.world_from_view.w_axis.truncate();
        let selection = select_nodes(
            config,
            CdlodRoot::Fixed {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
            },
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
            },
            |origin, size| self.bounds.bounds_for(origin, size),
        )?;
        let nodes = selection
            .iter()
            .flat_map(|n| {
                [
                    n.origin_xz[0] as f32,
                    n.origin_xz[1] as f32,
                    n.size_m as f32,
                    f32::from(n.lod),
                ]
            })
            .flat_map(f32::to_le_bytes)
            .collect();
        let count = u32::try_from(selection.len())?;
        Ok(super::Stream {
            nodes,
            count,
            view_position: position.as_vec3().extend(0.0),
            morph: config
                .morph_rows()?
                .into_iter()
                .map(|v| Vec4::from_array(v.map(|x| x as f32)))
                .collect::<Vec<_>>()
                .try_into()
                .map_err(|_| "nine morph rows required")?,
        })
    }
    pub fn at(&self, view: CascadeView) -> Result<super::Input, Box<dyn Error>> {
        let stream = self.select(view)?;
        Ok(super::Input {
            heights: self.heights.clone(),
            edit_side: self.edit_side,
            edits: self.edits.clone(),
            nodes: stream.nodes,
            count: stream.count,
            shader: self.shader.clone(),
            view_position: stream.view_position,
            morph: stream.morph,
        })
    }
}

/// One-shot adapter; interactive callers retain `PreparedSource` across camera updates.
pub fn prepare(
    doc: &TerrainDocument,
    heights: Vec<u8>,
    shader: &str,
    view: CascadeView,
) -> Result<super::Input, Box<dyn Error>> {
    validate_view(view)?;
    PreparedSource::new(doc, heights, shader)?.at(view)
}
fn validate_view(view: CascadeView) -> Result<(), Box<dyn Error>> {
    if !view.world_from_view.is_finite()
        || !view.world_from_view.determinant().is_finite()
        || view.world_from_view.determinant().abs() < 1e-12
        || !view.near.is_finite()
        || !(0.0..60000.0).contains(&view.near)
        || view.near == 0.0
        || !view.fov_degrees.is_finite()
        || !(1.0..179.0).contains(&view.fov_degrees)
        || !view.aspect.is_finite()
        || view.aspect <= 0.0
    {
        return Err("Invalid terrain shadow view".into());
    }
    Ok(())
}

const HEADER: &str = r"
struct Params {view_projection:mat4x4f,view_position:vec4f,morph:array<vec4f,9>};
@group(0) @binding(0) var<uniform> params:Params;
@group(0) @binding(1) var terrainHeightTex:texture_2d<f32>;
struct TerrainLodParams {morph:array<vec4f,9>};
var<private> terrainLod:TerrainLodParams;
struct HeightDomain {origin:f32,size:f32,res:f32};
var<private> terrainParams:HeightDomain;
@vertex fn vertex(@location(0) grid:vec2f,@location(1) node:vec4f)->@builtin(position) vec4f {
 terrainLod.morph=params.morph;
 terrainParams.origin=-1024.0;terrainParams.size=2048.0;terrainParams.res=2048.0;
 let snapped=terrainLodSnapped(node,grid);
 let cv=terrainLodMorph(node,grid,params.view_position.xyz,terrainHeightAt(snapped));
 let world=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);
 return params.view_projection*vec4f(world,1.0);
}
";

fn source_lod_config() -> CdlodConfig {
    CdlodConfig {
        grid_size: 40,
        leaf_size_m: 8.0,
        levels: 9,
        range_factor: 2.0,
        morph_start_ratio: 0.66,
        max_nodes: 1500,
    }
}
