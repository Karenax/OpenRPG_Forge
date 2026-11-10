//! Deterministic multi-zone session used by the M2 visit proof.
//!
//! The session is engine-neutral. It loads one zone at a time, walks on the projected
//! WOK surface, opens/closes doors, and transitions between zones without duplicating
//! the player or the zone state.

use crate::projection::{ProjectedDoor, ProjectedModule, ProjectedZone};
use forge_core::{EntityId, Vec3};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet, VecDeque};

pub const SESSION_SPEED_MPS: f32 = 3.0;
pub const DOOR_INTERACTION_RANGE_M: f32 = 2.5;
pub const TRANSITION_RANGE_M: f32 = 1.6;
pub const NAV_CELL_M: f32 = 0.5;
pub const PLAYER_RADIUS_M: f32 = 0.35;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct PlayerState {
    pub position: Vec3,
    pub yaw: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum SessionEventKind {
    SessionStarted,
    SessionRestored,
    ZoneLoaded,
    ZoneUnloaded,
    PathMoved,
    DoorOpened,
    DoorClosed,
    Transitioned,
    Blocked,
    Finished,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct SessionEvent {
    pub tick: u64,
    pub kind: SessionEventKind,
    pub area: String,
    pub detail: String,
    pub position: Vec3,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct TransitionResult {
    pub from: String,
    pub to: String,
    pub via_door: EntityId,
    pub player: PlayerState,
}

#[derive(Debug, Clone)]
pub struct ModuleSession {
    module: ProjectedModule,
    current_area: String,
    player: PlayerState,
    doors_open: BTreeMap<(String, EntityId), bool>,
    loaded: BTreeSet<String>,
    visited: BTreeSet<String>,
    events: Vec<SessionEvent>,
    tick: u64,
    transitions: usize,
    distance_m: f32,
    path_points: usize,
    return_positions: BTreeMap<String, Vec3>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct SessionSnapshot {
    pub current_area: String,
    pub player: PlayerState,
    pub doors_open: Vec<(String, EntityId, bool)>,
    pub loaded: BTreeSet<String>,
    pub visited: BTreeSet<String>,
    pub events: Vec<SessionEvent>,
    pub tick: u64,
    pub transitions: usize,
    pub distance_m: f32,
    pub path_points: usize,
    pub return_positions: BTreeMap<String, Vec3>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct VisitReport {
    pub schema: String,
    pub module: String,
    pub module_id: String,
    pub source_module_sha256: String,
    pub entry_area: String,
    pub final_area: String,
    pub zones_visited: Vec<String>,
    pub loaded_areas: Vec<String>,
    pub transitions: usize,
    pub doors_opened: usize,
    pub distance_m: f32,
    pub path_points: usize,
    pub ticks: u64,
    pub walkmesh_triangles: usize,
    pub assets: usize,
    pub events: Vec<SessionEvent>,
}

impl ModuleSession {
    pub fn new(module: ProjectedModule, start_area: &str) -> Result<Self, String> {
        let zone = module
            .zone(start_area)
            .ok_or_else(|| format!("zone {start_area} is absent from the module"))?;
        let player = PlayerState {
            position: zone.spawn,
            yaw: zone.spawn_yaw,
        };
        let mut loaded = BTreeSet::new();
        loaded.insert(start_area.to_owned());
        let mut visited = BTreeSet::new();
        visited.insert(start_area.to_owned());
        let mut session = Self {
            module,
            current_area: start_area.to_owned(),
            player,
            doors_open: BTreeMap::new(),
            loaded,
            visited,
            events: Vec::new(),
            tick: 0,
            transitions: 0,
            distance_m: 0.0,
            path_points: 0,
            return_positions: BTreeMap::new(),
        };
        session.log(
            SessionEventKind::SessionStarted,
            format!("Session ouverte sur {start_area}"),
        );
        session.log(
            SessionEventKind::ZoneLoaded,
            format!("Zone {start_area} chargée"),
        );
        Ok(session)
    }

    pub fn module(&self) -> &ProjectedModule {
        &self.module
    }

    pub fn current_area(&self) -> &str {
        &self.current_area
    }

    #[must_use]
    pub fn loaded_areas(&self) -> Vec<String> {
        self.loaded.iter().cloned().collect()
    }

    #[must_use]
    pub fn visited_areas(&self) -> Vec<String> {
        self.visited.iter().cloned().collect()
    }

    pub fn player(&self) -> &PlayerState {
        &self.player
    }

    pub fn events(&self) -> &[SessionEvent] {
        &self.events
    }

    pub fn zone(&self) -> &ProjectedZone {
        self.module
            .zone(&self.current_area)
            .expect("validated session always points at a projected zone")
    }

    pub fn doors_open(&self) -> &BTreeMap<(String, EntityId), bool> {
        &self.doors_open
    }

    pub fn is_door_open(&self, door: &ProjectedDoor) -> bool {
        self.doors_open
            .get(&(self.current_area.clone(), door.id))
            .copied()
            .unwrap_or(door.initially_open)
    }

    pub fn nearest_door(&self, range_m: f32) -> Option<&ProjectedDoor> {
        self.zone()
            .doors
            .iter()
            .filter(|door| horizontal_distance(self.player.position, door.position) <= range_m)
            .min_by(|left, right| {
                horizontal_distance(self.player.position, left.position)
                    .total_cmp(&horizontal_distance(self.player.position, right.position))
            })
    }

    pub fn interact(&mut self) -> Result<Option<&ProjectedDoor>, String> {
        let Some(door) = self.nearest_door(DOOR_INTERACTION_RANGE_M).cloned() else {
            return Ok(None);
        };
        let key = (self.current_area.clone(), door.id);
        let open = self
            .doors_open
            .get(&key)
            .copied()
            .unwrap_or(door.initially_open);
        self.doors_open.insert(key, !open);
        self.log(
            if open {
                SessionEventKind::DoorClosed
            } else {
                SessionEventKind::DoorOpened
            },
            format!(
                "Porte {} {}",
                door.tag.as_deref().unwrap_or("sans tag"),
                if open { "fermée" } else { "ouverte" }
            ),
        );
        Ok(self
            .zone()
            .doors
            .iter()
            .find(|candidate| candidate.id == door.id))
    }

    pub fn try_transition(&mut self) -> Result<Option<TransitionResult>, String> {
        let Some(door) = self.nearest_door(TRANSITION_RANGE_M).cloned() else {
            return Ok(None);
        };
        if !self.is_door_open(&door) {
            return Ok(None);
        }
        let Some(destination) = door.destination.clone() else {
            return Ok(None);
        };
        let Some(target) = self.module.zone(&destination) else {
            return Err(format!(
                "door {} targets missing zone {destination}",
                door.tag.as_deref().unwrap_or("sans tag")
            ));
        };
        let from = self.current_area.clone();
        self.return_positions
            .insert(from.clone(), self.player.position);
        let target_door = target
            .doors
            .iter()
            .find(|candidate| candidate.destination.as_deref() == Some(from.as_str()))
            .or_else(|| target.doors.first());
        let arrival = target_door
            .map(|candidate| approach_point(candidate.position, Vec3::ZERO, 1.2))
            .unwrap_or(target.spawn);
        let arrival_y = target
            .walkmesh
            .height_at(arrival.x, arrival.z)
            .unwrap_or(arrival.y)
            + 0.02;
        let target_spawn_yaw = target.spawn_yaw;
        self.loaded.remove(&from);
        self.log(
            SessionEventKind::ZoneUnloaded,
            format!("Zone {from} déchargée"),
        );
        self.loaded.insert(destination.clone());
        self.visited.insert(destination.clone());
        self.current_area = destination.clone();
        self.player.position = Vec3::new(arrival.x, arrival_y, arrival.z);
        self.player.yaw = target_spawn_yaw;
        self.transitions += 1;
        self.log(
            SessionEventKind::ZoneLoaded,
            format!("Zone {destination} chargée"),
        );
        self.log(
            SessionEventKind::Transitioned,
            format!(
                "Transition {from} → {destination} via {}",
                door.tag.as_deref().unwrap_or("porte")
            ),
        );
        Ok(Some(TransitionResult {
            from,
            to: destination,
            via_door: door.id,
            player: self.player.clone(),
        }))
    }

    pub fn move_by(&mut self, forward: f32, strafe: f32, dt: f32) -> bool {
        if !forward.is_finite() || !strafe.is_finite() || !dt.is_finite() || dt <= 0.0 {
            return false;
        }
        let yaw = self.player.yaw;
        let forward_vector = [yaw.sin(), 0.0, yaw.cos()];
        let right_vector = [yaw.cos(), 0.0, -yaw.sin()];
        let delta = [
            (forward_vector[0] * forward + right_vector[0] * strafe) * SESSION_SPEED_MPS * dt,
            0.0,
            (forward_vector[2] * forward + right_vector[2] * strafe) * SESSION_SPEED_MPS * dt,
        ];
        self.tick += 1;
        let current = self.player.position;
        let candidates = [
            Vec3::new(current.x + delta[0], current.y, current.z + delta[2]),
            Vec3::new(current.x + delta[0], current.y, current.z),
            Vec3::new(current.x, current.y, current.z + delta[2]),
        ];
        for candidate in candidates {
            if self.position_free(candidate) {
                let height = self
                    .zone()
                    .walkmesh
                    .height_at(candidate.x, candidate.z)
                    .unwrap_or(candidate.y);
                let next = Vec3::new(candidate.x, height + 0.02, candidate.z);
                self.distance_m += horizontal_distance(current, next);
                self.player.position = next;
                if delta[0].abs() > 0.0001 || delta[2].abs() > 0.0001 {
                    self.player.yaw = delta[2].atan2(delta[0]);
                }
                return true;
            }
        }
        false
    }

    pub fn walk_to(&mut self, target: Vec3) -> Result<usize, String> {
        let path = self.find_path(self.player.position, target)?;
        let mut moved = 0;
        for waypoint in path.iter().skip(1) {
            if self.walk_segment(*waypoint) {
                moved += 1;
            } else {
                self.log(
                    SessionEventKind::Blocked,
                    format!("Déplacement bloqué vers {waypoint:?}"),
                );
                return Err(format!("movement blocked before {waypoint:?}"));
            }
        }
        self.path_points += moved;
        self.log(
            SessionEventKind::PathMoved,
            format!(
                "Trajet terminé vers [{:.2}, {:.2}, {:.2}]",
                target.x, target.y, target.z
            ),
        );
        Ok(moved)
    }

    fn walk_segment(&mut self, target: Vec3) -> bool {
        let mut guard = 0;
        while horizontal_distance(self.player.position, target) > 0.05 {
            guard += 1;
            if guard > 10_000 {
                return false;
            }
            let dx = target.x - self.player.position.x;
            let dz = target.z - self.player.position.z;
            let length = (dx * dx + dz * dz).sqrt();
            if length <= 1.0e-4 {
                break;
            }
            let yaw = dz.atan2(dx);
            self.player.yaw = yaw;
            let step = length.min(0.25);
            let candidate = Vec3::new(
                self.player.position.x + dx / length * step,
                self.player.position.y,
                self.player.position.z + dz / length * step,
            );
            if !self.position_free(candidate) {
                return false;
            }
            let height = self
                .zone()
                .walkmesh
                .height_at(candidate.x, candidate.z)
                .unwrap_or(candidate.y);
            let next = Vec3::new(candidate.x, height + 0.02, candidate.z);
            self.distance_m += horizontal_distance(self.player.position, next);
            self.player.position = next;
            self.tick += 1;
        }
        true
    }

    pub fn find_path(&self, start: Vec3, goal: Vec3) -> Result<Vec<Vec3>, String> {
        let grid = NavGrid::new(self.zone(), &self.doors_open, &self.current_area);
        grid.find_path(start, goal)
    }

    pub fn snapshot(&self) -> SessionSnapshot {
        SessionSnapshot {
            current_area: self.current_area.clone(),
            player: self.player.clone(),
            doors_open: self
                .doors_open
                .iter()
                .map(|((area, door), open)| (area.clone(), *door, *open))
                .collect(),
            loaded: self.loaded.clone(),
            visited: self.visited.clone(),
            events: self.events.clone(),
            tick: self.tick,
            transitions: self.transitions,
            distance_m: self.distance_m,
            path_points: self.path_points,
            return_positions: self.return_positions.clone(),
        }
    }

    pub fn restore(module: ProjectedModule, snapshot: SessionSnapshot) -> Result<Self, String> {
        if module.zone(&snapshot.current_area).is_none() {
            return Err(format!(
                "snapshot area {} is absent from the module",
                snapshot.current_area
            ));
        }
        let mut session = Self {
            module,
            current_area: snapshot.current_area,
            player: snapshot.player,
            doors_open: snapshot
                .doors_open
                .into_iter()
                .map(|(area, door, open)| ((area, door), open))
                .collect(),
            loaded: snapshot.loaded,
            visited: snapshot.visited,
            events: snapshot.events,
            tick: snapshot.tick,
            transitions: snapshot.transitions,
            distance_m: snapshot.distance_m,
            path_points: snapshot.path_points,
            return_positions: snapshot.return_positions,
        };
        session.log(
            SessionEventKind::SessionRestored,
            "Session restaurée depuis une sauvegarde".to_owned(),
        );
        Ok(session)
    }

    pub fn report(&self) -> VisitReport {
        VisitReport {
            schema: "forge.aurora-visit@1".to_owned(),
            module: self.module.name.clone(),
            module_id: self.module.module_id.to_string(),
            source_module_sha256: self.module.source_module_sha256.to_string(),
            entry_area: self.module.entry_area.clone(),
            final_area: self.current_area.clone(),
            zones_visited: self.visited.iter().cloned().collect(),
            loaded_areas: self.loaded.iter().cloned().collect(),
            transitions: self.transitions,
            doors_opened: self
                .events
                .iter()
                .filter(|event| event.kind == SessionEventKind::DoorOpened)
                .count(),
            distance_m: self.distance_m,
            path_points: self.path_points,
            ticks: self.tick,
            walkmesh_triangles: self
                .module
                .zones
                .values()
                .map(|zone| zone.walkmesh.triangles.len())
                .sum(),
            assets: self.module.assets.len(),
            events: self.events.clone(),
        }
    }

    fn position_free(&self, candidate: Vec3) -> bool {
        if !candidate.is_finite() {
            return false;
        }
        let zone = self.zone();
        if zone.walkmesh.height_at(candidate.x, candidate.z).is_none() {
            return false;
        }
        for door in &zone.doors {
            if !self.is_door_open(door)
                && horizontal_distance(candidate, door.position) <= PLAYER_RADIUS_M + 0.55
            {
                return false;
            }
        }
        true
    }

    fn log(&mut self, kind: SessionEventKind, detail: String) {
        self.events.push(SessionEvent {
            tick: self.tick,
            kind,
            area: self.current_area.clone(),
            detail,
            position: self.player.position,
        });
    }
}

#[derive(Debug, Clone)]
struct NavGrid {
    min_x: f32,
    min_z: f32,
    width: usize,
    height: usize,
    walkable: Vec<bool>,
}

impl NavGrid {
    fn new(
        zone: &ProjectedZone,
        doors_open: &BTreeMap<(String, EntityId), bool>,
        area: &str,
    ) -> Self {
        let min_x = zone.walkmesh.min.x;
        let min_z = zone.walkmesh.min.z;
        let width = ((zone.walkmesh.max.x - min_x) / NAV_CELL_M).ceil() as usize + 1;
        let height = ((zone.walkmesh.max.z - min_z) / NAV_CELL_M).ceil() as usize + 1;
        let mut walkable = vec![false; width * height];
        for z in 0..height {
            for x in 0..width {
                let point_x = min_x + x as f32 * NAV_CELL_M;
                let point_z = min_z + z as f32 * NAV_CELL_M;
                let mut free = zone.walkmesh.height_at(point_x, point_z).is_some();
                if free {
                    for door in &zone.doors {
                        if !doors_open
                            .get(&(area.to_owned(), door.id))
                            .copied()
                            .unwrap_or(door.initially_open)
                            && horizontal_distance(
                                Vec3::new(point_x, 0.0, point_z),
                                Vec3::new(door.position.x, 0.0, door.position.z),
                            ) <= PLAYER_RADIUS_M + 0.55
                        {
                            free = false;
                            break;
                        }
                    }
                }
                walkable[z * width + x] = free;
            }
        }
        Self {
            min_x,
            min_z,
            width,
            height,
            walkable,
        }
    }

    fn cell(&self, point: Vec3) -> Option<(usize, usize)> {
        let x = ((point.x - self.min_x) / NAV_CELL_M).round();
        let z = ((point.z - self.min_z) / NAV_CELL_M).round();
        if x < 0.0 || z < 0.0 {
            return None;
        }
        let x = x as usize;
        let z = z as usize;
        (x < self.width && z < self.height).then_some((x, z))
    }

    fn point(&self, cell: (usize, usize)) -> Vec3 {
        Vec3::new(
            self.min_x + cell.0 as f32 * NAV_CELL_M,
            0.0,
            self.min_z + cell.1 as f32 * NAV_CELL_M,
        )
    }

    fn is_walkable(&self, cell: (usize, usize)) -> bool {
        self.walkable[cell.1 * self.width + cell.0]
    }

    fn nearest_walkable(&self, cell: (usize, usize)) -> Option<(usize, usize)> {
        if self.is_walkable(cell) {
            return Some(cell);
        }
        let mut queue = VecDeque::from([cell]);
        let mut seen = BTreeSet::from([cell]);
        while let Some(current) = queue.pop_front() {
            for dy in -2i32..=2 {
                for dx in -2i32..=2 {
                    if dx == 0 && dy == 0 {
                        continue;
                    }
                    let next = (
                        current.0.saturating_add_signed(dx as isize),
                        current.1.saturating_add_signed(dy as isize),
                    );
                    if next.0 >= self.width || next.1 >= self.height || !seen.insert(next) {
                        continue;
                    }
                    if self.is_walkable(next) {
                        return Some(next);
                    }
                    if seen.len() > 4096 {
                        return None;
                    }
                    queue.push_back(next);
                }
            }
        }
        None
    }

    fn find_path(&self, start: Vec3, goal: Vec3) -> Result<Vec<Vec3>, String> {
        let start_cell = self
            .cell(start)
            .and_then(|cell| self.nearest_walkable(cell))
            .ok_or_else(|| "start is outside the walkable surface".to_owned())?;
        let goal_cell = self
            .cell(goal)
            .and_then(|cell| self.nearest_walkable(cell))
            .ok_or_else(|| "goal is outside the walkable surface".to_owned())?;
        let mut open = BTreeSet::new();
        let mut closed = BTreeSet::new();
        let mut came_from = BTreeMap::<(usize, usize), (usize, usize)>::new();
        let mut g_score = BTreeMap::<(usize, usize), f32>::new();
        let mut f_score = BTreeMap::<(usize, usize), f32>::new();
        g_score.insert(start_cell, 0.0);
        f_score.insert(
            start_cell,
            heuristic(self.point(start_cell), self.point(goal_cell)),
        );
        open.insert((start_cell, ordered_score(f_score[&start_cell])));
        let mut iterations = 0;
        while let Some((current, _)) = open.pop_first() {
            if !closed.insert(current) {
                continue;
            }
            iterations += 1;
            if iterations > 200_000 {
                return Err("navigation search limit exceeded".to_owned());
            }
            if current == goal_cell {
                let mut path = vec![current];
                let mut cursor = current;
                while let Some(previous) = came_from.get(&cursor) {
                    path.push(*previous);
                    cursor = *previous;
                }
                path.reverse();
                return Ok(path.iter().map(|cell| self.point(*cell)).collect());
            }
            for dy in -1i32..=1 {
                for dx in -1i32..=1 {
                    if dx == 0 && dy == 0 {
                        continue;
                    }
                    let next = (
                        current.0.saturating_add_signed(dx as isize),
                        current.1.saturating_add_signed(dy as isize),
                    );
                    if next.0 >= self.width || next.1 >= self.height || !self.is_walkable(next) {
                        continue;
                    }
                    if dx != 0
                        && dy != 0
                        && (!self.is_walkable((next.0, current.1))
                            || !self.is_walkable((current.0, next.1)))
                    {
                        continue;
                    }
                    let step = if dx != 0 && dy != 0 {
                        NAV_CELL_M * std::f32::consts::SQRT_2
                    } else {
                        NAV_CELL_M
                    };
                    let tentative = g_score[&current] + step;
                    if tentative < *g_score.get(&next).unwrap_or(&f32::INFINITY) {
                        came_from.insert(next, current);
                        g_score.insert(next, tentative);
                        let score = tentative + heuristic(self.point(next), self.point(goal_cell));
                        f_score.insert(next, score);
                        open.insert((next, ordered_score(score)));
                    }
                }
            }
        }
        Err("no path exists on the walkable surface".to_owned())
    }
}

fn heuristic(a: Vec3, b: Vec3) -> f32 {
    ((a.x - b.x).powi(2) + (a.z - b.z).powi(2)).sqrt()
}

fn ordered_score(value: f32) -> u32 {
    value.clamp(0.0, 1_000_000.0).to_bits()
}

fn horizontal_distance(a: Vec3, b: Vec3) -> f32 {
    ((a.x - b.x).powi(2) + (a.z - b.z).powi(2)).sqrt()
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn nav_grid_paths_around_a_closed_door() {
        let zone = ProjectedZone {
            resref: "test".to_owned(),
            name: "Test".to_owned(),
            area_id: forge_core::AreaId::deterministic("test"),
            terrain: dummy_terrain(),
            gameplay: dummy_gameplay(),
            walkmesh: crate::walkmesh::WalkSurface {
                triangles: vec![
                    crate::walkmesh::WorldTriangle {
                        a: Vec3::new(0.0, 0.0, 0.0),
                        b: Vec3::new(10.0, 0.0, 0.0),
                        c: Vec3::new(10.0, 0.0, 10.0),
                        surface: 3,
                    },
                    crate::walkmesh::WorldTriangle {
                        a: Vec3::new(0.0, 0.0, 0.0),
                        b: Vec3::new(10.0, 0.0, 10.0),
                        c: Vec3::new(0.0, 0.0, 10.0),
                        surface: 3,
                    },
                ],
                min: Vec3::new(0.0, 0.0, 0.0),
                max: Vec3::new(10.0, 0.0, 10.0),
            },
            doors: vec![ProjectedDoor {
                id: EntityId::deterministic("door"),
                tag: Some("door".to_owned()),
                position: Vec3::new(5.0, 0.0, 5.0),
                destination: Some("other".to_owned()),
                initially_open: false,
                transition_flags: None,
                model_assets: Vec::new(),
            }],
            instances: Vec::new(),
            spawn: Vec3::new(2.0, 0.0, 2.0),
            spawn_yaw: 0.0,
            archive_path: String::new(),
            archive_bytes: Vec::new(),
            archive_sha256: forge_core::bytes_sha256(b"archive"),
            head_hash: forge_core::bytes_sha256(b"head"),
            gameplay_sha256: forge_core::bytes_sha256(b"gameplay"),
            tile_count: 1,
            navigation: crate::projection::NavigationStats {
                wok_files: 1,
                dwk_files: 0,
                pwk_files: 0,
                triangles: 2,
                unsupported: 0,
            },
            diagnostics: Vec::new(),
            area_root: std::path::PathBuf::new(),
        };
        let grid = NavGrid::new(&zone, &BTreeMap::new(), "test");
        assert!(
            grid.find_path(Vec3::new(2.0, 0.0, 2.0), Vec3::new(8.0, 0.0, 8.0))
                .is_ok()
        );
    }

    fn dummy_terrain() -> forge_project::TerrainDocument {
        forge_project::TerrainDocument::new(
            forge_core::ProjectId::deterministic("project"),
            forge_core::AreaId::deterministic("test"),
            forge_core::Revision::INITIAL,
            "Test",
            1,
            forge_project::TerrainExtent::new(10.0, 10.0),
            forge_project::HeightSamplingParameters {
                algorithm: forge_project::HeightSamplingAlgorithm::FractalValueNoiseV1,
                sample_spacing_m: 1.0,
                base_elevation_m: 0.0,
                amplitude_m: 0.0,
                base_frequency_cycles_per_m: 0.01,
                octaves: 1,
                lacunarity: 2.0,
                persistence: 0.5,
            },
            vec![forge_project::TerrainMaterialLayer::new("ground", "Ground")],
        )
    }

    fn dummy_gameplay() -> forge_gameplay::GameplayDocument {
        let player = EntityId::deterministic("player");
        forge_gameplay::GameplayDocument {
            schema: forge_gameplay::GAMEPLAY_SCHEMA.to_owned(),
            version: forge_gameplay::GAMEPLAY_VERSION,
            module_id: forge_core::ModuleId::deterministic("module"),
            area_id: forge_core::AreaId::deterministic("test"),
            profile: forge_gameplay::RulesProfile {
                id: "test".to_owned(),
                version: 1,
                interaction_range_m: 2.0,
                attack_range_m: 1.5,
                attack_damage: 1,
                attack_stamina_cost: 1,
                max_stamina: 100,
                max_action_receipts: 128,
                max_events_per_tick: 64,
            },
            creator: None,
            narrative: None,
            factions: None,
            simulation: None,
            scripting: None,
            cinematics: None,
            player,
            actors: vec![forge_gameplay::ActorDefinition {
                id: player,
                name: "Player".to_owned(),
                role: forge_gameplay::ActorRole::Player,
                position: Vec3::new(2.0, 0.0, 2.0),
                max_vitality: 100,
                protection: 0,
                inventory_capacity: 10,
                dialogue: None,
                patrol: Vec::new(),
                flees_danger: false,
            }],
            items: Vec::new(),
            objects: Vec::new(),
            places: Vec::new(),
            dialogues: Vec::new(),
            quests: Vec::new(),
        }
    }
}
