//! Registration diagnostics only: accepted-source and transform gating are separate.
use bevy::prelude::*;
use forge_engine::ManualPlacementCollisionStatus as Status;

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn report_status(statuses: Query<&Status>, mut previous: Local<String>) {
    if statuses.is_empty() {
        previous.clear();
        return;
    }
    let mut objects = 0;
    let mut registered = 0;
    let mut waiting = 0;
    let mut failure = None;
    for status in &statuses {
        objects += 1;
        match status {
            Status::Registered { primitives } => registered += primitives,
            Status::Waiting {
                registered: count,
                expected,
            } => {
                registered += count;
                waiting += expected - count;
            }
            Status::MissingPrimitives => failure = Some("aucune primitive UCX"),
            Status::Failed(reason) => failure = Some(*reason),
        }
    }
    let message = format!(
        "{objects} placements · {registered} primitives synchronisées dans Rapier · {waiting} en attente{} · Jouer vérifie aussi la source acceptée.",
        failure.map_or_else(String::new, |reason| format!(" · erreur : {reason}"))
    );
    // A same-size reconciliation can invalidate and then requalify a placement
    // without changing the final collision count. The projection system emits
    // an interim UCX message in that interval; restore the registered status
    // even when it equals our previous report.
    let interim_message_visible =
        super::STATUS.with(|slot| slot.borrow().contains("préparation UCX demandée"));
    if *previous != message || interim_message_visible {
        super::report(message.clone());
        *previous = message;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_count_reprojection_restores_registered_status() {
        let mut app = App::new();
        app.add_systems(Update, report_status);
        app.world_mut().spawn(Status::Registered { primitives: 3 });
        app.update();
        let registered = super::super::STATUS.with(|slot| slot.borrow().clone());
        assert!(registered.starts_with("1 placements · 3 primitives synchronisées"));

        super::super::report(
            "1 placements LOD0 projetés · préparation UCX demandée ; Jouer attend les collisions synchronisées."
                .into(),
        );
        app.update();
        assert_eq!(
            super::super::STATUS.with(|slot| slot.borrow().clone()),
            registered
        );
    }
}
