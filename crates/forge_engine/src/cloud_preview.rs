//! Explicit bounded preview; not a qualified environment preset or volumetric cloud model.
use bevy::{
    asset::{RenderAssetUsages, embedded_asset},
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    light::{NotShadowCaster, NotShadowReceiver},
    prelude::*,
    render::render_resource::{AsBindGroup, Extent3d, TextureDimension, TextureFormat},
    shader::ShaderRef,
};
use forge_project::CloudLayerSettings;
use serde::Serialize;

#[derive(Clone, Debug, Resource, Serialize)]
pub struct CloudPreviewObservation {
    pub status: &'static str,
    pub settings: CloudLayerSettings,
    pub settings_hash: String,
    pub virtual_seconds: f64,
    pub offset_uv: Option<[f32; 2]>,
    pub dome_entities: usize,
    pub visible_domes: usize,
    pub texture_bytes: usize,
    pub mip_levels: u32,
    pub density_dimension: &'static str,
    pub maximum_view_steps: u32,
    pub requested_quality_tier: Option<forge_project::VisualQualityTier>,
    pub layer_depth_m: f32,
    pub gpu_execution_verified: bool,
}

impl CloudPreviewObservation {
    fn new(settings: CloudLayerSettings) -> Self {
        Self {
            status: "awaiting_cpu_projection",
            settings,
            settings_hash: forge_core::canonical_json_sha256(&settings)
                .expect("validated finite cloud settings have canonical JSON")
                .to_string(),
            virtual_seconds: 0.0,
            offset_uv: None,
            dome_entities: 0,
            visible_domes: 0,
            texture_bytes: 64 * 64 * 64,
            mip_levels: 1,
            density_dimension: "3d",
            maximum_view_steps: 24,
            requested_quality_tier: None,
            layer_depth_m: settings.pattern_scale_m * forge_world::CLOUD_LAYER_DEPTH_RATIO,
            gpu_execution_verified: false,
        }
    }
}

#[derive(Asset, AsBindGroup, TypePath, Clone, Debug)]
#[cfg_attr(test, derive(Default))]
pub(crate) struct CloudMaterial {
    #[texture(0, dimension = "3d")]
    #[sampler(1)]
    pub(crate) density: Handle<Image>,
    #[uniform(2)]
    layer: Vec4,
    #[uniform(2)]
    offset_sun: Vec4,
    #[uniform(2)]
    radiance: Vec4,
    #[uniform(2)]
    march: Vec4,
}

impl Material for CloudMaterial {
    fn fragment_shader() -> ShaderRef {
        "embedded://forge_engine/shaders/cloud_layer_preview.wgsl".into()
    }
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Blend
    }
    fn enable_prepass() -> bool {
        false
    }
    fn depth_bias(&self) -> f32 {
        // The dome's centre is the camera: sort at the far plane, not over foreground foliage.
        // Custom material does not apply this bias to the geometric depth test.
        self.layer.w
    }
}

#[derive(Resource)]
struct CloudPreview(CloudLayerSettings);
#[derive(Component)]
struct CloudDome;
type CloudCameraFilter = (With<Camera3d>, Without<CloudDome>);

pub struct CloudLayerPreviewPlugin {
    settings: CloudLayerSettings,
    density: forge_world::CloudDensityVolume,
}

impl CloudLayerPreviewPlugin {
    pub fn new(settings: CloudLayerSettings) -> Result<Self, forge_world::CloudGenerationError> {
        Ok(Self {
            settings,
            density: forge_world::generate_cloud_density_volume(settings, 64)?,
        })
    }
}

impl Plugin for CloudLayerPreviewPlugin {
    fn build(&self, app: &mut App) {
        embedded_asset!(app, "shaders/cloud_layer_preview.wgsl");
        app.add_plugins(MaterialPlugin::<CloudMaterial>::default());
        let image = volume_image(&self.density);
        let density = app.world_mut().resource_mut::<Assets<Image>>().add(image);
        let material = app
            .world_mut()
            .resource_mut::<Assets<CloudMaterial>>()
            .add(CloudMaterial {
                density,
                layer: Vec4::new(
                    self.settings.altitude_m,
                    self.settings.pattern_scale_m,
                    self.settings.opacity,
                    0.0,
                ),
                offset_sun: Vec4::ZERO,
                radiance: Vec4::ZERO,
                march: Vec4::new(24.0, forge_world::CLOUD_LAYER_DEPTH_RATIO, 0.0, 0.0),
            });
        let mut sphere = Sphere::new(1.0).mesh().uv(32, 16);
        sphere
            .invert_winding()
            .expect("triangle-list sphere supports winding inversion");
        let mesh = app.world_mut().resource_mut::<Assets<Mesh>>().add(sphere);
        app.world_mut().spawn((
            Name::new("Experimental cloud layer"),
            CloudDome,
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::default(),
            Visibility::Hidden,
            NotShadowCaster,
            NotShadowReceiver,
        ));
        app.insert_resource(CloudPreview(self.settings))
            .insert_resource(CloudPreviewObservation::new(self.settings))
            .add_systems(
                PostUpdate,
                update_clouds.after(bevy::transform::TransformSystems::Propagate),
            );
        warn!(
            "Experimental cloud volume: R8 64 cubed, requested-tier preview budget; no terrain cloud shadows, cloud content in IBL or quality qualification"
        );
    }
}

fn volume_image(volume: &forge_world::CloudDensityVolume) -> Image {
    let side = u32::from(volume.resolution);
    let mut image = Image::new(
        Extent3d {
            width: side,
            height: side,
            depth_or_array_layers: side,
        },
        TextureDimension::D3,
        volume.samples.clone(),
        TextureFormat::R8Unorm,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::ClampToEdge,
        address_mode_w: ImageAddressMode::Repeat,
        min_filter: ImageFilterMode::Linear,
        mag_filter: ImageFilterMode::Linear,
        ..default()
    });
    image
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn update_clouds(
    inputs: (
        Res<CloudPreview>,
        Option<Res<crate::environment::EnvironmentQualityRequest>>,
    ),
    time: Res<Time<Virtual>>,
    cameras: Query<(&Camera, &GlobalTransform, &Projection), CloudCameraFilter>,
    suns: Query<(&DirectionalLight, &GlobalTransform), Without<CloudDome>>,
    mut domes: Query<
        (
            &mut Transform,
            &mut GlobalTransform,
            &mut Visibility,
            &MeshMaterial3d<CloudMaterial>,
        ),
        With<CloudDome>,
    >,
    mut materials: ResMut<Assets<CloudMaterial>>,
    mut observation: ResMut<CloudPreviewObservation>,
) {
    let (settings, quality) = inputs;
    let tier = quality.as_ref().map(|request| request.requested_tier());
    let steps = forge_project::cloud_preview_view_steps(
        tier.unwrap_or(forge_project::VisualQualityTier::High),
    );
    observation.requested_quality_tier = tier;
    observation.maximum_view_steps = u32::from(steps);
    let mut active = cameras.iter().filter(|(camera, ..)| camera.is_active);
    let camera = active.next();
    let ambiguous = active.next().is_some();
    observation.dome_entities = domes.iter().count();
    observation.visible_domes = 0;
    observation.offset_uv = None;
    observation.virtual_seconds = time.elapsed_secs_f64();
    observation.status = "missing_dome";
    for (mut transform, mut global, mut visible, handle) in &mut domes {
        *visible = Visibility::Hidden;
        observation.status = "ambiguous_dome";
        if observation.dome_entities != 1 {
            continue;
        }
        observation.status = "disabled_by_preview_quality_budget";
        if steps == 0 {
            continue;
        }
        observation.status = "camera_or_sun_topology_unavailable";
        let (
            Some((_, camera_transform, Projection::Perspective(projection))),
            Ok((sun, sun_transform)),
        ) = (camera, suns.single())
        else {
            continue;
        };
        observation.status = "ambiguous_camera_or_invalid_far_plane";
        if ambiguous || !projection.far.is_finite() || projection.far <= 10.0 {
            continue;
        }
        observation.status = "above_layer_or_empty_layer";
        if camera_transform.translation().y >= settings.0.altitude_m
            || settings.0.coverage <= 0.0
            || settings.0.opacity <= 0.0
        {
            continue;
        }
        observation.status = "invalid_time_or_settings";
        let Ok(offset) = settings.0.texture_offset(time.elapsed_secs_f64()) else {
            continue;
        };
        observation.status = "material_unavailable";
        let Some(mut material) = materials.get_mut(&handle.0) else {
            continue;
        };
        transform.translation = camera_transform.translation();
        transform.scale = Vec3::splat(projection.far * 0.9);
        *global = GlobalTransform::from(*transform);
        material.layer.w = -projection.far;
        material.march.x = f32::from(steps);
        let direction = -sun_transform.forward();
        material.offset_sun = Vec4::new(offset[0], offset[1], direction.x, direction.z);
        let color = sun.color.to_linear();
        material.radiance = Vec4::new(color.red, color.green, color.blue, 0.0)
            * (sun.illuminance / std::f32::consts::PI);
        material.radiance.w = direction.y;
        *visible = Visibility::Visible;
        observation.visible_domes = 1;
        observation.offset_uv = Some(offset);
        observation.status = "projected_cpu_not_gpu_verified";
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn verify_quality_cycle(app: &mut App, material: &Handle<CloudMaterial>, dome: Entity) {
        use crate::environment::EnvironmentQualityRequest;
        use forge_project::{VisualQualityTier, VisualValleyQualityProfileDocument};
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let source_hash = app
            .world()
            .resource::<CloudPreviewObservation>()
            .settings_hash
            .clone();
        app.insert_resource(
            EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
        );
        for (tier, expected) in [
            (VisualQualityTier::Low, 0u8),
            (VisualQualityTier::Medium, 12),
            (VisualQualityTier::Ultra, 32),
            (VisualQualityTier::High, 24),
        ] {
            app.world_mut()
                .resource_mut::<EnvironmentQualityRequest>()
                .request(tier);
            app.update();
            let observed = app.world().resource::<CloudPreviewObservation>();
            assert_eq!(observed.requested_quality_tier, Some(tier));
            assert_eq!(observed.maximum_view_steps, u32::from(expected));
            assert_eq!(observed.visible_domes, usize::from(expected > 0));
            assert_eq!(observed.settings_hash, source_hash);
            assert!(!observed.gpu_execution_verified);
            let materials = app.world().resource::<Assets<CloudMaterial>>();
            assert_eq!(materials.len(), 1);
            assert_eq!(
                *app.world().get::<Visibility>(dome).unwrap(),
                if expected > 0 {
                    Visibility::Visible
                } else {
                    Visibility::Hidden
                }
            );
            if expected > 0 {
                assert_eq!(
                    materials.get(material).unwrap().march.x.to_bits(),
                    f32::from(expected).to_bits()
                );
            } else {
                assert_eq!(observed.status, "disabled_by_preview_quality_budget");
            }
        }
    }

    #[test]
    fn volume_upload_preserves_density_and_axis_addressing() {
        let volume = forge_world::CloudDensityVolume {
            resolution: 64,
            samples: vec![127; 64 * 64 * 64],
        };
        let image = volume_image(&volume);
        assert_eq!(image.data.as_ref().unwrap(), &volume.samples);
        assert_eq!(image.texture_descriptor.dimension, TextureDimension::D3);
        assert_eq!(image.texture_descriptor.size.depth_or_array_layers, 64);
        assert_eq!(image.texture_descriptor.mip_level_count, 1);
        let ImageSampler::Descriptor(sampler) = image.sampler else {
            panic!("explicit sampler required")
        };
        assert_eq!(sampler.address_mode_u, ImageAddressMode::Repeat);
        assert_eq!(sampler.address_mode_v, ImageAddressMode::ClampToEdge);
        assert_eq!(sampler.address_mode_w, ImageAddressMode::Repeat);
    }

    #[test]
    fn preview_follows_camera_and_pauses_with_ambiguous_topology() {
        let settings = CloudLayerSettings {
            seed: 42,
            altitude_m: 1500.0,
            pattern_scale_m: 4000.0,
            coverage: 0.5,
            opacity: 0.8,
            wind_xz_m_per_second: [10.0, 0.0],
        };
        let mut app = App::new();
        app.insert_resource(CloudPreview(settings))
            .insert_resource(CloudPreviewObservation::new(settings))
            .init_resource::<Time<Virtual>>()
            .init_resource::<Assets<CloudMaterial>>()
            .add_systems(Update, update_clouds);
        let material = app
            .world_mut()
            .resource_mut::<Assets<CloudMaterial>>()
            .add(CloudMaterial {
                density: Handle::default(),
                layer: Vec4::ZERO,
                offset_sun: Vec4::ZERO,
                radiance: Vec4::ZERO,
                march: Vec4::ZERO,
            });
        let dome = app
            .world_mut()
            .spawn((
                CloudDome,
                Transform::default(),
                GlobalTransform::default(),
                Visibility::Hidden,
                MeshMaterial3d(material.clone()),
            ))
            .id();
        app.world_mut().spawn((
            Camera3d::default(),
            GlobalTransform::from_translation(Vec3::new(10.0, 5.0, 20.0)),
        ));
        app.world_mut()
            .spawn((DirectionalLight::default(), GlobalTransform::default()));
        app.world_mut()
            .resource_mut::<Time<Virtual>>()
            .advance_by(std::time::Duration::from_secs(100));
        app.update();
        assert_eq!(
            *app.world().get::<Visibility>(dome).unwrap(),
            Visibility::Visible
        );
        assert_eq!(
            app.world()
                .get::<GlobalTransform>(dome)
                .unwrap()
                .translation(),
            Vec3::new(10.0, 5.0, 20.0)
        );
        let assets = app.world().resource::<Assets<CloudMaterial>>();
        assert!(assets.get(&material).unwrap().depth_bias() < -10.0);
        assert_eq!(
            assets.get(&material).unwrap().offset_sun.x.to_bits(),
            0.25_f32.to_bits()
        );
        let observation = app.world().resource::<CloudPreviewObservation>();
        assert_eq!(observation.status, "projected_cpu_not_gpu_verified");
        assert_eq!(observation.visible_domes, 1);
        assert!(!observation.gpu_execution_verified);
        let settings_hash = observation.settings_hash.clone();
        app.world_mut()
            .resource_mut::<Time<Virtual>>()
            .advance_by(std::time::Duration::from_secs(200));
        app.update();
        let observation = app.world().resource::<CloudPreviewObservation>();
        assert_eq!(
            observation.offset_uv.unwrap()[0].to_bits(),
            0.75_f32.to_bits()
        );
        assert_eq!(observation.settings_hash, settings_hash);
        let json = serde_json::to_value(observation).unwrap();
        assert_eq!(json["gpu_execution_verified"], false);
        assert_eq!(json["texture_bytes"], 262_144);
        assert_eq!(json["maximum_view_steps"], 24);
        let second_camera = app.world_mut().spawn(Camera3d::default()).id();
        app.update();
        assert_eq!(
            *app.world().get::<Visibility>(dome).unwrap(),
            Visibility::Hidden
        );
        let observation = app.world().resource::<CloudPreviewObservation>();
        assert_eq!(observation.status, "ambiguous_camera_or_invalid_far_plane");
        assert_eq!(observation.visible_domes, 0);
        assert!(observation.offset_uv.is_none());
        app.world_mut().despawn(second_camera);
        app.update();
        verify_quality_cycle(&mut app, &material, dome);
        assert_eq!(
            app.world()
                .resource::<CloudPreviewObservation>()
                .visible_domes,
            1
        );
    }
}
