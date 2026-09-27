//! Reopen an explicitly pinned package without upstream or rendering fixtures.
use forge_assets::tidewater_package::BuildingPackage;
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
    let package = BuildingPackage::load(&manifest, expected, reader)?;
    assert_eq!(package.catalog().assets.len(), 23);
    assert!(package.file("village.wgsl").is_some());
    let corrupted = BuildingPackage::load(&manifest, expected, |name, limit| {
        let mut bytes = reader(name, limit)?;
        if name == "village.wgsl" {
            bytes[0] ^= 1;
        }
        Ok(bytes)
    });
    assert!(corrupted.is_err());
    let missing = BuildingPackage::load(&manifest, expected, |name, limit| {
        if name == "materials.json" {
            return Err("deliberately absent dependency".into());
        }
        reader(name, limit)
    });
    assert!(missing.is_err());
    let mut called = false;
    assert!(
        BuildingPackage::load(&manifest, ContentHash::from_bytes([0; 32]), |_, _| {
            called = true;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert!(!called);
    verify_async(&manifest, expected, &reader)?;
    println!(
        "23 buildings resolved; corrupted shader, missing library and wrong manifest pin rejected"
    );
    Ok(())
}

fn verify_async(
    manifest: &[u8],
    expected: ContentHash,
    reader: &impl Fn(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<(), String> {
    let package = drive(BuildingPackage::load_async(
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
    assert_eq!(package.catalog().assets.len(), 23);
    assert_eq!(
        package.file("village.wgsl").ok_or("shader")?,
        reader("village.wgsl", 32 * 1024 * 1024)?
    );
    let mut calls = 0;
    let invalid = drive(BuildingPackage::load_async(
        manifest,
        ContentHash::from_bytes([0; 32]),
        |_, _| {
            calls += 1;
            std::future::ready(Ok(Vec::new()))
        },
    ));
    assert!(invalid.is_err());
    assert_eq!(calls, 0);
    let corrupted = drive(BuildingPackage::load_async(manifest, expected, |_, _| {
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
