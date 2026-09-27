//! Reject events from a draft owned by another zone before sending a command.
use forge_core::AreaId;

/// Invalidate reviewed commands only when editable fields change, not on mount.
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(super) fn invalidate_on_change<T: PartialEq + 'static>(
    read: impl Fn() -> T + Send + Sync + 'static,
) {
    use leptos::prelude::*;
    Effect::new(move |previous: Option<T>| {
        let draft = read();
        if previous.as_ref().is_some_and(|old| old != &draft) {
            untrack(|| {
                super::authoring_brush_pointer::cancel();
                super::authoring_shortcuts::cancel_preview();
            });
        }
        draft
    });
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(super) fn protect(
    state: leptos::prelude::RwSignal<Option<forge_project::authoring::AuthoringState>>,
    send: leptos::prelude::Callback<Result<forge_project::authoring::AuthoringRequest, String>>,
) -> leptos::prelude::Callback<Result<forge_project::authoring::AuthoringRequest, String>> {
    use leptos::prelude::*;
    let area = move || {
        state.with_untracked(|s| {
            s.as_ref()
                .and_then(|s| s.zone_head.as_ref())
                .map(|h| h.base.terrain.area_id)
        })
    };
    let expected = area();
    Callback::new(move |request| {
        let scope = expected
            .ok_or_else(|| "Inspecteur sans zone source : rouvrir l’objet.".to_owned())
            .and_then(|expected| check(expected, area()));
        send.run(scope.and(request));
    })
}

pub(super) fn check(expected: AreaId, current: Option<AreaId>) -> Result<(), String> {
    if current == Some(expected) {
        Ok(())
    } else {
        Err("La zone de cet inspecteur a changé. Rouvrir l’objet dans la zone courante avant de retoucher.".into())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn draft_scope_rejects_other_zone_and_closed_session() {
        let a = AreaId::deterministic("draft-a");
        let b = AreaId::deterministic("draft-b");
        assert!(check(a, Some(a)).is_ok());
        assert!(check(a, Some(b)).is_err());
        assert!(check(a, None).is_err());
    }
}
