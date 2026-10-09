//! Opt-in native suspend/resume witness, separate from the terrain verdict.
use bevy::prelude::*;
use forge_engine::tidewater_refraction::{Activity, CaptureConsumer, Observation, Targets};

#[derive(Resource, Default)]
struct Proof {
    saved: Vec<(Entity, Visibility)>,
    samples: Vec<serde_json::Value>,
}

pub(super) fn install(app: &mut App) {
    if std::env::var("FORGE_TIDEWATER_CONTEXT_PROOF").as_deref() != Ok("1") {
        return;
    }
    app.init_resource::<Proof>()
        .add_systems(
            PostUpdate,
            drive
                .after(super::super::super::update)
                .before(bevy::camera::visibility::VisibilitySystems::VisibilityPropagate),
        )
        .add_systems(Last, observe);
}

fn drive(world: &mut World) {
    let frame = world.resource::<super::Proof>().frames;
    if (80..140).contains(&frame) {
        let saved = world
            .query_filtered::<(Entity, &Visibility), With<CaptureConsumer>>()
            .iter(world)
            .map(|(entity, visibility)| (entity, *visibility))
            .collect::<Vec<_>>();
        for &(entity, _) in &saved {
            world.entity_mut(entity).insert(Visibility::Hidden);
        }
        let mut proof = world.resource_mut::<Proof>();
        for item in saved {
            if !proof.saved.iter().any(|&(entity, _)| entity == item.0) {
                proof.saved.push(item);
            }
        }
    } else if frame == 140 {
        let saved = world.resource::<Proof>().saved.clone();
        for (entity, visibility) in saved {
            if let Ok(mut entity) = world.get_entity_mut(entity) {
                entity.insert(visibility);
            }
        }
    }
}

fn observe(world: &mut World) {
    let frame = world.resource::<super::Proof>().frames;
    if frame == 0 {
        return;
    }
    let activity = world.resource::<Activity>();
    let observation = world.resource::<Observation>();
    let encoded = world
        .resource::<Targets>()
        .encoded_frames
        .load(std::sync::atomic::Ordering::Acquire);
    let sample = serde_json::json!({
        "frame":frame, "activity":activity, "ready":observation.ready,
        "error":observation.error, "encoded":encoded,
        "fish":crate::source_fish::activity(world),
    });
    world.resource_mut::<Proof>().samples.push(sample);
    if frame != 300 {
        return;
    }
    let proof = world.resource::<Proof>();
    let suspended = proof
        .samples
        .iter()
        .filter(|s| (90..130).contains(&s["frame"].as_u64().unwrap_or(0)))
        .collect::<Vec<_>>();
    let resumed = proof
        .samples
        .iter()
        .filter(|s| (160..200).contains(&s["frame"].as_u64().unwrap_or(0)))
        .collect::<Vec<_>>();
    let stopped = !suspended.is_empty()
        && suspended.iter().all(|s| {
            s["activity"]["demanded"] == false
                && s["ready"] == false
                && s["activity"]["material_updates"] == 0
                && s["activity"]["renderable_updates"] == 0
                && s["encoded"] == suspended[0]["encoded"]
                && s["error"].is_null()
        });
    let fresh = !resumed.is_empty()
        && resumed.iter().all(|s| {
            s["activity"]["demanded"] == true
                && s["ready"] == true
                && s["error"].is_null()
                && s["encoded"].as_u64().unwrap_or(0)
                    >= suspended
                        .first()
                        .and_then(|s| s["encoded"].as_u64())
                        .unwrap_or(u64::MAX)
                        .saturating_add(2)
        });
    let inland = std::env::var("FORGE_TIDEWATER_TERRAIN_INLAND").as_deref() == Ok("1");
    let fish_dormant = !inland
        || proof
            .samples
            .iter()
            .filter(|s| s["frame"].as_u64().unwrap_or(0) > 50)
            .all(|s| s["fish"][0] == false && s["fish"][1] == 0);
    let receipt = serde_json::json!({
        "pass":!proof.saved.is_empty() && stopped && fresh && fish_dormant,
        "capture_stopped":stopped, "capture_resumed_with_fresh_frames":fresh,
        "inland_fish_dormant":fish_dormant, "inland":inland,
        "declared_consumers_tested":proof.saved.len(),
        "scope":"Native consumer hide/show verifies GPU suspend/resume; inland spawn verifies fish distance gating. No claim of automatic occlusion behind terrain.",
        "samples":proof.samples, "gate3_closed":false,
    });
    let path = world
        .resource::<crate::Probe>()
        .screenshot
        .with_extension("context-proof.json");
    std::fs::write(
        path,
        serde_json::to_vec_pretty(&receipt).expect("context proof receipt"),
    )
    .expect("context proof output");
}
