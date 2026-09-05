//! Pure correlation state for one browser-to-world asset drag.

use crate::area_tabs::AreaKey;
use forge_core::{AreaId, AssetId, ContentHash, ProjectId};

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AssetDragPayload {
    pub asset_id: AssetId,
    pub snapshot_hash: ContentHash,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct AssetSurfaceHit {
    pub generation: u64,
    pub serial: u64,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: u64,
    pub world: [f32; 3],
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AssetDropScope {
    pub key: AreaKey,
    pub revision: u64,
    pub generation: u64,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct AssetDropIntent {
    pub payload: AssetDragPayload,
    pub key: AreaKey,
    pub revision: u64,
    pub surface_generation: u64,
    pub surface_serial: u64,
    pub drop_xz_m: [f32; 2],
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct AssetDragState {
    payload: Option<AssetDragPayload>,
    next_surface_serial: u64,
    pending_surface_serial: Option<u64>,
}

impl AssetDragState {
    pub fn begin(&mut self, payload: AssetDragPayload) {
        self.payload = Some(payload);
        self.pending_surface_serial = None;
    }

    pub fn clear(&mut self) {
        self.payload = None;
        self.pending_surface_serial = None;
    }

    #[must_use]
    pub const fn payload(self) -> Option<AssetDragPayload> {
        self.payload
    }

    pub fn request_surface(&mut self) -> Result<u64, AssetDropError> {
        if self.payload.is_none() {
            return Err(AssetDropError::NoAsset);
        }
        let serial = self
            .next_surface_serial
            .checked_add(1)
            .filter(|serial| *serial <= crate::workspace_api::MAX_WORKSPACE_IPC_REVISION)
            .ok_or(AssetDropError::SerialExhausted)?;
        self.next_surface_serial = serial;
        self.pending_surface_serial = Some(serial);
        Ok(serial)
    }

    pub fn resolve_drop(
        &self,
        scope: AssetDropScope,
        hit: Option<AssetSurfaceHit>,
    ) -> Result<AssetDropIntent, AssetDropError> {
        let payload = self.payload.ok_or(AssetDropError::NoAsset)?;
        let expected_serial = self
            .pending_surface_serial
            .ok_or(AssetDropError::NoSurfaceSample)?;
        let hit = hit.ok_or(AssetDropError::NoSurfaceHit)?;
        if hit.serial != expected_serial {
            return Err(AssetDropError::StaleSurfaceSerial);
        }
        if hit.generation != scope.generation {
            return Err(AssetDropError::StaleGeneration);
        }
        if hit.project_id != scope.key.project_id || hit.area_id != scope.key.area_id {
            return Err(AssetDropError::WrongArea);
        }
        if hit.revision != scope.revision {
            return Err(AssetDropError::StaleRevision);
        }
        if !hit.world.iter().all(|value| value.is_finite()) {
            return Err(AssetDropError::NonFiniteHit);
        }
        Ok(AssetDropIntent {
            payload,
            key: scope.key,
            revision: scope.revision,
            surface_generation: hit.generation,
            surface_serial: hit.serial,
            drop_xz_m: [hit.world[0], hit.world[2]],
        })
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AssetDropError {
    NoAsset,
    SerialExhausted,
    NoSurfaceSample,
    NoSurfaceHit,
    StaleSurfaceSerial,
    StaleGeneration,
    WrongArea,
    StaleRevision,
    NonFiniteHit,
}

impl std::fmt::Display for AssetDropError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let message = match self {
            Self::NoAsset => "Aucun asset vérifié n’est en cours de déplacement.",
            Self::SerialExhausted => "Le compteur de ciblage du terrain est épuisé.",
            Self::NoSurfaceSample => "Déplacez l’asset au-dessus du terrain avant de le déposer.",
            Self::NoSurfaceHit => "Aucun point du terrain n’est disponible sous l’asset.",
            Self::StaleSurfaceSerial => "Le dernier point du terrain n’est pas encore confirmé.",
            Self::StaleGeneration => "La projection du terrain a changé pendant le déplacement.",
            Self::WrongArea => "Le point du terrain appartient à une autre zone.",
            Self::StaleRevision => "La zone a changé pendant le déplacement.",
            Self::NonFiniteHit => "Le point du terrain est invalide.",
        };
        formatter.write_str(message)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{ModuleId, ProjectId};

    fn payload() -> AssetDragPayload {
        AssetDragPayload {
            asset_id: AssetId::deterministic("asset-drag/tree"),
            snapshot_hash: ContentHash::from_bytes([7; 32]),
        }
    }

    fn scope() -> AssetDropScope {
        AssetDropScope {
            key: AreaKey {
                project_id: ProjectId::deterministic("asset-drag/project"),
                module_id: ModuleId::deterministic("asset-drag/module"),
                area_id: AreaId::deterministic("asset-drag/area"),
            },
            revision: 12,
            generation: 4,
        }
    }

    fn hit(serial: u64) -> AssetSurfaceHit {
        let scope = scope();
        AssetSurfaceHit {
            generation: scope.generation,
            serial,
            project_id: scope.key.project_id,
            area_id: scope.key.area_id,
            revision: scope.revision,
            world: [18.0, 3.0, -7.5],
        }
    }

    #[test]
    fn latest_exact_surface_hit_becomes_a_source_only_drop_intent() {
        let mut state = AssetDragState::default();
        state.begin(payload());
        let first = state.request_surface().unwrap();
        let latest = state.request_surface().unwrap();
        assert_eq!(first + 1, latest);
        assert_eq!(
            state.resolve_drop(scope(), Some(hit(first))),
            Err(AssetDropError::StaleSurfaceSerial)
        );
        let intent = state.resolve_drop(scope(), Some(hit(latest))).unwrap();
        assert_eq!(intent.payload, payload());
        assert!((intent.drop_xz_m[0] - 18.0).abs() < f32::EPSILON);
        assert!((intent.drop_xz_m[1] + 7.5).abs() < f32::EPSILON);
        assert_eq!(intent.surface_serial, latest);
    }

    #[test]
    fn stale_scope_missing_hit_and_non_finite_coordinates_never_place() {
        let mut state = AssetDragState::default();
        state.begin(payload());
        let serial = state.request_surface().unwrap();
        assert_eq!(
            state.resolve_drop(scope(), None),
            Err(AssetDropError::NoSurfaceHit)
        );
        let mut outdated_scope = scope();
        outdated_scope.revision += 1;
        assert_eq!(
            state.resolve_drop(outdated_scope, Some(hit(serial))),
            Err(AssetDropError::StaleRevision)
        );
        let mut invalid = hit(serial);
        invalid.world[0] = f32::NAN;
        assert_eq!(
            state.resolve_drop(scope(), Some(invalid)),
            Err(AssetDropError::NonFiniteHit)
        );
    }

    #[test]
    fn clear_and_absent_surface_request_are_explicit() {
        let mut state = AssetDragState::default();
        assert_eq!(state.request_surface(), Err(AssetDropError::NoAsset));
        state.begin(payload());
        assert_eq!(
            state.resolve_drop(scope(), Some(hit(1))),
            Err(AssetDropError::NoSurfaceSample)
        );
        state.clear();
        assert_eq!(state.payload(), None);
    }
}
