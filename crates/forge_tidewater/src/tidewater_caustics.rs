//! Photon-splat caustic map geometry for the pinned Tidewater ocean.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/ocean/Caustics.js` at 1438b1ab,
// copyright 2026 DRG Software Solutions LLC. See `third_party/tidewater/LICENSE`.

use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, ShaderType, TextureDimension, TextureFormat},
};

mod gpu;
pub use gpu::{CausticMaps, prepare_maps, render_maps};

pub const PHOTON_SHADER: &str = include_str!("tidewater_caustics.wgsl");
/// Material-side lookup of the two photon maps. The caller supplies the
/// water-column depth, surface slope, foam and sea-detail factor.
pub const SAMPLE_SHADER: &str = include_str!("tidewater_caustics/sample.wgsl");
const VILLAGE_LITE_SHADER: &str = include_str!("tidewater_caustics/village_lite.wgsl");
const LIGHT_MAP_LOOKUP: &str = include_str!("tidewater_caustics/light_map_lookup.wgsl");
const UNDERWATER_SHADOW: &str = include_str!("tidewater_caustics/underwater_shadow.wgsl");

#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct CausticSampleParams {
    /// Direction from water toward the sun, strength in W.
    pub sun_and_strength: Vec4,
    /// Fine and broad FFT tile size in metres.
    pub tiles_m: Vec4,
    /// Four FFT cascade tile sizes, coarsest first.
    pub wave_tiles_m: Vec4,
    /// XYZ: absorption coefficients. W: refracted shadow-entry control.
    pub water_absorption: Vec4,
    pub water_scattering: Vec4,
}

impl CausticSampleParams {
    pub fn new(
        fine_tile_m: f32,
        broad_tile_m: f32,
        sun_direction: Vec3,
        strength: f32,
    ) -> Result<Self, String> {
        if !fine_tile_m.is_finite()
            || fine_tile_m <= 0.0
            || !broad_tile_m.is_finite()
            || broad_tile_m <= fine_tile_m
            || !sun_direction.is_finite()
            || (sun_direction.length_squared() - 1.0).abs() > 0.001
            || sun_direction.y <= 0.0
            || !strength.is_finite()
            || !(0.0..=4.0).contains(&strength)
        {
            return Err("Invalid Tidewater caustic sampling inputs".into());
        }
        Ok(Self {
            sun_and_strength: sun_direction.extend(strength),
            tiles_m: Vec4::new(fine_tile_m, broad_tile_m, 0.0, 0.0),
            wave_tiles_m: Vec4::ZERO,
            water_absorption: Vec4::ZERO,
            water_scattering: Vec4::ZERO,
        })
    }

    pub fn with_underwater(
        mut self,
        wave_tiles_m: [f32; 4],
        sea_level_m: f32,
        reach_m: f32,
        absorption: Vec3,
        scattering: Vec3,
    ) -> Result<Self, String> {
        if wave_tiles_m
            .iter()
            .any(|size| !size.is_finite() || *size <= 0.0)
            || wave_tiles_m.windows(2).any(|pair| pair[0] <= pair[1])
            || !sea_level_m.is_finite()
            || !reach_m.is_finite()
            || !(0.0..=20.0).contains(&reach_m)
            || !absorption.is_finite()
            || !scattering.is_finite()
            || absorption.min_element() < 0.0
            || scattering.min_element() < 0.0
        {
            return Err("Invalid Tidewater underwater lighting inputs".into());
        }
        self.tiles_m.z = sea_level_m;
        self.tiles_m.w = reach_m;
        self.wave_tiles_m = Vec4::from_array(wave_tiles_m);
        self.water_absorption = absorption.extend(1.0);
        self.water_scattering = scattering.extend(1.0);
        Ok(self)
    }
}

#[derive(Resource, Clone)]
pub struct CausticMaterialInputs {
    pub fine: Handle<Image>,
    pub broad: Handle<Image>,
    pub light_maps: crate::tidewater_underwater_lighting::Bindings,
    pub params: CausticSampleParams,
}

impl CausticMaterialInputs {
    /// Valid bindable textures for scenes whose ocean has not been installed.
    /// The disabled bit in the scattering uniform makes their modulation neutral.
    pub fn disabled(images: &mut Assets<Image>) -> Self {
        let mut plain = Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            &[0, 60, 0, 60, 0, 0, 0, 0],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        plain.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::Repeat,
            address_mode_v: ImageAddressMode::Repeat,
            ..default()
        });
        let plain = images.add(plain);
        let array = images.add(Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 2,
            },
            TextureDimension::D2,
            &[0; 8],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        ));
        Self {
            fine: plain.clone(),
            broad: plain,
            light_maps: crate::tidewater_underwater_lighting::Bindings {
                waves: array.clone(),
                level: array,
                frame: crate::tidewater_underwater_lighting::NEUTRAL_FRAME,
            },
            params: CausticSampleParams::new(1.0, 2.0, Vec3::Y, 0.0)
                .expect("neutral caustic inputs"),
        }
    }
}

/// Extend a verified source-lighting terrain shader with the material-side
/// underwater direct and ambient terms. Earlier diagnostic shaders without
/// source lighting are intentionally left untouched.
pub fn terrain_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)+s.emissive;";
    if !source.contains("fn sourceSceneLight(") {
        return Ok(source.to_owned());
    }
    if source.matches(MARKER).count() != 1 || source.contains("fn terrainUnderwaterDirect(") {
        return Err("Unsupported Tidewater terrain lighting shader".into());
    }
    let source = refract_shadow_receiver(source)?.replace(
        MARKER,
        "*terrainUnderwaterDirect(P,N)+sourceEnvironmentLight(s,N,V)*terrainUnderwaterAmbient(P)+s.emissive;",
    );
    // Tidewater's Shader.js prepends this directive to its WGSL modules. Its
    // materials sample textures and take derivatives in data-dependent surface
    // branches; without it browser WebGPU rejects the shader and Studio's
    // rendering app exits before terrain picking can run.
    let sample = terrain_sample_source();
    Ok(format!(
        "diagnostic( off, derivative_uniformity );\n{source}\n{sample}"
    ))
}

/// The pinned scene gives buildings the `lite` underwater mode: attenuate
/// direct and ambient light without photon-map caustics. Keep emissive separate.
pub fn village_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)+s.emissive;";
    if !source.contains("fn sourceSceneLight(") {
        return Ok(source.to_owned());
    }
    if source.matches(MARKER).count() != 1 || source.contains("fn villageUnderwaterDirect(") {
        return Err("Unsupported Tidewater village lighting shader".into());
    }
    let source = refract_shadow_receiver(source)?.replace(
        MARKER,
        "*villageUnderwaterDirect(P)+sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive;",
    );
    Ok(format!("{source}\n{}", village_sample_source()))
}

/// Adapt only the scene-lighting lookup, never the shadow helper declaration,
/// hill/cloud modulation or the original receiver used for surface lighting.
fn refract_shadow_receiver(source: &str) -> Result<String, String> {
    const CALL: &str = "*sunShadow(P,N,pixel)";
    if source.matches(CALL).count() != 1 {
        return Err("Tidewater scene shadow-position call drift".into());
    }
    Ok(source.replace(
        CALL,
        "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)",
    ))
}

fn terrain_sample_source() -> String {
    let lookup = LIGHT_MAP_LOOKUP
        .replace("UW_SEA_LEVEL", "causticsParams.tiles_m.z")
        .replace("uwWaves", "causticsWaves")
        .replace("uwLevel", "causticsLevel")
        .replace("uwLinear", "causticsRepeat");
    let shadow = UNDERWATER_SHADOW
        .replace("UW_SHADOW_PARAMS", "causticsParams")
        .replace("UW_SHADOW_WAVES", "true");
    format!("{SAMPLE_SHADER}\n{lookup}\n{shadow}")
}
fn village_sample_source() -> String {
    let lookup = LIGHT_MAP_LOOKUP
        .replace(
            "        if (waves) { a = textureSampleLevel(uwWaves, uwLinear, uv, layer, 0.0); }",
            "",
        )
        .replace("UW_SEA_LEVEL", "villageUnderwaterParams.tiles_m.z")
        .replace("uwLevel", "villageOceanLevel")
        .replace("uwLinear", "villageOceanRepeat");
    let shadow = UNDERWATER_SHADOW
        .replace("UW_SHADOW_PARAMS", "villageUnderwaterParams")
        .replace("UW_SHADOW_WAVES", "false");
    format!("{VILLAGE_LITE_SHADER}\n{lookup}\n{shadow}")
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct CausticLayerSpec {
    resolution: u32,
    grid: u32,
    margin: f32,
    slope_lod: f32,
    focal_depths_m: [f32; 2],
    /// Zero is the finest FFT cascade, one is the next broader cascade.
    cascade_from_finest: u32,
}

pub const FINE: CausticLayerSpec = CausticLayerSpec {
    resolution: 512,
    // Tidewater expands its 256-cell base grid over [-0.35, 1.35]: ceil(256 * 1.7).
    grid: 436,
    margin: 0.35,
    slope_lod: 1.0,
    focal_depths_m: [1.2, 4.0],
    cascade_from_finest: 0,
};

pub const BROAD: CausticLayerSpec = CausticLayerSpec {
    resolution: 256,
    // Likewise, ceil(128 * 1.7) for the next broader FFT cascade.
    grid: 218,
    margin: 0.35,
    slope_lod: 0.5,
    focal_depths_m: [3.0, 9.0],
    cascade_from_finest: 1,
};

#[derive(Clone, Debug, ShaderType)]
pub struct PhotonParams {
    /// Unit direction from water toward the sun.
    pub sun_direction: Vec4,
    /// FFT tile metres, grid size, cascade index, derivative LOD.
    pub geometry: Vec4,
    /// Focal depth metres, output channel (0 R / 1 G), grid margin.
    pub optics: Vec4,
}

impl CausticLayerSpec {
    #[must_use]
    pub const fn resolution(self) -> u32 {
        self.resolution
    }

    #[must_use]
    pub const fn grid(self) -> u32 {
        self.grid
    }

    // Only FINE/BROAD can be constructed outside this module. Their grid,
    // vertex coordinates, cascade and focal index are all exactly representable
    // by f32; the casts transfer these integer shader parameters losslessly.
    #[expect(
        clippy::cast_precision_loss,
        reason = "pinned caustic bounds fit exactly in f32"
    )]
    pub fn parameters(
        self,
        cascade_count: u32,
        tile_m: f32,
        sun_direction: Vec3,
        focal_plane: usize,
    ) -> Result<PhotonParams, String> {
        if !(2..=8).contains(&cascade_count)
            || self.cascade_from_finest >= cascade_count
            || self.grid == 0
            || focal_plane > 1
            || !tile_m.is_finite()
            || tile_m <= 0.0
            || !sun_direction.is_finite()
            || (sun_direction.length_squared() - 1.0).abs() > 0.001
            || sun_direction.y <= 0.0
        {
            return Err("Invalid Tidewater caustic photon frame".into());
        }
        let cascade = cascade_count - 1 - self.cascade_from_finest;
        Ok(PhotonParams {
            sun_direction: sun_direction.extend(0.0),
            geometry: Vec4::new(tile_m, self.grid as f32, cascade as f32, self.slope_lod),
            optics: Vec4::new(
                self.focal_depths_m[focal_plane],
                focal_plane as f32,
                self.margin,
                0.0,
            ),
        })
    }

    /// Six indexed vertices per grid cell; the GPU transforms each shared
    /// photon once via its post-transform vertex cache.
    #[must_use]
    pub fn grid_indices(self) -> Vec<u32> {
        let row = self.grid + 1;
        let mut indices = Vec::with_capacity((self.grid * self.grid * 6) as usize);
        for y in 0..self.grid {
            for x in 0..self.grid {
                let a = y * row + x;
                indices.extend([a, a + 1, a + row, a + row, a + 1, a + row + 1]);
            }
        }
        indices
    }

    #[must_use]
    #[expect(
        clippy::cast_precision_loss,
        reason = "pinned caustic bounds fit exactly in f32"
    )]
    pub fn source_uv(self, vertex: u32) -> Option<Vec2> {
        let row = self.grid + 1;
        if vertex >= row * row {
            return None;
        }
        let cell = Vec2::new((vertex % row) as f32, (vertex / row) as f32);
        Some(cell / self.grid as f32 * (1.0 + 2.0 * self.margin) - Vec2::splat(self.margin))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pinned_grids_cover_neighboring_fft_tiles_and_share_vertices() {
        for spec in [FINE, BROAD] {
            let indices = spec.grid_indices();
            assert_eq!(indices.len(), (spec.grid * spec.grid * 6) as usize);
            assert_eq!(
                &indices[..6],
                &[0, 1, spec.grid + 1, spec.grid + 1, 1, spec.grid + 2]
            );
            assert!((spec.source_uv(0).unwrap() - Vec2::splat(-0.35)).length() < 1e-5);
            assert!(
                (spec.source_uv((spec.grid + 1).pow(2) - 1).unwrap() - Vec2::splat(1.35)).length()
                    < 1e-5
            );
            assert!(spec.source_uv((spec.grid + 1).pow(2)).is_none());
        }
    }

    #[test]
    fn two_cascades_and_focal_planes_match_the_pinned_reference() {
        let sun = Vec3::new(0.0, 1.0, 0.0);
        let fine = FINE.parameters(4, 32.0, sun, 0).unwrap();
        let broad = BROAD.parameters(4, 128.0, sun, 1).unwrap();
        assert_eq!(fine.geometry, Vec4::new(32.0, 436.0, 3.0, 1.0));
        assert_eq!(fine.optics, Vec4::new(1.2, 0.0, 0.35, 0.0));
        assert_eq!(broad.geometry, Vec4::new(128.0, 218.0, 2.0, 0.5));
        assert_eq!(broad.optics, Vec4::new(9.0, 1.0, 0.35, 0.0));
        assert!(FINE.parameters(1, 32.0, sun, 0).is_err());
        assert!(BROAD.parameters(4, -1.0, sun, 0).is_err());
    }

    #[test]
    fn photon_shader_parses_and_validates_for_gpu() {
        let shader = naga::front::wgsl::parse_str(PHOTON_SHADER).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
    }

    #[test]
    fn material_sampler_parses_and_validates_for_gpu() {
        let source = terrain_sample_source().replace("#{MATERIAL_BIND_GROUP}", "0");
        let source = format!(
            "{source}\n@fragment fn fragment() -> @location(0) vec4f {{\n\
             return vec4f(causticsSampleBaked(vec3f(0.0, -2.0, 0.0), 2.0, \
             vec2f(0.0), 0.0, vec2f(0.01), vec2f(0.01), 1.0), 1.0);\n}}"
        );
        let shader = naga::front::wgsl::parse_str(&source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "The shader control is an exact 0/1 flag, not an approximate optical measurement."
    )]
    fn sample_params_reject_invalid_physical_inputs() {
        let sun = Vec3::Y;
        assert!(CausticSampleParams::new(32.0, 128.0, sun, 0.75).is_ok());
        assert!(CausticSampleParams::new(32.0, 32.0, sun, 0.75).is_err());
        assert!(CausticSampleParams::new(32.0, 128.0, -sun, 0.75).is_err());
        assert!(CausticSampleParams::new(32.0, 128.0, sun, f32::NAN).is_err());
        let enabled = CausticSampleParams::new(32.0, 128.0, sun, 0.75)
            .unwrap()
            .with_underwater(
                [512.0, 256.0, 128.0, 32.0],
                0.0,
                3.0,
                Vec3::ZERO,
                Vec3::ZERO,
            )
            .unwrap();
        assert_eq!(enabled.water_scattering.w, 1.0);
        assert_eq!(
            CausticSampleParams::new(32.0, 128.0, sun, 0.0)
                .unwrap()
                .water_scattering
                .w,
            0.0
        );
        assert!(
            enabled
                .with_underwater(
                    [32.0, 128.0, 256.0, 512.0],
                    0.0,
                    3.0,
                    Vec3::ZERO,
                    Vec3::ZERO,
                )
                .is_err()
        );
    }

    #[test]
    fn terrain_lighting_patch_keeps_emissive_outside_water_attenuation() {
        let shader = "fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f { return sourceDirectLight(s)*sunShadow(P,N,pixel)+sourceEnvironmentLight(s,N,V)+s.emissive; }";
        let patched = terrain_shader_source(shader).unwrap();
        assert!(patched.starts_with("diagnostic( off, derivative_uniformity );\n"));
        assert!(patched.contains(
            "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)*terrainUnderwaterDirect(P,N)"
        ));
        assert!(
            patched
                .contains("sourceEnvironmentLight(s,N,V)*terrainUnderwaterAmbient(P)+s.emissive")
        );
        assert!(terrain_shader_source(&patched).is_err());
    }

    #[test]
    fn village_lite_shader_parses_and_keeps_emissive_unattenuated() {
        let source = village_sample_source().replace("#{MATERIAL_BIND_GROUP}", "0");
        let source = format!(
            "{source}\n@fragment fn fragment() -> @location(0) vec4f {{\n\
             let P = vec3f(0.0, -2.0, 0.0);\n\
             return vec4f(villageUnderwaterDirect(P) + villageUnderwaterAmbient(P), 1.0);\n}}"
        );
        let shader = naga::front::wgsl::parse_str(&source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
        let scene = "fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f { return sourceDirectLight(s)*sunShadow(P,N,pixel)+sourceEnvironmentLight(s,N,V)+s.emissive; }";
        let patched = village_shader_source(scene).unwrap();
        assert!(patched.contains(
            "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)*villageUnderwaterDirect(P)"
        ));
        assert!(
            patched
                .contains("sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive")
        );
        assert!(village_shader_source(&patched).is_err());
    }

    #[test]
    fn shadow_entry_patch_rejects_missing_or_duplicate_calls() {
        assert!(refract_shadow_receiver("fn sourceSceneLight() {}").is_err());
        assert!(refract_shadow_receiver("*sunShadow(P,N,pixel)*sunShadow(P,N,pixel)").is_err());
        let source = "terrainSunShadowAt(P)*cloudsShadow(P.xz)*sunShadow(P,N,pixel)";
        let patched = refract_shadow_receiver(source).unwrap();
        assert!(patched.starts_with("terrainSunShadowAt(P)*cloudsShadow(P.xz)"));
        assert!(patched.ends_with("*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)"));
    }
}
