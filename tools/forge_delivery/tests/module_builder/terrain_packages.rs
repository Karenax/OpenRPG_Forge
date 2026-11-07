//! Closed terrain transport is explicit; compatible geometry never creates companions.
use super::*;
use forge_core::{AreaId, canonical_json_sha256};
use forge_project::{
    TerrainDocument,
    module_package::{
        MODULE_TERRAIN_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole,
        ModulePackageManifest, ModuleTerrainBinding, ModuleTerrainFiles, ModuleTerrainSource,
    },
    zone_authoring::session::{ZoneAction, ZoneRequest},
};

pub(super) fn sampled_fixture(
    heights: &[u8],
    source: Option<forge_project::sampled_terrain::SampledTerrainSource>,
) -> Fixture {
    let mut fixture = Fixture::new(false);
    let mut document = fixture.zone.head().base.terrain.clone();
    document.schema_version = 4;
    let source = source.unwrap_or_else(|| forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 2048,
        height: 2048,
        origin_xz_m: [-1024.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(heights),
    });
    #[allow(clippy::cast_possible_truncation)] // Test fixtures use exact bounded f32 geometry.
    let spacing = source.sample_spacing_m as f32;
    document.extent_m.size_x_m = f32::from(u16::try_from(source.width).unwrap()) * spacing;
    document.extent_m.size_z_m = f32::from(u16::try_from(source.height).unwrap()) * spacing;
    document.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = spacing;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.sampled_source = Some(source);
    document.validate().unwrap();
    let mut zone = ZoneEditSession::from_initial_terrain(document).unwrap();
    let request = ZoneRequest {
        command_id: CommandId::deterministic("terrain/retain-gameplay"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: ZoneAction::edit(fixture.zone.head().corrections.clone()),
    };
    zone.execute(&request).unwrap();
    fixture.zone = zone;
    fixture.payloads.insert(
        format!("_forge-export/source-blobs/{}.bin", bytes_sha256(heights)),
        heights.to_vec(),
    );
    fixture
}

pub(super) fn attach(module: &mut ModulePackageManifest, document: &TerrainDocument, index: &[u8]) {
    module.compatibility.module_api_version = MODULE_TERRAIN_API_VERSION;
    let base = "terrain/profile";
    let manifest = ModuleFilePin {
        path: format!("{base}/package.json"),
        sha256: bytes_sha256(index),
    };
    module.files.push(ModuleFile {
        path: manifest.path.clone(),
        bytes: index.len() as u64,
        sha256: manifest.sha256,
        role: ModuleFileRole::TerrainSurface,
    });
    let pins =
        forge_assets::source_terrain_package::TerrainPackage::resource_pins(index, manifest.sha256)
            .unwrap();
    let license_name = forge_assets::source_terrain_package::TerrainPackage::manifest_license_name(
        index,
        manifest.sha256,
    )
    .unwrap();
    let mut resources = Vec::new();
    for (name, pin) in pins {
        let path = format!("{base}/{name}");
        let license = name == license_name;
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: pin.bytes as u64,
            sha256: pin.sha256,
            role: if license {
                ModuleFileRole::License
            } else {
                ModuleFileRole::TerrainSurface
            },
        });
        if license {
            module.provenance.license_paths.push(path.clone());
        }
        resources.push(ModuleFilePin {
            path,
            sha256: pin.sha256,
        });
    }
    module.terrain_surfaces.push(ModuleTerrainBinding {
        area_id: document.area_id,
        source: ModuleTerrainSource::SourceRaster {
            source_descriptor_sha256: canonical_json_sha256(
                document.sampled_source.as_ref().unwrap(),
            )
            .unwrap(),
            package: ModuleTerrainFiles {
                manifest,
                resources,
            },
        },
    });
    module.validate().unwrap();
}

fn index() -> Vec<u8> {
    let files = [
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
    .collect::<serde_json::Map<_, _>>();
    serde_json::to_vec(
        &json!({"schema":"forge.tidewater.terrain-package","version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c","files":files}),
    )
    .unwrap()
}

fn metadata_case() -> (Temp, ModulePackageManifest, TerrainDocument, Vec<u8>) {
    let temp = Temp::new();
    let heights = vec![0; 2048 * 2048 * 4];
    let fixture = sampled_fixture(&heights, None);
    let exported = fixture.build(&temp.0.join("base")).unwrap();
    let mut module = orpmod::inspect(&exported.package_path, false).unwrap();
    let document = fixture.zone.head().effective().unwrap().0;
    let index = index();
    attach(&mut module, &document, &index);
    (temp, module, document, index)
}

#[test]
fn no_terrain_companion_never_reads_or_uses_a_matching_global_raster() {
    let temp = Temp::new();
    let heights = vec![0; 2048 * 2048 * 4];
    let fixture = sampled_fixture(&heights, None);
    let exported = fixture.build(&temp.0.join("absent")).unwrap();
    let module = orpmod::inspect(&exported.package_path, false).unwrap();
    let document = fixture.zone.head().effective().unwrap().0;
    let mut calls = 0;
    assert!(
        forge_delivery::module_terrain::resolve(&module, document.area_id, &document, |_, _| {
            calls += 1;
            Err("No resources expected".into())
        })
        .unwrap()
        .is_none()
    );
    assert_eq!(calls, 0);
}

#[test]
fn terrain_source_frame_and_area_are_checked_before_transport() {
    let (_temp, module, document, _index) = metadata_case();
    let mut changed = document.clone();
    changed.sampled_source.as_mut().unwrap().origin_xz_m[0] += 0.25;
    let mut calls = 0;
    assert!(
        forge_delivery::module_terrain::resolve(&module, changed.area_id, &changed, |_, _| {
            calls += 1;
            Err("No resources expected".into())
        })
        .is_err()
    );
    let mut changed_samples = document.clone();
    changed_samples
        .sampled_source
        .as_mut()
        .unwrap()
        .samples_sha256 = bytes_sha256(b"different samples");
    assert!(
        forge_delivery::module_terrain::resolve(
            &module,
            changed_samples.area_id,
            &changed_samples,
            |_, _| {
                calls += 1;
                Err("No resources expected".into())
            }
        )
        .is_err()
    );
    assert!(
        forge_delivery::module_terrain::resolve(
            &module,
            AreaId::deterministic("foreign-terrain"),
            &document,
            |_, _| {
                calls += 1;
                Err("No resources expected".into())
            }
        )
        .is_err()
    );
    assert_eq!(calls, 0);
}

#[test]
fn terrain_index_closure_rejects_metadata_drift_before_payload_reads() {
    let (_temp, module, document, index) = metadata_case();
    for mode in 0..3 {
        let mut altered = module.clone();
        let ModuleTerrainSource::SourceRaster { package, .. } =
            &mut altered.terrain_surfaces[0].source;
        if mode == 0 {
            let path = package.resources[0].path.clone();
            altered
                .files
                .iter_mut()
                .find(|f| f.path == path)
                .unwrap()
                .bytes += 1;
        } else if mode == 1 {
            let pin = &mut package.resources[0];
            pin.sha256 = bytes_sha256(b"other bytes");
            altered
                .files
                .iter_mut()
                .find(|f| f.path == pin.path)
                .unwrap()
                .sha256 = pin.sha256;
        } else {
            let path = "terrain/profile/unexpected.bin".to_owned();
            altered.files.push(ModuleFile {
                path: path.clone(),
                bytes: 1,
                sha256: bytes_sha256(b"x"),
                role: ModuleFileRole::TerrainSurface,
            });
            package.resources.push(ModuleFilePin {
                path,
                sha256: bytes_sha256(b"x"),
            });
        }
        let mut calls = 0;
        let result = forge_delivery::module_terrain::resolve(
            &altered,
            document.area_id,
            &document,
            |_, _| {
                calls += 1;
                Ok(index.clone())
            },
        );
        assert!(result.is_err());
        assert_eq!(calls, 1);
    }
}

#[test]
fn terrain_license_role_is_required_even_when_all_pins_match() {
    let (_temp, module, document, index) = metadata_case();
    for omit_provenance in [false, true] {
        let mut altered = module.clone();
        let license = altered
            .files
            .iter_mut()
            .find(|f| f.path.ends_with("LICENSE-Tidewater.txt"))
            .unwrap();
        license.role = ModuleFileRole::TerrainSurface;
        if omit_provenance {
            altered
                .provenance
                .license_paths
                .retain(|path| path != &license.path);
            altered.validate().unwrap();
        } else {
            assert!(altered.validate().is_err());
        }
        let mut calls = 0;
        assert!(
            forge_delivery::module_terrain::resolve(
                &altered,
                document.area_id,
                &document,
                |_, _| {
                    calls += 1;
                    Ok(index.clone())
                }
            )
            .is_err()
        );
        // Declared provenance is rejected immediately; disguising the licence
        // as a surface resource is rejected when the closed index is admitted.
        assert_eq!(calls, usize::from(omit_provenance));
    }
}

pub(super) fn bounded(
    root: &std::path::Path,
    name: &str,
    maximum: usize,
) -> Result<Vec<u8>, String> {
    let base = root.canonicalize().map_err(|e| e.to_string())?;
    let path = base.join(name).canonicalize().map_err(|e| e.to_string())?;
    if !path.starts_with(&base)
        || path.metadata().map_err(|e| e.to_string())?.len() > maximum as u64
    {
        return Err("Private terrain input escapes its root or exceeds its bound".into());
    }
    fs::read(path).map_err(|e| e.to_string())
}

pub(super) fn write_new(path: &std::path::Path, bytes: &[u8]) {
    use std::io::Write;
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .unwrap()
        .write_all(bytes)
        .unwrap();
}

#[test]
#[allow(clippy::too_many_lines)] // One package admission, corruption and transport round-trip.
fn rectangular_surface_data_round_trips_with_matching_geometry_and_licence() {
    use forge_project::sampled_terrain::{
        HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource,
    };
    let heights = vec![0_u8; 11 * 19 * 4];
    let source = SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 11,
        height: 19,
        origin_xz_m: [-2.75, -4.75],
        sample_spacing_m: 0.5,
        outside_height_m: -12.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    };
    let fixture = sampled_fixture(&heights, Some(source));
    let temp = Temp::new();
    let baseline = fixture.build(&temp.0.join("base")).unwrap();
    let mut module = orpmod::inspect(&baseline.package_path, false).unwrap();
    let document = fixture.zone.head().effective().unwrap().0;
    let mips = |size: [u32; 2]| -> usize {
        (0..=size[0].max(size[1]).ilog2())
            .map(|level| ((size[0] >> level).max(1) * (size[1] >> level).max(1) * 4) as usize)
            .sum()
    };
    let payloads = std::collections::BTreeMap::from([
        ("terrain-normal.bin".to_owned(), vec![17_u8; mips([11, 19])]),
        ("terrain-splat.bin".to_owned(), vec![23_u8; mips([3, 5])]),
        ("terrain-detail.bin".to_owned(), vec![31_u8; mips([1, 3])]),
        (
            "LICENSE.txt".to_owned(),
            b"Fixture texture licence".to_vec(),
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
    let value = json!({"schema":"forge.terrain-surface-package","version":1,
        "raster":{"width":11,"height":19,"origin_xz_m":[-2.75,-4.75],"sample_spacing_m":0.5,
            "outside_height_m":-12.0,"encoding":"float32_le","extent_m":[5.5,9.5]},
        "textures":{"normal":[11,19],"splat":[3,5],"detail":[1,3]},"files":files});
    let index = serde_json::to_vec(&value).unwrap();
    attach(&mut module, &document, &index);
    let dir = temp.0.join("surface-source");
    for file in &module.files {
        let bytes = if let Some(name) = file.path.strip_prefix("terrain/profile/") {
            if name == "package.json" {
                index.clone()
            } else {
                payloads[name].clone()
            }
        } else {
            bounded(&baseline.source_root, &file.path, 64 * 1024 * 1024).unwrap()
        };
        assert_eq!(bytes_sha256(&bytes), file.sha256);
        write_new(&dir.join(&file.path), &bytes);
    }
    write_new(&dir.join("module.json"), &module.to_bytes().unwrap());
    let received = temp.0.join("surface.orpmod");
    orpmod::pack(
        &dir,
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
        &received,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&received, false).unwrap(), module);
    let loaded = forge_delivery::module_terrain::resolve(
        &module,
        document.area_id,
        &document,
        |name, maximum| bounded(&dir, name, maximum),
    )
    .unwrap()
    .unwrap();
    assert_eq!(
        loaded.package.format(),
        forge_assets::source_terrain_package::TerrainPackageFormat::SurfaceData
    );
    assert_eq!(loaded.package.texture_dimensions().splat, [3, 5]);
    assert_eq!(loaded.package.file("terrain.wgsl"), None);
    assert!(
        module
            .provenance
            .license_paths
            .iter()
            .any(|path| path == "terrain/profile/LICENSE.txt")
    );
    assert!(
        forge_delivery::module_terrain::resolve(
            &module,
            document.area_id,
            &document,
            |name, maximum| {
                let mut bytes = bounded(&dir, name, maximum)?;
                if name.ends_with("terrain-normal.bin") {
                    bytes[0] ^= 1;
                }
                Ok(bytes)
            }
        )
        .is_err()
    );
    let mut different = value;
    different["raster"]["outside_height_m"] = json!(-13.0);
    let changed = serde_json::to_vec(&different).unwrap();
    let mut mismatched = module.clone();
    let ModuleTerrainSource::SourceRaster { package, .. } =
        &mut mismatched.terrain_surfaces[0].source;
    package.manifest.sha256 = bytes_sha256(&changed);
    let entry = mismatched
        .files
        .iter_mut()
        .find(|f| f.path == package.manifest.path)
        .unwrap();
    entry.sha256 = package.manifest.sha256;
    entry.bytes = changed.len() as u64;
    let mut reads = 0;
    assert!(
        forge_delivery::module_terrain::resolve(
            &mismatched,
            document.area_id,
            &document,
            |_, _| {
                reads += 1;
                Ok(changed.clone())
            }
        )
        .is_err()
    );
    assert_eq!(reads, 1, "wrong domain must not read payloads");
}

#[test]
#[ignore = "Requires explicit private terrain package, canonical document, samples and a fresh persistent output"]
#[allow(
    clippy::too_many_lines,
    reason = "One preserved admission, retouch and re-export qualification transaction"
)]
fn private_terrain_resources_survive_admission_retouch_and_reexport() {
    let package = PathBuf::from(std::env::var_os("FORGE_TERRAIN_PACKAGE").unwrap());
    let document_path = PathBuf::from(std::env::var_os("FORGE_TERRAIN_DOCUMENT").unwrap());
    let heights_path = PathBuf::from(std::env::var_os("FORGE_TERRAIN_HEIGHTS").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_TERRAIN_OUTPUT").unwrap());
    assert!(package.is_absolute() && document_path.is_absolute() && heights_path.is_absolute());
    assert!(output.is_absolute() && !output.exists());
    let pin: forge_core::ContentHash = std::env::var("FORGE_TERRAIN_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let index = bounded(&package, "package.json", 16 * 1024).unwrap();
    let original =
        TerrainDocument::from_json(&fs::read_to_string(&document_path).unwrap()).unwrap();
    let heights = fs::read(heights_path).unwrap();
    assert_eq!(
        bytes_sha256(&heights),
        original.sampled_source.as_ref().unwrap().samples_sha256
    );
    let profile =
        forge_assets::source_terrain_package::TerrainPackage::load(&index, pin, |name, maximum| {
            bounded(&package, name, maximum)
        })
        .unwrap();
    let mut fixture = sampled_fixture(&heights, original.sampled_source.clone());
    let baseline = fixture.build(&output.join("baseline")).unwrap();
    let mut module = orpmod::inspect(&baseline.package_path, false).unwrap();
    let document = fixture.zone.head().effective().unwrap().0;
    attach(&mut module, &document, &index);
    let source = output.join("source");
    for file in &module.files {
        let bytes = if let Some(name) = file.path.strip_prefix("terrain/profile/") {
            if name == "package.json" {
                index.clone()
            } else {
                profile.file(name).unwrap().to_vec()
            }
        } else {
            bounded(&baseline.source_root, &file.path, 64 * 1024 * 1024).unwrap()
        };
        assert_eq!(bytes_sha256(&bytes), file.sha256);
        write_new(&source.join(&file.path), &bytes);
    }
    write_new(&source.join("module.json"), &module.to_bytes().unwrap());
    let received = output.join("terrain.orpmod");
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
        &received,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&received, false).unwrap(), module);
    let resolved = forge_delivery::module_terrain::resolve(
        &module,
        document.area_id,
        &document,
        |name, maximum| bounded(&source, name, maximum),
    )
    .unwrap()
    .unwrap();
    assert_eq!(resolved.package_sha256, pin);
    assert_eq!(resolved.package.file_count(), profile.file_count());
    let mut corrupt_calls = 0;
    assert!(
        forge_delivery::module_terrain::resolve(
            &module,
            document.area_id,
            &document,
            |name, maximum| {
                corrupt_calls += 1;
                let mut bytes = bounded(&source, name, maximum)?;
                if name.ends_with("terrain-detail.bin") {
                    bytes[0] ^= 1;
                }
                Ok(bytes)
            }
        )
        .is_err()
    );
    assert!(corrupt_calls > 1);
    let before = fixture.zone.head().content_hash().unwrap();
    let rename = fixture
        .zone
        .head()
        .prepare_terrain_action(
            forge_project::terrain_edit::TerrainAction::Edit(
                forge_project::terrain_edit::TerrainEdit::Rename {
                    name: "Terrain retouché avec le même profil".into(),
                },
            ),
            CommandId::deterministic("terrain-retouch"),
        )
        .unwrap();
    fixture.zone.execute(&rename).unwrap();
    assert_ne!(fixture.zone.head().content_hash().unwrap(), before);
    let original_bytes: std::sync::Arc<[u8]> = fs::read(&received).unwrap().into();
    let exported = build_zone_with_origin(
        &output.join("reexport"),
        &fixture.zone.archive_bytes().unwrap(),
        fixture.zone.head().content_hash().unwrap(),
        Fixture::metadata(),
        fixture.catalog.clone(),
        fixture.payloads.clone(),
        Some(ExportOrigin {
            expected_package_sha256: bytes_sha256(&original_bytes),
            package_bytes: original_bytes,
        }),
    )
    .unwrap();
    let restored = orpmod::inspect(&exported.package_path, false).unwrap();
    assert_eq!(
        restored.compatibility.module_api_version,
        MODULE_TERRAIN_API_VERSION
    );
    assert_eq!(restored.terrain_surfaces, module.terrain_surfaces);
    let changed = fixture.zone.head().effective().unwrap().0;
    let loaded = forge_delivery::module_terrain::resolve(
        &restored,
        changed.area_id,
        &changed,
        |name, maximum| bounded(&exported.source_root, name, maximum),
    )
    .unwrap()
    .unwrap();
    assert_eq!(loaded.package_sha256, pin);
    assert_eq!(
        loaded.package.file("terrain.wgsl"),
        resolved.package.file("terrain.wgsl")
    );
    assert_eq!(
        bytes_sha256(&bounded(&package, "package.json", 16 * 1024).unwrap()),
        pin
    );
    write_new(&output.join("proof.json"), &serde_json::to_vec_pretty(&json!({
        "schema":"forge.gate7.module-terrain-resource-roundtrip.v1", "passed":true,
        "package":received,"transport":transport,"terrain_package":pin,
        "resource_count":loaded.package.file_count(),"reexport":exported,
        "source_descriptor":loaded.source_descriptor_sha256,"corrupt_payload_refused":true,
        "original_index_unchanged":true,"render_qualified":false,"studio_transport_qualified":false,
        "gate_closed":false
    })).unwrap());
}

#[test]
#[ignore = "Requires an explicit admitted object/environment module and terrain package; fresh persistent output"]
#[allow(
    clippy::too_many_lines,
    reason = "One bounded private renderer fixture assembly, with all input pins checked"
)]
fn private_source_terrain_joins_the_real_module_scene() {
    let origin = PathBuf::from(std::env::var_os("FORGE_TERRAIN_RUNTIME_ORIGIN").unwrap());
    let package = PathBuf::from(std::env::var_os("FORGE_TERRAIN_PACKAGE").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_TERRAIN_OUTPUT").unwrap());
    let area: AreaId = std::env::var("FORGE_TERRAIN_RUNTIME_AREA")
        .unwrap()
        .parse()
        .unwrap();
    let pin = std::env::var("FORGE_TERRAIN_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    assert!(
        origin.is_absolute() && package.is_absolute() && output.is_absolute() && !output.exists()
    );
    let origin_manifest = fs::read(origin.join("module.json")).unwrap();
    let mut module = orpmod::validate_directory(&origin, false).unwrap();
    assert!(module.terrain_surfaces.is_empty());
    assert!(module.environments.iter().any(|e| e.area_id == area));
    assert!(module.objects.iter().any(|e| e.area_id == area));
    let loaded = forge_delivery::module_runtime::load(&origin, area, None).unwrap();
    let document = loaded.zone.head().effective().unwrap().0;
    let index = bounded(&package, "package.json", 16 * 1024).unwrap();
    let profile =
        forge_assets::source_terrain_package::TerrainPackage::load(&index, pin, |name, maximum| {
            bounded(&package, name, maximum)
        })
        .unwrap();
    attach(&mut module, &document, &index);
    let source = output.join("source");
    for file in &module.files {
        assert!(file.bytes <= 64 * 1024 * 1024);
        let target = source.join(&file.path);
        fs::create_dir_all(target.parent().unwrap()).unwrap();
        if file.path == "terrain/profile/package.json" {
            write_new(&target, &index);
        } else {
            let original = if let Some(name) = file.path.strip_prefix("terrain/profile/") {
                assert_eq!(bytes_sha256(profile.file(name).unwrap()), file.sha256);
                package.join(name)
            } else {
                assert_eq!(
                    bytes_sha256(&bounded(&origin, &file.path, 64 * 1024 * 1024).unwrap()),
                    file.sha256
                );
                origin.join(&file.path)
            };
            // Immutable inputs are reused. No author document/archive is edited in this fixture.
            fs::hard_link(original, &target).unwrap();
        }
        assert_eq!(fs::metadata(&target).unwrap().len(), file.bytes);
    }
    write_new(&source.join("module.json"), &module.to_bytes().unwrap());
    let received = output.join("terrain-runtime.orpmod");
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
        &received,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&received, false).unwrap(), module);
    let admitted =
        forge_delivery::module_runtime::load(&received, area, Some(&output.join("cache"))).unwrap();
    let terrain = admitted.terrain().unwrap().unwrap();
    assert_eq!(terrain.package_sha256, pin);
    assert_eq!(terrain.package.file_count(), profile.file_count());
    assert!(admitted.environment().unwrap().is_some());
    assert!(admitted.objects().unwrap().is_some());
    assert_eq!(
        fs::read(origin.join("module.json")).unwrap(),
        origin_manifest
    );
    for file in &module.files {
        let original = if file.path.starts_with("terrain/profile/") {
            continue;
        } else {
            &origin
        };
        assert_eq!(
            bytes_sha256(&bounded(original, &file.path, 64 * 1024 * 1024).unwrap()),
            file.sha256
        );
    }
    write_new(&output.join("proof.json"), &serde_json::to_vec_pretty(&json!({
        "schema":"forge.gate7.module-source-terrain-runtime-fixture.v1", "origin":origin,
        "origin_manifest_sha256":bytes_sha256(&origin_manifest), "package":received, "source":source,
        "head":admitted.zone.head().content_hash().unwrap(), "area":area, "transport":transport,
        "archive_sha256":bytes_sha256(&admitted.archive), "terrain_package_sha256":pin,
        "terrain_document_sha256":document.content_hash().unwrap(), "immutable_origin_preserved":true,
        "environment_admitted":true,"objects_admitted":true, "renderer_qualified":false,"gate_closed":false,
        "budget":{"maximum_bytes":1073741824_u64,"maximum_files":7500}
    })).unwrap());
}
