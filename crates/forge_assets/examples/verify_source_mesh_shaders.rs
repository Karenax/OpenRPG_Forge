//! Verify an actual autonomous source shader package and original CPU packing
//! oracles. No shader draw or appearance qualification is claimed by this tool.
use forge_assets::source_mesh_shaders::{SourceMeshShaderPackage, SourceUniformBlock};
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
    future::Future,
    path::Path,
    task::{Context, Poll, Waker},
};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct PackingOracle {
    block: SourceUniformBlock,
    packed_sha256: ContentHash,
    packed_bytes: usize,
    sites: Vec<String>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct PackingOracles {
    schema: String,
    version: u32,
    capture_sha256: ContentHash,
    source_packer_sha256: ContentHash,
    gpu_executed: bool,
    oracles: Vec<PackingOracle>,
}

fn load(
    root: &Path,
    manifest: &[u8],
    expected: ContentHash,
) -> Result<SourceMeshShaderPackage, Box<dyn Error>> {
    Ok(SourceMeshShaderPackage::load(
        manifest,
        expected,
        |name, size| {
            let bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
            if bytes.len() != size {
                return Err(format!("unexpected payload length for {name}"));
            }
            Ok(bytes)
        },
    )?)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.len() != 4 {
        return Err("Usage: verify_source_mesh_shaders PACKAGE_ROOT MANIFEST_SHA256 ORACLES_JSON ORACLES_SHA256".into());
    }
    let root = Path::new(&args[0]);
    let manifest = std::fs::read(root.join("manifest.json"))?;
    let package = load(root, &manifest, args[1].parse()?)?;
    verify_async(root, &manifest, &package)?;
    let oracle_bytes = std::fs::read(&args[2])?;
    if bytes_sha256(&oracle_bytes) != args[3].parse()? {
        return Err("packing oracle receipt differs".into());
    }
    let oracles: PackingOracles = serde_json::from_slice(&oracle_bytes)?;
    if oracles.schema != "forge.source-uniform-packing-oracles"
        || oracles.version != 1
        || oracles.gpu_executed
    {
        return Err("packing oracle schema or scope differs".into());
    }
    if oracles.capture_sha256 != package.manifest().provenance.capture.sha256 {
        return Err("packing oracles belong to another source capture".into());
    }
    let (sites, unique_blocks, packed_bytes) = verify_packing(&package, &oracles)?;
    let shader_bytes = package
        .manifest()
        .programs
        .iter()
        .try_fold(0, |n, p| package.code(p).map(|code| n + code.len()))?;
    let negative_checks = verify_rejections(root, &manifest, &package)?;
    println!(
        "{}",
        serde_json::json!({
            "status":"passed", "package_sha256":package.hash(), "materials":package.manifest().materials.len(),
            "programs":package.manifest().programs.len(), "hooks":package.manifest().hooks.len(),
            "shader_bytes":shader_bytes, "uniform_sites":sites, "unique_uniform_blocks":unique_blocks,
            "packed_bytes":packed_bytes, "source_packer_sha256":oracles.source_packer_sha256,
            "negative_checks":negative_checks, "sync_async_payloads_equal":true,
            "declared_binding_requirements":package.manifest().programs.iter().map(|p| serde_json::json!({
                "material":p.material,"kind":p.pass.kind,"requirements":p.declared_requirements()
            })).collect::<Vec<_>>(),
            "gpu_executed":false, "appearance_qualified":false
        })
    );
    Ok(())
}

fn verify_packing(
    package: &SourceMeshShaderPackage,
    oracles: &PackingOracles,
) -> Result<(usize, usize, usize), Box<dyn Error>> {
    let mut required: BTreeMap<Vec<u8>, BTreeSet<String>> = BTreeMap::new();
    let mut all_sites = BTreeSet::new();
    let mut packed_bytes = 0;
    let mut add = |block: &SourceUniformBlock, site: String| -> Result<(), Box<dyn Error>> {
        if !all_sites.insert(site.clone()) {
            return Err("duplicate package uniform site".into());
        }
        let key = serde_json::to_vec(block)?;
        if !required.contains_key(&key) {
            let oracle = oracles
                .oracles
                .iter()
                .find(|o| o.block == *block)
                .ok_or("source packing oracle missing")?;
            let packed = block.pack()?;
            if packed.len() != oracle.packed_bytes || bytes_sha256(&packed) != oracle.packed_sha256
            {
                return Err(format!("source packing differs for {}", block.struct_name).into());
            }
            packed_bytes += packed.len();
        }
        required.entry(key).or_default().insert(site);
        Ok(())
    };
    add(&package.manifest().frame, "frame".into())?;
    for material in &package.manifest().materials {
        add(&material.uniforms, format!("material/{}", material.name))?;
    }
    for program in &package.manifest().programs {
        let pass = match program.pass.kind {
            forge_assets::source_mesh_shaders::SourcePassKind::Main => "main",
            forge_assets::source_mesh_shaders::SourcePassKind::Color => "color",
            forge_assets::source_mesh_shaders::SourcePassKind::Depth => "depth",
        };
        for (group, bindings) in [(0, &program.group0), (1, &program.group1)] {
            for binding in bindings {
                if let Some(block) = &binding.uniform {
                    add(
                        block,
                        format!("{}/{pass}/group{group}/{}", program.material, binding.name),
                    )?;
                }
            }
        }
    }
    let unique_blocks = required.len();
    if unique_blocks != oracles.oracles.len() {
        return Err("packing oracle block inventory differs".into());
    }
    for oracle in &oracles.oracles {
        let key = serde_json::to_vec(&oracle.block)?;
        let expected: BTreeSet<_> = oracle.sites.iter().cloned().collect();
        if expected.is_empty()
            || expected.len() != oracle.sites.len()
            || required.remove(&key).as_ref() != Some(&expected)
        {
            return Err("packing oracle site inventory differs".into());
        }
    }
    Ok((all_sites.len(), unique_blocks, packed_bytes))
}

fn verify_async(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
) -> Result<(), Box<dyn Error>> {
    let future =
        SourceMeshShaderPackage::load_async(manifest, package.hash(), |name, size| async move {
            let bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
            if bytes.len() != size {
                return Err("async payload size differs".into());
            }
            Ok(bytes)
        });
    let mut future = std::pin::pin!(future);
    let async_package = match future
        .as_mut()
        .poll(&mut Context::from_waker(Waker::noop()))
    {
        Poll::Ready(result) => result?,
        Poll::Pending => {
            return Err("immediate source package callback unexpectedly suspended".into());
        }
    };
    if async_package.hash() != package.hash()
        || async_package.manifest().programs.len() != package.manifest().programs.len()
    {
        return Err("sync/async package receipt differs".into());
    }
    for (sync, asynchronous) in package
        .manifest()
        .programs
        .iter()
        .zip(&async_package.manifest().programs)
    {
        if package.code(sync)? != async_package.code(asynchronous)? {
            return Err("sync/async shader payload differs".into());
        }
    }
    Ok(())
}

fn verify_rejections(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
) -> Result<usize, Box<dyn Error>> {
    // This proof exercises the source profile's opaque main pass. Locate a
    // suitable fixture by semantics and fail explicitly if it is unavailable.
    let fixture = rejection_fixture(package)?;
    let edits = [
        (
            fixture.duplicate_slot,
            serde_json::json!(fixture.duplicate_value),
        ),
        (fixture.vertex_stride, serde_json::json!(4)),
        ("/frame/fields/0/offset".into(), serde_json::json!(4)),
        (fixture.fragment, serde_json::json!(false)),
        (fixture.draw_stride, serde_json::json!(160)),
        (
            fixture.mixed_layout,
            serde_json::json!({"visibility":3,"buffer":{"type":"uniform"},"sampler":{"type":"filtering"}}),
        ),
        (fixture.frame_value, serde_json::json!(99)),
    ];
    let mut negative_checks = 0;
    for (pointer, replacement) in edits {
        let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
        *bad.pointer_mut(&pointer)
            .ok_or("negative fixture field missing")? = replacement;
        let changed = serde_json::to_vec(&bad)?;
        if load(root, &changed, bytes_sha256(&changed)).is_ok() {
            return Err("invalid semantic package was accepted".into());
        }
        negative_checks += 1;
    }
    for unknown in [false, true] {
        let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
        let files = bad["files"]
            .as_object_mut()
            .ok_or("negative fixture file inventory missing")?;
        let key = files
            .keys()
            .next()
            .ok_or("negative fixture file inventory empty")?
            .clone();
        let value = files
            .remove(&key)
            .ok_or("negative fixture payload missing")?;
        files.insert("../escape".into(), value);
        if unknown {
            // Reparse the valid manifest so that this check isolates strictness.
            bad = serde_json::from_slice(manifest)?;
            bad.as_object_mut()
                .ok_or("negative fixture manifest missing")?
                .insert("unexpected".into(), true.into());
        }
        let changed = serde_json::to_vec(&bad)?;
        if load(root, &changed, bytes_sha256(&changed)).is_ok() {
            return Err("unsafe path or unknown root field accepted".into());
        }
        negative_checks += 1;
    }
    // Cryptographic integrity is a separate refusal from semantic validation.
    if SourceMeshShaderPackage::load(manifest, bytes_sha256(b"wrong receipt"), |_, _| Ok(vec![]))
        .is_ok()
        || SourceMeshShaderPackage::load(manifest, package.hash(), |_, _| Ok(vec![0])).is_ok()
    {
        return Err("bad manifest receipt or payload was accepted".into());
    }
    negative_checks += 2;
    if package.manifest().version == 2 {
        negative_checks += verify_pipeline_rejections(root, manifest, package)?;
    }
    Ok(negative_checks)
}

fn verify_pipeline_rejections(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
) -> Result<usize, Box<dyn Error>> {
    let (index, program) = package
        .manifest()
        .programs
        .iter()
        .enumerate()
        .find(|(_, p)| {
            p.pass.kind == forge_assets::source_mesh_shaders::SourcePassKind::Main && !p.pass.late
        })
        .ok_or("pipeline negatives need an early main variant")?;
    let state = program
        .pipeline_state
        .as_ref()
        .ok_or("pipeline state missing")?;
    let cull = match state.primitive.cull_mode {
        forge_assets::source_mesh_shaders::SourceCull::Back => "front",
        _ => "back",
    };
    let base = format!("/programs/{index}");
    let edits = [
        ("pipeline_state/primitive/cullMode", serde_json::json!(cull)),
        (
            "pipeline_state/primitive/frontFace",
            serde_json::json!("cw"),
        ),
        (
            "pipeline_state/primitive/topology",
            serde_json::json!("line-list"),
        ),
        ("pipeline_state/targets/2/blend", serde_json::Value::Null),
        ("pipeline_state/targets/0/writeMask", serde_json::json!(1)),
        (
            "pipeline_state/depthStencil/depthCompare",
            serde_json::json!("less-equal"),
        ),
        (
            "pipeline_state/depthStencil/depthBias",
            serde_json::json!(state.depth_stencil.depth_bias + 1),
        ),
        (
            "pipeline_state/depthStencil/depthBiasSlopeScale",
            serde_json::json!(state.depth_stencil.depth_bias_slope_scale + 0.5),
        ),
        ("pipeline_state/multisample/count", serde_json::json!(4)),
        ("pipeline_state/multisample/mask", serde_json::json!(0)),
        (
            "pipeline_state/multisample/alphaToCoverageEnabled",
            serde_json::json!(true),
        ),
        ("pass/rasterOverrides/cullMode", serde_json::json!(cull)),
        ("pass/rasterOverrides", serde_json::Value::Null),
    ];
    for (suffix, replacement) in &edits {
        let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
        *bad.pointer_mut(&format!("{base}/{suffix}"))
            .ok_or("pipeline negative field missing")? = replacement.clone();
        let changed = serde_json::to_vec(&bad)?;
        if load(root, &changed, bytes_sha256(&changed)).is_ok() {
            return Err(format!("inconsistent source pipeline accepted: {suffix}").into());
        }
    }
    let (depth_index, depth) = package
        .manifest()
        .programs
        .iter()
        .enumerate()
        .find(|(_, p)| {
            p.pass.kind == forge_assets::source_mesh_shaders::SourcePassKind::Depth
                && p.has_fragment
        })
        .ok_or("pipeline negatives need a fragment depth variant")?;
    let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
    bad["programs"][depth_index]["has_fragment"] = (!depth.has_fragment).into();
    let changed = serde_json::to_vec(&bad)?;
    if load(root, &changed, bytes_sha256(&changed)).is_ok() {
        return Err("alpha-tested depth fragment omission accepted".into());
    }
    Ok(edits.len() + 1 + verify_material_pipeline_rejections(root, manifest, package)?)
}

fn verify_material_pipeline_rejections(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
) -> Result<usize, Box<dyn Error>> {
    let (index, material) = package
        .manifest()
        .materials
        .iter()
        .enumerate()
        .find(|(_, m)| m.raster.as_ref().is_some_and(|r| r.color_write))
        .ok_or("material negatives require color-writing raster state")?;
    let raster = material.raster.as_ref().ok_or("material raster missing")?;
    let base = format!("/materials/{index}");
    let edits = [
        ("raster/colorWrite", serde_json::json!(false)),
        ("raster/depthCompare", serde_json::json!("always")),
        ("raster/depthBias", serde_json::json!(raster.depth_bias + 1)),
        (
            "raster/depthBiasSlopeScale",
            serde_json::json!(raster.depth_bias_slope_scale + 0.25),
        ),
        ("raster/topology", serde_json::json!("point-list")),
        ("raster", serde_json::Value::Null),
    ];
    for (suffix, replacement) in &edits {
        let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
        *bad.pointer_mut(&format!("{base}/{suffix}"))
            .ok_or("material negative field missing")? = replacement.clone();
        let changed = serde_json::to_vec(&bad)?;
        if load(root, &changed, bytes_sha256(&changed)).is_ok() {
            return Err(format!("inconsistent material raster accepted: {suffix}").into());
        }
    }
    for pointer in [
        format!("{base}/raster"),
        "/programs/0/pass/rasterOverrides".into(),
    ] {
        let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
        bad.pointer_mut(&pointer)
            .and_then(serde_json::Value::as_object_mut)
            .ok_or("nested strictness fixture missing")?
            .insert("unexpected".into(), true.into());
        let changed = serde_json::to_vec(&bad)?;
        if load(root, &changed, bytes_sha256(&changed)).is_ok() {
            return Err("unknown nested raster policy accepted".into());
        }
    }
    verify_raster_overrides(root, manifest, package)?;
    Ok(edits.len() + 2)
}

fn verify_raster_overrides(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
) -> Result<(), Box<dyn Error>> {
    // These source raster switches do not alter WGSL composition. Coherent
    // overrides must remain usable, while the negatives reject one-sided edits.
    let mut changed: serde_json::Value = serde_json::from_slice(manifest)?;
    let first = &package.manifest().programs[0];
    let state = first
        .pipeline_state
        .as_ref()
        .ok_or("override fixture state missing")?;
    let cull = match state.primitive.cull_mode {
        forge_assets::source_mesh_shaders::SourceCull::Back => "front",
        _ => "back",
    };
    changed["programs"][0]["pass"]["rasterOverrides"]["cullMode"] = cull.into();
    changed["programs"][0]["pipeline_state"]["primitive"]["cullMode"] = cull.into();
    let bytes = serde_json::to_vec(&changed)?;
    load(root, &bytes, bytes_sha256(&bytes))?;
    let mut changed: serde_json::Value = serde_json::from_slice(manifest)?;
    let (material_index, material) = package
        .manifest()
        .materials
        .iter()
        .enumerate()
        .find(|(_, m)| m.depth_test)
        .ok_or("depth compare override fixture missing")?;
    changed["materials"][material_index]["raster"]["depthCompare"] = "always".into();
    for (index, program) in package.manifest().programs.iter().enumerate() {
        if program.material == material.name {
            changed["programs"][index]["pipeline_state"]["depthStencil"]["depthCompare"] =
                "always".into();
        }
    }
    let bytes = serde_json::to_vec(&changed)?;
    load(root, &bytes, bytes_sha256(&bytes))?;
    Ok(())
}

struct RejectionFixture {
    duplicate_slot: String,
    duplicate_value: u32,
    vertex_stride: String,
    fragment: String,
    draw_stride: String,
    mixed_layout: String,
    frame_value: String,
}

fn rejection_fixture(
    package: &SourceMeshShaderPackage,
) -> Result<RejectionFixture, Box<dyn Error>> {
    use forge_assets::source_mesh_shaders::{SourcePassKind, SourceUniformValue};
    let manifest = package.manifest();
    let (p, program) = manifest
        .programs
        .iter()
        .enumerate()
        .find(|(_, p)| p.pass.kind == SourcePassKind::Main && !p.pass.late && p.group0.len() >= 2)
        .ok_or("negative proof requires an opaque main pass with two frame bindings")?;
    let (m, b) = manifest
        .materials
        .iter()
        .enumerate()
        .find_map(|(m, material)| {
            material
                .vertex_buffers
                .iter()
                .position(|b| {
                    b.attributes.iter().any(|a| {
                        a.offset > 0
                            || !matches!(
                                a.format,
                                forge_assets::source_mesh_shaders::SourceVertexFormat::Float32
                            )
                    })
                })
                .map(|b| (m, b))
        })
        .ok_or("negative proof requires a vertex buffer wider than a scalar")?;
    let (binding, block) = program
        .group0
        .iter()
        .enumerate()
        .find_map(|(i, b)| {
            b.uniform
                .as_ref()
                .filter(|block| *block == &manifest.frame)
                .map(|block| (i, block))
        })
        .ok_or("negative proof requires the program frame uniform")?;
    let field = block
        .fields
        .first()
        .ok_or("negative proof requires a frame field")?;
    let suffix = match &field.value {
        SourceUniformValue::Scalar(_) => "",
        SourceUniformValue::Vector(_) => "/0",
        SourceUniformValue::Array(_) => "/0/0",
    };
    Ok(RejectionFixture {
        duplicate_slot: format!("/programs/{p}/group0/1/binding"),
        duplicate_value: program.group0[0].binding,
        vertex_stride: format!("/materials/{m}/vertex_buffers/{b}/arrayStride"),
        fragment: format!("/programs/{p}/has_fragment"),
        draw_stride: format!("/programs/{p}/draw_binding/dynamic_stride"),
        mixed_layout: format!("/programs/{p}/group0/{binding}/layout"),
        frame_value: format!("/programs/{p}/group0/{binding}/uniform/fields/0/value{suffix}"),
    })
}
