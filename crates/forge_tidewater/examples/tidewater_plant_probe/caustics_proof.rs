//! Optional native readback of both photon maps for real GPU qualification.
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use forge_tidewater::tidewater_caustics::CausticMaps;
use std::{
    path::{Path, PathBuf},
    sync::atomic::Ordering,
};

#[derive(Resource)]
struct Proof {
    path: PathBuf,
    maps: CausticMaps,
    started: bool,
    results: [Option<serde_json::Value>; 2],
}

pub fn install(app: &mut App, capture: &Path, maps: CausticMaps) {
    app.insert_resource(Proof {
        path: capture.with_extension("caustics.json"),
        maps,
        started: false,
        results: [None, None],
    })
    .add_systems(Update, start);
}

fn start(mut commands: Commands, mut proof: ResMut<Proof>) {
    if proof.started || proof.maps.encoded_frames.load(Ordering::Acquire) == 0 {
        return;
    }
    proof.started = true;
    for (layer, image) in [proof.maps.fine.clone(), proof.maps.broad.clone()]
        .into_iter()
        .enumerate()
    {
        commands.spawn(Readback::texture(image)).observe(
            move |event: On<ReadbackComplete>, mut proof: ResMut<Proof>, mut commands: Commands| {
                record(&mut proof, layer, &event.data);
                commands.entity(event.entity).despawn();
            },
        );
    }
}

fn record(proof: &mut Proof, layer: usize, bytes: &[u8]) {
    let side = if layer == 0 { 512 } else { 256 };
    if bytes.len() != side * side * 8 || proof.results[layer].is_some() {
        eprintln!(
            "Invalid or duplicate Tidewater caustic readback: layer={layer} bytes={}",
            bytes.len()
        );
        return;
    }
    let mut red = 0_usize;
    let mut green = 0_usize;
    let mut invalid = 0_usize;
    for pixel in bytes.as_chunks::<8>().0 {
        for (channel, count) in [(0, &mut red), (2, &mut green)] {
            let bits = u16::from_le_bytes([pixel[channel], pixel[channel + 1]]);
            *count += usize::from(bits & 0x7fff != 0);
            invalid += usize::from(bits & 0x7c00 == 0x7c00);
        }
    }
    let raw = proof.path.with_extension(if layer == 0 {
        "caustics-fine.rgba16.bin"
    } else {
        "caustics-broad.rgba16.bin"
    });
    if let Err(error) = std::fs::write(&raw, bytes) {
        eprintln!("Tidewater caustic readback write failed: {error}");
        return;
    }
    proof.results[layer] = Some(serde_json::json!({
        "side": side,
        "bytes": bytes.len(),
        "sha256": forge_core::bytes_sha256(bytes).to_string(),
        "nonzero_red": red,
        "nonzero_green": green,
        "invalid_red_green": invalid,
        "raw": raw.file_name().and_then(|s| s.to_str()).unwrap_or_default()
    }));
    if let [Some(fine), Some(broad)] = &proof.results {
        let receipt = serde_json::json!({
            "schema": "forge.tidewater.caustic-gpu-proof.v1",
            "encoded_frames": proof.maps.encoded_frames.load(Ordering::Acquire),
            "fine": fine,
            "broad": broad,
            "pass": fine["nonzero_red"].as_u64().is_some_and(|v| v > 0)
                && fine["nonzero_green"].as_u64().is_some_and(|v| v > 0)
                && broad["nonzero_red"].as_u64().is_some_and(|v| v > 0)
                && broad["nonzero_green"].as_u64().is_some_and(|v| v > 0)
                && fine["invalid_red_green"] == 0
                && broad["invalid_red_green"] == 0
        });
        match serde_json::to_vec_pretty(&receipt)
            .map_err(std::io::Error::other)
            .and_then(|bytes| std::fs::write(&proof.path, bytes))
        {
            Ok(()) => eprintln!("TIDEWATER_CAUSTIC_GPU_PROOF {}", proof.path.display()),
            Err(error) => eprintln!("Tidewater caustic receipt write failed: {error}"),
        }
    }
}
