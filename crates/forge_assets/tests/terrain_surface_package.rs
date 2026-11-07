//! Closed, data-only terrain surfaces never import module-owned engine code.
use forge_assets::source_terrain_package::{TerrainPackage, TerrainPackageFormat};
use forge_core::bytes_sha256;
use serde_json::{Value, json};
use std::collections::BTreeMap;

fn mip_bytes(size: [u32; 2]) -> usize {
    (0..=size[0].max(size[1]).ilog2())
        .map(|level| ((size[0] >> level).max(1) * (size[1] >> level).max(1) * 4) as usize)
        .sum()
}

fn fixture() -> (Value, BTreeMap<String, Vec<u8>>) {
    let payloads = BTreeMap::from([
        (
            "terrain-normal.bin".to_owned(),
            vec![17; mip_bytes([11, 19])],
        ),
        ("terrain-splat.bin".to_owned(), vec![23; mip_bytes([3, 5])]),
        ("terrain-detail.bin".to_owned(), vec![31; mip_bytes([1, 3])]),
        (
            "LICENSE.txt".to_owned(),
            b"Fixture-owned texture licence".to_vec(),
        ),
    ]);
    let files: serde_json::Map<_, _> = payloads
        .iter()
        .map(|(name, bytes)| {
            (
                name.clone(),
                json!({"sha256":bytes_sha256(bytes),"bytes":bytes.len()}),
            )
        })
        .collect();
    (
        json!({
            "schema":"forge.terrain-surface-package", "version":1,
            "raster": {"width":11,"height":19,"origin_xz_m":[-2.75,-4.75],
                "sample_spacing_m":0.5,"outside_height_m":-12.0,"encoding":"float32_le",
                "extent_m":[5.5,9.5]},
            "textures":{"normal":[11,19],"splat":[3,5],"detail":[1,3]},
            "files":files
        }),
        payloads,
    )
}

#[test]
fn rectangular_surface_package_loads_only_its_pinned_data_and_notice() {
    let (manifest, payloads) = fixture();
    let index = serde_json::to_vec(&manifest).unwrap();
    let hash = bytes_sha256(&index);
    let geometry = TerrainPackage::manifest_geometry(&index, hash).unwrap();
    assert_eq!([geometry.width, geometry.height], [11, 19]);
    let matches = |dimensions, origin, spacing, outside, extent, authored_spacing| {
        geometry.matches_sampled_domain(
            dimensions,
            origin,
            spacing,
            outside,
            extent,
            authored_spacing,
        )
    };
    assert!(matches(
        [11, 19],
        [-2.75, -4.75],
        0.5,
        -12.0,
        [5.5, 9.5],
        0.5
    ));
    assert!(!matches(
        [11, 18],
        [-2.75, -4.75],
        0.5,
        -12.0,
        [5.5, 9.5],
        0.5
    ));
    assert!(!matches(
        [11, 19],
        [-2.5, -4.75],
        0.5,
        -12.0,
        [5.5, 9.5],
        0.5
    ));
    assert!(!matches(
        [11, 19],
        [-2.75, -4.75],
        1.0,
        -12.0,
        [5.5, 9.5],
        0.5
    ));
    assert!(!matches(
        [11, 19],
        [-2.75, -4.75],
        0.5,
        -90.0,
        [5.5, 9.5],
        0.5
    ));
    assert!(!matches(
        [11, 19],
        [-2.75, -4.75],
        0.5,
        -12.0,
        [5.5, 9.0],
        0.5
    ));
    assert!(!matches(
        [11, 19],
        [-2.75, -4.75],
        0.5,
        -12.0,
        [5.5, 9.5],
        1.0
    ));
    assert_eq!(
        TerrainPackage::manifest_license_name(&index, hash).unwrap(),
        "LICENSE.txt"
    );
    let pins = TerrainPackage::resource_pins(&index, hash).unwrap();
    assert_eq!(pins.len(), 4);
    assert!(!pins.contains_key("terrain.wgsl"));
    let mut reads = Vec::new();
    let package = TerrainPackage::load(&index, hash, |name, maximum| {
        reads.push(name.to_owned());
        let bytes = payloads.get(name).ok_or("unexpected payload")?;
        assert_eq!(bytes.len(), maximum);
        Ok(bytes.clone())
    })
    .unwrap();
    assert_eq!(reads.len(), 4);
    assert_eq!(package.format(), TerrainPackageFormat::SurfaceData);
    assert_eq!(package.raster_geometry(), geometry);
    assert_eq!(package.texture_dimensions().normal, [11, 19]);
    assert_eq!(package.texture_dimensions().splat, [3, 5]);
    assert_eq!(package.texture_dimensions().detail, [1, 3]);
    assert_eq!(
        package.file("terrain-splat.bin"),
        Some(payloads["terrain-splat.bin"].as_slice())
    );
    assert_eq!(package.file("terrain.wgsl"), None);
}

#[test]
fn invalid_surface_index_fails_before_payload_transport() {
    let (valid, _) = fixture();
    let mut invalid = Vec::new();
    let mut item = valid.clone();
    item["textures"]["normal"] = json!([11, 18]);
    invalid.push(item);
    let mut item = valid.clone();
    item["textures"]["splat"] = json!([0, 5]);
    invalid.push(item);
    let mut item = valid.clone();
    item["textures"]["detail"] = json!([4097, 1]);
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["origin_xz_m"] = json!([-2.0, -4.75]);
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["extent_m"] = json!([6.0, 9.5]);
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["sample_spacing_m"] = json!(0.3);
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["width"] = json!(4097);
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["encoding"] = json!("uint16");
    invalid.push(item);
    let mut item = valid.clone();
    item["files"]["terrain-normal.bin"]["bytes"] = json!(4);
    invalid.push(item);
    let mut item = valid.clone();
    item["files"].as_object_mut().unwrap().remove("LICENSE.txt");
    invalid.push(item);
    let mut item = valid.clone();
    item["files"]["../shader.wgsl"] = json!({"bytes":1,"sha256":bytes_sha256(b"x")});
    invalid.push(item);
    let mut item = valid.clone();
    item["files"]["terrain.wgsl"] = json!({"bytes":1,"sha256":bytes_sha256(b"x")});
    invalid.push(item);
    let mut item = valid.clone();
    item["source_commit"] = json!("claim");
    invalid.push(item);
    let mut item = valid.clone();
    item["source_commit"] = Value::Null;
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"] = Value::Null;
    invalid.push(item);
    let mut item = valid.clone();
    item["textures"] = Value::Null;
    invalid.push(item);
    let mut item = valid.clone();
    item["raster"]["extra"] = json!(true);
    invalid.push(item);
    for (case, value) in invalid.into_iter().enumerate() {
        let index = serde_json::to_vec(&value).unwrap();
        let mut reads = 0;
        let result = TerrainPackage::load(&index, bytes_sha256(&index), |_, _| {
            reads += 1;
            Ok(Vec::new())
        });
        assert!(result.is_err(), "invalid case {case} was accepted");
        assert_eq!(reads, 0, "invalid case {case} read payloads");
    }
}

#[test]
fn wrong_pins_stop_loading_and_do_not_return_a_partial_package() {
    let (manifest, mut payloads) = fixture();
    let index = serde_json::to_vec(&manifest).unwrap();
    payloads.get_mut("terrain-normal.bin").unwrap()[0] ^= 1;
    let mut reads = Vec::new();
    let result = TerrainPackage::load(&index, bytes_sha256(&index), |name, _| {
        reads.push(name.to_owned());
        Ok(payloads[name].clone())
    });
    assert!(result.is_err());
    assert_eq!(reads.last().map(String::as_str), Some("terrain-normal.bin"));
    assert!(!reads.iter().any(|name| name == "terrain-splat.bin"));
    let mut reads = 0;
    assert!(
        TerrainPackage::load(&index, bytes_sha256(b"wrong index"), |_, _| {
            reads += 1;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert_eq!(reads, 0);
}

#[test]
fn asynchronous_transport_uses_the_same_closed_index_and_pins() {
    use std::future::Future;
    use std::task::{Context, Poll, Waker};
    let (manifest, payloads) = fixture();
    let index = serde_json::to_vec(&manifest).unwrap();
    let hash = bytes_sha256(&index);
    let reader = |name: String, maximum: usize| {
        let payloads = &payloads;
        async move {
            let bytes = payloads.get(&name).ok_or("unexpected payload")?;
            assert_eq!(bytes.len(), maximum);
            Ok(bytes.clone())
        }
    };
    let mut future = std::pin::pin!(TerrainPackage::load_async(&index, hash, reader));
    let mut context = Context::from_waker(Waker::noop());
    let Poll::Ready(Ok(package)) = future.as_mut().poll(&mut context) else {
        panic!("immediate fixture transport did not complete");
    };
    assert_eq!(package.format(), TerrainPackageFormat::SurfaceData);
    assert_eq!(package.file_count(), 4);
    assert_eq!(
        package.file("LICENSE.txt"),
        Some(payloads["LICENSE.txt"].as_slice())
    );
}

#[test]
fn legacy_index_keeps_its_original_geometry_and_rejects_new_metadata() {
    let files: serde_json::Map<_, _> = [
        ("terrain.wgsl", 1),
        ("terrain-lod.wgsl", 1),
        ("terrain-normal.bin", 22_369_620),
        ("terrain-splat.bin", 22_369_620),
        ("terrain-detail.bin", 1_398_100),
        ("LICENSE-Tidewater.txt", 1),
    ]
    .into_iter()
    .map(|(name, bytes)| {
        (
            name.to_owned(),
            json!({"bytes":bytes,"sha256":bytes_sha256(b"x")}),
        )
    })
    .collect();
    let mut manifest = json!({"schema":"forge.tidewater.terrain-package","version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c","files":files});
    let index = serde_json::to_vec(&manifest).unwrap();
    let hash = bytes_sha256(&index);
    assert_eq!(
        TerrainPackage::manifest_geometry(&index, hash).unwrap(),
        forge_assets::source_terrain_package::supported_raster_geometry()
    );
    assert_eq!(
        TerrainPackage::manifest_license_name(&index, hash).unwrap(),
        "LICENSE-Tidewater.txt"
    );
    assert_eq!(
        TerrainPackage::resource_pins(&index, hash).unwrap().len(),
        6
    );
    manifest["raster"] = fixture().0["raster"].clone();
    let changed = serde_json::to_vec(&manifest).unwrap();
    assert!(TerrainPackage::resource_pins(&changed, bytes_sha256(&changed)).is_err());
    manifest["raster"] = Value::Null;
    let changed = serde_json::to_vec(&manifest).unwrap();
    assert!(TerrainPackage::resource_pins(&changed, bytes_sha256(&changed)).is_err());
}
