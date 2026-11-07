//! Read-only Rapier queries also work before the first simulation step.
//! A held simulation has synchronized shapes but its simulation BVH may be empty.
use bevy_rapier3d::{
    parry::query::DefaultQueryDispatcher,
    prelude::{QueryFilter, RapierContext, RapierQueryPipeline},
    rapier::geometry::DefaultBroadPhase,
};

/// Reuse the live simulation index, or build a scoped Rapier index from its
/// synchronized colliders while held. Never integrate bodies, consume change
/// flags, or alter the simulation's contact pairs/events. One index serves the
/// whole batch of rays and is dropped at the end of the query scope.
pub(crate) fn with_queries<T>(
    context: &RapierContext<'_>,
    simulation_active: bool,
    filter: QueryFilter<'_>,
    query: impl FnOnce(RapierQueryPipeline<'_>) -> T,
) -> T {
    if simulation_active {
        return context.with_query_pipeline(filter, query);
    }
    let mut broad_phase = DefaultBroadPhase::new();
    for (handle, collider) in context.colliders.colliders.iter() {
        if collider.is_enabled() {
            broad_phase.set_aabb(
                &context.simulation.integration_parameters,
                handle,
                collider.compute_aabb(),
            );
        }
    }
    RapierQueryPipeline::new_scoped(
        &broad_phase,
        context.colliders,
        context.rigidbody_set,
        &filter,
        &DefaultQueryDispatcher,
        query,
    )
}

#[cfg(test)]
mod tests;
