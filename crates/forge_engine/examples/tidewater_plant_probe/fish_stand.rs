//! Additive `FishStandStall` input for the fixed-view Tidewater probe.
use super::Probe;
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    gltf::GltfAssetLabel,
    mesh::Indices,
    prelude::*,
    render::render_resource::PrimitiveTopology,
    world_serialization::{WorldAssetRoot, WorldInstanceReady},
};
use forge_core::bytes_sha256;
use forge_engine::tidewater_stall::{FishStandMaterial, FishStandPrepared};
use std::{
    collections::BTreeMap,
    error::Error,
    fs,
    path::{Path, PathBuf},
};

#[derive(Resource)]
pub(super) struct ResidentStall {
    pub entity: Entity,
    pub resident: forge_engine::tidewater_stall::FishStandResident,
}

fn placement_transform(
    probe: &Probe,
    prepared: &FishStandPrepared,
) -> Result<Transform, Box<dyn Error>> {
    let y = probe
        .terrain
        .as_ref()
        .map(|terrain| {
            terrain.height_at(prepared.height_sample_xz[0], prepared.height_sample_xz[1])
        })
        .transpose()?
        .map_or(Ok(0.0), |height| checked_f32(height, "fish terrain height"))?;
    Ok(
        Transform::from_xyz(prepared.placement.x, y, prepared.placement.z)
            .with_rotation(Quat::from_rotation_y(prepared.placement.yaw_radians)),
    )
}

/// Source FishStand.js marks its static opaque `StallKit` mesh as a caster.
/// Use the same terrain placement and source positions as the visible stall.
pub fn shadow_draw(
    probe: &Probe,
) -> Result<Option<forge_engine::tidewater_object_shadow::OpaqueDraw>, Box<dyn Error>> {
    let Some(prepared) = probe.fish_stand.as_ref() else {
        return Ok(None);
    };
    let pose = placement_transform(probe, prepared)?;
    Ok(Some(
        forge_engine::tidewater_object_shadow::OpaqueDraw::from_mesh(
            &prepared.mesh,
            pose.to_matrix(),
        )?,
    ))
}

#[derive(Clone)]
pub struct FishStandContent {
    root: PathBuf,
    fish: Vec<Fish>,
    pub(super) source: Option<std::sync::Arc<super::source_fish::Inputs>>,
}

// Fish descriptors are authored as JSON numbers (f64 in serde_json), while
// the Bevy mesh/material contract is f32. Validate finiteness and range
// before this one intentional narrowing conversion.
#[allow(clippy::cast_possible_truncation)]
fn checked_f32(value: f64, label: &str) -> Result<f32, Box<dyn Error>> {
    if !value.is_finite() || value < f64::from(f32::MIN) || value > f64::from(f32::MAX) {
        return Err(format!("{label} is outside the finite f32 range").into());
    }
    Ok(value as f32)
}

#[derive(Component)]
struct JoeIdle {
    graph: Handle<AnimationGraph>,
    node: AnimationNodeIndex,
}
#[derive(Clone)]
struct Fish {
    species: String,
    length: f32,
    x: f32,
    z: f32,
    yaw: f32,
    pose: String,
    rest: f32,
    geometry: serde_json::Value,
}

pub fn load(scene_root: &Path) -> Result<Option<FishStandPrepared>, Box<dyn Error>> {
    let Some(bundle) = scene_root.parent() else {
        return Ok(None);
    };
    let root = bundle.join("fish-stand");
    if !root.is_dir() {
        return Ok(None);
    }
    let descriptor = fs::read(root.join("fish-stand.json"))?;
    let value: serde_json::Value = serde_json::from_slice(&descriptor)?;
    let mut files = BTreeMap::new();
    files.insert("fish-stand.json".into(), descriptor);
    for payload in value["geometry"]["attributes"]
        .as_object()
        .ok_or("fish attributes")?
        .values()
        .chain(std::iter::once(&value["geometry"]["indices"]))
    {
        let file = payload["file"].as_str().ok_or("fish geometry file")?;
        files.insert(file.into(), fs::read(root.join(file))?);
    }
    for texture in value["textures"]
        .as_object()
        .ok_or("fish textures")?
        .values()
    {
        let file = texture["file"].as_str().ok_or("fish texture file")?;
        files.insert(file.into(), fs::read(root.join(file))?);
    }
    Ok(Some(FishStandPrepared::load(&files)?))
}

pub fn load_content(scene_root: &Path) -> Result<Option<FishStandContent>, Box<dyn Error>> {
    let source = super::source_fish::load()?;
    let Some(bundle) = scene_root.parent() else {
        if source.is_some() {
            return Err("Native source fish require a scene bundle".into());
        }
        return Ok(None);
    };
    let root = bundle.join("fish-stand-fish");
    if !root.is_dir() {
        if source.is_some() {
            return Err("Native source fish require the stall content bundle".into());
        }
        return Ok(None);
    }
    let value: serde_json::Value = serde_json::from_slice(&fs::read(root.join("fish.json"))?)?;
    if value["schema"] != "forge.tidewater.fish-stand-fish-source-export"
        || value["fish"].as_array().map_or(0, Vec::len) != 5
    {
        return Err("invalid FishStand fish descriptor".into());
    }
    let fish = value["fish"]
        .as_array()
        .unwrap()
        .iter()
        .map(|item| {
            Ok(Fish {
                species: item["species"].as_str().ok_or("fish species")?.to_owned(),
                length: checked_f32(
                    item["length_meters"].as_f64().ok_or("fish length")?,
                    "fish length",
                )?,
                x: checked_f32(item["local_x"].as_f64().ok_or("fish x")?, "fish x")?,
                z: checked_f32(item["local_z"].as_f64().ok_or("fish z")?, "fish z")?,
                yaw: checked_f32(
                    item["local_yaw_radians"].as_f64().ok_or("fish yaw")?,
                    "fish yaw",
                )?,
                pose: item["pose"].as_str().ok_or("fish pose")?.to_owned(),
                rest: checked_f32(
                    item["rest_height_meters"].as_f64().ok_or("fish rest")?,
                    "fish rest",
                )?,
                geometry: item["geometry"].clone(),
            })
        })
        .collect::<Result<Vec<_>, Box<dyn Error>>>()?;
    // Validate every payload before Bevy starts, so an invalid export is a
    // descriptive launch error rather than a panic in the Startup schedule.
    for item in &fish {
        let _ = fish_mesh(&root, item)?;
    }
    Ok(Some(FishStandContent { root, fish, source }))
}

fn floats(bytes: &[u8], width: usize) -> Result<Vec<Vec<f32>>, Box<dyn Error>> {
    if !bytes.len().is_multiple_of(width * 4) {
        return Err("fish float payload shape".into());
    }
    Ok(bytes
        .chunks_exact(width * 4)
        .map(|row| {
            row.as_chunks::<4>()
                .0
                .iter()
                .map(|v| f32::from_le_bytes(*v))
                .collect()
        })
        .collect())
}
fn fish_mesh(root: &Path, fish: &Fish) -> Result<Mesh, Box<dyn Error>> {
    let attr = |name: &str| -> Result<serde_json::Value, Box<dyn Error>> {
        Ok(fish.geometry[name].clone())
    };
    let read = |entry: &serde_json::Value| -> Result<Vec<u8>, Box<dyn Error>> {
        let file = entry["file"].as_str().ok_or("fish payload file")?;
        let bytes = fs::read(root.join(&fish.species).join(file))?;
        let expected_bytes = entry["bytes"].as_u64().ok_or("fish bytes")?;
        if u64::try_from(bytes.len()).map_err(|_| "fish byte length overflow")? != expected_bytes
            || bytes_sha256(&bytes).to_string() != entry["sha256"].as_str().ok_or("fish hash")?
        {
            return Err("fish payload hash".into());
        }
        Ok(bytes)
    };
    let pos = floats(&read(&attr("position")?)?, 3)?;
    let norm = floats(&read(&attr("normal")?)?, 3)?;
    let index_entry = attr("indices")?;
    let index_bytes = read(&index_entry)?;
    let count = index_entry["count"].as_u64().ok_or("fish index count")?;
    if !count.is_multiple_of(3)
        || u64::try_from(index_bytes.len()).map_err(|_| "fish index byte length overflow")?
            != count.checked_mul(4).ok_or("fish index count")?
    {
        return Err("fish indices must be packed u32 triangles".into());
    }
    let index = index_bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|x| u32::from_le_bytes(*x))
        .collect::<Vec<_>>();
    if pos.len() != norm.len()
        || index
            .iter()
            .any(|i| usize::try_from(*i).map_or(true, |index| index >= pos.len()))
    {
        return Err("fish geometry bounds".into());
    }
    Ok(Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_POSITION,
        pos.into_iter()
            .map(|v| [v[0], v[1], v[2]])
            .collect::<Vec<_>>(),
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_NORMAL,
        norm.into_iter()
            .map(|v| [v[0], v[1], v[2]])
            .collect::<Vec<_>>(),
    )
    .with_inserted_indices(Indices::U32(index)))
}

fn color(species: &str) -> Color {
    match species {
        "redSnapper" => Color::srgb(0.72, 0.16, 0.12),
        "yellowtail" => Color::srgb(0.76, 0.68, 0.18),
        "grunt" => Color::srgb(0.58, 0.62, 0.55),
        "mullet" => Color::srgb(0.46, 0.54, 0.55),
        _ => Color::srgb(0.56, 0.58, 0.55),
    }
}

fn fish_pose(name: &str) -> Quat {
    let basis = match name {
        // FishProps.js POSE.side: model +x -> up, +y -> +z, +z (nose) -> +x.
        "side" => Mat3::from_cols(Vec3::Y, Vec3::Z, Vec3::X),
        // FishProps.js POSE.sideFlip: model +x -> down, +y -> -z, +z -> +x.
        "sideFlip" => Mat3::from_cols(-Vec3::Y, -Vec3::Z, Vec3::X),
        _ => Mat3::IDENTITY,
    };
    Quat::from_mat3(&basis)
}

// Bevy keeps these asset collections separate because each has a distinct
// lifetime and staging contract for the authored stall.
#[allow(clippy::too_many_arguments)]
pub fn setup(
    mut commands: Commands,
    probe: Res<Probe>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<FishStandMaterial>>,
    mut standard_materials: ResMut<Assets<StandardMaterial>>,
    mut animation_graphs: ResMut<Assets<AnimationGraph>>,
    asset_server: Res<AssetServer>,
) {
    let Some(prepared) = probe.fish_stand.as_ref() else {
        return;
    };
    let pose = placement_transform(&probe, prepared).expect("validated canonical stand placement");
    let y = pose.translation.y;
    let resident = prepared.clone_for_probe().stage(&mut meshes, &mut images);
    let lighting = probe
        .village
        .as_ref()
        .expect("FishStand lighting validated before scene setup")
        .lighting()
        .clone();
    let material = FishStandMaterial::from_resident(&resident, lighting)
        .expect("validated FishStand texture handles");
    let entity = commands
        .spawn((
            Name::new("FishStandStall source mesh (array-material preview)"),
            Mesh3d(resident.mesh.clone()),
            MeshMaterial3d(materials.add(material)),
            pose,
            NoFrustumCulling,
        ))
        .id();
    commands.insert_resource(ResidentStall { entity, resident });
    let Some(content) = probe.fish_stand_content.as_ref() else {
        return;
    };
    let stand = commands
        .spawn((
            Name::new("Joe · Fish buyer (Rocketbox MIT)"),
            Transform::from_xyz(49.9, y, -74.6).with_rotation(Quat::from_rotation_y(1.45)),
            Visibility::default(),
        ))
        .id();
    let (graph, node) = AnimationGraph::from_clip(
        asset_server.load(GltfAssetLabel::Animation(5).from_asset("joe.glb")),
    );
    let joe = commands
        .spawn((
            WorldAssetRoot(asset_server.load(GltfAssetLabel::Scene(0).from_asset("joe.glb"))),
            Transform::from_xyz(0.2, 0.06, 0.12),
            JoeIdle {
                graph: animation_graphs.add(graph),
                node,
            },
        ))
        .observe(start_joe_idle)
        .id();
    commands.entity(stand).add_child(joe);
    if content.source.is_some() {
        // The source adapter stages all three LODs and their dedicated material.
        return;
    }
    for (index, fish) in content.fish.iter().enumerate() {
        let mesh = fish_mesh(&content.root, fish).expect("validated FishGeometry payload");
        let pose =
            Quat::from_rotation_y(1.45) * Quat::from_rotation_y(fish.yaw) * fish_pose(&fish.pose);
        let offset = Quat::from_rotation_y(1.45) * Vec3::new(fish.x, 0.0, fish.z);
        let display_index =
            f32::from(u16::try_from(index).expect("fish descriptor count was validated as five"));
        commands.spawn((
            Name::new(format!("FishStand ice display · {}", fish.species)),
            Mesh3d(meshes.add(mesh)),
            MeshMaterial3d(standard_materials.add(StandardMaterial {
                base_color: color(&fish.species),
                metallic: 0.15,
                perceptual_roughness: 0.32,
                ..default()
            })),
            Transform::from_xyz(
                49.9 + offset.x,
                y + 1.27 + fish.rest + 0.006 * display_index,
                -74.6 + offset.z,
            )
            .with_rotation(pose)
            .with_scale(Vec3::splat(fish.length)),
            NoFrustumCulling,
        ));
    }
}

fn start_joe_idle(
    ready: On<WorldInstanceReady>,
    mut commands: Commands,
    roots: Query<&JoeIdle>,
    children: Query<&Children>,
    mut players: Query<&mut AnimationPlayer>,
) {
    let Ok(idle) = roots.get(ready.entity) else {
        return;
    };
    for child in children.iter_descendants(ready.entity) {
        if let Ok(mut player) = players.get_mut(child) {
            player.play(idle.node).repeat().seek_to(1.0 / 30.0);
            commands
                .entity(child)
                .insert(AnimationGraphHandle(idle.graph.clone()));
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn close(left: Vec3, right: Vec3) {
        assert!((left - right).length() < 1.0e-5, "{left:?} != {right:?}");
    }

    #[test]
    fn side_poses_match_fish_props_basis_columns() {
        close(fish_pose("side") * Vec3::Z, Vec3::X);
        close(fish_pose("side") * Vec3::Y, Vec3::Z);
        close(fish_pose("sideFlip") * Vec3::Z, Vec3::X);
        close(fish_pose("sideFlip") * Vec3::Y, -Vec3::Z);
    }
}
