//! Lab-only Rapier adapter probe fed by the exact canonical support collider.
//!
//! The probe inserts the authored static triangle collider at runtime, lets a dynamic actor
//! contact it through Rapier's real pipeline, removes the collider, and checks that the actor
//! resumes falling. It proves the collision adapter boundary only. It deliberately does not
//! claim a navmesh tile carve, a Landmass seam, water semantics, or NPC traversal.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    reason = "the canonical input already bounds triangle indices to its u32 geometry buffer"
)]
#![allow(
    clippy::struct_excessive_bools,
    reason = "the evidence schema records each independent collision and lifecycle assertion"
)]
#![allow(
    clippy::too_many_lines,
    reason = "the bounded simulation keeps every lifecycle assertion adjacent to its Rapier step"
)]

use forge_core::canonical_json_sha256;
use rapier3d::prelude::*;
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeMap,
    error::Error,
    fs,
    path::{Path, PathBuf},
    sync::mpsc,
};

const ACTOR_HALF_EXTENT_M: f32 = 0.25;
const INITIAL_DROP_M: f32 = 5.0;
const PREINSERT_STEPS: usize = 30;
const MAX_CONTACT_STEPS: usize = 240;
const POSTREMOVAL_STEPS: usize = 60;

#[derive(Debug, Deserialize)]
struct CanonicalReport {
    schema: String,
    version: u32,
    source_hash: String,
    geometry_hash: String,
    canonical_geometry: CanonicalGeometry,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    geometry_hash: String,
}

#[derive(Clone, Debug)]
struct SupportMesh {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    max_y: f32,
    center_x: f32,
    center_z: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct SimulationResult {
    preinsert_steps: usize,
    contact_step: usize,
    removal_step: usize,
    contact_y_m: f32,
    y_before_removal_m: f32,
    y_after_removal_m: f32,
    started_event_observed: bool,
    stopped_removed_event_observed: bool,
    support_collider_absent_after_removal: bool,
    actor_collider_still_present: bool,
}

#[derive(Debug, Serialize)]
struct ProbeReport {
    schema: &'static str,
    version: u32,
    runtime: &'static str,
    input_path: PathBuf,
    input_schema: String,
    input_version: u32,
    source_hash: String,
    geometry_hash_declared: String,
    geometry_hash_recomputed: String,
    canonical_geometry_unchanged_after_adapter: bool,
    adapter: AdapterReport,
    runs: Vec<SimulationResult>,
    deterministic_replay: bool,
    collision_semantics: &'static str,
    no_go: bool,
    missing_proofs: [&'static str; 5],
}

#[derive(Debug, Serialize)]
struct AdapterReport {
    backend: &'static str,
    canonical_subset: &'static str,
    vertices: usize,
    triangles: usize,
    runtime_insertion: &'static str,
    runtime_removal: &'static str,
}

fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err(
            "usage: canonical_rapier_dynamic_probe <canonical-support-report.json> <output.json>"
                .into(),
        );
    }
    let input_path = Path::new(&args[0]);
    let output_path = Path::new(&args[1]);
    if output_path.exists() {
        return Err(format!("refusing to overwrite {}", output_path.display()).into());
    }

    let input_bytes = fs::read(input_path)?;
    let input: CanonicalReport = serde_json::from_slice(&input_bytes)?;
    if input.schema != "forge.navigation.canonical-terrain-with-static-colliders" {
        return Err(format!("unexpected canonical schema: {}", input.schema).into());
    }
    if input.version != 1 {
        return Err(format!("unsupported canonical version: {}", input.version).into());
    }
    let geometry_hash = canonical_json_sha256(&(
        &input.canonical_geometry.positions_m,
        &input.canonical_geometry.triangles,
    ))?
    .to_string();
    if geometry_hash != input.geometry_hash
        || geometry_hash != input.canonical_geometry.geometry_hash
    {
        return Err(format!(
            "canonical geometry hash mismatch: declared outer={}, inner={}, recomputed={geometry_hash}",
            input.geometry_hash, input.canonical_geometry.geometry_hash
        )
        .into());
    }

    let support = extract_support_mesh(&input.canonical_geometry)?;
    let first = run_simulation(&support)?;
    let second = run_simulation(&support)?;
    let replay_equal = first == second;
    let output = ProbeReport {
        schema: "forge.navigation.canonical-rapier-dynamic-probe",
        version: 1,
        runtime: "native-rapier3d-0.35.0-glamx0.2",
        input_path: input_path.to_path_buf(),
        input_schema: input.schema,
        input_version: input.version,
        source_hash: input.source_hash,
        geometry_hash_declared: geometry_hash.clone(),
        geometry_hash_recomputed: canonical_json_sha256(&(
            &input.canonical_geometry.positions_m,
            &input.canonical_geometry.triangles,
        ))?
        .to_string(),
        canonical_geometry_unchanged_after_adapter: geometry_hash
            == canonical_json_sha256(&(
                &input.canonical_geometry.positions_m,
                &input.canonical_geometry.triangles,
            ))?
            .to_string(),
        adapter: AdapterReport {
            backend: "raw-rapier3d-physics-world",
            canonical_subset: "last authored support collider triangles selected by exact canonical indices",
            vertices: support.positions_m.len(),
            triangles: support.triangles.len(),
            runtime_insertion: "fixed trimesh inserted after actor pre-simulation",
            runtime_removal: "fixed trimesh removed through PhysicsWorld::remove_collider",
        },
        runs: vec![first, second],
        deterministic_replay: replay_equal,
        collision_semantics: "real CollisionEvent::Started and CollisionEvent::Stopped(REMOVED) from Rapier",
        no_go: true,
        missing_proofs: [
            "provider-native tile cache or bounded partial navmesh reconstruction",
            "Landmass seam ownership fed from this runtime collider adapter",
            "water collision/material semantics",
            "NPC steering/traversal through static, dynamic, and seam cases",
            "owner acceptance and provider selection under ADR-0009",
        ],
    };
    fs::write(output_path, serde_json::to_vec_pretty(&output)?)?;
    Ok(())
}

fn extract_support_mesh(geometry: &CanonicalGeometry) -> Result<SupportMesh, Box<dyn Error>> {
    let first_support_vertex = geometry
        .positions_m
        .len()
        .checked_sub(8)
        .ok_or("canonical geometry has fewer than eight authored support vertices")?;
    let support_triangles = geometry
        .triangles
        .iter()
        .filter(|triangle| {
            triangle
                .iter()
                .all(|index| (*index as usize) >= first_support_vertex)
        })
        .copied()
        .collect::<Vec<_>>();
    if support_triangles.len() != 12 {
        return Err(format!(
            "expected exactly 12 canonical support triangles, found {}",
            support_triangles.len()
        )
        .into());
    }

    let support_indices = support_triangles
        .iter()
        .flat_map(|triangle| triangle.iter().copied())
        .collect::<std::collections::BTreeSet<_>>();
    if support_indices.len() != 8
        || support_indices
            .iter()
            .any(|index| (*index as usize) < first_support_vertex)
    {
        return Err(
            "canonical support triangles do not reference exactly the final eight vertices".into(),
        );
    }

    let mut remap = BTreeMap::new();
    let mut positions_m = Vec::with_capacity(support_indices.len());
    for (new_index, old_index) in support_indices.iter().enumerate() {
        remap.insert(*old_index, new_index as u32);
        positions_m.push(geometry.positions_m[*old_index as usize]);
    }
    let triangles = support_triangles
        .iter()
        .map(|triangle| {
            Ok::<_, Box<dyn Error>>([
                *remap
                    .get(&triangle[0])
                    .ok_or("support index missing from remap")?,
                *remap
                    .get(&triangle[1])
                    .ok_or("support index missing from remap")?,
                *remap
                    .get(&triangle[2])
                    .ok_or("support index missing from remap")?,
            ])
        })
        .collect::<Result<Vec<_>, _>>()?;
    let max_y = positions_m
        .iter()
        .map(|position| position[1])
        .fold(f32::NEG_INFINITY, f32::max);
    let min_x = positions_m
        .iter()
        .map(|position| position[0])
        .fold(f32::INFINITY, f32::min);
    let max_x = positions_m
        .iter()
        .map(|position| position[0])
        .fold(f32::NEG_INFINITY, f32::max);
    let min_z = positions_m
        .iter()
        .map(|position| position[2])
        .fold(f32::INFINITY, f32::min);
    let max_z = positions_m
        .iter()
        .map(|position| position[2])
        .fold(f32::NEG_INFINITY, f32::max);
    Ok(SupportMesh {
        positions_m,
        triangles,
        max_y,
        center_x: f32::midpoint(min_x, max_x),
        center_z: f32::midpoint(min_z, max_z),
    })
}

fn run_simulation(support: &SupportMesh) -> Result<SimulationResult, Box<dyn Error>> {
    let mut world = PhysicsWorld::new();
    world.integration_parameters.dt = 1.0 / 60.0;
    let actor_body = world.insert_body(
        RigidBodyBuilder::dynamic()
            .translation(Vector::new(
                support.center_x,
                support.max_y + INITIAL_DROP_M,
                support.center_z,
            ))
            .ccd_enabled(true),
    );
    let actor_collider = world.insert_collider(
        ColliderBuilder::cuboid(
            ACTOR_HALF_EXTENT_M,
            ACTOR_HALF_EXTENT_M,
            ACTOR_HALF_EXTENT_M,
        )
        .active_events(ActiveEvents::COLLISION_EVENTS),
        Some(actor_body),
    );
    let (collision_sender, collision_receiver) = mpsc::channel();
    let (contact_sender, _contact_receiver) = mpsc::channel();
    let event_handler = ChannelEventCollector::new(collision_sender, contact_sender);
    for _ in 0..PREINSERT_STEPS {
        world.step_with_events(&(), &event_handler);
    }
    let actor_y_before_insert = world
        .bodies
        .get(actor_body)
        .ok_or("actor body disappeared before collider insertion")?
        .translation()
        .y;
    if actor_y_before_insert <= support.max_y + ACTOR_HALF_EXTENT_M {
        return Err(format!(
            "actor reached the support before runtime insertion: y={actor_y_before_insert}"
        )
        .into());
    }

    let support_vertices = support
        .positions_m
        .iter()
        .map(|position| Vector::new(position[0], position[1], position[2]))
        .collect::<Vec<_>>();
    let support_collider = world.insert_collider(
        ColliderBuilder::trimesh(support_vertices, support.triangles.clone())?
            .active_events(ActiveEvents::COLLISION_EVENTS),
        None,
    );

    let mut contact_step = None;
    let mut contact_y = None;
    let mut started_event_observed = false;
    for step in 0..MAX_CONTACT_STEPS {
        world.step_with_events(&(), &event_handler);
        while let Ok(event) = collision_receiver.try_recv() {
            if pair_matches(
                event.collider1(),
                event.collider2(),
                actor_collider,
                support_collider,
            ) && event.started()
            {
                started_event_observed = true;
                contact_step.get_or_insert(PREINSERT_STEPS + step + 1);
                contact_y.get_or_insert(
                    world
                        .bodies
                        .get(actor_body)
                        .ok_or("actor body disappeared at contact")?
                        .translation()
                        .y,
                );
            }
        }
        if started_event_observed {
            break;
        }
    }
    let contact_step =
        contact_step.ok_or("Rapier emitted no Started event for canonical support")?;
    let contact_y = contact_y.ok_or("contact event had no actor position")?;
    let y_before_removal = world
        .bodies
        .get(actor_body)
        .ok_or("actor body disappeared before support removal")?
        .translation()
        .y;
    if contact_y < support.max_y + ACTOR_HALF_EXTENT_M - 0.08
        || contact_y > support.max_y + ACTOR_HALF_EXTENT_M + 0.08
    {
        return Err(format!(
            "unexpected canonical support contact height: y={contact_y}, expected about {}",
            support.max_y + ACTOR_HALF_EXTENT_M
        )
        .into());
    }

    if world.remove_collider(support_collider).is_none() {
        return Err("Rapier refused to remove the inserted support collider".into());
    }
    let mut stopped_removed_event_observed = false;
    for _ in 0..POSTREMOVAL_STEPS {
        world.step_with_events(&(), &event_handler);
        while let Ok(event) = collision_receiver.try_recv() {
            if pair_matches(
                event.collider1(),
                event.collider2(),
                actor_collider,
                support_collider,
            ) && event.stopped()
                && event.removed()
            {
                stopped_removed_event_observed = true;
            }
        }
    }
    let y_after_removal = world
        .bodies
        .get(actor_body)
        .ok_or("actor body disappeared after support removal")?
        .translation()
        .y;
    if y_after_removal >= y_before_removal - 0.05 {
        return Err(format!(
            "actor did not resume falling after support removal: before={y_before_removal}, after={y_after_removal}"
        )
        .into());
    }
    Ok(SimulationResult {
        preinsert_steps: PREINSERT_STEPS,
        contact_step,
        removal_step: contact_step,
        contact_y_m: contact_y,
        y_before_removal_m: y_before_removal,
        y_after_removal_m: y_after_removal,
        started_event_observed,
        stopped_removed_event_observed,
        support_collider_absent_after_removal: world.colliders.get(support_collider).is_none(),
        actor_collider_still_present: world.colliders.get(actor_collider).is_some(),
    })
}

fn pair_matches(
    first: ColliderHandle,
    second: ColliderHandle,
    expected_first: ColliderHandle,
    expected_second: ColliderHandle,
) -> bool {
    (first == expected_first && second == expected_second)
        || (first == expected_second && second == expected_first)
}
