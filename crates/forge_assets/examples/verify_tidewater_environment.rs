//! Reopen an explicitly pinned package without upstream or rendering fixtures.
use forge_assets::tidewater_environment::EnvironmentPackage;
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
        .take(2 * 1024 * 1024 + 1)
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
    let package = EnvironmentPackage::load(&manifest, expected, reader)?;
    assert_eq!(package.file_count(), 266);
    assert!(package.file("sky.wgsl").is_some());
    let corrupted = EnvironmentPackage::load(&manifest, expected, |name, limit| {
        let mut bytes = reader(name, limit)?;
        if name == "sky.wgsl" {
            bytes[0] ^= 1;
        }
        Ok(bytes)
    });
    assert!(corrupted.is_err());
    let missing = EnvironmentPackage::load(&manifest, expected, |name, limit| {
        if name == "cloud-view-params.bin" {
            return Err("deliberately absent dependency".into());
        }
        reader(name, limit)
    });
    assert!(missing.is_err());
    let mut called = false;
    assert!(
        EnvironmentPackage::load(&manifest, ContentHash::from_bytes([0; 32]), |_, _| {
            called = true;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert!(!called);
    verify_async(&manifest, expected, &reader)?;
    verify_index(&manifest)?;
    println!(
        "266 environment files resolved; corrupted shader, missing dependency and wrong pin rejected"
    );
    Ok(())
}

fn verify_async(
    manifest: &[u8],
    expected: ContentHash,
    reader: &impl Fn(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<(), String> {
    let package = drive(EnvironmentPackage::load_async(
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
    assert_eq!(package.file_count(), 266);
    assert_eq!(
        package.file("sky.wgsl").ok_or("shader")?,
        reader("sky.wgsl", 32 * 1024 * 1024)?
    );
    let mut calls = 0;
    let invalid = drive(EnvironmentPackage::load_async(
        manifest,
        ContentHash::from_bytes([0; 32]),
        |_, _| {
            calls += 1;
            std::future::ready(Ok(Vec::new()))
        },
    ));
    assert!(invalid.is_err());
    assert_eq!(calls, 0);
    let corrupted = drive(EnvironmentPackage::load_async(
        manifest,
        expected,
        |_, _| {
            calls += 1;
            std::future::ready(Ok(vec![0]))
        },
    ));
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
    for mode in 0..3 {
        let mut invalid = original.clone();
        let files = invalid["files"].as_object_mut().ok_or("files")?;
        if mode == 0 {
            files.remove("sky.wgsl");
        }
        if mode == 1 {
            let entry = files.remove("sky.wgsl").ok_or("sky")?;
            files.insert("../sky.wgsl".into(), entry);
        }
        if mode == 2 {
            files.get_mut("sky.wgsl").ok_or("sky")?["bytes"] = (32 * 1024 * 1024).into();
        }
        let bytes = serde_json::to_vec(&invalid)?;
        let mut reads = 0;
        assert!(
            EnvironmentPackage::load(&bytes, forge_core::bytes_sha256(&bytes), |_, _| {
                reads += 1;
                Ok(Vec::new())
            })
            .is_err()
        );
        assert_eq!(reads, 0);
    }
    println!("Missing resource, path traversal and oversized entry rejected before reads");
    Ok(())
}
