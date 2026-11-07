//! Original transparent breaking lip, driven by the shared native crest buffer.
use bevy::{camera::visibility::NoFrustumCulling, light::NotShadowCaster, prelude::*};
use forge_engine::source_breaker_render::{self, LIP_SHADER, LipMaterial, lace_image, lip_mesh};
use std::{collections::BTreeMap, error::Error};
#[derive(Resource)]
struct Inputs {
    frame: [UVec4; 45],
    sky: [UVec4; 2],
    material: [UVec4; 3],
    lace: Image,
}
fn words<const N: usize>(v: &[u8]) -> Result<[UVec4; N], Box<dyn Error>> {
    if v.len() != N * 16 {
        return Err("lip uniform size".into());
    }
    Ok(std::array::from_fn(|i| {
        UVec4::from_array(std::array::from_fn(|j| {
            u32::from_le_bytes(
                v[i * 16 + j * 4..i * 16 + j * 4 + 4]
                    .try_into()
                    .expect("checked"),
            )
        }))
    }))
}
pub fn install(app: &mut App, files: &BTreeMap<String, Vec<u8>>) -> Result<(), Box<dyn Error>> {
    if !files.contains_key("breaker-lip.wgsl") {
        return Ok(());
    }
    let get = |n: &str| files.get(n).ok_or("missing lip input");
    let source = std::str::from_utf8(get("breaker-lip.wgsl")?)?;
    let selected = source_breaker_render::select_common_shader(source);
    if !std::ptr::eq(selected.as_ptr(), source.as_ptr()) {
        eprintln!("Tidewater breaker lip shader: shared engine source");
    }
    app.world_mut().resource_mut::<Assets<Shader>>().insert(
        LIP_SHADER.id(),
        Shader::from_wgsl(selected.to_owned(), "breaker-lip.wgsl"),
    )?;
    let mut lace = Vec::new();
    for mip in 0..10 {
        let data = get(&format!("lip-lace-m{mip}.rgba8.bin"))?;
        if data.len() != (512_usize >> mip).pow(2) * 4 {
            return Err("lip lace mip size".into());
        }
        lace.extend_from_slice(data);
    }
    app.insert_resource(Inputs {
        frame: words(get("water-frame.bin")?)?,
        sky: words(get("uniform-skyParams.bin")?)?,
        material: words(get("lip-material-params.bin")?)?,
        lace: lace_image(512, 10, lace)?,
    });
    app.add_plugins(MaterialPlugin::<LipMaterial>::default())
        .add_systems(Startup, setup);
    Ok(())
}
#[allow(clippy::needless_pass_by_value, clippy::too_many_arguments)]
fn setup(
    mut commands: Commands,
    inputs: Res<Inputs>,
    crests: Res<super::breakers::Targets>,
    atmosphere: Res<super::atmosphere::Targets>,
    clouds: Option<Res<super::sky::CloudInputs>>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<LipMaterial>>,
) {
    let Some(mesh) = lip_mesh(crests.stations) else {
        return; // An authored terrain can have no continuous main-beach shoreline.
    };
    let material = materials.add(LipMaterial {
        clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
        cloud_panorama: clouds.as_ref().map(|v| v.panorama.clone()),
        cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
        frame: inputs.frame,
        crest: crests.buffers[0].clone(),
        lace: images.add(inputs.lace.clone()),
        atmosphere: atmosphere.params,
        sky: inputs.sky,
        transmittance: atmosphere.images[0].clone(),
        sky_lut: atmosphere.images[2].clone(),
        material: inputs.material,
    });
    commands.spawn((
        Mesh3d(meshes.add(mesh)),
        MeshMaterial3d(material),
        Transform::default(),
        NoFrustumCulling,
        NotShadowCaster,
    ));
}
