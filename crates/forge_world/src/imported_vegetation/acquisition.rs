//! Async bounded acquisition shared by Studio and native transport qualification.
use super::ImportedPopulation;
use forge_project::{
    authoring::vegetation_source::{VegetationSourceAssembly, VegetationSourceChunk},
    zone_authoring::ZoneAuthoringHead,
};
use std::{collections::BTreeMap, future::Future};

pub async fn acquire<F: Future<Output = Result<VegetationSourceChunk, String>>>(
    head: &ZoneAuthoringHead,
    mut fetch: impl FnMut(usize, usize) -> F,
    current: impl Fn() -> bool,
) -> Result<ImportedPopulation, String> {
    head.validate()?;
    let document = head
        .base
        .vegetation
        .as_ref()
        .ok_or("Missing vegetation document")?;
    let source = document
        .imported_source
        .as_ref()
        .ok_or("Missing imported vegetation source")?;
    let mut blobs = BTreeMap::new();
    for (index, block) in source.blocks.iter().enumerate() {
        let mut assembly = VegetationSourceAssembly::new(head, index)?;
        while let Some(offset) = assembly.next_offset() {
            if !current() {
                return Err("Vegetation acquisition superseded".into());
            }
            let response = fetch(index, offset).await?;
            if !current() {
                return Err("Vegetation acquisition superseded".into());
            }
            assembly.accept(response)?;
        }
        blobs.insert(block.instances_sha256, assembly.finish()?);
    }
    if !current() {
        return Err("Vegetation acquisition superseded".into());
    }
    ImportedPopulation::resolve(document, |hash, size| {
        let bytes = blobs
            .get(&hash)
            .ok_or("Missing acquired vegetation block")?;
        if bytes.len() != size {
            return Err("Vegetation block length mismatch".into());
        }
        Ok(bytes.clone())
    })
}
