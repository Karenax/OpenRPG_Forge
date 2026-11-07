//! Disposable renderer assembly for one authored imported terrain.
//! Canonical edits remain in `TerrainDocument`; this resource owns only derived assets.
use super::{
    TerrainMaterial, TerrainSettings,
    lod::{self, LodMaterial, PreparedLod},
    normals,
    paint::PreparedPaint,
    textures::PreparedSurface,
};
use crate::source_shadows::ObjectShadowSettings;
use crate::source_surface::underwater::CausticMaterialInputs;
use bevy::{camera::visibility::NoFrustumCulling, light::NotShadowCaster, prelude::*};
use forge_assets::source_terrain_package::TerrainPackage;
use forge_core::ContentHash;
use {forge_world::sampled_terrain::SampledTerrainPlan, forge_world::terrain_lod::CdlodView};
mod repaint;
pub use repaint::PreparedRepaint;

/// Lighting and water outputs supplied by the same scene's render graphs.
#[derive(Clone)]
pub struct Bindings {
    pub environment: Handle<Image>,
    pub hill_shadow: Handle<Image>,
    pub object_shadow: Handle<Image>,
    pub object_settings: ObjectShadowSettings,
    pub clouds: [Vec4; 5],
    pub cloud_shadow: Option<Handle<Image>>,
    pub shore_state: Handle<Image>,
    pub shore_lace: Handle<Image>,
    pub shore_params: [UVec4; 3],
    pub caustics: Option<CausticMaterialInputs>,
    pub settings: TerrainSettings,
}

pub struct PreparedTerrain {
    hash: ContentHash,
    relief: ContentHash,
    reuse_key: ContentHash,
    geometry: Option<PreparedLod>,
    surface: PreparedSurface,
    paint: PreparedPaint,
    normal: Vec<u8>,
    normal_dimensions: [u32; 2],
    water_level_m: Option<f64>,
    edited: bool,
    fragment: Shader,
    vertex: Shader,
    lighting_seed: crate::source_lighting::scene_uniform::Seed,
}

#[derive(Resource, Clone)]
pub struct ResidentTerrain {
    pub document_hash: ContentHash,
    pub(super) relief: ContentHash,
    pub(super) water_level_m: Option<f64>,
    pub entity: Option<Entity>,
    pub material: Option<Handle<LodMaterial>>,
    pub normals: normals::Shared,
    reuse_key: ContentHash,
    source_linear_detail: Handle<Image>,
}
#[derive(Resource)]
struct Registered;

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    normals::register(app)?;
    crate::terrain_palette::register(app);
    crate::source_lighting::local_lights::gpu::install_neutral(app.world_mut())?;
    super::paint::install_neutral(app.world_mut())?;
    crate::source_surface::underwater::install_neutral(app.world_mut())?;
    if !app.is_plugin_added::<MaterialPlugin<TerrainMaterial>>() {
        app.add_plugins(MaterialPlugin::<TerrainMaterial>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<LodMaterial>>() {
        app.add_plugins(MaterialPlugin::<LodMaterial>::default());
    }
    app.insert_resource(Registered);
    Ok(())
}

impl PreparedTerrain {
    /// Finish all terrain-dependent CPU work before replacing the displayed document.
    pub fn new(
        plan: &SampledTerrainPlan,
        heights: &[u8],
        package: &TerrainPackage,
        view: CdlodView,
    ) -> Result<Self, String> {
        require_fixed_profile(plan)?;
        require_fixed_package(package)?;
        let geometry = PreparedLod::new(plan, heights, view)?;
        Self::with_geometry(plan, package, geometry, Some(0.0))
    }

    pub(super) fn for_host(
        plan: &SampledTerrainPlan,
        heights: &[u8],
        package: &TerrainPackage,
        geometry: &lod::ViewGeometry,
        water_level_m: Option<f64>,
    ) -> Result<Self, String> {
        Self::with_geometry(
            plan,
            package,
            Some(PreparedLod::resident_root(plan, heights, geometry)?),
            water_level_m,
        )
    }

    fn with_geometry(
        plan: &SampledTerrainPlan,
        package: &TerrainPackage,
        geometry: Option<PreparedLod>,
        water_level_m: Option<f64>,
    ) -> Result<Self, String> {
        require_matching_package(plan, package)?;
        if water_level_m.is_some_and(|level| !level.is_finite()) {
            return Err("Nonfinite terrain AO water level".into());
        }
        let document = plan.document();
        let surface = PreparedSurface::from_package(package)?;
        let paint = PreparedPaint::new(plan)?;
        let mut normal = package
            .file("terrain-normal.bin")
            .ok_or("Missing normal map")?
            .to_vec();
        let edited =
            !document.height_deltas.is_empty() || document.height_sampling.base_elevation_m != 0.0;
        if edited {
            normals::update_base_with_water_level(plan, &mut normal, water_level_m)?;
        }
        let (surface_program, lod_program) = super::programs::for_package(package)?;
        // This authored path installs its own shader and bypasses install_shader.
        // It must read the matrices of the depth layers encoded by the live graph.
        let painted = PreparedPaint::editable_shader(surface_program)?;
        let painted = super::sampling::prepare_surface_shader(&painted, document)?;
        let painted = super::sampling::prepare_hill_shadow_shader(&painted, document)?;
        let lighting = crate::source_lighting::scene_uniform::prepare(&painted)?;
        let fragment = crate::source_shadows::receiver::shader_source(&lighting.shader)?;
        let fragment =
            crate::source_surface::underwater::receivers::terrain_shader_source(&fragment)?;
        let fragment = Shader::from_wgsl(
            crate::source_lighting::local_lights::receiver::terrain_shader_source(&fragment)?,
            "source-package-terrain.wgsl",
        );
        let corrections = crate::sampled_terrain_corrections::Corrections::from_document(document)?;
        let vertex = super::sampling::prepare_shader(lod_program, document)?;
        let vertex = super::sampling::clip_vertex_shader(&vertex, document)?;
        let vertex = corrections.shader(&vertex, "#{MATERIAL_BIND_GROUP}", 102)?;
        Ok(Self {
            hash: plan.document_hash(),
            relief: super::sampling::relief_identity(plan)?,
            reuse_key: repaint::key(plan.document(), package.identity())?,
            geometry,
            surface,
            paint,
            normal,
            normal_dimensions: package.texture_dimensions().normal,
            water_level_m,
            edited,
            fragment,
            lighting_seed: lighting.seed,
            vertex: Shader::from_wgsl(vertex, "source-edited-terrain.wgsl"),
        })
    }

    /// Replace one derived terrain after validating the current world's dependencies.
    /// This does not acknowledge physics, GPU completion or persistence to the editor.
    pub fn stage(self, world: &mut World, bindings: Bindings) -> Result<ResidentTerrain, String> {
        if !world.contains_resource::<Registered>()
            || !world.contains_resource::<Assets<Shader>>()
            || !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<LodMaterial>>()
        {
            return Err("Imported terrain renderer is not registered".into());
        }
        bindings.validate(
            world
                .get_resource::<Assets<Image>>()
                .ok_or("Terrain images unavailable")?,
        )?;
        crate::source_lighting::scene_buffer::install(world, &self.lighting_seed)?;
        super::paint::install_neutral(world)?;
        let normal = normals::Shared::stage_with_dimensions(
            &self.normal,
            self.normal_dimensions,
            self.edited,
            &mut world.resource_mut::<Assets<Image>>(),
        )?;
        stage_programs(world, self.fragment, self.vertex);
        let mut entity = None;
        let mut material = None;
        let mut source_linear_detail = Handle::default();
        if let Some(geometry) = self.geometry {
            let mesh = world.resource_mut::<Assets<Mesh>>().add(geometry.mesh);
            let paint = self.paint.stage(world)?;
            let mut images = world.resource_mut::<Assets<Image>>();
            let surface = self.surface.stage(&mut images);
            source_linear_detail = surface.linear_detail.clone();
            let caustics = bindings
                .caustics
                .clone()
                .unwrap_or_else(|| CausticMaterialInputs::disabled(&mut images));
            let extension = lod::LodExtension {
                height: images.add(geometry.height),
                corrections: images.add(geometry.corrections),
                settings: geometry.settings,
            };
            let base = TerrainMaterial {
                local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
                paint_weights: paint.weights,
                paint_parameters: paint.parameters,
                scene_lighting: crate::source_lighting::scene_buffer::HANDLE.clone(),
                normal: normal.image.clone(),
                splat: surface.splat,
                detail: surface.detail,
                linear_detail: paint.detail.unwrap_or(surface.linear_detail),
                environment: bindings.environment,
                hill_shadow: bindings.hill_shadow,
                object_shadow: bindings.object_shadow,
                object_settings: bindings.object_settings,
                live_shadows: crate::source_shadows::receiver::DEFAULT_BUFFER.clone(),
                clouds: bindings.clouds,
                cloud_shadow: bindings.cloud_shadow,
                shore_state: bindings.shore_state,
                shore_lace: bindings.shore_lace,
                shore_params: bindings.shore_params,
                caustic_fine: caustics.fine,
                caustic_broad: caustics.broad,
                caustic_params: caustics.params,
                underwater_waves: caustics.light_maps.waves,
                underwater_level: caustics.light_maps.level,
                underwater_map_frame: caustics.light_maps.frame,
                settings: bindings.settings,
            };
            let handle = world
                .resource_mut::<Assets<LodMaterial>>()
                .add(LodMaterial { base, extension });
            entity = Some(
                world
                    .spawn((
                        Mesh3d(mesh),
                        MeshMaterial3d(handle.clone()),
                        NoFrustumCulling,
                        NotShadowCaster,
                    ))
                    .id(),
            );
            material = Some(handle);
        }
        let resident = ResidentTerrain {
            document_hash: self.hash,
            relief: self.relief,
            water_level_m: self.water_level_m,
            reuse_key: self.reuse_key,
            source_linear_detail,
            entity,
            material,
            normals: normal.clone(),
        };
        if let Some(previous) = world.remove_resource::<ResidentTerrain>()
            && let Some(entity) = previous.entity
        {
            world.despawn(entity);
        }
        world.insert_resource(normal);
        world.insert_resource(resident.clone());
        Ok(resident)
    }
}

/// The direct legacy scene API is still limited to the acquired reference;
/// the managed host has explicit domain, water and surface-generation inputs.
pub(super) fn require_fixed_profile(plan: &SampledTerrainPlan) -> Result<(), String> {
    if !super::supports_fixed_source_raster(plan.raster().source()) {
        return Err("Terrain scene requires its admitted fixed raster profile".into());
    }
    Ok(())
}

fn require_fixed_package(package: &TerrainPackage) -> Result<(), String> {
    if package.raster_geometry()
        != forge_assets::source_terrain_package::supported_raster_geometry()
    {
        return Err("Terrain package does not match the admitted fixed raster profile".into());
    }
    Ok(())
}

/// A verified package and a verified raster must describe the same domain;
/// provenance and the raster content hash are admitted by their own owners.
pub(super) fn require_matching_package(
    plan: &SampledTerrainPlan,
    package: &TerrainPackage,
) -> Result<(), String> {
    let source = plan.raster().source();
    let document = plan.document();
    let geometry = package.raster_geometry();
    if !geometry.matches_sampled_domain(
        [source.width, source.height],
        source.origin_xz_m,
        source.sample_spacing_m,
        source.outside_height_m,
        [document.extent_m.size_x_m, document.extent_m.size_z_m],
        document.height_sampling.sample_spacing_m,
    ) {
        return Err("Terrain package and sampled raster describe different domains".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests;

fn stage_programs(world: &mut World, fragment: Shader, vertex: Shader) {
    // Fixed UUID handles cannot produce an indexed-generation insertion failure.
    let mut shaders = world.resource_mut::<Assets<Shader>>();
    shaders
        .insert(super::TERRAIN_SHADER.id(), fragment)
        .expect("UUID shader handle");
    shaders
        .insert(lod::EDITED_SHADER.id(), vertex)
        .expect("UUID shader handle");
}

impl Bindings {
    fn validate(&self, images: &Assets<Image>) -> Result<(), String> {
        for handle in [
            &self.environment,
            &self.hill_shadow,
            &self.object_shadow,
            &self.shore_state,
            &self.shore_lace,
        ]
        .into_iter()
        .chain(self.cloud_shadow.iter())
        .chain(self.caustics.iter().flat_map(|inputs| {
            [
                &inputs.fine,
                &inputs.broad,
                &inputs.light_maps.waves,
                &inputs.light_maps.level,
            ]
        })) {
            if images.get(handle).is_none() {
                return Err("Unresolved terrain scene texture".into());
            }
        }
        let s = &self.settings;
        let o = &self.object_settings;
        if [s.wind, s.sun, s.domain, s.flags, o.camera, o.forward]
            .iter()
            .chain(&self.clouds)
            .any(|v| !v.is_finite())
            || o.matrices.iter().any(|m| !m.is_finite())
            || o.cascades.iter().chain(&o.blend).any(|v| !v.is_finite())
            || [
                o.map_size,
                o.bias,
                o.fade,
                o.enabled,
                o.sun_angular_diameter,
            ]
            .iter()
            .any(|v| !v.is_finite())
        {
            return Err("Nonfinite terrain scene settings".into());
        }
        Ok(())
    }
}
