//! Match a stall resource descriptor against the module's closed inventory.
use crate::{Result, module_objects::read_pin};
use forge_assets::{
    governed_glb_json_length, source_stall_package::Layout, validate_governed_glb_json,
};
use forge_project::module_package::{
    MAX_STALL_DESCRIPTOR_BYTES, MAX_STALL_MODEL_BYTES, ModulePackageManifest,
    ModuleSourceStallBinding,
};
use serde_json::Value;
use std::collections::BTreeMap;

pub struct ResolvedSourceStall<'a> {
    pub binding: &'a ModuleSourceStallBinding,
    pub layout: Layout,
    /// Resources remain keyed by names relative to the original descriptor.
    pub resources: BTreeMap<String, Vec<u8>>,
}

pub(crate) fn resolve<'a>(
    module: &ModulePackageManifest,
    binding: &'a ModuleSourceStallBinding,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<ResolvedSourceStall<'a>> {
    let layout = read_layout(module, binding, &mut read)?;
    let parent = descriptor_prefix(binding);
    let mut resources = BTreeMap::new();
    for (name, (size, _)) in layout.resource_pins()? {
        let path = format!("{parent}{name}");
        // read_layout matched the exact resource against the verified module inventory.
        resources.insert(name.to_owned(), read(&path, size)?);
    }
    Ok(ResolvedSourceStall {
        binding,
        layout,
        resources,
    })
}

/// Decode a pinned descriptor and check that every declared resource is present
/// exactly once. The archive or installed payload reader verifies the resource bytes.
pub(crate) fn read_layout(
    module: &ModulePackageManifest,
    binding: &ModuleSourceStallBinding,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Layout> {
    let descriptor = &binding.package.manifest;
    let bytes = read_pin(module, descriptor, MAX_STALL_DESCRIPTOR_BYTES, &mut read)?;
    let layout: Layout = serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    let pins = layout.resource_pins()?;
    let parent = descriptor_prefix(binding);
    if pins.len() != binding.package.resources.len() {
        return Err("Source stall resource count differs from its descriptor".into());
    }
    for (name, (bytes, sha256)) in pins {
        let path = format!("{parent}{name}");
        if !binding
            .package
            .resources
            .iter()
            .any(|pin| pin.path == path && pin.sha256 == sha256)
            || !module.files.iter().any(|file| {
                file.path == path && file.bytes == bytes as u64 && file.sha256 == sha256
            })
        {
            return Err(format!(
                "Source stall resource differs from its descriptor: {path}"
            ));
        }
    }
    if let Some(animated) = &binding.animated_model {
        let payload = read_pin(module, &animated.model, MAX_STALL_MODEL_BYTES, &mut read)?;
        let header = payload
            .first_chunk::<20>()
            .ok_or("Truncated stall model header")?;
        let length =
            governed_glb_json_length(header, payload.len() as u64).map_err(|e| e.to_string())?;
        let bytes = &payload[20..20 + length];
        validate_governed_glb_json(bytes).map_err(|e| e.to_string())?;
        let document: Value = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        for (collection, index, attribute) in [
            ("scenes", animated.scene_index, "nodes"),
            ("animations", animated.clip_index, "channels"),
        ] {
            if document[collection]
                .get(index as usize)
                .and_then(|entry| entry[attribute].as_array())
                .is_none_or(Vec::is_empty)
            {
                return Err(format!(
                    "Stall model declares no usable {collection} entry at {index}"
                ));
            }
        }
    }
    Ok(layout)
}

fn descriptor_prefix(binding: &ModuleSourceStallBinding) -> &str {
    let path = &binding.package.manifest.path;
    path.rsplit_once('/')
        .map_or("", |(parent, _)| &path[..parent.len() + 1])
}
