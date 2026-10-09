//! Connect the full source lighting model to the existing common scene receivers.
use super::full_shader::{self, SceneHooks};

fn take_function(source: &mut String, name: &str) -> Result<String, String> {
    let marker = format!("fn {name}(");
    if source.matches(&marker).count() != 1 {
        return Err(format!("Complete stall requires one scene helper {name}"));
    }
    let start = source.find(&marker).ok_or("Scene helper missing")?;
    let opening = source[start..]
        .find('{')
        .map(|offset| start + offset)
        .ok_or("Scene helper body missing")?;
    let mut depth = 0;
    for (offset, byte) in source.as_bytes()[opening..].iter().enumerate() {
        match byte {
            b'{' => depth += 1,
            b'}' => {
                depth -= 1;
                if depth == 0 {
                    let end = opening + offset + 1;
                    let function = source[start..end].to_owned();
                    source.replace_range(start..end, "");
                    return Ok(function);
                }
            }
            _ => {}
        }
    }
    Err(format!("Incomplete scene helper {name}"))
}

/// Assemble full Bevy shading from the caller's verified raw village lighting.
/// Uses the same input contract as the dedicated fish material adapter.
///
/// # Errors
/// Rejects missing/ambiguous scene seams and receiver binding collisions. The
/// resulting source still requires real GPU pipeline compilation before use.
pub fn shader_source(lighting_source: &str) -> Result<String, String> {
    if lighting_source.len() > 1_000_000
        || lighting_source.matches("struct LightingSurface {").count() != 1
    {
        return Err("Complete stall requires a verified common lighting closure".into());
    }
    let start = lighting_source
        .find("struct LightingSurface {")
        .ok_or("Scene lighting absent")?;
    let mut resources = crate::tidewater_material::scene_shader_source(&lighting_source[start..])?;
    // The complete source library supplies these exact physical BRDF functions.
    for name in [
        "F_Schlick",
        "V_GGX_SmithCorrelated",
        "D_GGX",
        "BRDF_GGX",
        "DFGApprox",
        "multiscatter",
    ] {
        take_function(&mut resources, name)?;
    }
    take_function(&mut resources, "sourceSceneLight")?;
    let diffuse = take_function(&mut resources, "hookEnvDiffuse")?;
    let sun_shadow = take_function(&mut resources, "sunShadow")?;
    let contact_shadow = full_shader::live_contact_receiver()?;
    let hooks = SceneHooks {
        direct_modulation: "fn hookDirectModulation(P:vec3f,N:vec3f)->vec3f { return vec3f(terrainSunShadowAt(P)*cloudsShadow(P.xz))*villageUnderwaterDirect(P); }",
        shadow_position: "fn hookShadowPosition(P:vec3f,N:vec3f,pixel:vec2f)->vec3f { return underwaterShadowPosition(P,pixel); }",
        sun_shadow: &sun_shadow,
        contact_shadow: &contact_shadow,
        local_lights: r"
fn hookLocalLights(s:Surface,P:vec3f,N:vec3f,V:vec3f,acc:ptr<function,LightAccum>) {
 let sharedSurface=LightingSurface(s.albedo,s.normal,s.roughness,s.metalness,s.specularIntensity,s.translucency,s.ao,s.envIntensity,s.emissive);
 // This common helper returns diffuse+specular. The full model sums both direct
 // accumulators before clearcoat, so this preserves its total local contribution.
 (*acc).directDiffuse+=villageLocalLights(sharedSurface,P,N,V);
}",
        environment_diffuse: &diffuse,
        environment_specular: "fn hookEnvSpecular(R:vec3f,rough:f32)->vec3f { return textureSampleLevel(envCube,envSampler,R,rough*5.0).rgb; }",
        bounce: r"
fn hookBounce(P:vec3f,N:vec3f)->vec3f {
 // Extract irradiance/PI from the common helper: unit albedo, no metal, unit AO.
 // shadeSurface multiplies diffuse color and AO exactly once afterwards.
 let irradianceOnly=LightingSurface(vec3f(1.0),N,1.0,0.0,1.0,vec3f(0.0),1.0,1.0,vec3f(0.0));
 return sourceGroundBounce(irradianceOnly,P,N);
}",
        ambient_modulation: "fn hookAmbientModulation(P:vec3f,N:vec3f)->vec3f { return villageUnderwaterAmbient(P); }",
    };
    full_shader::bevy_live_shader_source(&resources, &hooks)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn full_scene_refuses_unknown_or_already_adapted_lighting() {
        assert!(shader_source("").is_err());
        assert!(shader_source("struct LightingSurface { } struct LightingSurface { }").is_err());
        let mut source = "fn f(x:f32)->f32 { if(x>0.0){return x;} return 0.0; }".to_owned();
        let function = take_function(&mut source, "f").unwrap();
        assert!(source.is_empty());
        assert!(function.contains("return 0.0;"));
        assert!(take_function(&mut function.repeat(2), "f").is_err());
    }
}
