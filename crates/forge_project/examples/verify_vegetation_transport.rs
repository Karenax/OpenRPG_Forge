//! Read the complete real imported population through the native host protocol.
use forge_core::bytes_sha256;
use forge_project::authoring::{
    AuthoringRequest, host::AuthoringHost, vegetation_source::VegetationSourceAssembly,
    workspace::WorkspaceHostRequest,
};
use std::{error::Error, path::Path};

fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err("WORKSPACE AREA_ID required".into());
    }
    let mut host = AuthoringHost::new(Path::new(&args[0]))?;
    host.execute_workspace(WorkspaceHostRequest::Open)?;
    let state = host.execute(AuthoringRequest::OpenManaged {
        area_id: args[1].parse()?,
    })?;
    let head = state.zone_head.as_ref().ok_or("Missing zone head")?;
    let hash = head.content_hash()?;
    let document = head.base.vegetation.as_ref().ok_or("Missing vegetation")?;
    let source = document
        .imported_source
        .as_ref()
        .ok_or("Missing imported source")?;
    assert!(
        host.read_vegetation_source(bytes_sha256(b"stale"), 0, 0)
            .is_err()
    );
    assert!(
        host.read_vegetation_source(hash, source.blocks.len(), 0)
            .is_err()
    );
    let mut count = 0;
    let mut chunks = 0;
    let mut total = 0;
    for (index, block) in source.blocks.iter().enumerate() {
        let mut assembly = VegetationSourceAssembly::new(head, index)?;
        while let Some(offset) = assembly.next_offset() {
            assembly.accept(host.read_vegetation_source(hash, index, offset)?)?;
            chunks += 1;
        }
        let bytes = assembly.finish()?;
        count += block.decode(&bytes, document.extent_m)?.len();
        total += bytes.len();
    }
    assert_eq!(state, host.execute(AuthoringRequest::Inspect)?);
    host.execute(AuthoringRequest::Close)?;
    assert!(host.read_vegetation_source(hash, 0, 0).is_err());
    println!(
        "VEGETATION_TRANSPORT_PASS blocks={} instances={count} chunks={chunks} bytes={total} head={hash}; stale/unknown/closed refused; canonical state unchanged",
        source.blocks.len()
    );
    Ok(())
}
