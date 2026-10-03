//! Requires a real source package root: never skips or substitutes fixture data.
use forge_assets::tidewater_boat::{BoatPackage, package_hash};
use forge_core::bytes_sha256;
use serde_json::{Value, json};
use std::collections::BTreeMap;
use std::future::Future;
use std::path::Path;
use std::task::{Context, Poll, Waker};

#[derive(Clone)]
struct Case {
    manifest: Value,
    files: BTreeMap<String, Vec<u8>>,
}

impl Case {
    fn load(&self) -> Result<BoatPackage, String> {
        let bytes = serde_json::to_vec(&self.manifest).map_err(|e| e.to_string())?;
        BoatPackage::load(&bytes, bytes_sha256(&bytes), |name, _| {
            self.files
                .get(name)
                .cloned()
                .ok_or_else(|| format!("Missing {name}"))
        })
    }

    fn json(&mut self, name: &str, change: impl FnOnce(&mut Value)) {
        let mut value: Value = serde_json::from_slice(&self.files[name]).unwrap();
        change(&mut value);
        self.set(name, serde_json::to_vec(&value).unwrap());
    }

    /// Changes a governed payload and its pins, so negative tests reach semantic
    /// guards instead of merely failing the outer checksum.
    fn set(&mut self, name: &str, bytes: Vec<u8>) {
        self.files.insert(name.to_owned(), bytes);
        let pin = self.pin(name);
        for document in ["boat-materials.json", "boat.json"] {
            let mut value: Value = serde_json::from_slice(&self.files[document]).unwrap();
            let mut changed = replace_reference(&mut value, name, &pin);
            if document == "boat.json" {
                changed |= replace_reference(
                    &mut value,
                    "boat-materials.json",
                    &self.pin("boat-materials.json"),
                );
            }
            if changed {
                self.files
                    .insert(document.to_owned(), serde_json::to_vec(&value).unwrap());
            }
        }
        for payload in [name, "boat-materials.json", "boat.json"] {
            let pin = self.pin(payload);
            self.manifest["files"][payload] = json!({"bytes":pin["bytes"],"sha256":pin["sha256"]});
            replace_reference(&mut self.manifest, payload, &pin);
        }
    }

    fn pin(&self, name: &str) -> Value {
        let bytes = &self.files[name];
        json!({"file":name,"bytes":bytes.len(),"sha256":bytes_sha256(bytes).to_string()})
    }
}

fn replace_reference(value: &mut Value, name: &str, pin: &Value) -> bool {
    let mut changed = false;
    match value {
        Value::Object(object) => {
            if object.get("file").and_then(Value::as_str) == Some(name) {
                object.insert("bytes".into(), pin["bytes"].clone());
                object.insert("sha256".into(), pin["sha256"].clone());
                changed = true;
            }
            for child in object.values_mut() {
                changed |= replace_reference(child, name, pin);
            }
        }
        Value::Array(array) => {
            for child in array {
                changed |= replace_reference(child, name, pin);
            }
        }
        _ => {}
    }
    changed
}

fn with_external_glb_resource(bytes: &[u8]) -> Vec<u8> {
    let length = usize::try_from(u32::from_le_bytes(bytes[12..16].try_into().unwrap())).unwrap();
    let mut document: Value = serde_json::from_slice(&bytes[20..20 + length]).unwrap();
    document["extensions"]["untrusted"] = json!({"uri":"../unclaimed.bin"});
    let mut json_bytes = serde_json::to_vec(&document).unwrap();
    while !json_bytes.len().is_multiple_of(4) {
        json_bytes.push(b' ');
    }
    let rest = &bytes[20 + length..];
    let total = 20 + json_bytes.len() + rest.len();
    let mut result = b"glTF".to_vec();
    result.extend(2u32.to_le_bytes());
    result.extend(u32::try_from(total).unwrap().to_le_bytes());
    result.extend(u32::try_from(json_bytes.len()).unwrap().to_le_bytes());
    result.extend(b"JSON");
    result.extend(json_bytes);
    result.extend(rest);
    result
}

fn ready<T>(future: impl Future<Output = T>) -> T {
    let mut context = Context::from_waker(Waker::noop());
    match std::pin::pin!(future).as_mut().poll(&mut context) {
        Poll::Ready(value) => value,
        Poll::Pending => panic!("The in-memory async reader must be immediately ready"),
    }
}

fn reject(
    base: &Case,
    label: &str,
    change: impl FnOnce(&mut Case),
    expected: &str,
    cases: &mut Vec<Value>,
) {
    let mut case = base.clone();
    change(&mut case);
    let Err(error) = case.load() else {
        panic!("Invalid case accepted: {label}")
    };
    assert!(error.contains(expected), "{label}: wrong guard: {error}");
    cases.push(json!({"case":label,"accepted":false,"error":error}));
}

// Keep the real-package admission matrix and its single report in one harness.
#[allow(clippy::too_many_lines)]
fn verify(root: &Path) -> Result<Value, String> {
    let manifest_bytes = std::fs::read(root.join("manifest.json")).map_err(|e| e.to_string())?;
    if bytes_sha256(&manifest_bytes) != package_hash() {
        return Err("Expected the pinned real source package".into());
    }
    let package = BoatPackage::load(&manifest_bytes, package_hash(), |name, _| {
        std::fs::read(root.join(name)).map_err(|e| e.to_string())
    })?;
    let description = package.description();
    let manifest: Value = serde_json::from_slice(&manifest_bytes).map_err(|e| e.to_string())?;
    let mut files = BTreeMap::new();
    for name in manifest["files"].as_object().ok_or("No file index")?.keys() {
        files.insert(
            name.clone(),
            package.file(name).ok_or("No loaded payload")?.to_vec(),
        );
    }
    let base = Case { manifest, files };
    let asynchronous = ready(BoatPackage::load_async(
        &manifest_bytes,
        package_hash(),
        |name, _| {
            std::future::ready(
                base.files
                    .get(&name)
                    .cloned()
                    .ok_or_else(|| format!("Missing {name}")),
            )
        },
    ))?;
    assert_eq!(package.file_count(), 154);
    assert_eq!(package.file_count(), asynchronous.file_count());
    let mut mesh_summaries = Vec::new();
    let mut triangles = 0;
    for part in &description.parts {
        let sync = package.resolve_part(&part.id)?;
        let asynchronous = asynchronous.resolve_part(&part.id)?;
        assert_eq!(sync.attributes, asynchronous.attributes);
        assert_eq!(sync.indices, asynchronous.indices);
        triangles += sync.indices.len() / 3;
        mesh_summaries.push(
            json!({"id":sync.id,"node":sync.node,"material":sync.material,
            "vertices":sync.attributes["position"].len()/3,"triangles":sync.indices.len()/3}),
        );
    }
    assert_eq!(triangles, 46_845);
    let hull = package.water_exclusion()?;
    let async_hull = asynchronous.water_exclusion()?;
    assert_eq!(hull.positions, async_hull.positions);
    assert_eq!(hull.indices, async_hull.indices);
    assert_eq!(hull.positions.len(), 952);
    assert_eq!(hull.indices.len(), 5_436);
    assert_eq!(
        hull.fingerprint.to_string(),
        "52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e"
    );
    assert_eq!(hull.source_boundary_edges, Some(8));
    assert_eq!(hull.topology.welded_vertices, 910);
    assert_eq!(hull.topology.edges, 2_722);
    let mut negatives = Vec::new();
    let mut reads = 0;
    assert!(
        BoatPackage::load(&manifest_bytes, bytes_sha256(b"wrong receipt"), |_, _| {
            reads += 1;
            Err("must not read".into())
        })
        .is_err()
    );
    assert_eq!(reads, 0);
    negatives.push(json!({"case":"wrong manifest hash before reader","reads":reads}));
    for (label, mutate) in [
        ("manifest schema", 0),
        ("manifest path traversal", 1),
        ("reserved Windows filename", 2),
        ("manifest file size budget", 3),
        ("manifest case collision", 4),
        ("manifest total budget", 5),
    ] {
        let mut manifest = base.manifest.clone();
        match mutate {
            0 => manifest["version"] = json!(2),
            1 => {
                manifest["files"]["../escape.bin"] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x").to_string()});
            }
            2 => {
                manifest["files"]["CON.bin"] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x").to_string()});
            }
            3 => {
                manifest["files"]["huge.bin"] =
                    json!({"bytes":33*1024*1024,"sha256":bytes_sha256(b"x").to_string()});
            }
            4 => manifest["files"]["BOAT.JSON"] = manifest["files"]["boat.json"].clone(),
            _ => {
                for i in 0..5 {
                    manifest["files"][format!("budget-{i}.bin")] =
                        json!({"bytes":32*1024*1024,"sha256":bytes_sha256(b"x").to_string()});
                }
            }
        }
        let bytes = serde_json::to_vec(&manifest).unwrap();
        let mut reads = 0;
        assert!(
            BoatPackage::load(&bytes, bytes_sha256(&bytes), |_, _| {
                reads += 1;
                Err("must not read".into())
            })
            .is_err()
        );
        assert_eq!(reads, 0, "{label}");
        negatives.push(json!({"case":label,"reads":reads}));
    }
    reject(
        &base,
        "missing payload",
        |c| {
            c.files.remove("boat.json");
        },
        "Missing",
        &mut negatives,
    );
    reject(
        &base,
        "corrupted payload",
        |c| {
            c.files.get_mut("boat.json").unwrap()[0] ^= 1;
        },
        "pin mismatch",
        &mut negatives,
    );
    reject(
        &base,
        "descriptor schema",
        |c| c.json("boat.json", |v| v["version"] = json!(2)),
        "source description",
        &mut negatives,
    );
    reject(
        &base,
        "descriptor transitive pin",
        |c| {
            c.json("boat.json", |v| {
                v["parts"][0]["indices"]["sha256"] = json!(bytes_sha256(b"wrong").to_string());
            });
        },
        "dependency pin",
        &mut negatives,
    );
    reject(
        &base,
        "descriptor material root alias",
        |c| {
            c.json("boat.json", |v| {
                v["material_library"] = v["inspection_glb"].clone();
            });
        },
        "source description",
        &mut negatives,
    );
    reject(
        &base,
        "source graph digest",
        |c| {
            c.json("boat.json", |v| {
                v["source_graph_sha256"] = json!(bytes_sha256(b"wrong").to_string());
            });
        },
        "graph fingerprint",
        &mut negatives,
    );
    reject(
        &base,
        "support execution declaration",
        |c| {
            c.json("boat.json", |v| {
                v["source_support_files"][0]["executed"] = json!(true);
            });
        },
        "provenance",
        &mut negatives,
    );
    reject(
        &base,
        "missing parent",
        |c| c.json("boat.json", |v| v["nodes"][1]["parent"] = json!("missing")),
        "parent absent",
        &mut negatives,
    );
    reject(
        &base,
        "cyclic parent",
        |c| {
            c.json("boat.json", |v| {
                v["nodes"][1]["parent"] = v["nodes"][1]["id"].clone();
            });
        },
        "cycle",
        &mut negatives,
    );
    reject(
        &base,
        "duplicate node",
        |c| {
            c.json("boat.json", |v| {
                v["nodes"][1]["id"] = v["nodes"][2]["id"].clone();
            });
        },
        "node identities",
        &mut negatives,
    );
    reject(
        &base,
        "matrix drift",
        |c| {
            c.json("boat.json", |v| {
                v["nodes"][1]["matrix_world"][12] = json!(100);
            });
        },
        "matrix differs",
        &mut negatives,
    );
    reject(
        &base,
        "zero scale",
        |c| {
            c.json("boat.json", |v| {
                v["nodes"][1]["transform"]["scale"][0] = json!(0);
            });
        },
        "local frame",
        &mut negatives,
    );
    reject(
        &base,
        "missing material",
        |c| {
            c.json("boat.json", |v| {
                v["parts"][0]["material"] = json!("unknown");
            });
        },
        "binding",
        &mut negatives,
    );
    reject(
        &base,
        "attribute layout",
        |c| {
            c.json("boat.json", |v| {
                v["parts"][0]["attributes"]["aux"]["item_size"] = json!(3);
            });
        },
        "layout",
        &mut negatives,
    );
    reject(
        &base,
        "mesh bounds",
        |c| {
            c.json("boat.json", |v| {
                v["parts"][0]["bounds_local"]["min"][0] = json!(-999);
            });
        },
        "bounds",
        &mut negatives,
    );
    let positions = description.parts[0].attributes["position"].file.clone();
    reject(
        &base,
        "nonfinite mesh position",
        |c| {
            let mut bytes = c.files[&positions].clone();
            bytes[..4].copy_from_slice(&f32::NAN.to_le_bytes());
            c.set(&positions, bytes);
        },
        "nonfinite",
        &mut negatives,
    );
    let indices = description.parts[0].indices.file.clone();
    let component = &description.parts[0].indices.component_type;
    reject(
        &base,
        "out of bounds index",
        |c| {
            let mut bytes = c.files[&indices].clone();
            if component == "u16" {
                bytes[..2].copy_from_slice(&u16::MAX.to_le_bytes());
            } else {
                bytes[..4].copy_from_slice(&u32::MAX.to_le_bytes());
            }
            c.set(&indices, bytes);
        },
        "missing vertex",
        &mut negatives,
    );
    reject(
        &base,
        "hull fingerprint",
        |c| {
            c.json("boat.json", |v| {
                v["water_exclusion"]["geometry_fingerprint"] =
                    json!(bytes_sha256(b"wrong").to_string());
            });
        },
        "fingerprint",
        &mut negatives,
    );
    reject(
        &base,
        "hull boundary receipt",
        |c| {
            c.json("boat.json", |v| {
                v["water_exclusion"]["topology"]["boundary_edges"] = json!(0);
            });
        },
        "topology differs",
        &mut negatives,
    );
    reject(
        &base,
        "hull falsely declared closed",
        |c| {
            c.json("boat.json", |v| {
                v["water_exclusion"]["topology"]["kind"] = json!("closed");
                v["water_exclusion"]["topology"]["boundary_edges"] = json!(0);
            });
        },
        "topology differs",
        &mut negatives,
    );
    reject(
        &base,
        "hull welded receipt",
        |c| {
            c.json("boat.json", |v| {
                v["water_exclusion"]["topology"]["welded_vertices"] = json!(911);
            });
        },
        "topology differs",
        &mut negatives,
    );
    reject(
        &base,
        "hull parent",
        |c| {
            c.json("boat.json", |v| {
                v["water_exclusion"]["parent"] = json!("missing");
            });
        },
        "exclusion identity",
        &mut negatives,
    );
    reject(
        &base,
        "anchor shape",
        |c| c.json("boat.json", |v| v["anchors"]["propeller"] = json!([0, 1])),
        "vector differs",
        &mut negatives,
    );
    reject(
        &base,
        "control node",
        |c| {
            c.json("boat.json", |v| {
                v["controller_observations"]["part_controls"][0]["node"] = json!("missing");
            });
        },
        "node unknown",
        &mut negatives,
    );
    reject(
        &base,
        "duplicate collider",
        |c| {
            c.json("boat.json", |v| {
                v["colliders"][1]["id"] = v["colliders"][0]["id"].clone();
            });
        },
        "collider",
        &mut negatives,
    );
    reject(
        &base,
        "invalid uniform",
        |c| {
            c.json("boat-materials.json", |v| {
                v["materials"][0]["uniforms"][0]["value"] = json!([1, 1]);
            });
        },
        "uniform",
        &mut negatives,
    );
    reject(
        &base,
        "overflowing uniform",
        |c| {
            c.json("boat-materials.json", |v| {
                v["materials"][0]["uniforms"][0]["value"][0] = json!(f64::MAX);
            });
        },
        "finite renderer range",
        &mut negatives,
    );
    reject(
        &base,
        "unclaimed payload",
        |c| {
            c.set("extra.bin", b"unused".to_vec());
        },
        "unclaimed",
        &mut negatives,
    );
    reject(
        &base,
        "external GLB resource",
        |c| {
            let bytes = with_external_glb_resource(&c.files["lobster-boat.glb"]);
            c.set("lobster-boat.glb", bytes);
        },
        "without URI",
        &mut negatives,
    );
    Ok(
        json!({"status":"passed","package_root":root,"manifest_sha256":package.receipt_hash().to_string(),
        "payloads":package.file_count(),"nodes":description.nodes.len(),"meshes":mesh_summaries,"triangles":triangles,
        "materials":package.materials()["materials"].as_array().unwrap().len(),"colliders":description.colliders.len(),
        "flotation_samples":description.hull_samples.len(),"sync_async_identical":true,
        "hull":{"vertices":hull.positions.len(),"indices":hull.indices.len(),"fingerprint":hull.fingerprint.to_string(),"boundary_edges":hull.source_boundary_edges,"welded_vertices":hull.topology.welded_vertices,"edges":hull.topology.edges},
        "negative_cases":negatives,"qualification":"CPU package transport only; no rendering, Studio placement or gameplay qualification"}),
    )
}

fn main() {
    let mut args = std::env::args_os().skip(1);
    let root = args
        .next()
        .expect("Usage: verify_tidewater_boat PACKAGE_ROOT");
    assert!(args.next().is_none(), "Unexpected extra argument");
    match verify(Path::new(&root)) {
        Ok(result) => println!("{}", serde_json::to_string_pretty(&result).unwrap()),
        Err(error) => {
            eprintln!("{error}");
            std::process::exit(1);
        }
    }
}
