//! Bounded private fixture for the real module runtime's rectangular terrain.
use super::terrain_packages::{attach, bounded, sampled_fixture, write_new};
use super::*;
use forge_core::{AreaId, bytes_sha256};
use forge_project::{
    TerrainHeightDelta,
    module_package::{ModuleEnvironmentSource, ModuleFileRole},
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::session::{ZoneAction, ZoneRequest},
};
use std::collections::BTreeSet;

fn uniform_mips(size: [u32; 2], pixel: [u8; 4]) -> Vec<u8> {
    let mut bytes = Vec::new();
    for level in 0..=size[0].max(size[1]).ilog2() {
        let pixels = (size[0] >> level).max(1) * (size[1] >> level).max(1);
        for _ in 0..pixels {
            bytes.extend_from_slice(&pixel);
        }
    }
    bytes
}

#[test]
#[ignore = "requires a pinned private environment and a fresh qualification output"]
#[allow(clippy::too_many_lines)] // One reproducible module admission and package transaction.
fn build_rectangular_module_with_admitted_environment() {
    let origin = PathBuf::from(std::env::var_os("FORGE_RECT_ORIGIN").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_RECT_OUTPUT").unwrap());
    let area: AreaId = std::env::var("FORGE_RECT_ORIGIN_AREA")
        .unwrap()
        .parse()
        .unwrap();
    let expected_origin = std::env::var("FORGE_RECT_ORIGIN_SHA256").unwrap();
    assert!(origin.is_absolute() && output.is_absolute() && !output.exists());
    forge_delivery::manifest::safe_chain(&origin).unwrap();
    forge_delivery::manifest::safe_chain(&output).unwrap();
    let origin_index = fs::read(origin.join("module.json")).unwrap();
    assert_eq!(bytes_sha256(&origin_index).to_string(), expected_origin);
    let original = orpmod::validate_directory(&origin, false).unwrap();
    let mut environment = original
        .environments
        .iter()
        .find(|binding| binding.area_id == area)
        .unwrap()
        .clone();

    // A new 4,096 x 2,048 m sampled map; the original module is not edited.
    let size = [512_u32, 256_u32];
    let heights = vec![0_u8; (size[0] * size[1] * 4) as usize];
    let raster = SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: size[0],
        height: size[1],
        origin_xz_m: [-2048.0, -1024.0],
        sample_spacing_m: 8.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    };
    let mut fixture = sampled_fixture(&heights, Some(raster));
    let mut corrections = fixture.zone.head().corrections.clone();
    let terrain = fixture.zone.head().base.terrain.clone();
    let mut height_deltas = Vec::new();
    for dz in -18_i16..=18 {
        for dx in -18_i16..=18 {
            let distance = i32::from(dx).pow(2) + i32::from(dz).pow(2);
            if distance < 18 * 18 {
                let height = 20.0 * f32::from(u16::try_from(18 * 18 - distance).unwrap()) / 324.0;
                height_deltas.push(TerrainHeightDelta::new(
                    u16::try_from(312 + i32::from(dx)).unwrap(),
                    u16::try_from(128 + i32::from(dz)).unwrap(),
                    height,
                ));
            }
        }
    }
    corrections.height_deltas = Some(height_deltas);
    let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("gate7/rectangular/fixture"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
    fixture.zone = zone;

    let temp = Temp::new();
    let baseline = fixture.build(&temp.0.join("base")).unwrap();
    let mut module = orpmod::inspect(&baseline.package_path, false).unwrap();
    let document = fixture.zone.head().effective().unwrap().0;
    let payloads = BTreeMap::from([
        (
            "terrain-normal.bin".to_owned(),
            uniform_mips(size, [128, 128, 0, 255]),
        ),
        (
            "terrain-splat.bin".to_owned(),
            uniform_mips(size, [255, 0, 0, 255]),
        ),
        (
            "terrain-detail.bin".to_owned(),
            uniform_mips([2, 2], [96, 128, 70, 255]),
        ),
        (
            "LICENSE.txt".to_owned(),
            b"Original qualification textures, CC0-1.0.\n".to_vec(),
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
    let index = serde_json::to_vec(&json!({
        "schema":"forge.terrain-surface-package", "version":1,
        "raster":{"width":size[0],"height":size[1],"origin_xz_m":[-2048.0,-1024.0],
            "sample_spacing_m":8.0,"outside_height_m":-90.0,"encoding":"float32_le",
            "extent_m":[4096.0,2048.0]},
        "textures":{"normal":size,"splat":size,"detail":[2,2]}, "files":files
    }))
    .unwrap();
    attach(&mut module, &document, &index);

    // Reuse exactly the original environment closure, including its licences.
    environment.area_id = document.area_id;
    let ModuleEnvironmentSource::SourceProfile {
        environment: base,
        programs,
    } = &environment.source;
    let mut environment_paths = BTreeSet::new();
    for package in std::iter::once(base).chain(programs.iter()) {
        environment_paths.insert(package.manifest.path.clone());
        environment_paths.extend(package.resources.iter().map(|pin| pin.path.clone()));
    }
    for path in &environment_paths {
        let file = original
            .files
            .iter()
            .find(|file| &file.path == path)
            .unwrap();
        assert!(!module.files.iter().any(|existing| existing.path == *path));
        if file.role == ModuleFileRole::License {
            assert!(original.provenance.license_paths.contains(path));
            module.provenance.license_paths.push(path.clone());
        }
        module.files.push(file.clone());
    }
    module.environments.push(environment);
    module.compatibility.module_api_version = module
        .compatibility
        .module_api_version
        .max(original.compatibility.module_api_version);
    module.name = "Qualification terrain rectangulaire".into();
    module.provenance.source_revision = format!(
        "gate7/rectangular/{}",
        fixture.zone.head().content_hash().unwrap()
    );
    module.validate().unwrap();

    let source = output.join("source");
    for file in &module.files {
        let target = source.join(&file.path);
        if let Some(name) = file.path.strip_prefix("terrain/profile/") {
            let bytes = if name == "package.json" {
                &index
            } else {
                &payloads[name]
            };
            assert_eq!(bytes_sha256(bytes), file.sha256);
            write_new(&target, bytes);
            continue;
        }
        let input = if environment_paths.contains(&file.path) {
            &origin
        } else {
            &baseline.source_root
        };
        let bytes = bounded(input, &file.path, 64 * 1024 * 1024).unwrap();
        assert_eq!(bytes_sha256(&bytes), file.sha256);
        fs::create_dir_all(target.parent().unwrap()).unwrap();
        fs::hard_link(input.join(&file.path), &target).unwrap();
        assert_eq!(bytes_sha256(&fs::read(&target).unwrap()), file.sha256);
    }
    write_new(&source.join("module.json"), &module.to_bytes().unwrap());
    let package = output.join("rectangular-runtime.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        forge_delivery::manifest::Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: forge_delivery::manifest::Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: forge_delivery::manifest::Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &package,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&package, false).unwrap(), module);
    let admitted = module_runtime::load(&source, document.area_id, None).unwrap();
    assert!(admitted.environment().unwrap().is_some());
    let terrain = admitted.terrain().unwrap().unwrap();
    assert_eq!(terrain.package.texture_dimensions().normal, size);
    assert_eq!(terrain.package_sha256, bytes_sha256(&index));
    assert_eq!(fs::read(origin.join("module.json")).unwrap(), origin_index);
    let archive = module
        .zones
        .iter()
        .find(|zone| zone.area_id == document.area_id)
        .unwrap();
    let logical_bytes: u64 = module.files.iter().map(|file| file.bytes).sum();
    write_new(&output.join("proof.json"), &serde_json::to_vec_pretty(&json!({
        "schema":"forge.gate7.rectangular-runtime-fixture.v1",
        "origin":origin, "origin_manifest_sha256":bytes_sha256(&origin_index),
        "source":source, "package":package, "package_sha256":forge_delivery::file_hash(&package).unwrap(),
        "transport":transport, "area":document.area_id, "head":fixture.zone.head().content_hash().unwrap(),
        "archive_sha256":archive.archive_sha256, "terrain_document_sha256":document.content_hash().unwrap(),
        "terrain_package_sha256":terrain.package_sha256, "extent_m":[4096,2048], "samples":size,
        "height_edits":document.height_deltas.len(), "environment_files":environment_paths.len(),
        "source_logical_bytes":logical_bytes, "immutable_origin_preserved":true,
        "renderer_qualified":false, "performance_qualified":false, "gate_closed":false
    })).unwrap());
}
