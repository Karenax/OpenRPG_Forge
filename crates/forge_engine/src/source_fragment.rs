//! Bevy PBR adapter for closed imported fragment equations. No second renderer.
use bevy::{
    asset::{LoadContext, RenderAssetUsages, embedded_asset},
    gltf::{
        DefaultGltfImageSampler, GltfLoader, GltfLoaderSettings, GltfMaterial, GltfPlugin,
        extensions::{ErasedGltfExtensionHandler, GltfExtensionHandler, GltfExtensionHandlers},
        gltf,
    },
    image::{CompressedImageFormatSupport, CompressedImageFormats, ImageSampler, ImageType},
    material::OpaqueRendererMethod,
    pbr::{ExtendedMaterial, MaterialExtension, MaterialPlugin},
    prelude::*,
    render::render_resource::{AsBindGroup, TextureFormat},
    shader::ShaderRef,
};
use forge_assets::source_fragment::{
    SOURCE_FRAGMENT_EXTENSION, SourceBackfaceMode, SourceFragmentContract,
};
use std::collections::BTreeMap;

const SHADER: &str = "embedded://forge_engine/shaders/source_fragment.wgsl";
pub use crate::source_fragment_loader::prepare_source_glb;

#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
pub struct SourceFragmentExtension {
    #[uniform(100)]
    pub rgb_rows: [Vec4; 3],
    #[uniform(100)]
    pub metallic: Vec4,
    #[uniform(100)]
    pub occlusion: Vec4,
    #[uniform(100)]
    pub roughness: Vec4,
    /// XYZ offsets, W Unity `NormalStrength`.
    #[uniform(100)]
    pub scalar_offsets_normal_strength: Vec4,
    /// x: single=0, Flip=1, Mirror=2, None=3; y: audited tangent basis Y sign.
    #[uniform(100)]
    pub backface: Vec4,
    /// Saturation, power, signed projection period, source noise contrast.
    #[uniform(100)]
    pub world_params: Vec4,
    #[uniform(100)]
    /// XY: source mask scale; ZW: independently authored surface UV scale.
    pub world_mask_uv: Vec4,
    #[uniform(100)]
    pub world_tints: [Vec4; 3],
    #[uniform(100)]
    pub world_axes: [Vec4; 3],
    /// X: loaded world images; Y: required world program; Z: RGB blend; W: standalone colour power.
    #[uniform(100)]
    pub world_control: Vec4,
    #[texture(101)]
    #[sampler(102)]
    pub source_orm: Handle<Image>,
    #[texture(103, filterable = false)]
    pub source_normal: Option<Handle<Image>>,
    #[texture(104, filterable = false)]
    pub source_blend_mask: Option<Handle<Image>>,
    #[texture(105, filterable = false)]
    pub source_noise: Option<Handle<Image>>,
    #[texture(106, filterable = false)]
    pub source_noise_mask: Option<Handle<Image>>,
}
impl MaterialExtension for SourceFragmentExtension {
    fn fragment_shader() -> ShaderRef {
        SHADER.into()
    }
    fn prepass_fragment_shader() -> ShaderRef {
        "embedded://forge_engine/shaders/source_fragment_prepass.wgsl".into()
    }
}
pub type SourceFragmentMaterial = ExtendedMaterial<StandardMaterial, SourceFragmentExtension>;

/// Explicit refusal marker. Invalid source materials never render a silent fallback.
#[derive(Component, Clone, Debug, Reflect)]
#[reflect(Component)]
pub struct SourceFragmentRejected(pub String);

pub struct SourceFragmentPlugin;
impl Plugin for SourceFragmentPlugin {
    fn build(&self, app: &mut App) {
        app.register_type::<SourceFragmentRejected>();
        embedded_asset!(app, "shaders/source_fragment.wgsl");
        embedded_asset!(app, "shaders/source_pbr_lighting.wgsl");
        embedded_asset!(app, "shaders/source_fragment_common.wgsl");
        embedded_asset!(app, "shaders/source_fragment_prepass.wgsl");
        app.add_plugins(MaterialPlugin::<SourceFragmentMaterial>::default());
    }
    fn finish(&self, app: &mut App) {
        // PBR's standard-material handler is already installed during build.
        let handlers = app.world().resource::<GltfExtensionHandlers>().0.clone();
        #[cfg(not(target_family = "wasm"))]
        handlers
            .write_blocking()
            .push(Box::<SourceFragmentGltfHandler>::default());
        #[cfg(target_family = "wasm")]
        bevy::tasks::block_on(async {
            handlers
                .write()
                .await
                .push(Box::<SourceFragmentGltfHandler>::default());
        });
        let configured = app.get_added_plugins::<GltfPlugin>();
        let gltf = configured
            .first()
            .expect("source fragment requires the standard GltfPlugin");
        let inner = GltfLoader {
            supported_compressed_formats: app
                .world()
                .get_resource::<CompressedImageFormatSupport>()
                .map_or(CompressedImageFormats::NONE, |s| s.0),
            custom_vertex_attributes: gltf.custom_vertex_attributes.clone(),
            default_sampler: app
                .world()
                .resource::<DefaultGltfImageSampler>()
                .get_internal(),
            default_convert_coordinates: gltf.convert_coordinates,
            default_skinned_mesh_bounds_policy: gltf.skinned_mesh_bounds_policy,
            extensions: handlers,
        };
        app.register_asset_loader(crate::source_fragment_loader::SourceFragmentGlbLoader { inner });
    }
}

#[derive(Default, Clone)]
struct SourceFragmentGltfHandler {
    material_results: BTreeMap<String, Result<Handle<SourceFragmentMaterial>, String>>,
    normal_images: BTreeMap<String, Result<Handle<Image>, String>>,
    base_images: BTreeMap<String, Result<Handle<Image>, String>>,
    world_images: BTreeMap<String, Result<[Handle<Image>; 3], String>>,
    required: bool,
}
impl GltfExtensionHandler for SourceFragmentGltfHandler {
    fn dyn_clone(&self) -> Box<dyn ErasedGltfExtensionHandler> {
        Box::new(self.clone())
    }
    #[expect(
        clippy::too_many_lines,
        reason = "Ordered source validation and GPU preparation remain a single auditable operation"
    )]
    fn on_root(
        &mut self,
        context: &mut LoadContext<'_>,
        document: &gltf::Gltf,
        _settings: &GltfLoaderSettings,
    ) {
        self.required = document
            .extensions_required()
            .any(|name| name == SOURCE_FRAGMENT_EXTENSION)
            || document
                .extension_value(SOURCE_FRAGMENT_EXTENSION)
                .and_then(|v| v.get("required"))
                .and_then(serde_json::Value::as_bool)
                == Some(true);
        for material in document.materials() {
            let Some(value) = material
                .extensions()
                .and_then(|v| v.get(SOURCE_FRAGMENT_EXTENSION))
            else {
                continue;
            };
            let base_result = serde_json::from_value::<SourceFragmentContract>(value.clone())
                .map_err(|e| e.to_string())
                .and_then(|c| {
                    c.validate().map_err(str::to_owned)?;
                    let Some(base) = c.base_png else {
                        return Ok(None);
                    };
                    let png = base.image;
                    let view = document
                        .views()
                        .nth(usize::try_from(png.buffer_view).map_err(|e| e.to_string())?)
                        .ok_or("source PNG bufferView missing")?;
                    if view.buffer().index() != 0
                        || format!("{:?}", view.buffer().source()) != "Bin"
                    {
                        return Err("source PNG requires GLB BIN buffer zero".into());
                    }
                    let blob = document.blob.as_ref().ok_or("source PNG GLB BIN missing")?;
                    let end = view
                        .offset()
                        .checked_add(view.length())
                        .ok_or("source PNG view overflow")?;
                    let raw = blob
                        .get(view.offset()..end)
                        .ok_or("source PNG view outside BIN")?;
                    let image = source_image_from_png(raw, &png.sha256, base.srgb)?;
                    Ok(Some(context.add_labeled_asset(
                        format!(
                            "SourceBaseColor/{}",
                            material.index().ok_or("source material index missing")?
                        ),
                        image,
                    )))
                });
            if let Some(index) = material.index() {
                match base_result {
                    Ok(Some(image)) => {
                        self.base_images
                            .insert(format!("Material{index}"), Ok(image));
                    }
                    Ok(None) => {}
                    Err(error) => {
                        self.base_images
                            .insert(format!("Material{index}"), Err(error));
                    }
                }
            }
            let result = serde_json::from_value::<SourceFragmentContract>(value.clone())
                .map_err(|e| e.to_string())
                .and_then(|c| {
                    c.validate().map_err(str::to_owned)?;
                    let Some(png) = c.normal_png else {
                        return Ok(None);
                    };
                    let view = document
                        .views()
                        .nth(usize::try_from(png.buffer_view).map_err(|e| e.to_string())?)
                        .ok_or("source PNG bufferView missing")?;
                    if view.buffer().index() != 0
                        || format!("{:?}", view.buffer().source()) != "Bin"
                    {
                        return Err("source PNG requires GLB BIN buffer zero".into());
                    }
                    let blob = document.blob.as_ref().ok_or("source PNG GLB BIN missing")?;
                    let end = view
                        .offset()
                        .checked_add(view.length())
                        .ok_or("source PNG view overflow")?;
                    let raw = blob
                        .get(view.offset()..end)
                        .ok_or("source PNG view outside BIN")?;
                    let image = normal_image_from_png(raw, &png.sha256)?;
                    Ok(Some(context.add_labeled_asset(
                        format!(
                            "SourceNormal/{}",
                            material.index().ok_or("source material index missing")?
                        ),
                        image,
                    )))
                });
            if let Some(index) = material.index() {
                match result {
                    Ok(Some(image)) => {
                        self.normal_images
                            .insert(format!("Material{index}"), Ok(image));
                    }
                    Ok(None) => {}
                    Err(error) => {
                        self.normal_images
                            .insert(format!("Material{index}"), Err(error));
                    }
                }
                let world = serde_json::from_value::<SourceFragmentContract>(value.clone())
                    .map_err(|e| e.to_string())
                    .and_then(|contract| {
                        contract.validate().map_err(str::to_owned)?;
                        let Some(program) = contract.world_blend else {
                            return Ok(None);
                        };
                        let mut handles = Vec::with_capacity(3);
                        for (role, png, srgb) in [
                            ("BlendMask", &program.blend_mask, program.blend_mask_srgb),
                            ("Noise", &program.noise_texture, program.noise_texture_srgb),
                            ("NoiseMask", &program.noise_mask, program.noise_mask_srgb),
                        ] {
                            let view = document
                                .views()
                                .nth(usize::try_from(png.buffer_view).map_err(|e| e.to_string())?)
                                .ok_or("world PNG bufferView missing")?;
                            if view.buffer().index() != 0
                                || format!("{:?}", view.buffer().source()) != "Bin"
                            {
                                return Err("world PNG requires GLB BIN buffer zero".into());
                            }
                            let blob = document.blob.as_ref().ok_or("world PNG GLB BIN missing")?;
                            let end = view
                                .offset()
                                .checked_add(view.length())
                                .ok_or("world PNG view overflow")?;
                            let raw = blob
                                .get(view.offset()..end)
                                .ok_or("world PNG view outside BIN")?;
                            let image = source_image_from_png(raw, &png.sha256, srgb)?;
                            handles.push(
                                context.add_labeled_asset(
                                    format!("SourceWorld/{index}/{role}"),
                                    image,
                                ),
                            );
                        }
                        let images: [Handle<Image>; 3] =
                            handles.try_into().map_err(|_| "world PNG handle count")?;
                        Ok(Some(images))
                    });
                match world {
                    Ok(Some(images)) => {
                        self.world_images
                            .insert(format!("Material{index}"), Ok(images));
                    }
                    Ok(None) => {}
                    Err(error) => {
                        self.world_images
                            .insert(format!("Material{index}"), Err(error));
                    }
                }
            }
        }
    }
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn on_material(
        &mut self,
        context: &mut LoadContext<'_>,
        material: &gltf::Material,
        _handle: Handle<GltfMaterial>,
        asset: &GltfMaterial,
        label: &str,
    ) {
        let Some(value) = material
            .extensions()
            .and_then(|v| v.get(SOURCE_FRAGMENT_EXTENSION))
        else {
            if self.required {
                self.material_results.insert(
                    label.to_owned(),
                    Err("required source fragment contract missing on material".into()),
                );
            }
            return;
        };
        let normal_key = material.index().map(|index| format!("Material{index}"));
        let result = serde_json::from_value::<SourceFragmentContract>(value.clone())
            .map_err(|e| e.to_string())
            .and_then(|contract| build_material_inner(&contract, asset, true))
            .and_then(|mut material| {
                if let Some(result) = normal_key
                    .as_ref()
                    .and_then(|key| self.normal_images.get(key))
                {
                    material.extension.source_normal = Some(result.clone()?);
                    material.extension.backface.z = 1.0;
                }
                if let Some(result) = normal_key
                    .as_ref()
                    .and_then(|key| self.base_images.get(key))
                {
                    material.base.base_color_texture = Some(result.clone()?);
                } else if value.get("base_png").is_some_and(|v| !v.is_null()) {
                    return Err("original base PNG image result missing".into());
                }
                if material.extension.world_control.y == 1.0 {
                    let key = normal_key.as_ref().ok_or("world material index missing")?;
                    let images = self
                        .world_images
                        .get(key)
                        .ok_or("world source image results missing")?
                        .clone()?;
                    material.extension.source_blend_mask = Some(images[0].clone());
                    material.extension.source_noise = Some(images[1].clone());
                    material.extension.source_noise_mask = Some(images[2].clone());
                    material.extension.world_control.x = 1.0;
                }
                Ok(material)
            })
            .map(|derived| context.add_labeled_asset(format!("{label}/source_fragment"), derived));
        if let Err(error) = &result {
            error!("Source material {label} refused: {error}");
        }
        self.material_results.insert(label.to_owned(), result);
    }
    fn on_spawn_mesh_and_material(
        &mut self,
        _context: &mut LoadContext<'_>,
        primitive: &gltf::Primitive,
        _mesh: &gltf::Mesh,
        _material: &gltf::Material,
        entity: &mut EntityWorldMut,
        label: &str,
    ) {
        let Some(result) = self.material_results.get(label) else {
            if self.required {
                entity.remove::<MeshMaterial3d<StandardMaterial>>();
                entity.insert((
                    SourceFragmentRejected(format!(
                        "required source material result missing: {label}"
                    )),
                    Visibility::Hidden,
                ));
            }
            return;
        };
        entity.remove::<MeshMaterial3d<StandardMaterial>>();
        let attributes: Vec<_> = primitive.attributes().map(|(s, _)| s.to_string()).collect();
        if !attributes.iter().any(|s| s == "NORMAL")
            || !attributes.iter().any(|s| s == "TANGENT")
            || !attributes.iter().any(|s| s == "TEXCOORD_0")
        {
            entity.insert((
                SourceFragmentRejected(
                    "source corner normal, tangent and UV0 attributes required".into(),
                ),
                Visibility::Hidden,
            ));
            return;
        }
        if primitive
            .attributes()
            .any(|(semantic, _)| semantic.to_string().starts_with("COLOR_"))
        {
            entity.insert((
                SourceFragmentRejected(
                    "closed source fragment does not consume vertex colours".into(),
                ),
                Visibility::Hidden,
            ));
            return;
        }
        match result {
            Ok(handle) => {
                entity.insert(MeshMaterial3d(handle.clone()));
            }
            Err(error) => {
                entity.insert((SourceFragmentRejected(error.clone()), Visibility::Hidden));
            }
        }
    }
}

pub fn build_material(
    contract: &SourceFragmentContract,
    source: &GltfMaterial,
) -> Result<SourceFragmentMaterial, String> {
    build_material_inner(contract, source, false)
}

#[expect(
    clippy::too_many_lines,
    reason = "Ordered source validation and GPU preparation remain a single auditable operation"
)]
fn build_material_inner(
    contract: &SourceFragmentContract,
    source: &GltfMaterial,
    loading_pinned_world_images: bool,
) -> Result<SourceFragmentMaterial, String> {
    contract.validate().map_err(str::to_owned)?;
    if contract.base_png.is_some() && !loading_pinned_world_images {
        return Err("original base PNG must load through the GLB handler".into());
    }
    if contract.world_blend.is_some() && !loading_pinned_world_images {
        return Err(
            "world source material must load its pinned images through the GLB handler".into(),
        );
    }
    if contract.world_blend.is_some() && contract.normal_png.is_none() {
        return Err("world source material requires pinned original normal PNG".into());
    }
    if source.unlit
        || source.base_color.to_linear() != LinearRgba::WHITE
        || source.emissive != LinearRgba::BLACK
        || source.uv_transform != bevy::math::Affine2::IDENTITY
        || source.specular_transmission != 0.0
        || source.clearcoat != 0.0
        || source.anisotropy_strength != 0.0
        || !matches!(source.alpha_mode, AlphaMode::Opaque | AlphaMode::Mask(_))
    {
        return Err(
            "source fragment requires identity UV, neutral core factors, opaque or mask PBR".into(),
        );
    }
    if source.double_sided != (contract.double_sided_normal_mode != SourceBackfaceMode::SingleSided)
    {
        return Err("source fragment backface mode contradicts core material".into());
    }
    if source.base_color_texture.is_none()
        || (source.normal_map_texture.is_none() && contract.normal_png.is_none())
    {
        return Err("source fragment requires original base and normal textures".into());
    }
    let orm = source
        .metallic_roughness_texture
        .clone()
        .ok_or("source fragment original ORM texture missing")?;
    if source.occlusion_texture.as_ref() != Some(&orm) {
        return Err("source AO and packed ORM must share the source image".into());
    }
    #[expect(
        clippy::cast_possible_truncation,
        reason = "Validated finite bounded source coefficients are uploaded as GPU f32; source f64 contract is retained"
    )]
    let vector = |v: [f64; 4]| Vec4::from_array(v.map(|x| x as f32));
    let coefficients = |v: [f64; 5]| vector([v[0], v[1], v[2], v[3]]);
    let backface = match contract.double_sided_normal_mode {
        SourceBackfaceMode::SingleSided => 0.0,
        SourceBackfaceMode::Flip => 1.0,
        SourceBackfaceMode::Mirror => 2.0,
        SourceBackfaceMode::None => 3.0,
    };
    let world = contract.world_blend.as_ref();
    let surface_scale = contract.effective_surface_uv_scale();
    let rgb_vector = |v: [f64; 3]| vector([v[0], v[1], v[2], 0.0]);
    // The admitted closed contract needs only these core fields. The extension
    // samples source ORM once; leaving core ORM/AO empty preserves external SSAO.
    let base = StandardMaterial {
        base_color: source.base_color,
        base_color_channel: source.base_color_channel.clone(),
        base_color_texture: source.base_color_texture.clone(),
        normal_map_channel: source.normal_map_channel.clone(),
        normal_map_texture: source.normal_map_texture.clone(),
        double_sided: source.double_sided,
        cull_mode: source.cull_mode,
        alpha_mode: source.alpha_mode,
        opaque_render_method: OpaqueRendererMethod::Forward,
        ..Default::default()
    };
    Ok(SourceFragmentMaterial {
        base,
        extension: SourceFragmentExtension {
            rgb_rows: contract.linear_rgb_rows.map(vector),
            metallic: coefficients(contract.metallic),
            occlusion: coefficients(contract.occlusion),
            roughness: coefficients(contract.perceptual_roughness),
            scalar_offsets_normal_strength: vector([
                contract.metallic[4],
                contract.occlusion[4],
                contract.perceptual_roughness[4],
                contract.normal_strength,
            ]),
            backface: vector([backface, contract.target_tangent_space_y_sign, 0.0, 0.0]),
            world_params: world.map_or(
                contract
                    .base_color_power
                    .map_or(Vec4::ZERO, |power| vector([0.0, power, 0.0, 0.0])),
                |w| {
                    vector([
                        w.base_saturation,
                        w.base_power,
                        w.noise_period,
                        w.noise_contrast,
                    ])
                },
            ),
            world_mask_uv: vector([
                world.map_or(0.0, |w| w.mask_uv_scale[0]),
                world.map_or(0.0, |w| w.mask_uv_scale[1]),
                surface_scale[0],
                surface_scale[1],
            ]),
            world_tints: world.map_or([Vec4::ZERO; 3], |w| {
                [w.red_tint, w.green_tint, w.blue_tint].map(rgb_vector)
            }),
            world_axes: world.map_or([Vec4::ZERO; 3], |w| {
                w.source_axes_from_target.map(rgb_vector)
            }),
            world_control: vector([
                0.0,
                if world.is_some() { 1.0 } else { 0.0 },
                if world.is_some_and(|w| w.blend_mask_enabled) {
                    1.0
                } else {
                    0.0
                },
                if contract.base_color_power.is_some() {
                    1.0
                } else {
                    0.0
                },
            ]),
            source_orm: orm,
            source_normal: None,
            source_blend_mask: None,
            source_noise: None,
            source_noise_mask: None,
        },
    })
}

/// Portable linear normal storage. All 65,536 source integer codes remain distinct.
pub fn normal_image_from_png(raw: &[u8], pin: &str) -> Result<Image, String> {
    source_image_from_png(raw, pin, false)
}

/// Preserve original PNG channel codes in float storage. RGB transfer follows
/// the pinned source importer flag; alpha always remains linear. Sampling and
/// mip generation are separate responsibilities and are not qualified here.
pub fn source_image_from_png(raw: &[u8], pin: &str, srgb: bool) -> Result<Image, String> {
    if raw.len() < 33
        || raw.len() > 64 * 1024 * 1024
        || raw[..8] != *b"\x89PNG\r\n\x1a\n"
        || forge_core::bytes_sha256(raw).to_string() != pin
    {
        return Err("invalid or unpinned source PNG".into());
    }
    let width = u32::from_be_bytes(raw[16..20].try_into().map_err(|_| "PNG width")?);
    let height = u32::from_be_bytes(raw[20..24].try_into().map_err(|_| "PNG height")?);
    if width == 0
        || height == 0
        || width > 4096
        || height > 4096
        || !matches!(raw[24], 8 | 16)
        || !matches!(raw[25], 2 | 6)
        || raw[26..29] != [0, 0, 0]
    {
        return Err("source PNG must be bounded RGB/RGBA8/16, noninterlaced".into());
    }
    let mut image = Image::from_buffer(
        raw,
        ImageType::Extension("png"),
        CompressedImageFormats::NONE,
        false,
        ImageSampler::default(),
        RenderAssetUsages::default(),
    )
    .map_err(|e| e.to_string())?;
    let pixels = image.data.take().ok_or("source pixels unavailable")?;
    let (bytes_per_component, denominator) = match image.texture_descriptor.format {
        TextureFormat::Rgba8Unorm => (1, 255.0),
        TextureFormat::Rgba16Unorm => (2, 65535.0),
        _ => return Err("source decoder changed format".into()),
    };
    let expected = usize::try_from(width).map_err(|e| e.to_string())?
        * usize::try_from(height).map_err(|e| e.to_string())?
        * 4
        * bytes_per_component;
    if pixels.len() != expected {
        return Err("source decoded byte length".into());
    }
    let mut floats = Vec::with_capacity(pixels.len() / bytes_per_component * 4);
    for (channel, component) in pixels.chunks_exact(bytes_per_component).enumerate() {
        let code = if bytes_per_component == 1 {
            u16::from(component[0])
        } else {
            u16::from_le_bytes([component[0], component[1]])
        };
        let value = f32::from(code) / denominator;
        floats.extend_from_slice(
            &source_channel_to_linear(value, srgb && channel % 4 != 3).to_le_bytes(),
        );
    }
    image.data = Some(floats);
    image.texture_descriptor.format = TextureFormat::Rgba32Float;
    Ok(image)
}

/// IEC sRGB transfer on RGB only; callers preserve linear alpha independently.
fn source_channel_to_linear(value: f32, srgb: bool) -> f32 {
    if !srgb {
        value
    } else if value <= 0.04045 {
        value / 12.92
    } else {
        ((value + 0.055) / 1.055).powf(2.4)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{ecs::reflect::AppTypeRegistry, world_serialization::WorldAsset};

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn source_image_transfer_preserves_linear_codes_and_srgb_order() {
        let mut previous = -1.0;
        for code in 0_u16..=u16::MAX {
            let value = f32::from(code) / 65535.0;
            assert_eq!(source_channel_to_linear(value, false), value);
            let transferred = source_channel_to_linear(value, true);
            assert!(transferred > previous);
            let v = f64::from(value);
            let reference = if v <= 0.04045 {
                v / 12.92
            } else {
                ((v + 0.055) / 1.055).powf(2.4)
            };
            assert!((f64::from(transferred) - reference).abs() < 3e-7);
            previous = transferred;
        }
        assert_eq!(source_channel_to_linear(0.0, true), 0.0);
        assert_eq!(source_channel_to_linear(1.0, true), 1.0);
        // Alpha follows the false branch even when source RGB is sRGB.
        assert_eq!(source_channel_to_linear(0.5, false), 0.5);
    }

    #[test]
    fn required_rejection_marker_survives_world_asset_reflection_copy() {
        let mut app = App::new();
        app.register_type::<SourceFragmentRejected>();
        let mut source_world = World::new();
        source_world.spawn(SourceFragmentRejected(
            "required source material result missing: Material0".to_owned(),
        ));
        let source_asset = WorldAsset::new(source_world);
        let registry = app.world().resource::<AppTypeRegistry>().clone();
        let mut copied = source_asset
            .clone_with(&registry)
            .expect("registered rejection marker must be reflect-copyable");
        let marker = copied
            .world
            .query::<&SourceFragmentRejected>()
            .single(&copied.world)
            .expect("copied WorldAsset must retain its rejection marker");
        assert_eq!(
            marker.0,
            "required source material result missing: Material0"
        );
    }
}
