//! Exercise the actual current library reader, without GUI or visual qualification.
#[allow(dead_code)]
#[path = "@@A@@/asset_library_protocol.rs"]
mod asset_library_protocol;
#[allow(dead_code)]
#[path = "@@A@@/asset_library_model_wire.rs"]
mod asset_library_model_wire;
#[allow(dead_code)]
mod asset_library_native;
use asset_library_protocol::*;
use std::{collections::BTreeSet, io::Write, path::Path};

fn main() -> Result<(), String> {
    let args: Vec<_> = std::env::args().collect();
    let out = Path::new(&args[2]);
    let mut library = asset_library_native::Library::new(Path::new(&args[1]))?;
    let mut ledger = std::fs::File::create(out.join("OBSERVATIONS.v1.jsonl")).map_err(|e| e.to_string())?;
    let mut seen = BTreeSet::new();
    let mut offset = 0usize;
    let mut total = 0usize;
    let mut revision = String::new();
    let mut verified = 0usize;
    let mut missing = 0usize;
    let mut failed = 0usize;
    let mut source_cards = 0usize;
    let mut model_cards = 0usize;
    loop {
        let reply = library.execute(LibraryRequest::Search { query: LibraryQuery {
            offset, limit: 48, ..Default::default()
        } });
        let page = match reply {
            Ok(LibraryResponse::Page { page }) => page,
            Ok(_) => return Err("Unexpected search response".into()),
            Err(error) if total > 0 => {
                failed += 1;
                writeln!(ledger, "{}", serde_json::json!({"page_offset":offset,"status":"search_failed","error":error})).map_err(|e|e.to_string())?;
                offset += 48;
                if offset >= total { break; }
                continue;
            }
            Err(error) => return Err(error),
        };
        if revision.is_empty() { revision.clone_from(&page.revision); total = page.total_assets; }
        if revision != page.revision || total != page.total_assets { return Err("Library changed during global image check".into()); }
        for card in page.assets {
            if !seen.insert(card.id.clone()) { return Err("Duplicate paginated identity".into()); }
            if card.availability == "source_support" { source_cards += 1; } else if card.availability == "converted_model" { model_cards += 1; }
            let mut row = serde_json::json!({"id":card.id,"pack":card.pack,"availability":card.availability});
            if !card.has_thumbnail {
                missing += 1; row["status"] = "no_primary_thumbnail_declared".into();
            } else {
                let checked = (|| {
                    let LibraryResponse::Thumbnail { bytes } = library.execute(LibraryRequest::Thumbnail {
                        id: card.id.clone(), revision: revision.clone(),
                    })? else { return Err("Unexpected thumbnail response".into()); };
                    if bytes.len() < 24 || !bytes.starts_with(b"\x89PNG\r\n\x1a\n") || &bytes[12..16] != b"IHDR" {
                        return Err("Invalid PNG header".into());
                    }
                    let width = u32::from_be_bytes(bytes[16..20].try_into().map_err(|_|"PNG width")?);
                    let height = u32::from_be_bytes(bytes[20..24].try_into().map_err(|_|"PNG height")?);
                    if width == 0 || height == 0 { return Err("Empty PNG dimensions".into()); }
                    Ok((bytes.len(), forge_core::bytes_sha256(&bytes).to_string(), width, height))
                })();
                match checked {
                    Ok((len, sha, width, height)) => {
                        verified += 1;
                        row["status"]="native_thumbnail_read_verified".into();row["bytes"]=len.into();row["sha256"]=sha.into();row["dimensions_px"]=serde_json::json!([width,height]);
                    }
                    Err(error) => { failed += 1;row["status"]="thumbnail_read_failed".into();row["error"]=error.into(); }
                }
            }
            writeln!(ledger,"{row}").map_err(|e|e.to_string())?;
        }
        ledger.flush().map_err(|e|e.to_string())?;
        let progress=serde_json::json!({"cards_observed":seen.len(),"expected_cards":total,"verified_primary_images":verified,"missing_declared_primary_images":missing,"failed":failed,"next_offset":offset+48,"revision":revision});
        std::fs::write(out.join("PROGRESS.v1.json"),serde_json::to_vec_pretty(&progress).unwrap()).map_err(|e|e.to_string())?;
        offset += 48;
        if offset >= total { break; }
    }
    let result=serde_json::json!({"cards_observed":seen.len(),"expected_cards":total,"model_cards":model_cards,"source_cards":source_cards,"verified_primary_images":verified,"missing_declared_primary_images":missing,"failed":failed,"all_cards_observed":seen.len()==total,"revision":revision,"studio_visual_verified":false,"full_import_increment":0});
    std::fs::write(out.join("NATIVE-RESULT.v1.json"),serde_json::to_vec_pretty(&result).unwrap()).map_err(|e|e.to_string())?;
    println!("{result}");
    if failed != 0 || seen.len() != total { return Err("Native library image access incomplete; inspect observations".into()); }
    Ok(())
}
