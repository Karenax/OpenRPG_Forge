//! Diagnostic output over the actual derived world shader; canonical uniforms stay intact.
use super::Probe;
use bevy::{prelude::*, shader::Source};
use forge_core::{ContentHash, bytes_sha256};
use std::io::Write;

const ANCHOR: &str =
    "    // Diagnostic coverage pass: preserves the source's alpha mask and vertex";
const BEGIN: &str = "// FORGE_PLANT_DIAGNOSTIC_BEGIN\n";
const END: &str = "// FORGE_PLANT_DIAGNOSTIC_END\n";

#[derive(Clone, Copy)]
enum Mode {
    Coverage,
    Albedo,
    Normal,
    Direct,
    Environment,
    Local,
    DomainFlags,
}

#[derive(Resource)]
struct State {
    mode: Mode,
    original: Option<ContentHash>,
    instrumented: Option<ContentHash>,
}

pub(super) fn install(app: &mut App, probe: &Probe) {
    let Some(value) = std::env::var_os("FORGE_SOURCE_FINAL_PLANT_PROBE") else {
        return;
    };
    let mode = match value.to_str() {
        Some("coverage") => Mode::Coverage,
        Some("albedo") => Mode::Albedo,
        Some("normal") => Mode::Normal,
        Some("direct") => Mode::Direct,
        Some("environment") => Mode::Environment,
        Some("local") => Mode::Local,
        Some("domain-flags") => Mode::DomainFlags,
        _ => {
            error!("Unsupported plant diagnostic mode");
            app.add_systems(Startup, |mut exit: MessageWriter<bevy::app::AppExit>| {
                exit.write(bevy::app::AppExit::error());
            });
            return;
        }
    };
    probe
        .metadata
        .lock()
        .expect("View probe metadata")
        .plant_probe_mode = Some(value.to_string_lossy().into_owned());
    app.insert_resource(State {
        mode,
        original: None,
        instrumented: None,
    })
    .add_systems(Last, patch);
}

fn instrument(source: &str, mode: Mode) -> Result<String, String> {
    if source.matches(ANCHOR).count() != 4
        || source.matches("fn forgeSourceFragment(").count() != 4
        || source.contains(BEGIN)
    {
        return Err("Plant diagnostic source seam drift".into());
    }
    let expression = match mode {
        Mode::Coverage => "vec3f(1.0,0.0,1.0)".to_owned(),
        Mode::Albedo => "surface.albedo".to_owned(),
        Mode::Normal => "surface.normal*0.5+vec3f(0.5)".to_owned(),
        Mode::DomainFlags => r"
#ifdef PLANT_KIND_0
vec3f(input.vs.vMat.x,select(select(0.0,2.0,input.vs.vIDat.z<0.0),1.0,input.vs.vIDat.z==0.0),select(0.0,1.0,input.vs.vTrunkY<0.0))
#else
#ifdef PLANT_KIND_1
vec3f(100.0+input.vs.vMat.x,select(0.0,1.0,dot(input.vs.normal,input.vs.normal)==0.0),select(0.0,1.0,input.vs.vMat.y<0.0))
#else
vec3f(200.0,select(0.0,1.0,input.vs.vIDat.y==0.0),select(0.0,1.0,input.vs.vImp.w==0.0))
#endif
#endif
".to_owned(),
        Mode::Environment => "sourceEnvironmentLight(surface,normalize(surface.normal),input.V)".to_owned(),
        Mode::Local => "sourceLocalLights(surface,input.P,normalize(surface.normal),input.V,0.0,vec3f(0.0),vec3f(0.0))".to_owned(),
        Mode::Direct => {
            let start = "return vec4f(";
            let calls: Vec<_> = source.match_indices("return vec4f(sourceDirectLight(").map(|(offset, _)| {
                let rest = &source[offset + start.len()..];
                rest.split_once("+sourceEnvironmentLight").map(|(call, _)| call.trim())
            }).collect();
            if calls.len() != 4 || calls[0].is_none() || calls.iter().any(|call| call != &calls[0]) {
                return Err("Plant diagnostic direct-light calls differ".into());
            }
            calls[0].ok_or("Plant direct-light call absent")?.to_owned()
        }
    };
    // This condition uses the original, checked zero coverage uniform. It keeps
    // valid WGSL control flow and changes neither alpha evaluation nor geometry.
    let insertion = format!(
        "{BEGIN}#ifndef PLANT_KIND_3\n    if probe.lodEndFrame.w < 0.5 {{ return vec4f({expression},1.0); }}\n#endif\n{END}"
    );
    Ok(source.replace(ANCHOR, &format!("{insertion}{ANCHOR}")))
}

fn patch(
    mut shaders: ResMut<Assets<Shader>>,
    mut state: ResMut<State>,
    probe: Res<Probe>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    let result = (|| -> Result<(), String> {
        let id = crate::source_vegetation::world::SHADER.id();
        let Some(shader) = shaders.get(id) else {
            return Ok(());
        };
        let Source::Wgsl(original) = &shader.source else {
            return Err("Plant probe requires the actual WGSL shader".into());
        };
        let original_hash = bytes_sha256(original.as_bytes());
        if state.instrumented == Some(original_hash) {
            return Ok(());
        }
        if state
            .original
            .is_some_and(|expected| expected != original_hash)
        {
            return Err("Plant shader changed during attribution".into());
        }
        let derived = instrument(original, state.mode)?;
        let derived_hash = bytes_sha256(derived.as_bytes());
        let dump = probe.folder.join("plant-probe.wgsl");
        if dump.exists() {
            if std::fs::read(&dump).map_err(|e| e.to_string())? != derived.as_bytes() {
                return Err("Existing plant diagnostic dump differs".into());
            }
        } else {
            std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&dump)
                .and_then(|mut file| file.write_all(derived.as_bytes()))
                .map_err(|e| e.to_string())?;
        }
        shaders.get_mut(id).ok_or("Plant shader retired")?.source = Source::Wgsl(derived.into());
        state.original = Some(original_hash);
        state.instrumented = Some(derived_hash);
        let mut metadata = probe.metadata.lock().expect("View probe metadata");
        metadata.plant_shader_original_sha256 = Some(original_hash.to_string());
        metadata.plant_shader_diagnostic_sha256 = Some(derived_hash.to_string());
        Ok(())
    })();
    if let Err(error) = result {
        error!(%error,"Plant attribution failed");
        exit.write(bevy::app::AppExit::error());
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn source() -> String {
        format!("fn forgeSourceFragment() {{ sourceSurface(input,&surface);\n{ANCHOR}\n return vec4f(sourceDirectLight(a,b)+sourceEnvironmentLight(c),1.0); }}\n").repeat(4)
    }
    #[test]
    fn diagnostic_insertion_is_reversible_and_keeps_original_surface_and_lighting() {
        let original = source();
        for mode in [
            Mode::Coverage,
            Mode::Albedo,
            Mode::Normal,
            Mode::Direct,
            Mode::Environment,
            Mode::Local,
            Mode::DomainFlags,
        ] {
            let derived = instrument(&original, mode).unwrap();
            let mut restored = String::new();
            let mut rest = derived.as_str();
            while let Some((before, after)) = rest.split_once(BEGIN) {
                restored.push_str(before);
                rest = after.split_once(END).unwrap().1;
            }
            restored.push_str(rest);
            assert_eq!(restored, original);
            assert_eq!(derived.matches("#ifndef PLANT_KIND_3").count(), 4);
        }
        assert!(instrument(&original.replacen(ANCHOR, "", 1), Mode::Coverage).is_err());
        assert!(
            instrument(
                &original.replacen("sourceDirectLight(a,b)", "sourceDirectLight(changed)", 1),
                Mode::Direct
            )
            .is_err()
        );
    }
}
