//! Bounded presentation path. Sources change only when the finished stroke is accepted natively.

use crate::area_preview::AreaPreviewRequest;

/// Eight dispatch opportunities per second keep native regeneration bounded during a stroke.
pub(crate) const STROKE_PREVIEW_INTERVAL_MS: u64 = 125;

/// Identity copied into an asynchronous request and returned unchanged with its response.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) struct StrokePreviewTicket {
    pub serial: u64,
    pub generation: u64,
    pub request: AreaPreviewRequest,
}

/// One request which the adapter may now send to the native preview service.
#[derive(Clone, Debug, PartialEq)]
pub(crate) struct StrokePreviewDispatch<T> {
    pub ticket: StrokePreviewTicket,
    pub trajectory: T,
    pub finished: bool,
}

/// Result of settling one asynchronous response.
#[derive(Clone, Debug, PartialEq)]
pub(crate) struct StrokePreviewSettlement<T> {
    /// The response belongs to the current generation, area and revision.
    pub present_response: bool,
    /// At most one successor is released after the previous request settles.
    pub next: Option<StrokePreviewDispatch<T>>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) enum StrokePreviewCoordinatorError {
    CounterExhausted,
}

#[derive(Clone, Debug, PartialEq)]
struct QueuedStrokePreview<T> {
    generation: u64,
    request: AreaPreviewRequest,
    trajectory: T,
    finished: bool,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct ActiveStrokePreview {
    ticket: StrokePreviewTicket,
    finished: bool,
}

/// Pure latest-wins coordinator. Callers provide monotone milliseconds; it owns no timer or IPC.
#[derive(Clone, Debug)]
pub(crate) struct StrokePreviewCoordinator<T> {
    request: AreaPreviewRequest,
    generation: u64,
    next_serial: u64,
    last_dispatch_ms: Option<u64>,
    active: Option<ActiveStrokePreview>,
    pending: Option<QueuedStrokePreview<T>>,
}

impl<T> StrokePreviewCoordinator<T> {
    #[must_use]
    pub(crate) const fn new(request: AreaPreviewRequest) -> Self {
        Self {
            request,
            generation: 1,
            next_serial: 1,
            last_dispatch_ms: None,
            active: None,
            pending: None,
        }
    }

    #[must_use]
    pub(crate) fn has_in_flight(&self) -> bool {
        self.active.is_some()
    }

    /// Drops a ticket which the adapter obtained but could not send.
    pub(crate) fn discard_before_send(
        &mut self,
        ticket: StrokePreviewTicket,
    ) -> Result<bool, StrokePreviewCoordinatorError> {
        if self.active.is_none_or(|active| active.ticket != ticket) {
            return Ok(false);
        }
        self.active = None;
        self.advance_generation()?;
        self.pending = None;
        Ok(true)
    }

    /// Changes of zone or accepted workspace revision start a new response generation.
    pub(crate) fn set_request(
        &mut self,
        request: AreaPreviewRequest,
    ) -> Result<(), StrokePreviewCoordinatorError> {
        if self.request != request {
            self.advance_generation()?;
            self.request = request;
            self.pending = None;
        }
        Ok(())
    }

    /// Invalidates queued work and any late response without pretending an in-flight task stopped.
    pub(crate) fn cancel(&mut self) -> Result<(), StrokePreviewCoordinatorError> {
        self.advance_generation()?;
        self.pending = None;
        Ok(())
    }

    /// Queues the newest complete trajectory and may release it immediately when cadence permits.
    pub(crate) fn submit(
        &mut self,
        now_ms: u64,
        trajectory: T,
        finished: bool,
    ) -> Result<Option<StrokePreviewDispatch<T>>, StrokePreviewCoordinatorError> {
        let candidate = QueuedStrokePreview {
            generation: self.generation,
            request: self.request,
            trajectory,
            finished,
        };
        self.replace_pending(candidate);
        self.dispatch_ready(now_ms)
    }

    /// Releases a cadence-delayed request. The caller decides how this is scheduled.
    pub(crate) fn tick(
        &mut self,
        now_ms: u64,
    ) -> Result<Option<StrokePreviewDispatch<T>>, StrokePreviewCoordinatorError> {
        self.dispatch_ready(now_ms)
    }

    /// Settles only the exact active ticket; forged or out-of-order responses change no state.
    pub(crate) fn settle(
        &mut self,
        now_ms: u64,
        response: StrokePreviewTicket,
    ) -> Result<StrokePreviewSettlement<T>, StrokePreviewCoordinatorError> {
        if self.active.is_none_or(|active| active.ticket != response) {
            return Ok(StrokePreviewSettlement {
                present_response: false,
                next: None,
            });
        }

        self.active = None;
        let superseded = self.pending.as_ref().is_some_and(|pending| {
            pending.generation == response.generation && pending.request == response.request
        });
        let present_response = response.generation == self.generation
            && response.request == self.request
            && !superseded;
        let next = self.dispatch_ready(now_ms)?;
        Ok(StrokePreviewSettlement {
            present_response,
            next,
        })
    }

    fn advance_generation(&mut self) -> Result<(), StrokePreviewCoordinatorError> {
        self.generation = self
            .generation
            .checked_add(1)
            .ok_or(StrokePreviewCoordinatorError::CounterExhausted)?;
        Ok(())
    }

    fn replace_pending(&mut self, candidate: QueuedStrokePreview<T>) {
        if self.active.is_some_and(|active| active.finished) && !candidate.finished {
            return;
        }
        if self.pending.as_ref().is_some_and(|queued| queued.finished) && !candidate.finished {
            return;
        }
        self.pending = Some(candidate);
    }

    fn dispatch_ready(
        &mut self,
        now_ms: u64,
    ) -> Result<Option<StrokePreviewDispatch<T>>, StrokePreviewCoordinatorError> {
        if self.active.is_some() {
            return Ok(None);
        }
        let Some(candidate) = self.pending.as_ref() else {
            return Ok(None);
        };
        if !candidate.finished && !self.cadence_ready(now_ms) {
            return Ok(None);
        }

        let following_serial = self
            .next_serial
            .checked_add(1)
            .ok_or(StrokePreviewCoordinatorError::CounterExhausted)?;
        let candidate = self.pending.take().expect("pending preview checked");
        let ticket = StrokePreviewTicket {
            serial: self.next_serial,
            generation: candidate.generation,
            request: candidate.request,
        };
        self.next_serial = following_serial;
        self.last_dispatch_ms = Some(now_ms);
        self.active = Some(ActiveStrokePreview {
            ticket,
            finished: candidate.finished,
        });
        Ok(Some(StrokePreviewDispatch {
            ticket,
            trajectory: candidate.trajectory,
            finished: candidate.finished,
        }))
    }

    fn cadence_ready(&self, now_ms: u64) -> bool {
        self.last_dispatch_ms
            .is_none_or(|last| now_ms.saturating_sub(last) >= STROKE_PREVIEW_INTERVAL_MS)
    }
}

#[derive(Clone, Debug, Default, PartialEq)]
pub(crate) struct BrushPath {
    pub points: Vec<[f32; 2]>,
}

impl BrushPath {
    pub fn append(&mut self, point: [f32; 2], radius: f32) -> Result<(), String> {
        if !radius.is_finite()
            || !(2.0..=64.0).contains(&radius)
            || point
                .iter()
                .any(|p| !p.is_finite() || !(-256.0..=256.0).contains(p))
        {
            return Err("Position de brosse invalide.".into());
        }
        let Some(previous) = self.points.last().copied() else {
            self.points.push(point);
            return Ok(());
        };
        let dx = point[0] - previous[0];
        let dz = point[1] - previous[1];
        let distance = dx.hypot(dz);
        let spacing = (radius * 0.25).max(1.0);
        if distance < spacing * 0.5 {
            return Ok(());
        }
        #[allow(
            clippy::cast_possible_truncation,
            clippy::cast_sign_loss,
            reason = "Finite distance within the bounded 512 m area"
        )]
        let count = (distance / spacing).ceil() as u16;
        if self.points.len() + usize::from(count) > 64 {
            return Err("Trait trop long (64 empreintes maximum). Rien n’a été appliqué ; dessinez un trait plus court.".into());
        }
        for index in 1..=count {
            let t = f32::from(index) / f32::from(count);
            self.points
                .push([previous[0] + dx * t, previous[1] + dz * t]);
        }
        Ok(())
    }
}

pub(crate) const PATH_TRACE_MIN_POINTS: usize = 2;
pub(crate) const PATH_TRACE_MAX_POINTS: usize = 64;
pub(crate) const PATH_TRACE_MIN_SPACING_M: f32 = 4.0;

/// One disposable path gesture. Three-dimensional hits feed the local overlay, while only X/Z
/// coordinates cross the command boundary for canonical terrain resolution.
#[derive(Clone, Debug, Default, PartialEq)]
pub(crate) struct PathTrace {
    pub points_m: Vec<[f32; 3]>,
}

impl PathTrace {
    pub fn append(&mut self, point_m: [f32; 3]) -> Result<bool, String> {
        if !point_m.iter().all(|coordinate| coordinate.is_finite())
            || !(-256.0..256.0).contains(&point_m[0])
            || !(-10_000.0..=10_000.0).contains(&point_m[1])
            || !(-256.0..256.0).contains(&point_m[2])
        {
            return Err("Point de chemin hors de la zone éditable.".into());
        }
        let Some(previous) = self.points_m.last() else {
            self.points_m.push(point_m);
            return Ok(true);
        };
        let horizontal_distance = (point_m[0] - previous[0]).hypot(point_m[2] - previous[2]);
        if horizontal_distance < PATH_TRACE_MIN_SPACING_M {
            return Ok(false);
        }
        if self.points_m.len() == PATH_TRACE_MAX_POINTS {
            return Err(
                "Tracé trop long (64 points maximum). Rien n’a été appliqué ; recommencez avec un geste plus simple."
                    .into(),
            );
        }
        self.points_m.push(point_m);
        Ok(true)
    }

    pub fn command_points(&self) -> Result<Vec<[f32; 2]>, String> {
        if self.points_m.len() < PATH_TRACE_MIN_POINTS {
            return Err(
                "Un chemin exige au moins deux points espacés de 4 m. Rien n’a été appliqué."
                    .into(),
            );
        }
        Ok(self
            .points_m
            .iter()
            .map(|point| [point[0], point[2]])
            .collect())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::area_tabs::AreaKey;
    use forge_core::{AreaId, ModuleId, ProjectId};

    fn request(area: &str, revision: u64) -> AreaPreviewRequest {
        AreaPreviewRequest {
            key: AreaKey {
                project_id: ProjectId::deterministic("preview/project"),
                module_id: ModuleId::deterministic("preview/module"),
                area_id: AreaId::deterministic(area),
            },
            base_revision: revision,
        }
    }

    #[test]
    fn path_fills_gaps_without_duplicates_and_refuses_overflow_atomically() {
        let mut path = BrushPath::default();
        path.append([0.0, 0.0], 8.0).unwrap();
        path.append([0.0, 0.0], 8.0).unwrap();
        path.append([8.0, 0.0], 8.0).unwrap();
        assert_eq!(
            path.points,
            vec![[0.0, 0.0], [2.0, 0.0], [4.0, 0.0], [6.0, 0.0], [8.0, 0.0]]
        );
        let before = path.clone();
        assert!(path.append([256.0, 0.0], 2.0).is_err());
        assert_eq!(path, before);
        assert!(path.append([f32::NAN, 0.0], 8.0).is_err());
        assert!(path.append([0.0, 0.0], 0.0).is_err());
    }

    #[test]
    fn authored_path_trace_keeps_3d_draft_and_emits_bounded_xz_only() {
        let mut trace = PathTrace::default();
        assert!(trace.append([-255.0, 4.0, -255.0]).unwrap());
        assert!(!trace.append([-252.0, 40.0, -255.0]).unwrap());
        assert!(trace.append([-251.0, 5.0, -255.0]).unwrap());
        assert_eq!(
            trace.points_m,
            vec![[-255.0, 4.0, -255.0], [-251.0, 5.0, -255.0]]
        );
        assert_eq!(
            trace.command_points().unwrap(),
            vec![[-255.0, -255.0], [-251.0, -255.0]]
        );
        let before = trace.clone();
        assert!(trace.append([256.0, 5.0, 0.0]).is_err());
        assert!(trace.append([0.0, f32::NAN, 0.0]).is_err());
        assert_eq!(trace, before);
    }

    #[test]
    fn authored_path_trace_requires_two_points_and_refuses_the_sixty_fifth_atomically() {
        let mut trace = PathTrace::default();
        trace.append([-252.0, 0.0, 0.0]).unwrap();
        assert!(trace.command_points().is_err());
        for index in 1..PATH_TRACE_MAX_POINTS {
            #[allow(
                clippy::cast_precision_loss,
                reason = "the bounded test index is exactly representable as f32"
            )]
            let x = -252.0 + index as f32 * PATH_TRACE_MIN_SPACING_M;
            trace.append([x, 0.0, 0.0]).unwrap();
        }
        assert_eq!(trace.points_m.len(), PATH_TRACE_MAX_POINTS);
        let before = trace.clone();
        assert!(trace.append([4.0, 0.0, 4.0]).is_err());
        assert_eq!(trace, before);
    }

    #[test]
    fn burst_keeps_one_active_request_and_only_the_latest_pending_trajectory() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let first = coordinator.submit(0, 0_u8, false).unwrap().unwrap();
        assert!(coordinator.has_in_flight());
        for trajectory in 1..30 {
            assert!(
                coordinator
                    .submit(u64::from(trajectory), trajectory, false)
                    .unwrap()
                    .is_none()
            );
        }

        let settled = coordinator.settle(124, first.ticket).unwrap();
        assert!(!settled.present_response);
        assert!(settled.next.is_none());
        let latest = coordinator.tick(125).unwrap().unwrap();
        assert_eq!(latest.trajectory, 29);
        assert!(!latest.finished);
        assert!(coordinator.tick(250).unwrap().is_none());
    }

    #[test]
    fn settling_a_superseded_response_releases_but_never_presents_it() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let first = coordinator.submit(1_000, "first", false).unwrap().unwrap();
        assert!(
            coordinator
                .submit(1_010, "obsolete", false)
                .unwrap()
                .is_none()
        );
        assert!(
            coordinator
                .submit(1_020, "latest", false)
                .unwrap()
                .is_none()
        );

        let settled = coordinator.settle(1_125, first.ticket).unwrap();
        assert!(!settled.present_response);
        let next = settled.next.unwrap();
        assert_eq!(next.trajectory, "latest");
        assert_eq!(next.ticket.serial, first.ticket.serial + 1);
    }

    #[test]
    fn finished_trajectory_has_priority_and_bypasses_the_cadence_after_settlement() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let first = coordinator.submit(500, "first", false).unwrap().unwrap();
        assert!(coordinator.submit(510, "moving", false).unwrap().is_none());
        assert!(coordinator.submit(511, "finished", true).unwrap().is_none());
        assert!(
            coordinator
                .submit(512, "late-move", false)
                .unwrap()
                .is_none()
        );

        let settled = coordinator.settle(513, first.ticket).unwrap();
        assert!(!settled.present_response);
        let finish = settled.next.unwrap();
        assert_eq!(finish.trajectory, "finished");
        assert!(finish.finished);
    }

    #[test]
    fn adapter_ignores_a_failed_superseded_response_and_dispatches_the_pending_finish() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let obsolete = coordinator.submit(500, "obsolete", false).unwrap().unwrap();
        assert!(coordinator.submit(501, "final", true).unwrap().is_none());

        // The adapter settles the failed ticket before deciding whether its error is fatal.
        let settlement = coordinator.settle(502, obsolete.ticket).unwrap();
        assert!(!settlement.present_response);
        let final_preview = settlement.next.unwrap();
        assert_eq!(final_preview.trajectory, "final");
        assert!(final_preview.finished);
    }

    #[test]
    fn active_finish_cannot_be_followed_by_a_late_movement_preview() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let finish = coordinator.submit(500, "finished", true).unwrap().unwrap();
        assert!(finish.finished);
        assert!(
            coordinator
                .submit(501, "late-move", false)
                .unwrap()
                .is_none()
        );

        let settled = coordinator.settle(502, finish.ticket).unwrap();
        assert!(settled.present_response);
        assert!(settled.next.is_none());
    }

    #[test]
    fn adapter_can_discard_a_released_ticket_before_sending_it() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let dispatch = coordinator
            .submit(500, "candidate", false)
            .unwrap()
            .unwrap();
        assert!(coordinator.discard_before_send(dispatch.ticket).unwrap());
        assert!(!coordinator.has_in_flight());
        assert!(!coordinator.discard_before_send(dispatch.ticket).unwrap());

        let replacement = coordinator
            .submit(625, "replacement", false)
            .unwrap()
            .unwrap();
        assert_ne!(replacement.ticket.generation, dispatch.ticket.generation);
    }

    #[test]
    fn cancel_invalidates_a_late_response_before_presentation() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let active = coordinator.submit(0, "stroke", false).unwrap().unwrap();
        coordinator.cancel().unwrap();

        let settled = coordinator.settle(500, active.ticket).unwrap();
        assert!(!settled.present_response);
        assert!(settled.next.is_none());
    }

    #[test]
    fn generation_area_and_revision_mismatches_are_never_presented() {
        let original_request = request("area-a", 4);
        let mut coordinator = StrokePreviewCoordinator::new(original_request);
        let active = coordinator.submit(0, "stroke", false).unwrap().unwrap();

        let mut wrong_serial = active.ticket;
        wrong_serial.serial += 1;
        assert!(
            !coordinator
                .settle(125, wrong_serial)
                .unwrap()
                .present_response
        );
        let mut wrong_generation = active.ticket;
        wrong_generation.generation += 1;
        assert!(
            !coordinator
                .settle(125, wrong_generation)
                .unwrap()
                .present_response
        );
        let mut wrong_area = active.ticket;
        wrong_area.request = request("area-b", 4);
        assert!(
            !coordinator
                .settle(125, wrong_area)
                .unwrap()
                .present_response
        );
        let mut wrong_revision = active.ticket;
        wrong_revision.request = request("area-a", 5);
        assert!(
            !coordinator
                .settle(125, wrong_revision)
                .unwrap()
                .present_response
        );

        let accepted = coordinator.settle(125, active.ticket).unwrap();
        assert!(accepted.present_response);
    }

    #[test]
    fn changing_target_invalidates_late_work_and_releases_only_the_new_context() {
        let mut coordinator = StrokePreviewCoordinator::new(request("area-a", 4));
        let old = coordinator.submit(0, "old", false).unwrap().unwrap();
        coordinator.set_request(request("area-b", 5)).unwrap();
        assert!(coordinator.submit(10, "new", false).unwrap().is_none());

        let settled = coordinator.settle(125, old.ticket).unwrap();
        assert!(!settled.present_response);
        let next = settled.next.unwrap();
        assert_eq!(next.trajectory, "new");
        assert_eq!(next.ticket.request, request("area-b", 5));
        assert_ne!(next.ticket.generation, old.ticket.generation);
    }
}
