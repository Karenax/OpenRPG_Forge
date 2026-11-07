// SPDX-License-Identifier: MIT
// Adapted from Tidewater spray sprites, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Shared source spray sprite material and geometry; callers admit the inputs.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{
            AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, TextureDimension,
            TextureFormat, VertexFormat,
        },
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};

const SLOT: MeshVertexAttribute =
    MeshVertexAttribute::new("SpraySlot", 0x7457_0007, VertexFormat::Uint32);
pub const SPRITE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("6e8cc1d5-5811-4f49-a124-de4efc4132dd");
pub const MAX_SPRITES: u32 = 40960;

/// Source sprite material compiled into the shared engine.
pub const COMMON_SPRITE_SHADER: &str = include_str!("source_breaker_kernels/spray-sprites.wgsl");
const SOURCE_HEADER: &str = "// SPDX-License-Identifier: MIT\n// Adapted from Tidewater spray sprites, copyright 2026 DRG Software Solutions LLC.\n// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.\n";

/// Reuse the common shader only for the exact admitted source; preserve variants.
#[must_use]
pub fn select_common_shader(source: &str) -> &str {
    let shared = COMMON_SPRITE_SHADER
        .strip_prefix(SOURCE_HEADER)
        .expect("common sprite shader attribution");
    if source == shared { shared } else { source }
}

/// Restrict source crest-shadow sampling to this zone's actual station count.
pub fn prepare_shadow_shader(
    source: &str,
    source_stations: u32,
    stations: u32,
) -> Result<String, &'static str> {
    if source_stations > 4096 || stations > 4096 {
        return Err("Spray station count exceeds the supported limit");
    }
    let guard = format!("idx < {}.0", source_stations * 2);
    if source.matches(&guard).count() != 1 {
        return Err("Sprite crest shadow guard differs from its source");
    }
    Ok(source.replacen(&guard, &format!("idx < {}.0", stations * 2), 1))
}

/// Bound resident crest shadows by the actual bound crest buffer. Each
/// shadow index uses three vec4 values; a station has two indices.
pub fn prepare_resident_shadow_shader(
    source: &str,
    source_stations: u32,
) -> Result<String, &'static str> {
    if source_stations > 4096 {
        return Err("Spray station count exceeds the supported limit");
    }
    let guard = format!("idx < {}.0", source_stations * 2);
    if source.matches(&guard).count() != 1 {
        return Err("Sprite crest shadow guard differs from its source");
    }
    Ok(source.replacen(&guard, "idx < f32( arrayLength( &breakersCrest ) / 3u )", 1))
}

/// Adapt the resident sprite shader to reject crest shadows from particles
/// reserved before a station-generation handoff. The cutoff buffer contains
/// the GPU head at the handoff and a state. An owner that activates
/// filtering must retire it after one ring of new reservations, before the
/// unsigned head can wrap a second time.
pub fn prepare_resident_generation_shader(
    source: &str,
    source_stations: u32,
) -> Result<String, &'static str> {
    const CREST: &str =
        "@group(#{MATERIAL_BIND_GROUP}) @binding(4) var<storage,read> breakersCrest:array<vec4f>;";
    const VISIBILITY: &str =
        "let sunVis = sprayCloudShadow( p.xz ) * sprayWaveShadow( p, info.w );";
    const BINDINGS: &str = r"
@group(#{MATERIAL_BIND_GROUP}) @binding(42) var<storage,read> residentSprayHead:array<u32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(43) var<storage,read> residentShadowCutoff:array<u32>;
";
    if source.matches(CREST).count() != 1 || source.matches(VISIBILITY).count() != 1 {
        return Err("Sprite crest generation sites differ from their source");
    }
    let shader = prepare_resident_shadow_shader(source, source_stations)?;
    let shader = shader.replacen(
        CREST,
        &format!(
            "{CREST}{BINDINGS}{}",
            crate::source_spray_generation::CLASSIFIER_WGSL
        ),
        1,
    );
    Ok(shader.replacen(
        VISIBILITY,
        "let sunVis = sprayCloudShadow( p.xz ) * select( 1.0, sprayWaveShadow( p, info.w ), residentShadowIsCurrent( slot ) );",
        1,
    ))
}

/// Make the source quad-per-particle geometry with bounded capacity.
pub fn sprite_mesh(capacity: u32) -> Result<Mesh, &'static str> {
    if capacity == 0 || capacity > MAX_SPRITES {
        return Err("Invalid spray sprite capacity");
    }
    let mut positions = Vec::with_capacity(capacity as usize * 4);
    let mut slots = Vec::with_capacity(capacity as usize * 4);
    let mut indices = Vec::with_capacity(capacity as usize * 6);
    for slot in 0..capacity {
        positions.extend([[-1., -1., 0.], [1., -1., 0.], [1., 1., 0.], [-1., 1., 0.]]);
        slots.extend([slot; 4]);
        indices.extend([0, 1, 2, 0, 2, 3].map(|i| slot * 4 + i));
    }
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_attribute(SLOT, slots);
    mesh.insert_indices(Indices::U32(indices));
    Ok(mesh)
}

/// Build an RGBA8 mip chain with the original repeating linear sampler.
pub fn sprite_texture(side: u32, mips: u32, data: Vec<u8>) -> Result<Image, &'static str> {
    if side == 0 || mips == 0 || mips > u32::BITS - side.leading_zeros() {
        return Err("Invalid spray texture dimensions");
    }
    let mut bytes = 0_u64;
    for mip in 0..mips {
        let width = u64::from(side >> mip);
        bytes = bytes
            .checked_add(
                width
                    .checked_mul(width)
                    .and_then(|n| n.checked_mul(4))
                    .ok_or("Spray texture overflow")?,
            )
            .ok_or("Spray texture overflow")?;
    }
    if usize::try_from(bytes).ok() != Some(data.len()) {
        return Err("Spray texture mip size differs");
    }
    let mut image = Image::new_uninit(
        Extent3d {
            width: side,
            height: side,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(data);
    image.texture_descriptor.mip_level_count = mips;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        ..default()
    });
    Ok(image)
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct SprayMaterial {
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
    #[uniform(0)]
    pub frame: [UVec4; 45],
    #[storage(1, read_only)]
    #[dependency]
    pub pos: Handle<ShaderBuffer>,
    #[storage(2, read_only)]
    #[dependency]
    pub vel: Handle<ShaderBuffer>,
    #[storage(3, read_only)]
    #[dependency]
    pub info: Handle<ShaderBuffer>,
    #[storage(4, read_only)]
    #[dependency]
    pub crest: Handle<ShaderBuffer>,
    #[storage(42, read_only)]
    #[dependency]
    pub ring_head: Handle<ShaderBuffer>,
    #[storage(43, read_only)]
    #[dependency]
    pub shadow_cutoff: Handle<ShaderBuffer>,
    #[texture(5)]
    #[sampler(6)]
    #[dependency]
    pub puff: Handle<Image>,
    #[texture(7)]
    #[dependency]
    pub dots: Handle<Image>,
    #[texture(8, sample_type = "depth")]
    #[dependency]
    pub depth: Handle<Image>,
    #[uniform(9)]
    pub material: [UVec4; 3],
}
impl Material for SprayMaterial {
    fn vertex_shader() -> ShaderRef {
        SPRITE_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SPRITE_SHADER.into()
    }
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Premultiplied
    }
    fn depth_bias(&self) -> f32 {
        -20.0
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            SLOT.at_shader_location(1),
        ])?];
        descriptor.primitive.cull_mode = None;
        if let Some(depth) = &mut descriptor.depth_stencil {
            depth.depth_write_enabled = Some(false);
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;

    #[test]
    fn common_sprite_shader_preserves_distinct_variants() {
        let common = COMMON_SPRITE_SHADER.strip_prefix(SOURCE_HEADER).unwrap();
        let copy = common.to_owned();
        let selected = select_common_shader(&copy);
        assert!(!std::ptr::eq(selected.as_ptr(), copy.as_ptr()));
        assert_eq!(selected, common);
        let variant = format!("{common}\n// variant");
        let selected_variant = select_common_shader(&variant);
        assert!(std::ptr::eq(selected_variant.as_ptr(), variant.as_ptr()));
        assert_eq!(
            forge_core::bytes_sha256(common.as_bytes()).to_string(),
            "88b920de2bcc2124d71145a61f950b2e5e4bb496948b2dda3e3f881921dd2f3f"
        );
        assert!(prepare_shadow_shader(common, 577, 35).is_ok());
    }

    #[test]
    fn sprite_mesh_and_mips_are_bounded_and_keep_source_layout() {
        let mesh = sprite_mesh(2).unwrap();
        assert_eq!(mesh.count_vertices(), 8);
        assert!(
            matches!(mesh.attribute(SLOT), Some(VertexAttributeValues::Uint32(slots)) if slots == &[0, 0, 0, 0, 1, 1, 1, 1])
        );
        assert!(
            matches!(mesh.indices(), Some(Indices::U32(indices)) if indices == &[0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7])
        );
        assert!(sprite_mesh(0).is_err());
        assert!(sprite_mesh(MAX_SPRITES + 1).is_err());
        let image = sprite_texture(2, 2, vec![0; 20]).unwrap();
        assert_eq!(image.texture_descriptor.mip_level_count, 2);
        assert!(sprite_texture(2, 3, vec![0; 20]).is_err());
        assert!(sprite_texture(2, 2, vec![0; 19]).is_err());
    }

    #[test]
    fn sprite_shadow_guard_tracks_zone_and_rejects_ambiguous_source() {
        let source = "if idx < 1154.0 { return; }";
        assert_eq!(
            prepare_shadow_shader(source, 577, 8).unwrap(),
            "if idx < 16.0 { return; }"
        );
        assert_eq!(
            prepare_shadow_shader(source, 577, 0).unwrap(),
            "if idx < 0.0 { return; }"
        );
        assert!(prepare_shadow_shader(source, 576, 8).is_err());
        assert!(prepare_shadow_shader(&format!("{source}{source}"), 577, 8).is_err());
        assert!(prepare_shadow_shader(source, 577, 4097).is_err());
        assert_eq!(
            prepare_resident_shadow_shader(source, 577).unwrap(),
            "if idx < f32( arrayLength( &breakersCrest ) / 3u ) { return; }"
        );
        assert!(prepare_resident_shadow_shader(source, 576).is_err());
        assert!(prepare_resident_shadow_shader(&format!("{source}{source}"), 577).is_err());
        assert!(prepare_resident_shadow_shader(source, 4097).is_err());
    }

    #[test]
    fn resident_shadow_generation_keeps_only_slots_reserved_after_the_handoff() {
        let current = |slot: u32, head: u32, cutoff: u32, state: u32| {
            if state == 0 {
                return true;
            }
            if slot >= 32_768 {
                return false;
            }
            if state == 2 {
                return true;
            }
            let newer = head.wrapping_sub(cutoff);
            newer >= 32_768 || (head.wrapping_sub(1).wrapping_sub(slot) & 32_767) < newer
        };
        assert!(current(99, 103, 100, 0));
        assert!(!current(99, 103, 100, 1));
        for slot in 100..=102 {
            assert!(current(slot, 103, 100, 1));
        }
        for slot in [32_766, 32_767, 0, 1] {
            assert!(current(slot, 32_770, 32_766, 1));
        }
        assert!(!current(2, 32_770, 32_766, 1));
        assert!(current(0, 1, u32::MAX - 1, 1));
        assert!(current(32_767, 1, u32::MAX - 1, 1));
        assert!(!current(1, 1, u32::MAX - 1, 1));
        assert!((0..32_768).all(|slot| current(slot, 100_000, 67_232, 1)));
        assert!(!current(40_000, 103, 100, 1));
        assert!(!current(40_000, 103, 100, 2));
        assert!(current(40_000, 103, 100, 0));

        let shader = prepare_resident_generation_shader(COMMON_SPRITE_SHADER, 577).unwrap();
        assert!(shader.contains("residentShadowIsCurrent( slot )"));
        assert!(shader.contains("arrayLength( &breakersCrest )"));
        assert!(prepare_resident_generation_shader("missing sites", 577).is_err());
        assert!(
            prepare_resident_generation_shader(
                &format!("{COMMON_SPRITE_SHADER}{COMMON_SPRITE_SHADER}"),
                577
            )
            .is_err()
        );
    }
}
