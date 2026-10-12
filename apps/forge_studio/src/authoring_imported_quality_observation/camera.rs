//! Current camera component parameters; pose is excluded from the profile hash.
use super::{CurrentProfileInventory, Observation, Origin, ParameterValue as V};
use bevy::{camera::Hdr, prelude::*};

pub(super) fn camera_profile(world: &World, entity: Entity, out: &mut CurrentProfileInventory) {
    let origin = Origin::MainWorldComponent;
    out.put("camera/entity", V::Unsigned(entity.to_bits()), origin, true);
    if let Some(camera) = world.get::<Camera>(entity)
        && let Some(size) = camera.physical_viewport_size()
    {
        out.put(
            "camera/physical_viewport_px",
            V::Dimensions(size.to_array().to_vec()),
            origin,
            true,
        );
    }
    if let Some(transform) = world.get::<GlobalTransform>(entity) {
        out.numbers(
            "camera/world_pose",
            transform.to_matrix().to_cols_array(),
            origin,
            false,
        );
    }
    match world.get::<Projection>(entity) {
        Some(Projection::Perspective(p)) => {
            out.put(
                "camera/projection_kind",
                V::Text("perspective".into()),
                origin,
                true,
            );
            for (key, value) in [
                ("fov_radians", p.fov),
                ("aspect_ratio", p.aspect_ratio),
                ("near_m", p.near),
                ("far_m", p.far),
            ] {
                out.number(&format!("camera/{key}"), value, origin, true);
            }
        }
        Some(_) => {
            out.put(
                "camera/projection_kind",
                V::Text("non_perspective".into()),
                origin,
                true,
            );
        }
        None => {
            out.observations.insert(
                "camera/projection_kind".into(),
                Observation::unavailable("projection_component_unavailable", true),
            );
        }
    }
    camera_post_profile(world, entity, out);
    camera_post_lighting(world, entity, out);
}

fn camera_post_profile(world: &World, entity: Entity, out: &mut CurrentProfileInventory) {
    use bevy::{
        anti_alias::taa::TemporalAntiAliasing,
        core_pipeline::tonemapping::Tonemapping,
        post_process::{auto_exposure::AutoExposure, bloom::Bloom},
        render::view::ColorGrading,
    };
    let origin = Origin::MainWorldComponent;
    out.put(
        "post/parameters",
        V::Text("observed_camera_components_follow".into()),
        origin,
        true,
    );
    for (key, present) in [
        ("hdr", world.get::<Hdr>(entity).is_some()),
        ("auto_exposure", world.get::<AutoExposure>(entity).is_some()),
        (
            "bevy_taa",
            world.get::<TemporalAntiAliasing>(entity).is_some(),
        ),
        (
            "source_resolve",
            world
                .get::<forge_engine::tidewater_temporal::resolve::ResolveEnabled>(entity)
                .is_some(),
        ),
    ] {
        out.put(
            format!("post/{key}_component_present"),
            V::Boolean(present),
            origin,
            true,
        );
    }
    if let Some(value) = world.get::<Tonemapping>(entity) {
        out.put(
            "post/tonemapping",
            V::Text(format!("{value:?}")),
            origin,
            true,
        );
    }
    if let Some(value) = world.get::<ColorGrading>(entity) {
        out.numbers(
            "post/grading_exposure_saturation_contrast",
            [
                value.global.exposure,
                value.global.post_saturation,
                value.midtones.contrast,
            ],
            origin,
            true,
        );
    }
    if let Some(value) = world.get::<Bloom>(entity) {
        out.number("post/bloom_intensity", value.intensity, origin, true);
    }
    camera_post_aa(world, entity, out);
}

fn camera_post_aa(world: &World, entity: Entity, out: &mut CurrentProfileInventory) {
    use bevy::{
        anti_alias::{contrast_adaptive_sharpening::ContrastAdaptiveSharpening, fxaa::Fxaa},
        post_process::{effect_stack::Vignette, motion_blur::MotionBlur},
    };
    let origin = Origin::MainWorldComponent;
    if let Some(value) = world.get::<Msaa>(entity) {
        out.put(
            "post/msaa_samples",
            V::Unsigned(u64::from(value.samples())),
            origin,
            true,
        );
    }
    if let Some(value) = world.get::<Fxaa>(entity) {
        out.put("post/fxaa_enabled", V::Boolean(value.enabled), origin, true);
    }
    if let Some(value) = world.get::<ContrastAdaptiveSharpening>(entity) {
        out.put(
            "post/sharpening_enabled",
            V::Boolean(value.enabled),
            origin,
            true,
        );
        out.number(
            "post/sharpening_strength",
            value.sharpening_strength,
            origin,
            true,
        );
    }
    if let Some(value) = world.get::<Vignette>(entity) {
        out.number("post/vignette_intensity", value.intensity, origin, true);
    }
    if let Some(value) = world.get::<MotionBlur>(entity) {
        out.number(
            "post/motion_blur_shutter_angle",
            value.shutter_angle,
            origin,
            true,
        );
    }
}

fn camera_post_lighting(world: &World, entity: Entity, out: &mut CurrentProfileInventory) {
    let origin = Origin::MainWorldComponent;
    if let Some(value) = world.get::<forge_engine::tidewater_gtao::TidewaterGtao>(entity) {
        out.number("post/gtao_strength", value.strength(), origin, true);
    }
    if let Some(value) = world.get::<forge_engine::tidewater_lens_flare::TidewaterLensFlare>(entity)
    {
        out.number("post/lens_flare_strength", value.strength(), origin, true);
    }
    if let Some(value) = world.get::<forge_engine::tidewater_film_grain::TidewaterFilmGrain>(entity)
    {
        out.number("post/film_grain_intensity", value.intensity.x, origin, true);
    }
    if let Some(value) = world.get::<forge_engine::atmospheric_haze::AirHazeSettings>(entity) {
        out.put("post/haze_enabled", V::Boolean(value.enabled), origin, true);
        out.put(
            "post/haze_god_rays",
            V::Boolean(value.god_rays),
            origin,
            true,
        );
        out.number("post/haze_density", value.density(), origin, true);
        out.number("post/haze_shafts", value.shafts(), origin, true);
        out.put(
            "post/haze_resolution_scale",
            V::Number(value.resolution_scale()),
            origin,
            true,
        );
    }
}
