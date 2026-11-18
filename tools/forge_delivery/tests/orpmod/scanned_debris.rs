use super::*;
use forge_assets::source_scanned_package::{ScannedPackage, geometry, templates};
use forge_core::{AreaId, CommandId, ContentHash, EntityId, Vec3};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};
use std::{fs::OpenOptions, io::Write, path::Path};

const SCAN_REVISION: &str = "f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4";

fn stage_file(source: &Path, path: &str, bytes: &[u8]) {
    let target = source.join(path);
    fs::create_dir_all(target.parent().unwrap()).unwrap();
    OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(target)
        .unwrap()
        .write_all(bytes)
        .unwrap();
}

fn attach_scans(
    source: &Path,
    module: &mut ModulePackageManifest,
    area_id: AreaId,
    notice: &[u8],
) -> (Vec<ManualPlacement>, Vec<u8>) {
    let root = PathBuf::from(std::env::var_os("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
    let program = PathBuf::from(std::env::var_os("FORGE_DEBRIS_SCANNED_PROGRAM_FIXTURE").unwrap());
    let index = fs::read(root.join("package.json")).unwrap();
    let revision: ContentHash = SCAN_REVISION.parse().unwrap();
    let mut stage = |path: String, bytes: &[u8], role| {
        stage_file(source, &path, bytes);
        let sha256 = bytes_sha256(bytes);
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256,
            role,
        });
        ModuleFilePin { path, sha256 }
    };
    let manifest = stage(
        "scans/package.json".into(),
        &index,
        ModuleFileRole::ScannedDebris,
    );
    let shader = fs::read(program).unwrap();
    assert_eq!(
        bytes_sha256(&shader).to_string(),
        "3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3"
    );
    let instance_program = stage(
        "scans/instance.wgsl".into(),
        &shader,
        ModuleFileRole::ScannedDebris,
    );
    let license_path = stage(
        "scans/PRIVATE-ACQUISITION.txt".into(),
        notice,
        ModuleFileRole::License,
    )
    .path;
    let mut resources = Vec::new();
    let package = ScannedPackage::load(&index, revision, |name, _| {
        let bytes = fs::read(root.join(name)).map_err(|error| error.to_string())?;
        resources.push(stage(
            format!("scans/{name}"),
            &bytes,
            ModuleFileRole::ScannedDebris,
        ));
        Ok(bytes)
    })
    .unwrap();
    let decoded = geometry::prepare(&package).unwrap();
    let templates = templates::prepare(
        &decoded,
        revision,
        &["Tronc", "Branche", "Branche", "Coquillage"],
    )
    .unwrap();
    let mut assets: Vec<_> = templates.iter().map(|template| template.asset_id).collect();
    assets.sort_unstable();
    let placements = templates
        .into_iter()
        .map(|template| ManualPlacement {
            id: EntityId::deterministic(format!("scanned-replay/{}", template.instance.source_id)),
            asset_id: template.asset_id,
            asset_hash: template.asset_hash,
            transform: template.transform,
            name: template.name,
            locked: false,
            physics: None,
            particles: None,
            local_lights: None,
        })
        .collect();
    module.compatibility.module_api_version = MODULE_SCANNED_DEBRIS_API_VERSION;
    module.provenance.license_paths.push(license_path.clone());
    module.scanned_debris.push(ModuleScannedDebrisBinding {
        area_id,
        package: ModuleObjectFiles {
            manifest,
            resources,
        },
        instance_program,
        license_path,
        assets,
    });
    (placements, shader)
}

#[test]
#[ignore = "Requires the locally acquired scan package and exact instance shader"]
fn private_scans_survive_archive_and_installed_resolution() {
    let mut fixture = Fixture::new("private-scanned-debris");
    let area_id = fixture.manifest.zones[0].area_id;
    let (_, shader) = attach_scans(
        &fixture.source,
        &mut fixture.manifest,
        area_id,
        b"Private structural test of acquired source inputs. No distribution rights granted.",
    );
    fixture.save();
    let archive = fixture.root.join("scans.orpmod");
    let mut descriptor = fixture.descriptor();
    descriptor.license_paths = fixture.manifest.provenance.license_paths.clone();
    orpmod::pack(&fixture.source, descriptor, &archive, false).unwrap();
    assert!(orpmod::inspect(&archive, true).is_err());
    let loaded =
        forge_delivery::module_runtime::load(&archive, area_id, Some(&fixture.root.join("cache")))
            .unwrap();
    let resolved = loaded.scanned_debris().unwrap().unwrap();
    assert_eq!(resolved.revision.to_string(), SCAN_REVISION);
    assert_eq!(resolved.geometry.instances.len(), 141);
    assert_eq!(resolved.collisions.owners().len(), 946);
    assert_eq!(resolved.instance_program.as_bytes(), shader.as_slice());
}

#[test]
#[ignore = "Prepares the private graphical replay from an explicitly pinned source-terrain module"]
fn prepare_installed_scanned_debris_replay() {
    let source_package = PathBuf::from(std::env::var_os("FORGE_SCANNED_REPLAY_BASE").unwrap());
    let expected_transport = std::env::var("FORGE_SCANNED_REPLAY_BASE_TRANSPORT").unwrap();
    let area: AreaId = std::env::var("FORGE_SCANNED_REPLAY_AREA")
        .unwrap()
        .parse()
        .unwrap();
    let output = PathBuf::from(std::env::var_os("FORGE_SCANNED_REPLAY_OUTPUT").unwrap());
    assert!(source_package.is_absolute() && output.is_absolute());
    let stage = output.join("source");
    let output_package = output.join("scanned-debris.orpmod");
    assert!(!output_package.exists());
    let base_descriptor = output.join("base-package-manifest.json");
    let (_, transport) = archive::inspect(&source_package).unwrap();
    assert_eq!(transport, expected_transport);
    if !output.exists() {
        fs::create_dir(&output).unwrap();
        archive::extract(&source_package, &stage, &expected_transport).unwrap();
        fs::rename(stage.join("package-manifest.json"), &base_descriptor).unwrap();
    }
    let descriptor_bytes = fs::read(&base_descriptor).unwrap();
    assert_eq!(
        bytes_sha256(&descriptor_bytes).to_string(),
        expected_transport
    );
    let mut descriptor: Descriptor = serde_json::from_slice(&descriptor_bytes).unwrap();
    let manifest_path = stage.join(MODULE_MANIFEST_PATH);
    let mut module = ModulePackageManifest::from_bytes(&fs::read(&manifest_path).unwrap()).unwrap();
    assert_eq!(module.provenance.rights, ModuleRights::PrivateLocalOnly);
    assert_eq!(module.module_id.to_string(), descriptor.id);
    assert!(
        module.scanned_debris.is_empty()
            || module.scanned_debris.len() == 1
                && module.scanned_debris[0].area_id == area
                && module.scanned_debris[0].package.manifest.sha256.to_string() == SCAN_REVISION
    );
    assert!(
        module
            .terrain_surfaces
            .iter()
            .any(|binding| binding.area_id == area)
    );
    assert!(
        module
            .environments
            .iter()
            .any(|binding| binding.area_id == area)
    );
    let zone_index = module
        .zones
        .iter()
        .position(|zone| zone.area_id == area)
        .unwrap();
    let archive_path = module.zones[zone_index].archive_path.clone();
    let original_archive = fs::read(stage.join(&archive_path)).unwrap();
    let mut zone = ZoneEditSession::from_archive_bytes(&original_archive).unwrap();
    if module.scanned_debris.is_empty() {
        module
            .validate_zone_archive(area, &original_archive)
            .unwrap();
        let notice = b"Private local replay of the acquired Tidewater scanned surfaces. All conditions of the original acquisition and the source module remain applicable. No distribution rights granted.";
        let (mut placements, _) = attach_scans(&stage, &mut module, area, notice);
        add_witnesses(&stage, &module, &zone, &mut placements);
        let request = zone
            .head()
            .prepare_placement(
                CommandId::deterministic("installed-scanned-debris-replay"),
                PlacementEdit::InsertMany { placements },
            )
            .unwrap();
        zone.execute(&request).unwrap();
    }
    for binding in module
        .vegetation
        .iter_mut()
        .filter(|binding| binding.area_id == area)
    {
        binding.vegetation_sha256 = zone
            .head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .content_hash()
            .unwrap();
    }
    let witnesses: Vec<_> = zone
        .head()
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .filter(|placement| placement.name.starts_with("Tronc témoin "))
        .cloned()
        .collect();
    assert_eq!(witnesses.len(), 4);
    let revised = zone.archive_bytes().unwrap();
    let head = zone.head().content_hash().unwrap();
    let archive_sha = bytes_sha256(&revised);
    module.zones[zone_index].head_hash = head;
    module.zones[zone_index].archive_sha256 = archive_sha;
    let file = module
        .files
        .iter_mut()
        .find(|file| file.path == archive_path)
        .unwrap();
    file.bytes = revised.len() as u64;
    file.sha256 = archive_sha;
    fs::write(stage.join(&archive_path), revised).unwrap();

    let revision = format!("qualification/scanned-debris/{head}");
    module.provenance.source_revision.clone_from(&revision);
    descriptor.source_revision = revision;
    descriptor
        .license_paths
        .clone_from(&module.provenance.license_paths);
    fs::write(manifest_path, module.to_bytes().unwrap()).unwrap();
    let (_, transport) = orpmod::pack(&stage, descriptor, &output_package, false).unwrap();
    let proof = serde_json::json!({
        "schema": "forge.gate7.installed-scanned-debris-input.v1",
        "status": "PACKAGE_ONLY",
        "base_transport": expected_transport,
        "package": output_package,
        "transport": transport,
        "area": area,
        "head": head,
        "archive_sha256": archive_sha,
        "acquired_placements": module.scanned_debris[0].assets.len(),
        "witness_placements": witnesses,
        "source_revision": SCAN_REVISION
    });
    stage_file(
        &output,
        "scanned-input.json",
        &serde_json::to_vec_pretty(&proof).unwrap(),
    );
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Witness placement uses the validated finite terrain metres."
)]
fn add_witnesses(
    stage: &Path,
    module: &ModulePackageManifest,
    zone: &ZoneEditSession,
    placements: &mut Vec<ManualPlacement>,
) {
    let terrain = zone.head().effective().unwrap().0;
    let raster = terrain.sampled_source.as_ref().unwrap();
    let samples = module
        .files
        .iter()
        .find(|file| {
            file.role == ModuleFileRole::SourceBlob && file.sha256 == raster.samples_sha256
        })
        .unwrap();
    let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
        &terrain,
        &fs::read(stage.join(&samples.path)).unwrap(),
    )
    .unwrap();
    let gameplay = zone.head().corrections.gameplay.as_ref().unwrap();
    let start = gameplay
        .actors
        .iter()
        .find(|actor| actor.id == gameplay.player)
        .unwrap()
        .position;
    let source =
        templates::asset_id("tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:debris-scanned:0");
    let sample = placements
        .iter()
        .find(|item| item.asset_id == source)
        .unwrap()
        .clone();
    let witnesses: Vec<_> = [(0.0, -6.0), (0.0, 6.0), (-6.0, 0.0), (6.0, 0.0)]
        .into_iter()
        .enumerate()
        .map(|(index, (dx, dz))| {
            let mut item = sample.clone();
            let (x, z) = (start.x + dx, start.z + dz);
            let y = plan.height_at(f64::from(x), f64::from(z)).unwrap() as f32;
            item.id = EntityId::deterministic(format!("scanned-replay/witness/{index}"));
            item.name = format!("Tronc témoin {}", index + 1);
            item.transform.translation_m = Vec3::new(x, y + 0.2, z);
            item
        })
        .collect();
    placements.extend(witnesses);
}
