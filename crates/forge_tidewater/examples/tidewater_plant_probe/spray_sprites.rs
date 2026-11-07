//! Original spray sprite shading fed directly by native simulation buffers.
use bevy::{camera::visibility::NoFrustumCulling, light::NotShadowCaster, prelude::*};
pub use forge_engine::source_spray_render::SprayMaterial;
use forge_engine::source_spray_render::{self, SPRITE_SHADER};
use std::{collections::BTreeMap, error::Error};
#[derive(Resource)]
struct Inputs {
    frame: [UVec4; 45],
    material: [UVec4; 3],
    textures: TextureInputs,
}
enum TextureInputs {
    Shared {
        puff: Handle<Image>,
        dots: Handle<Image>,
    },
    Captured {
        puff: Vec<u8>,
        dots: Vec<u8>,
    },
}

/// Use the generator only for the exact captured source chains and base bytes.
pub(crate) fn is_common_capture(puff: &[u8], dots: &[u8]) -> bool {
    forge_engine::source_spray_textures::captured_chains_match(
        puff,
        dots,
        [
            "7e2646287b29d176f7048d981008e6c6139ba6a1781b631f20b15e5d5037d868"
                .parse()
                .expect("fixed puff capture hash"),
            "2da7417941e41c1108268f96af747d9d8e9832d983832ab126da6d0d42d341ed"
                .parse()
                .expect("fixed dots capture hash"),
        ],
    )
}
fn words<const N: usize>(v: &[u8]) -> Result<[UVec4; N], Box<dyn Error>> {
    if v.len() != N * 16 {
        return Err("spray uniform size".into());
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
    let get = |n: &str| files.get(n).ok_or("missing sprite input");
    let source = std::str::from_utf8(get("spray-sprites.wgsl")?)?;
    let stations = app.world().resource::<super::spray::Targets>().stations;
    let selected = source_spray_render::select_common_shader(source);
    if !std::ptr::eq(selected.as_ptr(), source.as_ptr()) {
        eprintln!("Tidewater spray shader: shared engine source");
    }
    let shader = source_spray_render::prepare_shadow_shader(selected, 577, stations)?;
    app.world_mut().resource_mut::<Assets<Shader>>().insert(
        SPRITE_SHADER.id(),
        Shader::from_wgsl(shader, "spray-sprites.wgsl"),
    )?;
    let mips = |name: &str, side: usize, count: usize| -> Result<Vec<u8>, Box<dyn Error>> {
        let mut out = Vec::new();
        for i in 0..count {
            let data = get(&format!("sprite-{name}-m{i}.rgba8.bin"))?;
            if data.len() != (side >> i).pow(2) * 4 {
                return Err("sprite texture mip size".into());
            }
            out.extend_from_slice(data);
        }
        Ok(out)
    };
    let puff = mips("sprayPuff", 64, 7)?;
    let dots = mips("sprayDots", 128, 8)?;
    let textures = if is_common_capture(&puff, &dots) {
        eprintln!("Tidewater spray textures: shared procedural GPU generation");
        forge_engine::source_spray_mips::register(app)?;
        let shared = forge_engine::source_spray_mips::SpriteTextures::stage(
            &mut app.world_mut().resource_mut::<Assets<Image>>(),
        )?;
        let textures = TextureInputs::Shared {
            puff: shared.puff.clone(),
            dots: shared.dots.clone(),
        };
        app.insert_resource(shared);
        textures
    } else {
        eprintln!("Tidewater spray textures: captured variant");
        TextureInputs::Captured { puff, dots }
    };
    app.insert_resource(Inputs {
        frame: words(get("water-frame.bin")?)?,
        material: words(get("sprite-material-params.bin")?)?,
        textures,
    });
    app.add_plugins(MaterialPlugin::<SprayMaterial>::default())
        .add_systems(Startup, setup);
    Ok(())
}
#[allow(clippy::needless_pass_by_value, clippy::too_many_arguments)]
fn setup(
    mut commands: Commands,
    inputs: Res<Inputs>,
    clouds: Option<Res<super::sky::CloudInputs>>,
    spray: Res<super::spray::Targets>,
    depth: Res<super::water_scene::SceneDepth>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<SprayMaterial>>,
) {
    let mesh = source_spray_render::sprite_mesh(source_spray_render::MAX_SPRITES)
        .expect("fixed source sprite capacity");
    let mut texture = |side, mips, data: &Vec<u8>| {
        images.add(
            source_spray_render::sprite_texture(side, mips, data.clone())
                .expect("validated source sprite texture"),
        )
    };
    let (puff, dots) = match &inputs.textures {
        TextureInputs::Shared { puff, dots } => (puff.clone(), dots.clone()),
        TextureInputs::Captured { puff, dots } => (texture(64, 7, puff), texture(128, 8, dots)),
    };
    let material = materials.add(SprayMaterial {
        clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
        cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
        frame: inputs.frame,
        material: inputs.material,
        pos: spray.buffers[1].clone(),
        vel: spray.buffers[2].clone(),
        info: spray.buffers[3].clone(),
        crest: spray.buffers[0].clone(),
        ring_head: spray.buffers[4].clone(),
        shadow_cutoff: spray.buffers[4].clone(),
        depth: depth.0.clone(),
        puff,
        dots,
    });
    commands.spawn((
        Mesh3d(meshes.add(mesh)),
        MeshMaterial3d(material),
        Transform::default(),
        NoFrustumCulling,
        NotShadowCaster,
    ));
}
