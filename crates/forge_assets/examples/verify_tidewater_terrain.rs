//! Reopen an explicitly pinned package without upstream or rendering fixtures.
use forge_assets::tidewater_terrain::TerrainPackage;
use forge_core::ContentHash;
use std::{error::Error, fs, io::Read, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("PACKAGE_ROOT EXPECTED_MANIFEST_SHA256 required".into());
    }
    let root = PathBuf::from(&args[0]).canonicalize()?;
    let expected: ContentHash = args[1].parse()?;
    let mut manifest = Vec::new();
    fs::File::open(root.join("package.json"))?
        .take(16 * 1024 + 1)
        .read_to_end(&mut manifest)?;
    let reader = |name: &str, limit: usize| -> Result<Vec<u8>, String> {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Payload escapes package".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        Ok(bytes)
    };
    let package = TerrainPackage::load(&manifest, expected, reader)?;
    assert!(matches!(package.file_count(), 6 | 7));
    assert!(package.file("terrain.wgsl").is_some());
    let corrupted = TerrainPackage::load(&manifest, expected, |name, limit| {
        let mut bytes = reader(name, limit)?;
        if name == "terrain.wgsl" {
            bytes[0] ^= 1;
        }
        Ok(bytes)
    });
    assert!(corrupted.is_err());
    let missing = TerrainPackage::load(&manifest, expected, |name, limit| {
        if name == "terrain-detail.bin" {
            return Err("deliberately absent dependency".into());
        }
        reader(name, limit)
    });
    assert!(missing.is_err());
    let mut called = false;
    assert!(
        TerrainPackage::load(&manifest, ContentHash::from_bytes([0; 32]), |_, _| {
            called = true;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert!(!called);
    verify_async(&manifest, expected, &reader)?;
    verify_index(&manifest)?;
    verify_text(&manifest, &reader)?;
    println!(
        "Terrain resources resolved; corrupted shader, missing dependency and wrong pin rejected"
    );
    Ok(())
}

fn verify_async(
    manifest: &[u8],
    expected: ContentHash,
    reader: &impl Fn(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<(), String> {
    let package = drive(TerrainPackage::load_async(
        manifest,
        expected,
        |name, limit| async move {
            let mut yielded = false;
            std::future::poll_fn(move |cx| {
                if yielded {
                    std::task::Poll::Ready(())
                } else {
                    yielded = true;
                    cx.waker().wake_by_ref();
                    std::task::Poll::Pending
                }
            })
            .await;
            reader(&name, limit)
        },
    ))?;
    assert!(matches!(package.file_count(), 6 | 7));
    assert_eq!(
        package.file("terrain.wgsl").ok_or("shader")?,
        reader("terrain.wgsl", 32 * 1024 * 1024)?
    );
    let mut calls = 0;
    let invalid = drive(TerrainPackage::load_async(
        manifest,
        ContentHash::from_bytes([0; 32]),
        |_, _| {
            calls += 1;
            std::future::ready(Ok(Vec::new()))
        },
    ));
    assert!(invalid.is_err());
    assert_eq!(calls, 0);
    let corrupted = drive(TerrainPackage::load_async(manifest, expected, |_, _| {
        calls += 1;
        std::future::ready(Ok(vec![0]))
    }));
    assert!(corrupted.is_err());
    assert_eq!(calls, 1);
    println!(
        "Async acquisition survives pending reads; invalid pin makes no request; corrupt payload stops acquisition"
    );
    Ok(())
}

/// Bounded executor for deterministic futures that only yield once per payload.
fn drive<F: std::future::Future>(future: F) -> F::Output {
    let mut future = std::pin::pin!(future);
    let mut context = std::task::Context::from_waker(std::task::Waker::noop());
    for _ in 0..8192 {
        if let std::task::Poll::Ready(output) = future.as_mut().poll(&mut context) {
            return output;
        }
    }
    panic!("Package verification future exceeded its bounded poll count");
}

fn verify_index(manifest: &[u8]) -> Result<(), Box<dyn Error>> {
    let original: serde_json::Value = serde_json::from_slice(manifest)?;
    for mode in 0..7 {
        let mut invalid = original.clone();
        let files = invalid["files"].as_object_mut().ok_or("files")?;
        if mode == 0 {
            files.remove("terrain.wgsl");
        }
        if mode == 1 {
            let entry = files.remove("terrain.wgsl").ok_or("sky")?;
            files.insert("../terrain.wgsl".into(), entry);
        }
        if mode == 2 {
            files.get_mut("terrain.wgsl").ok_or("sky")?["bytes"] = (32 * 1024 * 1024).into();
        }
        if mode == 3 {
            files.get_mut("terrain-normal.bin").ok_or("normal")?["bytes"] = 4.into();
        }
        if mode == 4 {
            invalid["version"] = 3.into();
        }
        if mode == 5 {
            let files = invalid["files"].as_object_mut().ok_or("files")?;
            if files.remove("hill-shadow.wgsl").is_none() {
                files.insert(
                    "hill-shadow.wgsl".into(),
                    serde_json::json!({
                        "bytes": 1, "sha256": forge_core::bytes_sha256(b"x")
                    }),
                );
            }
        }
        if mode == 6 {
            invalid["version"] = if invalid["version"] == 1 {
                2.into()
            } else {
                1.into()
            };
        }
        let bytes = serde_json::to_vec(&invalid)?;
        let mut reads = 0;
        assert!(
            TerrainPackage::load(&bytes, forge_core::bytes_sha256(&bytes), |_, _| {
                reads += 1;
                Ok(Vec::new())
            })
            .is_err()
        );
        assert_eq!(reads, 0);
    }
    println!(
        "Missing resource, path traversal, oversized entry, truncated mip chain unknown version and mismatched version inventory rejected before reads"
    );
    Ok(())
}

/// A newly pinned manifest must not make invalid shader bytes acceptable.
fn verify_text(
    manifest: &[u8],
    reader: &impl Fn(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<(), Box<dyn Error>> {
    for replacement in [vec![0xff], vec![b' '; 8]] {
        let mut index: serde_json::Value = serde_json::from_slice(manifest)?;
        index["files"]["terrain-lod.wgsl"] = serde_json::json!({
            "sha256": forge_core::bytes_sha256(&replacement),
            "bytes": replacement.len(),
        });
        let encoded = serde_json::to_vec(&index)?;
        let expected = forge_core::bytes_sha256(&encoded);
        let mut names = Vec::new();
        let result = TerrainPackage::load(&encoded, expected, |name, limit| {
            names.push(name.to_owned());
            if name == "terrain-lod.wgsl" {
                Ok(replacement.clone())
            } else {
                reader(name, limit)
            }
        });
        assert!(result.is_err());
        assert_eq!(names.last().map(String::as_str), Some("terrain-lod.wgsl"));
    }
    println!("Pinned invalid UTF-8 and blank shaders rejected; no subsequent payload requested");
    Ok(())
}
