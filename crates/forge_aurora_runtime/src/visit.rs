//! Scripted M2 visit: enter the camp, open the gate, visit the crypt, return.

use crate::projection::ProjectedModule;
use crate::session::{ModuleSession, VisitReport};
use forge_core::Vec3;

pub fn run_scripted_visit(module: ProjectedModule) -> Result<VisitReport, String> {
    let entry = module.entry_area.clone();
    let mut session = ModuleSession::new(module, &entry)?;
    let first_door = session
        .zone()
        .doors
        .iter()
        .find(|door| door.destination.is_some())
        .cloned()
        .ok_or_else(|| "entry zone has no inter-zone door".to_owned())?;
    let start = session.player().position;
    if let Some(landmark) = far_landmark(&session, start) {
        let _ = session.walk_to(landmark);
    }
    let approach = approach_point(first_door.position, session.player().position, 1.2);
    session.walk_to(approach)?;
    session.interact()?;
    let transition = session.try_transition()?;
    if transition.is_none() {
        // The approach point is deliberately just outside the transition radius in some
        // layouts; one short step toward the door is enough.
        session.walk_to(first_door.position)?;
        if session.try_transition()?.is_none() {
            return Err("first transition did not trigger after opening the door".to_owned());
        }
    }
    let exit_door = session
        .zone()
        .doors
        .iter()
        .find(|door| door.destination.is_some())
        .cloned()
        .ok_or_else(|| "destination zone has no return door".to_owned())?;
    let current = session.player().position;
    if let Some(landmark) = far_landmark(&session, current) {
        let _ = session.walk_to(landmark);
    }
    session.walk_to(approach_point(
        exit_door.position,
        session.player().position,
        1.2,
    ))?;
    session.interact()?;
    if session.try_transition()?.is_none() {
        session.walk_to(exit_door.position)?;
        if session.try_transition()?.is_none() {
            return Err("return transition did not trigger after opening the door".to_owned());
        }
    }
    Ok(session.report())
}

/// Renders a deterministic top-down SVG of every projected walkmesh and the visit path.
pub fn render_zone_map_svg(module: &ProjectedModule, report: &VisitReport) -> String {
    let mut zones = module.zones.values().collect::<Vec<_>>();
    zones.sort_by(|left, right| left.resref.cmp(&right.resref));
    let mut widths = Vec::new();
    let mut heights = Vec::new();
    for zone in &zones {
        widths.push(zone.walkmesh.max.x - zone.walkmesh.min.x);
        heights.push(zone.walkmesh.max.z - zone.walkmesh.min.z);
    }
    let gap = 4.0f32;
    let total_width =
        widths.iter().copied().sum::<f32>() + gap * (zones.len().saturating_sub(1) as f32);
    let total_height = heights.iter().copied().fold(0.0f32, f32::max);
    let scale = 12.0f32;
    let width = ((total_width + 4.0) * scale).max(320.0);
    let height = ((total_height + 4.0) * scale).max(240.0);
    let mut output = String::new();
    output.push_str(&format!(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width:.0}\" height=\"{height:.0}\" viewBox=\"0 0 {width:.0} {height:.0}\">\n"
    ));
    output.push_str("<rect width=\"100%\" height=\"100%\" fill=\"#10151d\"/>\n");
    output.push_str("<g font-family=\"monospace\" fill=\"#dce6f2\">\n");
    let mut offset_x = 2.0f32;
    let mut zone_offsets = Vec::new();
    for (index, zone) in zones.iter().enumerate() {
        let ox = offset_x;
        let oz = 2.0 + (total_height - heights[index]) * 0.5;
        zone_offsets.push((zone.resref.clone(), ox, oz));
        output.push_str(&format!(
            "<text x=\"{:.1}\" y=\"{:.1}\" font-size=\"14\">{}</text>\n",
            (ox) * scale,
            (oz - 0.3) * scale,
            escape_xml(&zone.name)
        ));
        for triangle in &zone.walkmesh.triangles {
            let points = [
                map_point(triangle.a, zone.walkmesh.min, ox, oz, scale),
                map_point(triangle.b, zone.walkmesh.min, ox, oz, scale),
                map_point(triangle.c, zone.walkmesh.min, ox, oz, scale),
            ];
            let fill = if crate::walkmesh::surface_is_walkable(triangle.surface) {
                "#25384a"
            } else {
                "#5a2630"
            };
            output.push_str(&format!(
                "<polygon points=\"{:.1},{:.1} {:.1},{:.1} {:.1},{:.1}\" fill=\"{}\" stroke=\"#38536b\" stroke-width=\"0.4\"/>\n",
                points[0].0, points[0].1, points[1].0, points[1].1, points[2].0, points[2].1, fill
            ));
        }
        for door in &zone.doors {
            let point = map_point(door.position, zone.walkmesh.min, ox, oz, scale);
            output.push_str(&format!(
                "<circle cx=\"{:.1}\" cy=\"{:.1}\" r=\"4\" fill=\"#e0b24a\"/>\n",
                point.0, point.1
            ));
            output.push_str(&format!(
                "<text x=\"{:.1}\" y=\"{:.1}\" font-size=\"9\" fill=\"#e0b24a\">{}</text>\n",
                point.0 + 6.0,
                point.1 + 3.0,
                escape_xml(door.tag.as_deref().unwrap_or("door"))
            ));
        }
        offset_x += widths[index] + gap;
    }
    for event in &report.events {
        if !matches!(
            event.kind,
            crate::session::SessionEventKind::PathMoved
                | crate::session::SessionEventKind::Transitioned
                | crate::session::SessionEventKind::SessionStarted
        ) {
            continue;
        }
        let Some((_, ox, oz)) = zone_offsets
            .iter()
            .find(|(resref, _, _)| resref == &event.area)
        else {
            continue;
        };
        let Some(zone) = module.zones.get(&event.area) else {
            continue;
        };
        let point = map_point(event.position, zone.walkmesh.min, *ox, *oz, scale);
        output.push_str(&format!(
            "<circle cx=\"{:.1}\" cy=\"{:.1}\" r=\"2.5\" fill=\"#6ee7a8\"/>\n",
            point.0, point.1
        ));
    }
    output.push_str("</g>\n</svg>\n");
    output
}

fn map_point(point: Vec3, min: Vec3, ox: f32, oz: f32, scale: f32) -> (f32, f32) {
    (
        (ox + point.x - min.x) * scale,
        (oz + point.z - min.z) * scale,
    )
}

fn approach_point(door: Vec3, from: Vec3, distance: f32) -> Vec3 {
    let dx = from.x - door.x;
    let dz = from.z - door.z;
    let length = (dx * dx + dz * dz).sqrt();
    if length <= 1.0e-4 {
        Vec3::new(door.x, door.y, door.z + distance)
    } else {
        Vec3::new(
            door.x + dx / length * distance,
            door.y,
            door.z + dz / length * distance,
        )
    }
}

fn far_landmark(session: &ModuleSession, from: Vec3) -> Option<Vec3> {
    let zone = session.zone();
    let min = zone.walkmesh.min;
    let max = zone.walkmesh.max;
    let quarter_x = min.x + (max.x - min.x) * 0.25;
    let three_quarter_x = min.x + (max.x - min.x) * 0.75;
    let quarter_z = min.z + (max.z - min.z) * 0.25;
    let three_quarter_z = min.z + (max.z - min.z) * 0.75;
    let candidates = [
        Vec3::new(0.0, 0.0, 0.0),
        Vec3::new(quarter_x, 0.0, quarter_z),
        Vec3::new(three_quarter_x, 0.0, quarter_z),
        Vec3::new(quarter_x, 0.0, three_quarter_z),
        Vec3::new(three_quarter_x, 0.0, three_quarter_z),
    ];
    candidates
        .into_iter()
        .filter(|candidate| {
            let distance = ((candidate.x - from.x).powi(2) + (candidate.z - from.z).powi(2)).sqrt();
            distance > 8.0 && zone.walkmesh.height_at(candidate.x, candidate.z).is_some()
        })
        .max_by(|left, right| {
            let left = (left.x - from.x).powi(2) + (left.z - from.z).powi(2);
            let right = (right.x - from.x).powi(2) + (right.z - from.z).powi(2);
            left.total_cmp(&right)
        })
        .map(|candidate| {
            let y = zone
                .walkmesh
                .height_at(candidate.x, candidate.z)
                .unwrap_or(0.0);
            Vec3::new(candidate.x, y, candidate.z)
        })
}

fn escape_xml(value: &str) -> String {
    value
        .replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}
