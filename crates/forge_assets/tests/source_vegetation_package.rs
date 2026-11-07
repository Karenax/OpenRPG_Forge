use forge_assets::source_vegetation_package::{PlantPackage, profile::PlantPrototype};
use forge_core::bytes_sha256;
#[path = "support/plant_package.rs"]
mod support;

fn load(fixture: &support::Fixture) -> Result<PlantPackage, String> {
    let raw = fixture.index();
    PlantPackage::load(&raw, bytes_sha256(&raw), |name, maximum| {
        let bytes = &fixture.files[name];
        assert_eq!(bytes.len(), maximum);
        Ok(bytes.clone())
    })
}

#[test]
fn source_profile_admission_retains_exact_files_and_decodes_without_population_data() {
    let fixture = support::Fixture::new();
    let package = load(&fixture).unwrap();
    for (name, bytes) in &fixture.files {
        assert_eq!(package.file(name).unwrap(), bytes);
    }
    let geometry = package.decode_geometry().unwrap();
    assert_eq!(geometry.groups.len(), 6);
    assert_eq!(geometry.grass.len(), 3);
    assert_eq!(geometry.groups["palms"][0].indices, [0, 1, 2]);
    assert!(
        geometry
            .groups
            .values()
            .all(|levels| levels.len() == 1 && levels[0].positions.len() == 3)
    );
}

#[test]
fn corrupt_resources_and_rehashed_dataset_shape_changes_still_refuse() {
    let mut fixture = support::Fixture::new();
    let raw = fixture.index();
    let mut reads = 0;
    assert!(
        PlantPackage::load(&raw, bytes_sha256(b"wrong index"), |_, _| {
            reads += 1;
            Ok(Vec::new())
        })
        .is_err()
    );
    assert_eq!(reads, 0);
    assert!(
        PlantPackage::load(&raw, bytes_sha256(&raw), |name, _| {
            let mut bytes = fixture.files[name].clone();
            if name == "normal.bin" {
                bytes[0] ^= 1;
            }
            Ok(bytes)
        })
        .is_err()
    );
    let mut document: serde_json::Value =
        serde_json::from_slice(&fixture.files["vegetation-scene.json"]).unwrap();
    document["groups"][0]["count"] = serde_json::json!(1);
    fixture.files.insert(
        "vegetation-scene.json".into(),
        serde_json::to_vec(&document).unwrap(),
    );
    assert!(load(&fixture).is_err());
}

#[test]
fn grass_profile_decodes_exact_streams_and_rejects_rehashed_invalid_heights() {
    let mut fixture = support::Fixture::new();
    let mut description: serde_json::Value =
        serde_json::from_slice(&fixture.files["grass.json"]).unwrap();
    for level in description["levels"].as_array_mut().unwrap() {
        for (name, value) in [
            ("near", serde_json::json!(true)),
            ("capacity", serde_json::json!(48)),
            ("range", serde_json::json!([0, 18, 18])),
            ("kind", serde_json::json!(3)),
        ] {
            level[name] = value;
        }
    }
    fixture.files.insert(
        "grass.json".into(),
        serde_json::to_vec(&description).unwrap(),
    );
    let decoded = load(&fixture).unwrap().grass().unwrap();
    assert_eq!(decoded.flags, vec![0; 65_536]);
    assert_eq!(decoded.min_y.len(), 65_536);
    assert!(decoded.min_y.iter().all(|value| value.to_bits() == 0));
    assert!(decoded.meshes.iter().all(|mesh| mesh.indices == [0, 1, 2]));
    for value in [f32::NAN, f32::INFINITY, 1.0] {
        fixture.files.get_mut("min.bin").unwrap()[..4].copy_from_slice(&value.to_le_bytes());
        assert!(load(&fixture).unwrap().grass().is_err());
    }
}

#[test]
fn aggregate_decoded_geometry_budget_counts_shared_payloads_once_per_level() {
    let mut fixture = support::Fixture::new();
    fixture
        .files
        .insert("indices.bin".into(), vec![0; 36 * 1024 * 1024]);
    let package = load(&fixture).unwrap();
    assert!(
        package
            .decode_geometry()
            .err()
            .unwrap()
            .contains("payload budget")
    );
}

#[test]
fn parameter_abi_and_logical_prototypes_are_explicit_and_preserve_variant_boundary() {
    let mut names: Vec<String> = forge_assets::source_vegetation_package::profile::PARAMETERS
        .iter()
        .map(|s| (*s).into())
        .collect();
    PlantPrototype::validate_parameters(&names).unwrap();
    names.push("source_order".into());
    PlantPrototype::validate_parameters(&names).unwrap();
    names.swap(0, 1);
    assert!(PlantPrototype::validate_parameters(&names).is_err());
    assert!(PlantPrototype::from_key("unknown_tree").is_err());
    assert_eq!(
        PlantPrototype::from_key("tidewater_bananas")
            .unwrap()
            .group(),
        "bananas"
    );
    assert_eq!(
        PlantPrototype::Bananas
            .shader_kind_and_radius(0.49)
            .0
            .to_bits(),
        2.0_f64.to_bits()
    );
    assert_eq!(
        PlantPrototype::Bananas
            .shader_kind_and_radius(0.5)
            .0
            .to_bits(),
        4.0_f64.to_bits()
    );
}
