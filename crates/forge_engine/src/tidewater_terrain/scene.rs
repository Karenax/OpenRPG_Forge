//! Disposable renderer assembly for one authored imported terrain.
//! Canonical edits remain in `TerrainDocument`; this resource owns only derived assets.
use super::{
    TerrainMaterial, TerrainSettings,
    lod::{self, LodMaterial, PreparedLod},
    normals,
    textures::PreparedSurface,
};
use crate::tidewater_material::ObjectShadowSettings;
use bevy::{camera::visibility::NoFrustumCulling, light::NotShadowCaster, prelude::*};
use forge_assets::tidewater_terrain::TerrainPackage;
use forge_core::ContentHash;
use forge_world::{cdlod::CdlodView, sampled_terrain::SampledTerrainPlan};

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
    pub settings: TerrainSettings,
}

pub struct PreparedTerrain {
    hash: ContentHash,
    geometry: Option<PreparedLod>,
    surface: PreparedSurface,
    normal: Vec<u8>,
    edited: bool,
    fragment: Shader,
    vertex: Shader,
}

#[derive(Resource, Clone)]
pub struct ResidentTerrain {
    pub document_hash: ContentHash,
    pub entity: Option<Entity>,
    pub material: Option<Handle<LodMaterial>>,
    pub normals: normals::Shared,
}
#[derive(Resource)]
struct Registered;

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    normals::register(app)?;
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
        let document = plan.document();
        let geometry = PreparedLod::new(plan, heights, view)?;
        let surface = PreparedSurface::from_package(package)?;
        let mut normal = package
            .file("terrain-normal.bin")
            .ok_or("Missing normal map")?
            .to_vec();
        let edited =
            !document.height_deltas.is_empty() || document.height_sampling.base_elevation_m != 0.0;
        if edited {
            normals::update_base(plan, &mut normal)?;
        }
        let text = |name| {
            std::str::from_utf8(package.file(name).ok_or("Missing terrain shader")?)
                .map_err(|e| e.to_string())
        };
        let fragment = Shader::from_wgsl(
            text("terrain.wgsl")?.to_owned(),
            "tidewater-package-terrain.wgsl",
        );
        let corrections = crate::tidewater_terrain_edits::Corrections::from_document(document)?;
        let vertex =
            corrections.shader(text("terrain-lod.wgsl")?, "#{MATERIAL_BIND_GROUP}", 102)?;
        Ok(Self {
            hash: plan.document_hash(),
            geometry,
            surface,
            normal,
            edited,
            fragment,
            vertex: Shader::from_wgsl(vertex, "tidewater-edited-terrain.wgsl"),
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
        let normal = normals::Shared::stage(
            &self.normal,
            self.edited,
            &mut world.resource_mut::<Assets<Image>>(),
        )?;
        // These fixed UUID handles cannot produce an indexed-generation insertion failure.
        {
            let mut shaders = world.resource_mut::<Assets<Shader>>();
            shaders
                .insert(super::TERRAIN_SHADER.id(), self.fragment)
                .expect("UUID shader handle");
            shaders
                .insert(lod::EDITED_SHADER.id(), self.vertex)
                .expect("UUID shader handle");
        }
        let mut entity = None;
        let mut material = None;
        if let Some(geometry) = self.geometry {
            let mesh = world.resource_mut::<Assets<Mesh>>().add(geometry.mesh);
            let mut images = world.resource_mut::<Assets<Image>>();
            let surface = self.surface.stage(&mut images);
            let extension = lod::LodExtension {
                height: images.add(geometry.height),
                corrections: images.add(geometry.corrections),
                settings: geometry.settings,
            };
            let base = TerrainMaterial {
                normal: normal.image.clone(),
                splat: surface.splat,
                detail: surface.detail,
                linear_detail: surface.linear_detail,
                environment: bindings.environment,
                hill_shadow: bindings.hill_shadow,
                object_shadow: bindings.object_shadow,
                object_settings: bindings.object_settings,
                clouds: bindings.clouds,
                cloud_shadow: bindings.cloud_shadow,
                shore_state: bindings.shore_state,
                shore_lace: bindings.shore_lace,
                shore_params: bindings.shore_params,
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
        {
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
