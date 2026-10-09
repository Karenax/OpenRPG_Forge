//! Transient catalog facade. All insertions still use ManualPlacement/PlacementEdit.
use forge_assets::asset_browser::AssetBrowser;
use forge_core::{AssetId, ContentHash};
use leptos::prelude::*;
use std::cell::RefCell;

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) struct Entry {
    pub id: String,
    pub label: String,
    pub kind: String,
    pub reference: (AssetId, ContentHash),
    pub provenance: String,
}

type Provider = fn() -> Vec<Entry>;
thread_local! {
    static PROVIDERS: RefCell<Vec<Provider>> = const { RefCell::new(Vec::new()) };
    static REVISION: RefCell<Option<RwSignal<u64>>> = const { RefCell::new(None) };
}

pub(super) fn mount(provider: Provider) {
    PROVIDERS.with(|slot| *slot.borrow_mut() = vec![provider]);
    REVISION.with(|slot| *slot.borrow_mut() = Some(RwSignal::new(0)));
}

pub(super) fn unmount() {
    PROVIDERS.with(|slot| slot.borrow_mut().clear());
    REVISION.with(|slot| slot.replace(None));
}

pub(super) fn changed() {
    REVISION.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.update(|revision| *revision = revision.wrapping_add(1));
        }
    });
}

pub(super) fn imported() -> Vec<Entry> {
    REVISION.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            let _ = signal.get();
        }
    });
    current_imported()
}

fn current_imported() -> Vec<Entry> {
    PROVIDERS.with(|slot| {
        slot.borrow()
            .iter()
            .flat_map(|provider| provider())
            .collect()
    })
}

pub(super) fn entries(browser: Option<&AssetBrowser>) -> Vec<Entry> {
    let mut result = imported();
    if let Some(browser) = browser {
        result.extend(browser.report().assets.iter().filter_map(|asset| {
            Some(Entry {
                id: asset.id.clone(),
                label: asset.id.clone(),
                kind: asset.kind.clone(),
                reference: browser.reference(&asset.id).ok()?,
                provenance: String::new(),
            })
        }));
    }
    result.sort_by(|a, b| a.label.cmp(&b.label).then(a.id.cmp(&b.id)));
    result
}

/// Composite resources are admitted only while their verified provider is resident.
/// A revoked or changed package never falls back to GLB transport.
#[derive(Clone)]
pub(super) enum Admission {
    Package(Entry),
    Glb(AssetBrowser, (AssetId, ContentHash)),
}

pub(super) fn resolve(browser: Option<&AssetBrowser>, name: &str) -> Result<Admission, String> {
    if name.starts_with("verified-package/") {
        return current_imported()
            .into_iter()
            .find(|entry| entry.id == name)
            .map(Admission::Package)
            .ok_or(
                "Ressources de l’objet importé indisponibles ; recharger sa bibliothèque".into(),
            );
    }
    let browser = browser.ok_or("Charger le catalogue")?;
    let reference = browser.reference(name).map_err(|error| error.to_string())?;
    Ok(Admission::Glb(browser.clone(), reference))
}

impl Admission {
    pub(super) fn label(&self, fallback: &str) -> String {
        match self {
            Self::Package(entry) => entry.label.clone(),
            Self::Glb(_, _) => fallback.to_owned(),
        }
    }

    pub(super) fn reference(&self) -> (AssetId, ContentHash) {
        match self {
            Self::Package(entry) => entry.reference,
            Self::Glb(_, reference) => *reference,
        }
    }

    pub(super) fn still_available(&self) -> Result<(), String> {
        if let Self::Package(entry) = self {
            if !current_imported().iter().any(|current| current == entry) {
                return Err(
                    "Bibliothèque remplacée depuis la demande ; recommencer le placement".into(),
                );
            }
        }
        Ok(())
    }

    pub(super) async fn check(&self) -> Result<(), String> {
        match self {
            Self::Package(_) => self.still_available(),
            Self::Glb(browser, reference) => {
                super::verify::check_reference(browser, reference.0, reference.1)
                    .await
                    .map(|_| ())
            }
        }
    }
}

/// Preserve the governed browser's accent and separator tolerant search.
pub(super) fn search_terms(text: &str) -> String {
    text.to_lowercase()
        .chars()
        .map(|ch| match ch {
            'é' | 'è' | 'ê' | 'ë' => 'e',
            'à' | 'â' | 'ä' => 'a',
            'î' | 'ï' => 'i',
            'ô' | 'ö' => 'o',
            'ù' | 'û' | 'ü' => 'u',
            'ç' => 'c',
            '-' | '_' => ' ',
            other => other,
        })
        .collect()
}
