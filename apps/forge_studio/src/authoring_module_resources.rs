//! Module-scoped acquisition feeding the existing engine resource consumers.
use crate::authoring_assets::imported_buildings::{
    self, BuildingUpdate, EnvironmentUpdate, PlantUpdate,
};
use crate::{
    module_resources_acquisition::{self as acquisition, Acquired, Scope},
    module_resources_protocol::{Chunk, MAX_CHUNK_WIRE, Reply, Request},
};
use forge_assets::source_vegetation_package::PlantPackage;
use forge_core::ContentHash;
use forge_project::authoring::AuthoringState;
use std::{
    cell::RefCell,
    future::Future,
    pin::Pin,
    rc::Rc,
    task::{Context, Poll, Waker},
};
use wasm_bindgen::{JsCast, JsValue};

async fn read_resources(request: Request) -> Result<Reply, String> {
    let args = js_sys::Object::new();
    let payload = crate::encode_tauri_wire(&request)?;
    js_sys::Reflect::set(
        &args,
        &JsValue::from_str("request"),
        &JsValue::from_str(&payload),
    )
    .map_err(|_| "Requête de ressources du module illisible")?;
    let response =
        crate::web::invoke_tauri_raw_with_deadline("read_module_resources", args.as_ref(), 30_000)
            .await
            .map_err(|error| match request {
                Request::Describe { .. } => {
                    format!("Description des ressources du module : {error}")
                }
                Request::Read {
                    resource_index,
                    offset,
                    ..
                } => format!("Ressource du module {resource_index}, octet {offset} : {error}"),
            })?;
    let bytes = if response.is_instance_of::<js_sys::ArrayBuffer>() {
        js_sys::Uint8Array::new(&response)
    } else if response.is_instance_of::<js_sys::Uint8Array>() {
        response.unchecked_into::<js_sys::Uint8Array>()
    } else {
        return Err("Réponse binaire de ressources du module attendue".into());
    };
    match request {
        Request::Describe { .. } => {
            if bytes.length() > 16 * 1024 * 1024 {
                return Err("Description des ressources du module trop volumineuse".into());
            }
            serde_json::from_slice(&bytes.to_vec()).map_err(|error| error.to_string())
        }
        Request::Read { .. } => {
            if bytes.length() as usize > MAX_CHUNK_WIRE {
                return Err("Bloc binaire de ressources du module trop volumineux".into());
            }
            Chunk::from_wire(bytes.to_vec()).map(|chunk| Reply::Resource { chunk })
        }
    }
}

#[derive(Default)]
enum Phase {
    #[default]
    Idle,
    Pending,
    Ready(Option<Rc<Acquired>>),
    Failed(String),
}
#[derive(Default)]
struct Restage {
    plants: bool,
    objects: bool,
    environment: bool,
}
#[derive(Default)]
struct State {
    scope: Option<Scope>,
    owned: bool,
    epoch: u64,
    revision: u64,
    phase: Phase,
    last: Option<Rc<Acquired>>,
    waiters: Vec<Waker>,
    update: Option<PlantUpdate>,
    delivered: Option<ContentHash>,
    restage: Restage,
    buildings: Option<BuildingUpdate>,
    environment: Option<EnvironmentUpdate>,
    delivered_objects: Option<ContentHash>,
    delivered_environment: Option<ContentHash>,
    render_revision: u64,
}
thread_local! { static STATE: RefCell<State> = RefCell::new(State::default()); }

pub(crate) fn observe(state: &AuthoringState) -> bool {
    let scope = acquisition::Scope::from_state(state);
    let (changed, start, waiters) = STATE.with(|slot| {
        let mut current = slot.borrow_mut();
        let owned = Scope::owns_source(state);
        if scope.as_ref().ok().copied().flatten() == current.scope && owned == current.owned {
            return (false, None, Vec::new());
        }
        current.epoch = current.epoch.wrapping_add(1);
        current.revision = current.revision.wrapping_add(1);
        current.restage.plants |= !current.owned;
        current.restage.objects |= !current.owned;
        current.restage.environment |= !current.owned;
        current.owned = owned;
        current.update = None;
        current.buildings = None;
        current.environment = None;
        let start = match scope {
            Ok(Some(scope)) => {
                current.scope = Some(scope);
                current.phase = Phase::Pending;
                Some((scope, current.epoch, current.last.clone()))
            }
            Ok(None) => {
                current.scope = None;
                current.phase = Phase::Idle;
                current.last = None;
                None
            }
            Err(error) => {
                current.scope = None;
                current.phase = Phase::Failed(error);
                None
            }
        };
        (true, start, std::mem::take(&mut current.waiters))
    });
    wake(waiters);
    if let Some((scope, epoch, previous)) = start {
        launch(scope, epoch, previous);
    }
    if changed && !owns_source() {
        let _ = imported_buildings::restore_plants();
    }
    changed
}

fn launch(scope: Scope, epoch: u64, previous: Option<Rc<Acquired>>) {
    wasm_bindgen_futures::spawn_local(async move {
        let current = || STATE.with(|slot| slot.borrow().epoch == epoch);
        let mut reading = false;
        let result = acquisition::acquire(
            scope,
            previous,
            |request| {
                match request {
                    Request::Describe { .. } => report_loading("Lecture de l’inventaire du module…"),
                    Request::Read { .. } if !reading => {
                        reading = true;
                        report_loading("Transfert des ressources du module…");
                    }
                    Request::Read { .. } => {}
                }
                read_resources(request)
            },
            current,
        )
        .await;
        if !current() {
            return;
        }
        if result.is_ok() {
            report_loading("Préparation des ressources vérifiées du module…");
        }
        let waiters = STATE.with(|slot| {
            let mut state = slot.borrow_mut();
            if state.epoch != epoch {
                return Vec::new();
            }
            state.revision = state.revision.wrapping_add(1);
            match result.map(|value| value.map(Rc::new)) {
                Ok(data) => {
                    let prepared = prepare(&state, data.as_deref());
                    if let Err(error) = prepared {
                        state.phase = Phase::Failed(error.clone());
                        crate::authoring_vegetation::report(format!(
                            "Ressources végétales du module indisponibles : {error}"
                        ));
                    } else {
                        state.update = prepared.unwrap();
                        match prepare_surfaces(&state, data.as_deref()) {
                            Ok((objects, environment)) => {
                                state.buildings = objects;
                                state.environment = environment;
                                state.render_revision = state.render_revision.wrapping_add(1);
                            }
                            Err(error) => {
                                state.phase = Phase::Failed(error.clone());
                                crate::authoring_vegetation::report(format!(
                                    "Ressources du module indisponibles : {error}"
                                ));
                                return std::mem::take(&mut state.waiters);
                            }
                        }
                        state.last.clone_from(&data);
                        state.phase = Phase::Ready(data);
                    }
                }
                Err(error) => {
                    state.phase = Phase::Failed(error.clone());
                    crate::authoring_vegetation::report(format!(
                        "Ressources végétales du module indisponibles : {error}"
                    ));
                }
            }
            std::mem::take(&mut state.waiters)
        });
        wake(waiters);
        if available() {
            report_loading("Ressources du module prêtes pour la projection.");
        }
        imported_buildings::module_source_changed();
        // The initial viewport also needs to consume Phase::Failed and clear its loading overlay.
        crate::authoring_viewport::retry_imported();
    });
}

fn report_loading(message: &str) {
    if !crate::authoring_viewport::has_projection() {
        crate::authoring_viewport::report(message.into());
    }
}

fn prepare_surfaces(
    state: &State,
    data: Option<&Acquired>,
) -> Result<(Option<BuildingUpdate>, Option<EnvironmentUpdate>), String> {
    let objects_index = data.and_then(|data| data.objects_index);
    let environment_index = data.and_then(|data| data.environment_index);
    let objects = if state.restage.objects || objects_index != state.delivered_objects {
        Some(match data.and_then(|data| data.objects.as_ref()) {
            Some(package) => BuildingUpdate::Replace {
                prepared: Box::new((
                    forge_engine::source_surface::package::prepare_meshes(package)?,
                    forge_engine::source_surface::package::PreparedLibrary::load(package)?,
                )),
                module_index: objects_index,
            },
            None => BuildingUpdate::Remove,
        })
    } else {
        None
    };
    let environment =
        if state.restage.environment || environment_index != state.delivered_environment {
            Some(match data.and_then(|data| data.environment.as_ref()) {
                Some(package) => EnvironmentUpdate::Replace {
                    prepared: Box::new(
                        forge_engine::source_environment::package::prepare(package)
                            .map_err(|error| error.to_string())?,
                    ),
                    module_index: environment_index,
                },
                None => EnvironmentUpdate::Remove,
            })
        } else {
            None
        };
    Ok((objects, environment))
}

pub(crate) fn objects() -> Option<Rc<forge_assets::source_surface_package::SurfacePackage>> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.objects.clone(),
        _ => None,
    })
}

/// Classification may reuse immutable identities while a newer head is loading.
/// Drawing still requires the Ready phase and the current head's resource barrier.
pub(crate) fn object_catalog_package()
-> Option<Rc<forge_assets::source_surface_package::SurfacePackage>> {
    STATE.with(|slot| {
        let state = slot.borrow();
        let scope = state.scope?;
        state
            .last
            .as_ref()
            .filter(|data| {
                data.snapshot.module_id == scope.module
                    && data.snapshot.area_id == scope.area
                    && match scope.package() {
                        Some(pin) => {
                            !data.snapshot.legacy_reference && data.snapshot.package_sha256 == pin
                        }
                        None => data.snapshot.legacy_reference,
                    }
            })
            .and_then(|data| data.objects.clone())
    })
}

/// Keep classification scoped to the same module and package during a head reload.
pub(crate) fn fish_catalog_profiles() -> Vec<Rc<acquisition::ModuleFishProfile>> {
    STATE.with(|slot| {
        let state = slot.borrow();
        let Some(scope) = state.scope else {
            return Vec::new();
        };
        state
            .last
            .as_ref()
            .filter(|data| {
                data.snapshot.module_id == scope.module
                    && data.snapshot.area_id == scope.area
                    && Some(data.snapshot.package_sha256) == scope.package()
            })
            .map_or_else(Vec::new, |data| data.fish_profiles.clone())
    })
}
pub(crate) fn environment()
-> Option<Rc<forge_assets::source_environment_package::EnvironmentPackage>> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.environment.clone(),
        _ => None,
    })
}
pub(crate) fn programs()
-> Option<Rc<forge_assets::source_environment_package::programs::ProgramsPackage>> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.programs.clone(),
        _ => None,
    })
}
pub(crate) fn objects_index() -> Option<ContentHash> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.objects_index,
        _ => None,
    })
}
pub(crate) fn environment_index() -> Option<ContentHash> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.environment_index,
        _ => None,
    })
}
pub(crate) fn render_revision() -> u64 {
    STATE.with(|slot| slot.borrow().render_revision)
}
pub(crate) fn loading_status() -> Result<(), String> {
    STATE.with(|slot| {
        if let Phase::Failed(error) = &slot.borrow().phase {
            Err(format!("Ressources du module indisponibles : {error}"))
        } else {
            crate::authoring_viewport::report("Chargement des ressources du module…".into());
            Ok(())
        }
    })
}
pub(crate) fn take_buildings() -> Option<BuildingUpdate> {
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        let update = state.buildings.take()?;
        state.delivered_objects = match &update {
            BuildingUpdate::Remove => None,
            BuildingUpdate::Replace { module_index, .. } => *module_index,
        };
        state.restage.objects = false;
        Some(update)
    })
}
pub(crate) fn take_environment() -> Option<EnvironmentUpdate> {
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        let update = state.environment.take()?;
        state.delivered_environment = match &update {
            EnvironmentUpdate::Remove => None,
            EnvironmentUpdate::Replace { module_index, .. } => *module_index,
        };
        state.restage.environment = false;
        Some(update)
    })
}
pub(crate) fn restore_buildings() -> Result<(), String> {
    restore_surfaces(true)
}
pub(crate) fn restore_environment() -> Result<(), String> {
    restore_surfaces(false)
}
fn restore_surfaces(objects: bool) -> Result<(), String> {
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        if objects {
            state.restage.objects = true;
        } else {
            state.restage.environment = true;
        }
        let data = match &state.phase {
            Phase::Ready(data) => data.clone(),
            _ => None,
        };
        let (buildings, environment) = prepare_surfaces(&state, data.as_deref())?;
        if objects {
            state.buildings = buildings;
        } else {
            state.environment = environment;
        }
        Ok(())
    })
}

fn prepare(state: &State, data: Option<&Acquired>) -> Result<Option<PlantUpdate>, String> {
    let package = data.and_then(|data| data.imported.clone());
    let index = data.and_then(|data| data.imported_index);
    if !state.restage.plants && index == state.delivered {
        return Ok(None);
    }
    imported_buildings::prepare_plants(package, index).map(Some)
}

fn wake(waiters: Vec<Waker>) {
    for waker in waiters {
        waker.wake();
    }
}
pub(crate) fn owns_source() -> bool {
    STATE.with(|slot| slot.borrow().owned)
}
pub(crate) fn revision() -> u64 {
    STATE.with(|slot| slot.borrow().revision)
}
pub(crate) fn plant_package() -> Option<Rc<PlantPackage>> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.imported.clone(),
        _ => None,
    })
}
pub(crate) fn expected_index() -> Option<ContentHash> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(Some(data)) => data.imported_index,
        _ => None,
    })
}
pub(crate) fn available() -> bool {
    STATE.with(|slot| matches!(slot.borrow().phase, Phase::Ready(_)))
}

pub(crate) fn acquired() -> Option<Rc<Acquired>> {
    STATE.with(|slot| match &slot.borrow().phase {
        Phase::Ready(data) => data.clone(),
        _ => None,
    })
}

pub(crate) fn scope() -> Option<Scope> {
    STATE.with(|slot| slot.borrow().scope)
}

pub(crate) fn take_plants() -> Option<PlantUpdate> {
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        let update = state.update.take()?;
        state.delivered = match &update {
            PlantUpdate::Remove => None,
            PlantUpdate::Replace { module_index, .. } => *module_index,
        };
        state.restage.plants = false;
        Some(update)
    })
}

pub(crate) fn restore_plants() -> Result<(), String> {
    let retry = STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        if !matches!(state.phase, Phase::Failed(_)) {
            return None;
        }
        let scope = state.scope?;
        state.epoch = state.epoch.wrapping_add(1);
        state.revision = state.revision.wrapping_add(1);
        state.phase = Phase::Pending;
        Some((scope, state.epoch, state.last.clone()))
    });
    if let Some((scope, epoch, previous)) = retry {
        launch(scope, epoch, previous);
    }
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        state.restage.plants = true;
        state.update = match &state.phase {
            Phase::Ready(data) => prepare(&state, data.as_deref())?,
            _ => Some(PlantUpdate::Remove),
        };
        Ok(())
    })
}

struct Waiting(Scope);
impl Future for Waiting {
    type Output = Result<Option<Rc<Acquired>>, String>;
    fn poll(self: Pin<&mut Self>, context: &mut Context<'_>) -> Poll<Self::Output> {
        STATE.with(|slot| {
            let mut state = slot.borrow_mut();
            if state.scope != Some(self.0) {
                return Poll::Ready(Err("Chargement végétal remplacé par une autre carte".into()));
            }
            match &state.phase {
                Phase::Ready(data) => Poll::Ready(Ok(data.clone())),
                Phase::Failed(error) => Poll::Ready(Err(error.clone())),
                _ => {
                    if !state
                        .waiters
                        .iter()
                        .any(|waker| waker.will_wake(context.waker()))
                    {
                        state.waiters.push(context.waker().clone());
                    }
                    Poll::Pending
                }
            }
        })
    }
}

pub(crate) async fn for_head(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> Result<Option<Rc<Acquired>>, String> {
    let scope = STATE
        .with(|slot| slot.borrow().scope)
        .ok_or("Origine du module indisponible")?;
    if scope.area != head.base.terrain.area_id {
        return Err("Espèces demandées pour une autre carte".into());
    }
    Waiting(scope).await
}

pub(crate) fn current_companion_scope() -> Result<acquisition::SourceToken, String> {
    let (owned, scope, epoch) = STATE.with(|slot| {
        let state = slot.borrow();
        (state.owned, state.scope, state.epoch)
    });
    acquisition::SourceToken::capture(owned, scope, epoch)
}

/// Pin the complete module scope before the caller awaits any other resource.
pub(crate) fn capture_companion_scope(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> Result<acquisition::SourceToken, String> {
    let token = current_companion_scope()?;
    let scope = token.scope();
    if scope.area != head.base.terrain.area_id || scope.head != head.content_hash()? {
        return Err("Ressource demandée pour une autre révision de carte".into());
    }
    Ok(token)
}

pub(crate) fn ensure_companion_scope(token: acquisition::SourceToken) -> Result<(), String> {
    STATE.with(|slot| {
        let state = slot.borrow();
        token.ensure_current(state.owned, state.scope, state.epoch)
    })
}

/// Fetch an exact package from the retained origin before considering the Vault.
pub(crate) async fn placement_companion(
    token: acquisition::SourceToken,
    kind: forge_project::module_package::ModulePlacementCompanionKind,
    pin: forge_project::zone_authoring::session::SourceCompanionPin,
) -> Result<Option<Vec<u8>>, String> {
    ensure_companion_scope(token)?;
    let scope = token.scope();
    let acquired = Waiting(scope).await?;
    ensure_companion_scope(token)?;
    let Some(acquired) = acquired else {
        return Ok(None);
    };
    acquisition::read_companion(scope, &acquired.snapshot, kind, pin, read_resources, || {
        ensure_companion_scope(token).is_ok()
    })
    .await
}
