//! Verified procedural acquisition; publication shares the library generation boundary.
use forge_core::{AssetId, ContentHash};
use forge_tidewater::tidewater_debris::{DebrisTemplate, PreparedDebris};
use forge_tidewater_assets::tidewater_debris::native::NativeInput;
use std::{cell::RefCell, collections::BTreeSet, rc::Rc};

pub(crate) struct Prepared {
    pub input: NativeInput,
    pub geometry: PreparedDebris,
    assets: BTreeSet<AssetId>,
}

thread_local! {
    static INPUT: RefCell<Option<Rc<Prepared>>> = const { RefCell::new(None) };
}

pub(super) async fn load(
    ticket: u64,
    pin: Option<ContentHash>,
) -> Result<Option<Rc<Prepared>>, String> {
    let Some(pin) = pin else {
        return Ok(None);
    };
    let manifest = crate::governed_kit::transport::fetch_building_payload(
        pin,
        "debris-native.json",
        1_048_576,
    )
    .await?;
    if !super::current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let input = NativeInput::load_async(&manifest, pin, |name, limit| async move {
        if !super::current(ticket) {
            return Err("Chargement remplacé".into());
        }
        crate::governed_kit::transport::fetch_building_payload(pin, &name, limit).await
    })
    .await?;
    if !super::current(ticket) {
        return Err("Chargement remplacé".into());
    }
    // Fail before publishing if renderer decoding or the shared source ABI differs.
    let geometry = PreparedDebris::from_package(&input.geometry)?;
    forge_tidewater::tidewater_debris::nature::shader_source(&input.nature_program)?;
    let assets = geometry
        .objects
        .iter()
        .map(|object| object.asset_id)
        .collect();
    Ok(Some(Rc::new(Prepared {
        input,
        geometry,
        assets,
    })))
}

pub(super) fn publish(input: Option<Rc<Prepared>>) {
    INPUT.with(|slot| slot.replace(input));
}

pub(super) fn input() -> Option<Rc<Prepared>> {
    INPUT.with(|slot| slot.borrow().clone())
}

pub(super) fn templates() -> Vec<DebrisTemplate> {
    input().map_or_else(Vec::new, |prepared| prepared.geometry.templates())
}

pub(super) fn contains(asset: AssetId) -> bool {
    INPUT.with(|slot| {
        slot.borrow()
            .as_ref()
            .is_some_and(|input| input.assets.contains(&asset))
    })
}

pub(super) fn summary() -> String {
    INPUT.with(|slot| {
        if slot.borrow().is_some() {
            " Les 805 décors de plage sont vérifiés ; leur édition est en préparation.".into()
        } else {
            String::new()
        }
    })
}
