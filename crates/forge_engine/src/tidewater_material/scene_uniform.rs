//! Replace acquired scene-light literals with one aligned material uniform.
//! The returned seed preserves the acquired light before the scene publisher runs.

#[derive(Debug, Clone, PartialEq)]
pub struct Seed {
    pub direction: [f32; 3],
    pub color: [f32; 3],
    pub sh: [[f32; 3]; 9],
}
pub struct Prepared {
    pub shader: String,
    pub seed: Seed,
}

fn vector(source: &str) -> Result<(&str, [f32; 3]), String> {
    let start = source.find("vec3f(").ok_or("missing scene vec3 literal")?;
    let end = source[start..].find(')').ok_or("unterminated scene vec3")? + start + 1;
    let literal = &source[start..end];
    let values = literal[6..literal.len() - 1]
        .split(',')
        .map(|v| {
            v.trim()
                .parse::<f32>()
                .map_err(|_| "invalid scene vec3 scalar".to_owned())
        })
        .collect::<Result<Vec<_>, _>>()?;
    let values: [f32; 3] = values
        .try_into()
        .map_err(|_| "scene vector must have three scalars")?;
    if values.iter().any(|v| !v.is_finite()) {
        return Err("non-finite scene literal".into());
    }
    Ok((literal, values))
}

/// Accept only the acquired terrain/plant lighting interfaces, never guess a
/// different shader's meaning. Binding 54 must be supplied by its material.
pub fn prepare(source: &str) -> Result<Prepared, String> {
    if source.len() > 1_000_000
        || source.contains("@binding(54)")
        || source.contains("struct ForgeSceneLighting")
    {
        return Err("scene-light interface size or binding collision".into());
    }
    let direction_anchor = "frame.sunDir=vec3f(";
    let direction_start = source
        .find(direction_anchor)
        .ok_or("scene direction assignment absent")?;
    let (direction_literal, direction) = vector(&source[direction_start..])?;
    let plants = source.contains("frame.sunDir,vec3f(");
    let color_anchor = if plants {
        "frame.sunDir,vec3f("
    } else {
        "s.translucency,N,V,vec3f("
    };
    let color_start = source
        .find(color_anchor)
        .ok_or("direct scene-light call absent")?;
    let (color_literal, color) = if plants {
        vector(&source[color_start..])?
    } else {
        let (literal, direct_direction) = vector(&source[color_start..])?;
        if direct_direction != direction {
            return Err("direct light direction differs from frame".into());
        }
        vector(
            &source[color_start + source[color_start..].find(literal).unwrap() + literal.len()..],
        )?
    };
    if color.iter().any(|v| *v < 0.0)
        || (direction.iter().map(|v| v * v).sum::<f32>() - 1.0).abs() > 0.001
    {
        return Err("invalid acquired scene light".into());
    }
    let sh_anchor = "const envSH=array<vec3f,9>(";
    if source.matches(sh_anchor).count() != 1 {
        return Err("one acquired SH array required".into());
    }
    let sh_start = source.find(sh_anchor).unwrap();
    let sh_end = source[sh_start..]
        .find(";")
        .ok_or("unterminated SH array")?
        + sh_start
        + 1;
    let mut remaining = &source[sh_start + sh_anchor.len()..sh_end];
    let mut coefficients = Vec::new();
    while remaining.contains("vec3f(") {
        let (literal, value) = vector(remaining)?;
        let offset = remaining.find(literal).unwrap() + literal.len();
        coefficients.push(value);
        remaining = &remaining[offset..];
    }
    let sh = coefficients
        .try_into()
        .map_err(|_| "nine acquired SH coefficients required")?;
    let mut shader = source.to_owned();
    shader.replace_range(sh_start..sh_end, "");
    shader = shader.replace(
        &format!("frame.sunDir={direction_literal}"),
        "frame.sunDir=sceneLighting.sunDirection.xyz",
    );
    let old_call = if plants {
        format!("frame.sunDir,{color_literal}")
    } else {
        format!("s.translucency,N,V,{direction_literal},{color_literal}")
    };
    let new_call = if plants {
        "frame.sunDir,sceneLighting.sunColor.xyz"
    } else {
        "s.translucency,N,V,sceneLighting.sunDirection.xyz,sceneLighting.sunColor.xyz"
    };
    if shader.matches(&old_call).count() != source.matches(color_anchor).count() {
        return Err("scene-light calls have inconsistent acquired literals".into());
    }
    shader = shader
        .replace(&old_call, new_call)
        .replace("envSH[", "sceneLighting.environmentSH[");
    shader.push_str("\nstruct ForgeSceneLighting { sunDirection:vec4f, sunColor:vec4f, environmentSH:array<vec4f,9> };\n@group(#{MATERIAL_BIND_GROUP}) @binding(54) var<storage,read> sceneLighting:ForgeSceneLighting;\n");
    Ok(Prepared {
        shader,
        seed: Seed {
            direction,
            color,
            sh,
        },
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    fn source() -> String {
        format!(
            "fn setup() {{ frame.sunDir=vec3f(0,1,0); }}\nfn shade() {{ direct(frame.sunDir,vec3f(2,3,4)*terrainSunShadowAt(P)*cloudsShadow(P.xz)); }}\nconst envSH=array<vec3f,9>({});\nfn indirect() {{ return envSH[ 0 ].rgb; }}",
            vec!["vec3f(0.1,0.2,0.3)"; 9].join(",")
        )
    }
    #[test]
    fn replaces_light_and_sh_while_preserving_shadow_factors() {
        let prepared = prepare(&source()).unwrap();
        assert_eq!(prepared.seed.color, [2.0, 3.0, 4.0]);
        assert_eq!(prepared.seed.sh, [[0.1, 0.2, 0.3]; 9]);
        assert!(
            prepared
                .shader
                .contains("sceneLighting.sunColor.xyz*terrainSunShadowAt(P)*cloudsShadow(P.xz)")
        );
        assert!(
            prepared
                .shader
                .contains("sceneLighting.environmentSH[ 0 ].rgb")
        );
        assert!(!prepared.shader.contains("const envSH="));
        let unrelated = source() + "\nlet up=vec3f(0,1,0); let tint=vec3f(2,3,4);";
        let prepared = prepare(&unrelated).unwrap();
        assert!(
            prepared
                .shader
                .contains("let up=vec3f(0,1,0); let tint=vec3f(2,3,4);")
        );
    }
    #[test]
    fn refuses_collisions_incomplete_sh_and_invalid_color() {
        assert!(prepare(&(source() + "\n@binding(54)")).is_err());
        assert!(prepare(&source().replace("vec3f(2,3,4)", "vec3f(-2,3,4)")).is_err());
        assert!(prepare(&source().replacen("vec3f(0.1,0.2,0.3),", "", 1)).is_err());
    }
}
