//! Existing package decoder and inert transport, under the same module authority.
use super::{Acquired, Reply, Request, Snapshot, read};
use forge_assets::source_local_lights::{SourceLocalLightsPackage, package::transport};
use forge_core::ContentHash;
use std::{cell::RefCell, collections::BTreeMap, future::Future, sync::Arc};

pub(super) async fn acquire<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    request: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<BTreeMap<ContentHash, Arc<SourceLocalLightsPackage>>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let mut result = BTreeMap::new();
    for binding in &snapshot.local_lights {
        let pin = binding.manifest.sha256;
        let package = if let Some(package) = previous.and_then(|old| old.local_lights.get(&pin)) {
            Arc::clone(package)
        } else {
            let manifest = read(
                snapshot,
                &binding.manifest.path,
                2 * 1024 * 1024,
                request,
                current,
            )
            .await?;
            let members = SourceLocalLightsPackage::resource_pins(&manifest, pin)
                .map_err(|e| e.to_string())?;
            let license =
                SourceLocalLightsPackage::license_pin(&manifest, pin).map_err(|e| e.to_string())?;
            let prefix = binding
                .manifest
                .path
                .rsplit_once('/')
                .map_or(String::new(), |(parent, _)| format!("{parent}/"));
            let package = SourceLocalLightsPackage::load_async(&manifest, pin, |name, maximum| {
                let expected = members.get(&name).map(|reference| reference.sha256);
                let path =
                    expected.map(|hash| transport::member_path(&prefix, hash, license.sha256));
                let raw = expected == Some(license.sha256);
                async move {
                    let path =
                        path.ok_or("Source light member absent from its admitted manifest")?;
                    let bytes = read(
                        snapshot,
                        &path,
                        transport::encoded_bound(maximum, raw)?,
                        request,
                        current,
                    )
                    .await?;
                    transport::decode(bytes, raw, maximum)
                }
            })
            .await
            .map_err(|e| e.to_string())?;
            Arc::new(package)
        };
        if package.descriptor().content_hash() != binding.descriptor_sha256 {
            return Err("Source light descriptor differs from its admitted module binding".into());
        }
        if result.insert(pin, package).is_some() {
            return Err("Duplicate module source-light package".into());
        }
    }
    Ok(result)
}
