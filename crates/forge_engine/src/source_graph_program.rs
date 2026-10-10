//! Atomic source-program registration and material construction.
//! Source textures, matrices and time must be qualified by the caller; tests
//! below exercise structural assembly, not GPU or original-image qualification.
use super::{
    SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS, SourceGraphExtension, SourceGraphForwardState,
    SourceGraphKey, SourceGraphMaterial, SourceGraphShaderArtifact, SourceGraphShaderRegistry,
    SourceGraphSurfacePhase, SourceGraphUniforms, SourceGraphUvRequirements,
    register_source_graph_shaders, source_graph_material,
};
use bevy::{prelude::*, shader::ShaderImport};

#[allow(
    clippy::struct_excessive_bools,
    reason = "Transparency, alpha test, coverage and original vertex-color requirements are independent source flags."
)]
pub struct SourceGraphMaterialProgram {
    pub prepass_cull: u8,
    pub shaders: Vec<SourceGraphShaderArtifact>,
    pub forward_index: usize,
    pub prepass_index: usize,
    pub required_texture_count: usize,
    pub color_write_mask: u32,
    pub forward_state: SourceGraphForwardState,
    pub original_transparent: bool,
    pub original_alpha_test: bool,
    pub original_alpha_to_mask: bool,
    pub requires_original_vertex_color: bool,
}
/// Generated source-stage declarations have one canonical layout. Reject any
/// other texture binding syntax rather than inferring or supplying defaults.
fn source_graph_declared_texture_count(source: &str) -> Result<usize, &'static str> {
    forge_assets::source_graph_bundle::stage_texture_count(source)
        .map_err(|_|"Source stage texture/sampler declarations differ from the canonical authored and scene layout")
}
/// Construct every binding and validate shader ownership before registration.
/// Any error leaves Assets<Shader> and registry unchanged. Forward and prepass
/// sources each contain their vertex and fragment entry points.
#[expect(
    clippy::needless_pass_by_value,
    reason = "Public admission consumes the source program bundle so it cannot be reused after shader registration"
)]
pub fn register_source_graph_material(
    assets: &mut Assets<Shader>,
    registry: &mut SourceGraphShaderRegistry,
    program: SourceGraphMaterialProgram,
    source: SourceGraphUniforms,
    images: Vec<Handle<Image>>,
) -> Result<SourceGraphMaterial, &'static str> {
    let forward = program
        .shaders
        .get(program.forward_index)
        .ok_or("Source forward shader index missing")?;
    let prepass = program
        .shaders
        .get(program.prepass_index)
        .ok_or("Source prepass shader index missing")?;
    for artifact in [forward, prepass] {
        let shader = Shader::from_wgsl(artifact.source.clone(), "source-stage".to_owned());
        if matches!(shader.import_path, ShaderImport::Custom(_)) {
            return Err("Source stage points to a library module");
        }
    }
    let forward_uv = SourceGraphUvRequirements::from_shader(
        &forward.source,
        SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS,
    )?;
    let prepass_uv = SourceGraphUvRequirements::from_shader(
        &prepass.source,
        SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS,
    )?;
    if forward_uv != prepass_uv {
        return Err("Source forward and prepass stages require different original UV channels");
    }
    let forward_count = source_graph_declared_texture_count(&forward.source)?;
    let prepass_count = source_graph_declared_texture_count(&prepass.source)?;
    if forward_count != prepass_count || forward_count != program.required_texture_count {
        return Err("Source stage declarations differ from required texture binding count");
    }
    let uuid = |artifact: &SourceGraphShaderArtifact| {
        u128::from_be_bytes(
            artifact.expected_sha256.as_bytes()[..16]
                .try_into()
                .unwrap(),
        )
    };
    let declares = |declaration: &str| {
        [forward, prepass]
            .into_iter()
            .any(|a| a.source.lines().any(|l| l.trim() == declaration))
    };
    let key = SourceGraphKey {
        requires_native_srgb_encoder: declares(
            forge_assets::source_graph_bundle::SOURCE_GRAPH_NATIVE_SRGB_THRESHOLDS_DECLARATION,
        ),
        native_srgb_encoder_ready: false,
        requires_deferred_srgb: declares(
            forge_assets::source_graph_bundle::SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION,
        ),
        deferred_srgb_ready: false,
        requires_preintegrated_brdf: declares(
            forge_assets::source_graph_bundle::SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION,
        ),
        preintegrated_brdf_ready: false,
        requires_sky_cube: declares(
            forge_assets::source_graph_bundle::SOURCE_GRAPH_SKY_CUBE_DECLARATION,
        ),
        sky_cube_ready: false,
        prepass_cull: program.prepass_cull,
        shadow_only: false,
        orthographic_shadow_raster: None,
        vertex: uuid(forward),
        fragment: uuid(forward),
        prepass_vertex: uuid(prepass),
        prepass_fragment: uuid(prepass),
        original_alpha_to_mask: program.original_alpha_to_mask,
        requires_original_vertex_color: program.requires_original_vertex_color,
        original_uv_requirements: forward_uv,
        color_write_mask: program.color_write_mask,
        forward_state: program.forward_state,
    };
    let extension = SourceGraphExtension::from_source_bindings(
        key,
        source,
        program.required_texture_count,
        images,
    )?;
    let phase = SourceGraphSurfacePhase::from_original_flags(
        program.original_transparent,
        program.original_alpha_test,
    );
    register_source_graph_shaders(assets, registry, &program.shaders)?;
    Ok(source_graph_material(extension, phase))
}

#[cfg(all(test, feature = "private-source-fixtures"))]
mod tests {
    use super::*;
    use bevy::{asset::AssetId, pbr::Material, shader::ValidateShader};
    use forge_core::ContentHash;
    const ARTIFACTS: &[(&str, &str)] = &[
        (
            "d98220343731686c13b0fd2a97ccd7c30b4c5d6ad0a48eb85c2ddc158e7e2858",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/d98220343731686c13b0fd2a97ccd7c30b4c5d6ad0a48eb85c2ddc158e7e2858.wgsl"
            )),
        ),
        (
            "7d86291f04a12a141b36c4a5afa812c51d9862d618dcc35b8bc64d1ab334728e",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/7d86291f04a12a141b36c4a5afa812c51d9862d618dcc35b8bc64d1ab334728e.wgsl"
            )),
        ),
        (
            "6ecec4701770333fd482a32915e93a571606c0f5ebec97a4524b834d471d49e1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/6ecec4701770333fd482a32915e93a571606c0f5ebec97a4524b834d471d49e1.wgsl"
            )),
        ),
        (
            "a8be7fd51ab56c421df75c0409947d300b735c6f987261fe38e3a642eebee10d",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/a8be7fd51ab56c421df75c0409947d300b735c6f987261fe38e3a642eebee10d.wgsl"
            )),
        ),
        (
            "1714a5fc48629da68f4095949e6b379c0c5b93258936a768ca3a6be2efa855a5",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/1714a5fc48629da68f4095949e6b379c0c5b93258936a768ca3a6be2efa855a5.wgsl"
            )),
        ),
        (
            "a62438b284cc44dd5938799ff0600522c67d56159ec0c7ec8728986ec1c3c6ba",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/a62438b284cc44dd5938799ff0600522c67d56159ec0c7ec8728986ec1c3c6ba.wgsl"
            )),
        ),
        (
            "814497a45b729fc7d3e2c5eeb74da4890074a69e60cf98b8ecf9f85ddd5867e6",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/814497a45b729fc7d3e2c5eeb74da4890074a69e60cf98b8ecf9f85ddd5867e6.wgsl"
            )),
        ),
        (
            "4040115b8a627ea748da11e9e72e53701081b1f325b2faa0253f49f65619ec77",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/4040115b8a627ea748da11e9e72e53701081b1f325b2faa0253f49f65619ec77.wgsl"
            )),
        ),
        (
            "bc89f690f4db515b7853d61830525a8764cc359672492a2f82b0157b46323554",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/bc89f690f4db515b7853d61830525a8764cc359672492a2f82b0157b46323554.wgsl"
            )),
        ),
        (
            "aa54e9d2598937960188283037d2375d879f8d6f8391bb454c16439f6766c6a5",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/aa54e9d2598937960188283037d2375d879f8d6f8391bb454c16439f6766c6a5.wgsl"
            )),
        ),
        (
            "345ab976b54889257df14bbc8d426243794fd5bd496a9141e685fbe2ca28f721",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/345ab976b54889257df14bbc8d426243794fd5bd496a9141e685fbe2ca28f721.wgsl"
            )),
        ),
        (
            "1ed63e98607dcf98eef9438e27f292404ed38f0f4a743ba9ff9a27f8631497c1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/1ed63e98607dcf98eef9438e27f292404ed38f0f4a743ba9ff9a27f8631497c1.wgsl"
            )),
        ),
        (
            "2c0f41dac39a2053909b2c0bce5b2b3a8ea90949de7b82a6525304d00e34d5bf",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/2c0f41dac39a2053909b2c0bce5b2b3a8ea90949de7b82a6525304d00e34d5bf.wgsl"
            )),
        ),
        (
            "dab5c8f5a13295b921edfdcf3287bf780c7f00f35c815796199f5b67513c1dca",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/dab5c8f5a13295b921edfdcf3287bf780c7f00f35c815796199f5b67513c1dca.wgsl"
            )),
        ),
        (
            "283686f05f89efdc442e1805517668f14fdf453e7e1bf31a8a345a8af867b29f",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/283686f05f89efdc442e1805517668f14fdf453e7e1bf31a8a345a8af867b29f.wgsl"
            )),
        ),
        (
            "17228e9c8b46dfa8c6576cd976c726298c39b4edcb774b507ca888285f78453a",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/17228e9c8b46dfa8c6576cd976c726298c39b4edcb774b507ca888285f78453a.wgsl"
            )),
        ),
        (
            "b164a768459fcf36305f4eb00a64e53023ad9884b96edcd936b57f334d4a51fe",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/b164a768459fcf36305f4eb00a64e53023ad9884b96edcd936b57f334d4a51fe.wgsl"
            )),
        ),
        (
            "72b8fdf1eaffd48528038491f332876009447aeb708dc0317ccfea8a8a76100e",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/72b8fdf1eaffd48528038491f332876009447aeb708dc0317ccfea8a8a76100e.wgsl"
            )),
        ),
        (
            "1c0a6e363b931e3a2cc0c420cb615a8c88d002c64bf7a5a698a2e2ea9dcbbe34",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/1c0a6e363b931e3a2cc0c420cb615a8c88d002c64bf7a5a698a2e2ea9dcbbe34.wgsl"
            )),
        ),
        (
            "d0d56e73b179ca0fb52400be1a64503ec9357c1c6dd5007930dec112fe8def24",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/d0d56e73b179ca0fb52400be1a64503ec9357c1c6dd5007930dec112fe8def24.wgsl"
            )),
        ),
        (
            "e1055cbf97bc3ff7501cf980b5b4dc90c637a7f325567ba79b6cfa91046755f1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/e1055cbf97bc3ff7501cf980b5b4dc90c637a7f325567ba79b6cfa91046755f1.wgsl"
            )),
        ),
        (
            "a211349ac650b2837066ef2d583d82a74a1c85bf5231211a4890ead6fc34dbc2",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/a211349ac650b2837066ef2d583d82a74a1c85bf5231211a4890ead6fc34dbc2.wgsl"
            )),
        ),
        (
            "c15925255e7d9770804b9e1b15143eb7a1fd414cc300d1bd8a229ffd6365277c",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/c15925255e7d9770804b9e1b15143eb7a1fd414cc300d1bd8a229ffd6365277c.wgsl"
            )),
        ),
        (
            "4eac4bc6042b4ebc985354e9047cb589043bf387f24287ac91b2112f5927554f",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/4eac4bc6042b4ebc985354e9047cb589043bf387f24287ac91b2112f5927554f.wgsl"
            )),
        ),
        (
            "dd2f6561bdf6f06befe22e31ab395dd4efa2425ff3e21d2df20ce92d3ccacefd",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/dd2f6561bdf6f06befe22e31ab395dd4efa2425ff3e21d2df20ce92d3ccacefd.wgsl"
            )),
        ),
        (
            "c22c1c23083b8b287b3c0ebd01bfa5b6696f1155b2d5a4b1d6dfbc2f320f5abd",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/c22c1c23083b8b287b3c0ebd01bfa5b6696f1155b2d5a4b1d6dfbc2f320f5abd.wgsl"
            )),
        ),
        (
            "061a7095639e56a7611e932b7eecb268ceaeb6afd61bf6ea86515135a3b6564d",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/061a7095639e56a7611e932b7eecb268ceaeb6afd61bf6ea86515135a3b6564d.wgsl"
            )),
        ),
        (
            "bded2ff6a0bf932245d4b6261fefda62aca6671cc37d6496ba1f4b0c5b40e139",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/bded2ff6a0bf932245d4b6261fefda62aca6671cc37d6496ba1f4b0c5b40e139.wgsl"
            )),
        ),
        (
            "03e84b2ccdc9d8a3438a14be5c975a714639a04df2bb264b2c2ad5da8dfd2684",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/03e84b2ccdc9d8a3438a14be5c975a714639a04df2bb264b2c2ad5da8dfd2684.wgsl"
            )),
        ),
        (
            "46e3835521fe3d81b8ac4946011838d7ff83b9b73ab58d7bc11646bd4eac3b59",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/46e3835521fe3d81b8ac4946011838d7ff83b9b73ab58d7bc11646bd4eac3b59.wgsl"
            )),
        ),
        (
            "0d8b813e14333fddc867a4690d659b1e8f484a0438b5e8235fb64c8f439bcdb1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/0d8b813e14333fddc867a4690d659b1e8f484a0438b5e8235fb64c8f439bcdb1.wgsl"
            )),
        ),
        (
            "8d3ac918a8bcf9efb0bdc156490729c00d0f776e60b926ef91da6b21a6d376b1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/8d3ac918a8bcf9efb0bdc156490729c00d0f776e60b926ef91da6b21a6d376b1.wgsl"
            )),
        ),
        (
            "c4f284c8c50f8c7d7e4470a0113fb53d47aeab8509a576a380ab0a1af714658c",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/c4f284c8c50f8c7d7e4470a0113fb53d47aeab8509a576a380ab0a1af714658c.wgsl"
            )),
        ),
        (
            "26ab778d9ce5b67e58eccac3623f80f772fbdd5e1cc674d623b60d9bd1707839",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/26ab778d9ce5b67e58eccac3623f80f772fbdd5e1cc674d623b60d9bd1707839.wgsl"
            )),
        ),
        (
            "ac0bba5fd915d228fa123aaafe7999cc6c24c9b4b30f1216277c13b3776e0dc9",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/ac0bba5fd915d228fa123aaafe7999cc6c24c9b4b30f1216277c13b3776e0dc9.wgsl"
            )),
        ),
        (
            "09c3a7b888ad5d37b9b2b2c4b896ecb0ca0aad6fa36152336444f1be53a6a841",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/09c3a7b888ad5d37b9b2b2c4b896ecb0ca0aad6fa36152336444f1be53a6a841.wgsl"
            )),
        ),
        (
            "33322c4101c92080c4ab05f6066701f7f5c2fe94532ec88aa4c81da5ba1fa979",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/33322c4101c92080c4ab05f6066701f7f5c2fe94532ec88aa4c81da5ba1fa979.wgsl"
            )),
        ),
        (
            "624615658ad538f192ad49aea5f75f60df80c8a8b60254cc4f4f9f9eaf2d1085",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/624615658ad538f192ad49aea5f75f60df80c8a8b60254cc4f4f9f9eaf2d1085.wgsl"
            )),
        ),
        (
            "c0fe6b04b321842d8f6991bdde5663c5cea90b0115378a42f28bf9d1583c9381",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/c0fe6b04b321842d8f6991bdde5663c5cea90b0115378a42f28bf9d1583c9381.wgsl"
            )),
        ),
        (
            "967d02da409f39f5f71a74259d54e9b2cc205e9cc314bf49fb50a6997b89f210",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/967d02da409f39f5f71a74259d54e9b2cc205e9cc314bf49fb50a6997b89f210.wgsl"
            )),
        ),
        (
            "78db3f2d1b8caf2649970d064fae8273ebc0452dd33d07544106bff5fdfffeb3",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/78db3f2d1b8caf2649970d064fae8273ebc0452dd33d07544106bff5fdfffeb3.wgsl"
            )),
        ),
        (
            "1552b6f082da1be9815c78f8f6ffab42ca436c18e9989fe0f807fb47d2b32b95",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/1552b6f082da1be9815c78f8f6ffab42ca436c18e9989fe0f807fb47d2b32b95.wgsl"
            )),
        ),
        (
            "7be32589b3be9d579303f2b507ccbf526ccaa43692a9adb1f66d8163820c2da0",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/7be32589b3be9d579303f2b507ccbf526ccaa43692a9adb1f66d8163820c2da0.wgsl"
            )),
        ),
        (
            "60d1e936f503122d92a52e003e857a2146e4b3d2d8370d952daa2d0ca730baa5",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/60d1e936f503122d92a52e003e857a2146e4b3d2d8370d952daa2d0ca730baa5.wgsl"
            )),
        ),
        (
            "e745ff498d8ac931d4d8a90f10963d2555f21283863b66da52f691109f1d5fd7",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/e745ff498d8ac931d4d8a90f10963d2555f21283863b66da52f691109f1d5fd7.wgsl"
            )),
        ),
        (
            "69eb90b9d33ae0e82fb70d6d638572d88d3c41ad047d0b0beef567fe80e2d1da",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/69eb90b9d33ae0e82fb70d6d638572d88d3c41ad047d0b0beef567fe80e2d1da.wgsl"
            )),
        ),
        (
            "aa0765edf3e458a1a05198a98f09e60c64a1808594e7e227f702b3fd932efeb1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/aa0765edf3e458a1a05198a98f09e60c64a1808594e7e227f702b3fd932efeb1.wgsl"
            )),
        ),
        (
            "f2a2db4b96b012e3a3813484c9b89f92a0312bd16c510d2b759c607d2b88f369",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/f2a2db4b96b012e3a3813484c9b89f92a0312bd16c510d2b759c607d2b88f369.wgsl"
            )),
        ),
        (
            "37bc6415e438ff434bbd39e1641ba3acd4332e719b850d81f496e0e34e6fca20",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/37bc6415e438ff434bbd39e1641ba3acd4332e719b850d81f496e0e34e6fca20.wgsl"
            )),
        ),
        (
            "2516b7326875db0d1b7787d96d159f50f7fd4a167c561d1c1f2bf4c7e3becc8d",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/2516b7326875db0d1b7787d96d159f50f7fd4a167c561d1c1f2bf4c7e3becc8d.wgsl"
            )),
        ),
        (
            "be298d90faf6592129d33e5472c373d25e0d05c1be1db3a79558e3174e202dc8",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/be298d90faf6592129d33e5472c373d25e0d05c1be1db3a79558e3174e202dc8.wgsl"
            )),
        ),
        (
            "2e7c9cfcaa208b38e9c8c33729e6ff775091231a859f384fb2f4f3c7ff860f54",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/2e7c9cfcaa208b38e9c8c33729e6ff775091231a859f384fb2f4f3c7ff860f54.wgsl"
            )),
        ),
        (
            "6c7bdd276281396478e524ae38d4ab7365686d23e12803de118f253f1e749058",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/6c7bdd276281396478e524ae38d4ab7365686d23e12803de118f253f1e749058.wgsl"
            )),
        ),
        (
            "db68d32114a2782d4db0f3f3809f5d27498a2b07984d7e6eb9aaafee07fd9bc3",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/db68d32114a2782d4db0f3f3809f5d27498a2b07984d7e6eb9aaafee07fd9bc3.wgsl"
            )),
        ),
        (
            "6a819e5a3a337f4946369bed00fb13ca919bb1c8022b18996403f82dc7ae8740",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/6a819e5a3a337f4946369bed00fb13ca919bb1c8022b18996403f82dc7ae8740.wgsl"
            )),
        ),
        (
            "d556a34648ff4870433055baab9677914088caa0eab287cacd34c5f1d5ef3195",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/d556a34648ff4870433055baab9677914088caa0eab287cacd34c5f1d5ef3195.wgsl"
            )),
        ),
        (
            "7727a42a75c3d42b3a3d155bd40a5d7337587414b1e9204b29e9ba26eea3e4b7",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/7727a42a75c3d42b3a3d155bd40a5d7337587414b1e9204b29e9ba26eea3e4b7.wgsl"
            )),
        ),
        (
            "3524cb959e2a1e0feabcad7fc9ba42a467dcd3cc044125e5cdfafdb925b8f1a1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/3524cb959e2a1e0feabcad7fc9ba42a467dcd3cc044125e5cdfafdb925b8f1a1.wgsl"
            )),
        ),
        (
            "128d3ed2baaffaddc383ed627fe7b29efc455c317712c421a62eb0f2c1b091fa",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/128d3ed2baaffaddc383ed627fe7b29efc455c317712c421a62eb0f2c1b091fa.wgsl"
            )),
        ),
        (
            "c6dbdeef889e76af24860252297b2bb9b07462e0d35d32f67d8dea2231b09d5f",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/c6dbdeef889e76af24860252297b2bb9b07462e0d35d32f67d8dea2231b09d5f.wgsl"
            )),
        ),
        (
            "963af2e0c75477a25a0ce887a3c333645c3b86f4abd69e081aabd185479e7925",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/963af2e0c75477a25a0ce887a3c333645c3b86f4abd69e081aabd185479e7925.wgsl"
            )),
        ),
        (
            "0a3a53ae4b57dcbf4d144db2d63d7c3c17460c0dd84199dcc2978e1fbde8b2ed",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/0a3a53ae4b57dcbf4d144db2d63d7c3c17460c0dd84199dcc2978e1fbde8b2ed.wgsl"
            )),
        ),
        (
            "7b23f2e7987c4633f6219b84cf1d1f0913448ad43c9baec04eb495478eac5351",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/7b23f2e7987c4633f6219b84cf1d1f0913448ad43c9baec04eb495478eac5351.wgsl"
            )),
        ),
        (
            "8867b0b4f04a4b4a54ffba5a01e4b1e1b1a6ed563b0473af88f6e1df5543ac3d",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/8867b0b4f04a4b4a54ffba5a01e4b1e1b1a6ed563b0473af88f6e1df5543ac3d.wgsl"
            )),
        ),
        (
            "5da06de9e8077d107cfc332c709badfb16d3e08ad149ec3805f1c26f6aa15eb4",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/5da06de9e8077d107cfc332c709badfb16d3e08ad149ec3805f1c26f6aa15eb4.wgsl"
            )),
        ),
        (
            "e950544fdcad0c2f957e49e7af7df6078891c41dd2952bab496a706b74c9e3b4",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/e950544fdcad0c2f957e49e7af7df6078891c41dd2952bab496a706b74c9e3b4.wgsl"
            )),
        ),
        (
            "74bc610b7c64b387f270dce2ae9e80f94020d5f59fbdc38fd04b114dacb49660",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/74bc610b7c64b387f270dce2ae9e80f94020d5f59fbdc38fd04b114dacb49660.wgsl"
            )),
        ),
        (
            "b6996f421fba4011bfdf9fecb638eabd9206d4ede22533ddf6b96853387b41e0",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/b6996f421fba4011bfdf9fecb638eabd9206d4ede22533ddf6b96853387b41e0.wgsl"
            )),
        ),
        (
            "f45e2bc47fcec79de64fe93b82082200170c7867a98c0c3e1fa732818dd437b6",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/f45e2bc47fcec79de64fe93b82082200170c7867a98c0c3e1fa732818dd437b6.wgsl"
            )),
        ),
        (
            "aa5b5c368ae2ab7f312fadc64c70ee6214cffc1c2982fbf963b2d27a0c8ae8d8",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/aa5b5c368ae2ab7f312fadc64c70ee6214cffc1c2982fbf963b2d27a0c8ae8d8.wgsl"
            )),
        ),
        (
            "f619a5513da05b14518276aa080568355c90b8ee93653708d739253165f77394",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/f619a5513da05b14518276aa080568355c90b8ee93653708d739253165f77394.wgsl"
            )),
        ),
        (
            "4edf95477dbbd47da33cd8dc92c0876817844057a145bc8b7326433a5995658c",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/4edf95477dbbd47da33cd8dc92c0876817844057a145bc8b7326433a5995658c.wgsl"
            )),
        ),
        (
            "b09b37c2f0316e36b6b795436e93cc6646127c42efe31609fe4e3453dbb467e2",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/b09b37c2f0316e36b6b795436e93cc6646127c42efe31609fe4e3453dbb467e2.wgsl"
            )),
        ),
        (
            "ea50afaa879ef86e73e9ed0249df48acdc2fa8b29604811e3b848ab37f069472",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/ea50afaa879ef86e73e9ed0249df48acdc2fa8b29604811e3b848ab37f069472.wgsl"
            )),
        ),
        (
            "9acf401476d4092861a4d200bbcaac5ef899e420a80bbd76492aae56c0f3b251",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/9acf401476d4092861a4d200bbcaac5ef899e420a80bbd76492aae56c0f3b251.wgsl"
            )),
        ),
        (
            "6ec04ed64a5aac6586a818799f6ce08e9abbb28d51b7abe46562911684102123",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/6ec04ed64a5aac6586a818799f6ce08e9abbb28d51b7abe46562911684102123.wgsl"
            )),
        ),
        (
            "aeaa95a742ebbd4c7bf84b1d24f4b37ac523e686f7e23c32285809fa527e787d",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/aeaa95a742ebbd4c7bf84b1d24f4b37ac523e686f7e23c32285809fa527e787d.wgsl"
            )),
        ),
        (
            "fbc4d43ec310ddff5b802e634971f5a5a48dd2a02f076620ef358e3faef737d1",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/fbc4d43ec310ddff5b802e634971f5a5a48dd2a02f076620ef358e3faef737d1.wgsl"
            )),
        ),
        (
            "05c5d3f02d63ea90ecf7ede5140d2b1357a845655cd9b5ac2b3b2d24855956ad",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/05c5d3f02d63ea90ecf7ede5140d2b1357a845655cd9b5ac2b3b2d24855956ad.wgsl"
            )),
        ),
        (
            "130104b7f9d3d7f717a5c849b742337ad692b25666de4b6d0f99d5488dd5f75b",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/130104b7f9d3d7f717a5c849b742337ad692b25666de4b6d0f99d5488dd5f75b.wgsl"
            )),
        ),
        (
            "3c2e9b7e3cdd5ca9c8ceb5d34785d86ed29aa78d5f523f29bc3bee9a47dd4eb3",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/3c2e9b7e3cdd5ca9c8ceb5d34785d86ed29aa78d5f523f29bc3bee9a47dd4eb3.wgsl"
            )),
        ),
        (
            "b479d7b97ac0977a4515c43d13890977d2a93b4cc066a6b77601d6fcc0d6fda2",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/b479d7b97ac0977a4515c43d13890977d2a93b4cc066a6b77601d6fcc0d6fda2.wgsl"
            )),
        ),
        (
            "6b398f505dd7073dc2a189511197bed8c6c1a28b1a96bbdce3619764ca34f3dd",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/6b398f505dd7073dc2a189511197bed8c6c1a28b1a96bbdce3619764ca34f3dd.wgsl"
            )),
        ),
        (
            "8f673cfd3a93e830b53b8e5123eff5c32b41b51e3ae01a7a8816e9bf21b1c49a",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/8f673cfd3a93e830b53b8e5123eff5c32b41b51e3ae01a7a8816e9bf21b1c49a.wgsl"
            )),
        ),
        (
            "fabc6e612c4209c7cda268e0eb12d7419ac84c2d784d9d53553d6c5e69980a0e",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/fabc6e612c4209c7cda268e0eb12d7419ac84c2d784d9d53553d6c5e69980a0e.wgsl"
            )),
        ),
        (
            "dd9d88cc0f9e7aeda26fd5ed4b4e225827e08944a2377998232c71e8462502f6",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/dd9d88cc0f9e7aeda26fd5ed4b4e225827e08944a2377998232c71e8462502f6.wgsl"
            )),
        ),
        (
            "2239debfb856e2a14fc417564fd6fa8b48fef5a950751960fc8133d94360e38c",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/2239debfb856e2a14fc417564fd6fa8b48fef5a950751960fc8133d94360e38c.wgsl"
            )),
        ),
        (
            "0df36c1ad5fe836f272e9354e96284656ca11476bf0338024bf86ac9f565ce7f",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/0df36c1ad5fe836f272e9354e96284656ca11476bf0338024bf86ac9f565ce7f.wgsl"
            )),
        ),
        (
            "74d6d4fce78050d465e939c537097b5b4558f695ad0cdbcf88a5a302455dbe67",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/74d6d4fce78050d465e939c537097b5b4558f695ad0cdbcf88a5a302455dbe67.wgsl"
            )),
        ),
        (
            "1c672478d843d7017ca34ca0b5aac20259e59aec9c12ce60c799f59f586dd9ba",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/1c672478d843d7017ca34ca0b5aac20259e59aec9c12ce60c799f59f586dd9ba.wgsl"
            )),
        ),
        (
            "5b7fdbf454d07fe3dec7c2bbd89fb27dcc02241bfeea6f7adf1ea6bf2ceab677",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/5b7fdbf454d07fe3dec7c2bbd89fb27dcc02241bfeea6f7adf1ea6bf2ceab677.wgsl"
            )),
        ),
        (
            "fc0c8bb5d7dd4acb76b7dc946b0be65ebe7030ab4cd18b8677876853990c5da2",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/fc0c8bb5d7dd4acb76b7dc946b0be65ebe7030ab4cd18b8677876853990c5da2.wgsl"
            )),
        ),
        (
            "fe66c9e9ed3609975874847552dbcbddd9fd828dcd2472677ae77525f7bcecfb",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/fe66c9e9ed3609975874847552dbcbddd9fd828dcd2472677ae77525f7bcecfb.wgsl"
            )),
        ),
        (
            "f717f9cd8c8157c89a2d7fb4604a65de7f0d82a342bda2ef170aa99ec3103561",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/f717f9cd8c8157c89a2d7fb4604a65de7f0d82a342bda2ef170aa99ec3103561.wgsl"
            )),
        ),
        (
            "9bfa781d5af93c3c0d46cc8364c2f3800517536d3e41cee131ffc0d56456c8a9",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/9bfa781d5af93c3c0d46cc8364c2f3800517536d3e41cee131ffc0d56456c8a9.wgsl"
            )),
        ),
        (
            "53f48c24ddbdfb23148e33c5ca98cceb9eabcb7dbe8e7fca77803c6cffc382e4",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/53f48c24ddbdfb23148e33c5ca98cceb9eabcb7dbe8e7fca77803c6cffc382e4.wgsl"
            )),
        ),
        (
            "55c7c28e68e41bf672a2acd65c6d9734301f787780ca23d0c5a8a700203a8d7a",
            include_str!(concat!(
                env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
                "/crates/forge_engine/source_graph_fixtures/55c7c28e68e41bf672a2acd65c6d9734301f787780ca23d0c5a8a700203a8d7a.wgsl"
            )),
        ),
    ];
    fn artifacts() -> Vec<SourceGraphShaderArtifact> {
        ARTIFACTS
            .iter()
            .map(|(hash, source)| SourceGraphShaderArtifact {
                source: source.to_string(),
                expected_sha256: ContentHash::parse(hash).unwrap(),
            })
            .collect()
    }
    fn uniforms() -> SourceGraphUniforms {
        SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::ZERO; 2],
        }
    }
    fn images(count: usize) -> Vec<Handle<Image>> {
        (0..count)
            .map(|i| {
                Handle::Uuid(
                    bevy::asset::uuid::Uuid::from_u128(i as u128 + 1),
                    std::marker::PhantomData,
                )
            })
            .collect()
    }
    #[test]
    fn source_graph_loader_registers_all97_actual_shader_sources_idempotently() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let artifacts = artifacts();
        let ids = register_source_graph_shaders(&mut assets, &mut registry, &artifacts).unwrap();
        assert_eq!(ids.len(), 97);
        assert_eq!(assets.len(), 97);
        assert_eq!(
            ids,
            register_source_graph_shaders(&mut assets, &mut registry, &artifacts).unwrap()
        );
        assert_eq!(assets.len(), 97);
        for id in ids {
            let shader = assets
                .get(AssetId::Uuid {
                    uuid: bevy::asset::uuid::Uuid::from_u128(id),
                })
                .unwrap();
            assert!(matches!(shader.validate_shader, ValidateShader::Enabled));
        }
    }
    #[test]
    fn source_graph_loader_all48_actual_programs_construct_original_keys_and_phases() {
        let all = artifacts();
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let cases = [
            (
                1,
                2,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                3,
                4,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                5,
                6,
                None,
                1,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: false,
                },
            ),
            (
                7,
                8,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                9,
                10,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                11,
                12,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                13,
                14,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                15,
                16,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                17,
                18,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                19,
                20,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                21,
                22,
                Some(0),
                7,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                23,
                24,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                25,
                26,
                Some(0),
                6,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                27,
                28,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                29,
                30,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                31,
                32,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                33,
                34,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                35,
                36,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                37,
                38,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                39,
                40,
                Some(0),
                6,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                41,
                42,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                43,
                44,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                45,
                46,
                Some(0),
                2,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 10,
                    alpha_src: 1,
                    alpha_dst: 10,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                47,
                48,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 3,
                    write_depth: true,
                },
            ),
            (
                49,
                50,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                51,
                52,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                53,
                54,
                Some(0),
                1,
                true,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 5,
                    color_dst: 10,
                    alpha_src: 5,
                    alpha_dst: 10,
                    cull: 2,
                    compare: 4,
                    write_depth: false,
                },
            ),
            (
                55,
                56,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                57,
                58,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                59,
                60,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                61,
                62,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                63,
                64,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                65,
                66,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                67,
                68,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                69,
                70,
                Some(0),
                7,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                71,
                72,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                73,
                74,
                Some(0),
                6,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                75,
                76,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                77,
                78,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                79,
                80,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                81,
                82,
                Some(0),
                4,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                83,
                84,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                85,
                86,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                87,
                88,
                Some(0),
                6,
                false,
                false,
                true,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                89,
                90,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                91,
                92,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                93,
                94,
                Some(0),
                2,
                false,
                true,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 0,
                    compare: 4,
                    write_depth: true,
                },
            ),
            (
                95,
                96,
                Some(0),
                3,
                false,
                false,
                false,
                false,
                SourceGraphForwardState {
                    color_src: 1,
                    color_dst: 0,
                    alpha_src: 1,
                    alpha_dst: 0,
                    cull: 2,
                    compare: 4,
                    write_depth: true,
                },
            ),
        ];
        assert_eq!(cases.len(), 48);
        let cull_plan: serde_json::Value = serde_json::from_str(include_str!(concat!(
            env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
            "/crates/forge_engine/source_graph_fixtures/SHADOW-CULL.v1.json"
        )))
        .unwrap();
        for (
            case_index,
            (f, p, library, count, transparent, alpha, color, coverage, forward_state),
        ) in cases.into_iter().enumerate()
        {
            let original_cull = &cull_plan["materials"][case_index];
            assert_eq!(
                original_cull["forward_source_sha256"].as_str().unwrap(),
                all[f].expected_sha256.to_string()
            );
            let prepass_cull = original_cull["cull"].as_u64().unwrap() as u8;
            let mut shaders = vec![all[f].clone(), all[p].clone()];
            if let Some(index) = library {
                shaders.push(all[index].clone());
            }
            let expected_forward =
                u128::from_be_bytes(all[f].expected_sha256.as_bytes()[..16].try_into().unwrap());
            let expected_prepass =
                u128::from_be_bytes(all[p].expected_sha256.as_bytes()[..16].try_into().unwrap());
            let program = SourceGraphMaterialProgram {
                color_write_mask: 15,
                prepass_cull,
                shaders,
                forward_index: 0,
                prepass_index: 1,
                required_texture_count: count,
                forward_state,
                original_transparent: transparent,
                original_alpha_test: alpha,
                original_alpha_to_mask: coverage,
                requires_original_vertex_color: color,
            };
            let material = register_source_graph_material(
                &mut assets,
                &mut registry,
                program,
                uniforms(),
                images(count),
            )
            .unwrap();
            assert!(!material.extension.program_key.shadow_only);
            let shadow = source_graph_shadow_only_material(&material).unwrap();
            assert!(shadow.extension.program_key.shadow_only);
            assert_ne!(shadow.extension.program_key, material.extension.program_key);
            assert_eq!(
                shadow.extension.program_key,
                SourceGraphKey {
                    shadow_only: true,
                    ..material.extension.program_key
                }
            );
            assert_eq!(shadow.extension.texture_0, material.extension.texture_0);
            assert_eq!(shadow.extension.texture_6, material.extension.texture_6);
            assert_eq!(
                shadow.extension.source.source_object_from_target_object,
                material.extension.source.source_object_from_target_object
            );
            assert!(source_graph_shadow_only_material(&shadow).is_err());
            assert_eq!(material.extension.program_key.vertex, expected_forward);
            assert_eq!(material.extension.program_key.fragment, expected_forward);
            assert_eq!(
                material.extension.program_key.prepass_vertex,
                expected_prepass
            );
            assert_eq!(
                material.extension.program_key.prepass_fragment,
                expected_prepass
            );
            assert_eq!(material.extension.program_key.forward_state, forward_state);
            assert_eq!(
                material
                    .extension
                    .program_key
                    .requires_original_vertex_color,
                color
            );
            if transparent {
                assert_eq!(Material::alpha_mode(&material), AlphaMode::Blend);
            } else if alpha {
                assert!(matches!(
                    Material::alpha_mode(&material),
                    AlphaMode::Mask(_)
                ));
            } else {
                assert_eq!(Material::alpha_mode(&material), AlphaMode::Opaque);
            }
        }
        assert_eq!(assets.len(), 97);
    }
    fn first_program() -> SourceGraphMaterialProgram {
        let all = artifacts();
        SourceGraphMaterialProgram {
            color_write_mask: 15,
            prepass_cull: 0,
            shaders: all,
            forward_index: 1,
            prepass_index: 2,
            required_texture_count: 2,
            forward_state: SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            original_transparent: false,
            original_alpha_test: false,
            original_alpha_to_mask: false,
            requires_original_vertex_color: false,
        }
    }
    #[test]
    fn source_graph_loader_missing_required_images_does_not_register_any_shader() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let program = first_program();
        assert!(program.required_texture_count > 0);
        assert!(
            register_source_graph_material(&mut assets, &mut registry, program, uniforms(), vec![])
                .is_err()
        );
        assert!(assets.is_empty());
    }
    #[test]
    fn source_graph_loader_missing_private_module_rejects_real_forward_shader_atomically() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let all = artifacts();
        assert!(
            register_source_graph_shaders(&mut assets, &mut registry, &[all[1].clone()]).is_err()
        );
        assert!(assets.is_empty());
    }
    #[test]
    fn source_graph_loader_library_as_stage_and_bad_index_do_not_register_shaders() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let mut program = first_program();
        program.forward_index = 0;
        let count = program.required_texture_count;
        assert!(
            register_source_graph_material(
                &mut assets,
                &mut registry,
                program,
                uniforms(),
                images(count)
            )
            .is_err()
        );
        assert!(assets.is_empty());
        let mut program = first_program();
        program.prepass_index = usize::MAX;
        let count = program.required_texture_count;
        assert!(
            register_source_graph_material(
                &mut assets,
                &mut registry,
                program,
                uniforms(),
                images(count)
            )
            .is_err()
        );
        assert!(assets.is_empty());
    }
    #[test]
    fn source_graph_loader_wrong_required_count_cannot_enable_unused_slot_fallback() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let mut program = first_program();
        assert!(program.required_texture_count > 0);
        program.required_texture_count = 0;
        assert!(
            register_source_graph_material(&mut assets, &mut registry, program, uniforms(), vec![])
                .is_err()
        );
        assert!(assets.is_empty());
    }
    #[test]
    fn source_graph_loader_changed_prepass_texture_layout_rejects_even_with_correct_sha() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let mut program = first_program();
        let index = program.prepass_index;
        let previous = program.shaders[index].source.clone();
        let changed = previous.replace("var t0:texture_2d<f32>;", "var t99:texture_2d<f32>;");
        assert_ne!(previous, changed);
        program.shaders[index].expected_sha256 = forge_core::bytes_sha256(changed.as_bytes());
        program.shaders[index].source = changed;
        let count = program.required_texture_count;
        assert!(
            register_source_graph_material(
                &mut assets,
                &mut registry,
                program,
                uniforms(),
                images(count)
            )
            .is_err()
        );
        assert!(assets.is_empty());
    }

    #[test]
    fn source_graph_loader_unknown_prepass_cull_refuses_before_shader_registration() {
        let mut program = first_program();
        program.prepass_cull = 255;
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        assert!(
            register_source_graph_material(
                &mut assets,
                &mut registry,
                program,
                uniforms(),
                images(2)
            )
            .is_err()
        );
        assert_eq!(assets.len(), 0);
    }
}
