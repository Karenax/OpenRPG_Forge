use super::ZoneEditSession;
use crate::zone_authoring::ZoneBase;
use forge_core::ContentHash;
use std::collections::BTreeMap;

impl ZoneEditSession {
    /// Immutable source closure, including checkpoints and both sides of every
    /// regeneration. Undo/redo sources remain required even when not visible now.
    pub fn required_source_blobs(&self) -> Result<BTreeMap<ContentHash, usize>, String> {
        let mut required = BTreeMap::new();
        collect(&self.migration.head().base, &mut required)?;
        collect(&self.head.base, &mut required)?;
        for receipt in &self.receipts {
            if let Some(change) = &receipt.base_change {
                collect(&change.applied, &mut required)?;
                collect(&change.inverse, &mut required)?;
            }
        }
        Ok(required)
    }
}

fn collect(base: &ZoneBase, required: &mut BTreeMap<ContentHash, usize>) -> Result<(), String> {
    let mut insert = |hash, bytes| -> Result<(), String> {
        if required.insert(hash, bytes).is_some_and(|old| old != bytes) {
            return Err("Conflicting sizes for the same immutable source hash".into());
        }
        Ok(())
    };
    if let Some(source) = &base.terrain.sampled_source {
        insert(
            source.samples_sha256,
            source.byte_length().map_err(|e| e.to_string())?,
        )?;
    }
    if let Some(source) = base
        .vegetation
        .as_ref()
        .and_then(|v| v.imported_source.as_ref())
    {
        for block in &source.blocks {
            insert(block.instances_sha256, block.byte_len()?)?;
        }
    }
    Ok(())
}
