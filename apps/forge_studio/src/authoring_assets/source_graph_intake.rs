//! Select the renderer from the admitted GLB bytes for both preview and placement.
//! This selection neither registers resources nor qualifies rendering.
use forge_assets::source_graph_glb::SourceGraphGlb;
use forge_engine::source_graph_scene_geometry::{
    SourceGraphSceneGeometry, prepare_source_graph_scene_geometry,
};

pub(super) enum SceneIntake<'a> {
    /// The existing glTF loader remains responsible for ordinary scenes.
    OrdinaryGltf,
    /// Keep the admitted material declarations beside the complete hierarchy.
    SourceGraph {
        glb: SourceGraphGlb<'a>,
        geometry: SourceGraphSceneGeometry,
    },
}

/// Only absence of the source extension admits the ordinary loader. A malformed
/// declaration, missing source buffer, or failed scene admission remains an
/// error; none can be hidden by loading the same bytes as an ordinary GLB.
pub(super) fn prepare(
    bytes: &[u8],
    selected_scene: Option<usize>,
) -> Result<SceneIntake<'_>, String> {
    let Some(glb) = super::source_graph_route::read(bytes)? else {
        return Ok(SceneIntake::OrdinaryGltf);
    };
    let geometry = prepare_source_graph_scene_geometry(&glb, selected_scene)?;
    Ok(SceneIntake::SourceGraph { glb, geometry })
}
