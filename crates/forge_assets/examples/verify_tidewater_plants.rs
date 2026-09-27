//! Real portable package, synchronous/asynchronous acquisition and hostile-input refusal.
use forge_assets::tidewater_plants::PlantPackage;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::{Value, json};
use std::{error::Error, fs, io::Read, path::PathBuf};
fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err("PACKAGE_ROOT MANIFEST_SHA256 required".into());
    }
    let root = PathBuf::from(&args[0]).canonicalize()?;
    let pin: ContentHash = args[1].parse()?;
    let manifest = fs::read(root.join("package.json"))?;
    let reader = |name: &str, limit: usize| -> Result<Vec<u8>, String> {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Payload escapes package".into());
        }
        let mut data = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut data)
            .map_err(|e| e.to_string())?;
        Ok(data)
    };
    let package = PlantPackage::load(&manifest, pin, reader)?;
    assert_eq!(package.files().len(), 83);
    assert!(package.file("plant.wgsl").is_some());
    assert_eq!(
        package.source_fixture().to_string(),
        "0b4437a7fa404d47ef22b430f7fa4eff573b86c5fc5f62742d25a25be5561027"
    );
    index_refusals(&manifest)?;
    descriptor_refusals(&manifest, &reader)?;
    let mut calls = 0;
    assert!(
        PlantPackage::load(&manifest, ContentHash::from_bytes([0; 32]), |_, _| {
            calls += 1;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert_eq!(calls, 0);
    assert!(
        PlantPackage::load(&manifest, pin, |name, limit| {
            let mut data = reader(name, limit)?;
            if name == "leaf.bin" {
                data[0] ^= 1;
            }
            Ok(data)
        })
        .is_err()
    );
    assert!(
        PlantPackage::load(&manifest, pin, |name, limit| {
            if name == "leaf.bin" {
                return Err("deliberately missing dependency".into());
            }
            reader(name, limit)
        })
        .is_err()
    );
    let asynchronous = drive(PlantPackage::load_async(
        &manifest,
        pin,
        |name, limit| async move {
            let mut pending = true;
            std::future::poll_fn(move |cx| {
                if pending {
                    pending = false;
                    cx.waker().wake_by_ref();
                    std::task::Poll::Pending
                } else {
                    std::task::Poll::Ready(())
                }
            })
            .await;
            reader(&name, limit)
        },
    ))?;
    assert_eq!(package.files(), asynchronous.files());
    let mut calls = 0;
    assert!(
        drive(PlantPackage::load_async(&manifest, pin, |_, _| {
            calls += 1;
            std::future::ready(Ok(vec![0]))
        }))
        .is_err()
    );
    assert_eq!(calls, 1);
    println!(
        "PLANT_PACKAGE_PASS 83 files; sync/async identical; pending reads; preflight index and dependency closure; texture sizes; missing/corrupt resources; abort after first failure"
    );
    Ok(())
}
fn index_refusals(manifest: &[u8]) -> Result<(), Box<dyn Error>> {
    let source: Value = serde_json::from_slice(manifest)?;
    for mode in 0..6 {
        let mut invalid = source.clone();
        match mode {
            0 => {
                invalid["version"] = json!(2);
            }
            1 => {
                invalid["source_commit"] = json!("wrong");
            }
            2 => {
                let e = invalid["files"]
                    .as_object_mut()
                    .ok_or("files")?
                    .remove("plant.wgsl")
                    .ok_or("shader")?;
                invalid["files"]["../plant.wgsl"] = e;
            }
            3 => {
                invalid["files"]["plant.wgsl"]["bytes"] = json!(1_000_001);
            }
            4 => {
                invalid["files"]["leaf.bin"]["bytes"] = json!(0);
            }
            _ => {
                invalid["unexpected"] = json!(true);
            }
        }
        let raw = serde_json::to_vec(&invalid)?;
        let mut calls = 0;
        assert!(
            PlantPackage::load(&raw, bytes_sha256(&raw), |_, _| {
                calls += 1;
                Ok(Vec::new())
            })
            .is_err()
        );
        assert_eq!(calls, 0);
    }
    Ok(())
}
fn descriptor_refusals(
    manifest: &[u8],
    read: &impl Fn(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<(), Box<dyn Error>> {
    let source: Value = serde_json::from_slice(manifest)?;
    for mode in 0..4 {
        let mut invalid = source.clone();
        let mut replacement = None;
        match mode {
            0 => {
                invalid["files"]["leaf.bin"]["bytes"] = json!(4);
            }
            1 => {
                invalid["files"]
                    .as_object_mut()
                    .ok_or("files")?
                    .remove("grass-0-indices.bin");
            }
            2 => {
                invalid["files"]["unreferenced.bin"] =
                    json!({"bytes":4,"sha256":bytes_sha256(&[0;4])});
            }
            _ => {
                let mut grass: Value = serde_json::from_slice(&read("grass.json", 1_000_000)?)?;
                grass["mask"]["file"] = json!("../mask.bin");
                let raw = serde_json::to_vec(&grass)?;
                invalid["files"]["grass.json"] =
                    json!({"bytes":raw.len(),"sha256":bytes_sha256(&raw)});
                replacement = Some(raw);
            }
        }
        let raw = serde_json::to_vec(&invalid)?;
        let mut names = Vec::new();
        assert!(
            PlantPackage::load(&raw, bytes_sha256(&raw), |name, limit| {
                names.push(name.to_owned());
                if name == "grass.json"
                    && let Some(bytes) = &replacement
                {
                    return Ok(bytes.clone());
                }
                read(name, limit)
            })
            .is_err()
        );
        assert_eq!(
            names,
            ["vegetation-scene.json", "grass.json", "plant-textures.json"]
        );
    }
    Ok(())
}
fn drive<F: std::future::Future>(future: F) -> F::Output {
    let mut future = std::pin::pin!(future);
    let mut cx = std::task::Context::from_waker(std::task::Waker::noop());
    for _ in 0..8192 {
        if let std::task::Poll::Ready(value) = future.as_mut().poll(&mut cx) {
            return value;
        }
    }
    panic!("bounded async verification exceeded poll budget");
}
