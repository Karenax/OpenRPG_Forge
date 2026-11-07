use forge_assets::source_vegetation_geometry::{
    MAX_SOURCE_VEGETATION_VERTICES, SourceVegetationGeometry,
};
use std::collections::BTreeMap;

fn floats<const N: usize>(values: &[[f32; N]]) -> Vec<u8> {
    values
        .iter()
        .flatten()
        .flat_map(|value| value.to_le_bytes())
        .collect()
}

struct Fixture {
    attributes: BTreeMap<String, String>,
    files: BTreeMap<String, Vec<u8>>,
}

impl Fixture {
    fn new() -> Self {
        Self {
            attributes: BTreeMap::from([
                ("position".into(), "p.bin".into()),
                ("normal".into(), "n.bin".into()),
            ]),
            files: BTreeMap::from([
                (
                    "p.bin".into(),
                    floats(&[[-0.0, 2.0, 3.0], [1.0, 0.0, 2.0], [4.0, 5.0, 6.0]]),
                ),
                ("n.bin".into(), floats(&[[0.0, 2.0, -0.0]; 3])),
                (
                    "i.bin".into(),
                    [2_u32, 0, 1]
                        .into_iter()
                        .flat_map(u32::to_le_bytes)
                        .collect(),
                ),
            ]),
        }
    }

    fn decode(&self) -> Result<SourceVegetationGeometry, String> {
        SourceVegetationGeometry::decode(&self.attributes, "i.bin", |name| {
            self.files
                .get(name)
                .map(Vec::as_slice)
                .ok_or_else(|| format!("Absent resource {name}"))
        })
    }
}

#[test]
fn source_attribute_bits_order_and_absence_survive_without_normalization() {
    let fixture = Fixture::new();
    let geometry = fixture.decode().unwrap();
    assert_eq!(floats(&geometry.positions), fixture.files["p.bin"]);
    assert_eq!(floats(&geometry.normals), fixture.files["n.bin"]);
    assert_eq!(geometry.positions[0][0].to_bits(), (-0.0_f32).to_bits());
    assert_eq!(geometry.indices, [2, 0, 1]);
    assert!(
        geometry.uv.is_none()
            && geometry.vegetation.is_none()
            && geometry.material.is_none()
            && geometry.lobe.is_none()
    );
}

#[test]
fn shader_streams_are_exact_and_a_declared_missing_stream_cannot_be_defaulted() {
    let mut fixture = Fixture::new();
    for (name, data) in [
        ("uv", floats(&[[-0.0, 2.25], [3.0, -4.0], [0.5, 0.75]])),
        ("aVeg", floats(&[[1.25, -2.0, 0.0, 5.0]; 3])),
        ("aMat", floats(&[[6.0, 7.0, 8.0, 9.0]; 3])),
        ("aLobe", floats(&[[10.0, 11.0, 12.0, 13.0]; 3])),
    ] {
        fixture.attributes.insert(name.into(), name.into());
        fixture.files.insert(name.into(), data);
    }
    let geometry = fixture.decode().unwrap();
    assert_eq!(floats(geometry.uv.as_ref().unwrap()), fixture.files["uv"]);
    assert_eq!(
        floats(geometry.vegetation.as_ref().unwrap()),
        fixture.files["aVeg"]
    );
    assert_eq!(
        floats(geometry.material.as_ref().unwrap()),
        fixture.files["aMat"]
    );
    assert_eq!(
        floats(geometry.lobe.as_ref().unwrap()),
        fixture.files["aLobe"]
    );
    fixture.files.remove("aLobe");
    assert!(
        fixture
            .decode()
            .err()
            .unwrap()
            .contains("Absent resource aLobe")
    );
    for name in ["uv", "aVeg", "aMat", "aLobe"] {
        let mut malformed = Fixture::new();
        malformed.attributes.insert(name.into(), name.into());
        malformed.files.insert(name.into(), vec![0; 7]);
        assert!(malformed.decode().is_err(), "accepted malformed {name}");
        let width = if name == "uv" { 2 } else { 4 };
        malformed.files.insert(
            name.into(),
            (0..3 * width)
                .flat_map(|_| f32::NAN.to_le_bytes())
                .collect(),
        );
        assert!(malformed.decode().is_err(), "accepted nonfinite {name}");
    }
}

#[test]
fn malformed_geometry_is_refused_before_it_can_reach_a_renderer() {
    for (path, bytes) in [
        ("p.bin", vec![]),
        ("p.bin", vec![0; 35]),
        ("p.bin", floats(&[[f32::NAN, 0.0, 0.0]; 3])),
        ("n.bin", floats(&[[0.0, 1.0, 0.0]; 2])),
        ("n.bin", floats(&[[0.0, f32::INFINITY, 0.0]; 3])),
        ("i.bin", vec![]),
        ("i.bin", vec![0; 11]),
        (
            "i.bin",
            [0_u32, 1, 3]
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect(),
        ),
        (
            "i.bin",
            [0_u32, 1, u32::MAX]
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect(),
        ),
    ] {
        let mut fixture = Fixture::new();
        fixture.files.insert(path.into(), bytes);
        assert!(fixture.decode().is_err(), "accepted {path}");
    }
    let mut fixture = Fixture::new();
    fixture.attributes.insert("unknown".into(), "n.bin".into());
    assert!(fixture.decode().is_err());
    fixture.attributes.remove("unknown");
    fixture.attributes.remove("normal");
    assert!(fixture.decode().is_err());
}

#[test]
fn oversized_positions_are_refused_before_reading_other_payloads() {
    let fixture = Fixture::new();
    let oversized = vec![0; (MAX_SOURCE_VEGETATION_VERTICES + 1) * 12];
    let mut reads = Vec::new();
    let result = SourceVegetationGeometry::decode(&fixture.attributes, "i.bin", |name| {
        reads.push(name.to_owned());
        assert_eq!(name, "p.bin");
        Ok(&oversized)
    });
    assert!(result.is_err());
    assert_eq!(reads, ["p.bin"]);
}
