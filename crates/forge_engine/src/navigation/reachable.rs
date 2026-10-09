//! Query alternative destinations on the same current provider; never actuate.
use super::{NavigationRuntime, NavigationStatus};
use bevy::prelude::Vec3;
use forge_core::EntityId;
use forge_navigation::{TileKey, landmass, point};

pub(super) struct EscapeSearch {
    generation: u64,
    builds: u64,
    start: Vec3,
    target: Vec3,
    candidates: Vec<Vec3>,
    selected: Option<Vec3>,
}

impl NavigationRuntime {
    pub(crate) fn needs_escape_search(&self, actor: EntityId) -> bool {
        self.routes.get(&actor).is_some_and(|route| {
            self.actuation_ready
                && route.observation.grounded
                && matches!(
                    route.observation.provider_state.as_deref(),
                    Some("NoPath" | "TargetNotOnNavMesh")
                )
                && matches!(
                    route.observation.status,
                    NavigationStatus::NoPath | NavigationStatus::OutsideMesh
                )
                && route
                    .observation
                    .route_diagnostic
                    .as_ref()
                    .is_some_and(|diagnostic| diagnostic.start_sample.is_some())
        })
    }
    /// A successful query only selects next tick's intent. The agent must still
    /// plan its route after the normal source guard before the KCC can move.
    pub(crate) fn reachable_escape_goal(
        &mut self,
        actor: EntityId,
        candidates: &[Vec3],
    ) -> Option<Vec3> {
        let route = self.routes.get_mut(&actor)?;
        let diagnostic = route.observation.route_diagnostic.as_ref()?;
        if !self.actuation_ready
            || !route.observation.grounded
            || !matches!(
                route.observation.provider_state.as_deref(),
                Some("NoPath" | "TargetNotOnNavMesh")
            )
            || !matches!(
                route.observation.status,
                NavigationStatus::NoPath | NavigationStatus::OutsideMesh
            )
            || diagnostic.start_sample.is_none()
            || candidates.len() > 8
        {
            return None;
        }
        let start = Vec3::from_array(route.observation.feet);
        let target = Vec3::from_array(route.observation.target?);
        let tolerance = self.profile.radius_m * 0.5;
        if let Some(previous) = &route.escape_search
            && previous.generation == self.token
            && previous.builds == self.builds
            && previous.start.distance(start) < tolerance
            && previous.target.distance(target) < tolerance
            && previous.candidates.len() == candidates.len()
            && previous
                .candidates
                .iter()
                .zip(candidates)
                .all(|(a, b)| a.distance(*b) < tolerance)
        {
            return previous.selected;
        }
        let distance = &self.archipelago.archipelago_options.point_sample_distance;
        let sampled_start = self
            .archipelago
            .sample_point(point(start.to_array()), distance)
            .ok()?;
        let mut selected = None;
        for &candidate in candidates {
            if !candidate.is_finite() {
                continue;
            }
            let feet = candidate - Vec3::Y * (self.profile.height_m * 0.5);
            // Do not query an island whose source receipt was not guarded for
            // this actor's current corridor, or whose rebuild is pending.
            let key = TileKey::at(feet.to_array(), self.profile);
            if !route.observation.required_tiles.contains(&key)
                || !self
                    .tiles
                    .get(&key)
                    .is_some_and(|tile| tile.observation.status == NavigationStatus::Ready)
            {
                continue;
            }
            route.observation.reachable_goal_queries += 1;
            let Ok(goal) = self
                .archipelago
                .sample_point(point(feet.to_array()), distance)
            else {
                continue;
            };
            if let Ok(path) = self.archipelago.find_path(
                &sampled_start,
                &goal,
                &std::collections::HashMap::default(),
                landmass::PermittedAnimationLinks::default(),
            ) && !path
                .iter()
                .any(|step| matches!(step, landmass::PathStep::AnimationLink { .. }))
            {
                selected = Some(candidate);
                break;
            }
        }
        route.escape_search = Some(EscapeSearch {
            generation: self.token,
            builds: self.builds,
            start,
            target,
            candidates: candidates.to_vec(),
            selected,
        });
        selected
    }
}
