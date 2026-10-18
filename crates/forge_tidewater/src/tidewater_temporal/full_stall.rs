//! Exact auxiliary coverage and previous rigid transform for the complete stall.
use super::{closing, fragments, replace, require};

pub(super) fn derive(vertex: &str, fragment: &str) -> Result<(String, String), &'static str> {
    if fragment.contains("discard") {
        return Err("complete stall coverage is no longer opaque");
    }
    require(
        vertex,
        "out.world=(mesh_functions::get_world_from_local(input.instance)*vec4f(input.position,1)).xyz;",
        1,
    )?;
    let mut vertex = vertex.to_owned();
    let mut fragment = fragment.to_owned();
    for source in [&mut vertex, &mut fragment] {
        require(source, "struct StallVertexOutput {", 1)?;
        let start = source
            .find("struct StallVertexOutput {")
            .ok_or("complete stall output")?;
        let end = closing(source, start)?;
        if source[start..end].contains("@location(6)") {
            return Err("complete stall varying location 6 occupied");
        }
        source.insert_str(end, "\n @location(6) tidewater_previous_P:vec3f,\n");
    }
    vertex = replace(
        &vertex,
        "out.uv=input.uv;out.color=input.color;out.layer=input.layer;out.uv2=input.uv2;",
        "out.uv=input.uv;out.color=input.color;out.layer=input.layer;out.uv2=input.uv2;\n out.tidewater_previous_P=(mesh_functions::get_previous_world_from_local(input.instance)*vec4f(input.position,1)).xyz;",
        1,
    )?;
    fragment = fragments(
        &fragment,
        "fn fragment(input:StallVertexOutput)->@location(0) vec4f {",
        1,
        |body| {
            require(
                body,
                "return vec4f(shadeSurface(surface,input.world,V,input.position.xy,light),surface.alpha);",
                1,
            )?;
            // Source stall is submitted in the opaque phase. Emissive/material
            // alpha must not change that phase's auxiliary history mask.
            Ok("\n return tidewater_auxiliary_world(input.world,input.tidewater_previous_P,1.0,1.0,vec4f(0.0));\n".into())
        },
    )?;
    Ok((vertex, fragment))
}

#[cfg(test)]
mod tests {
    use super::*;
    const ADAPTER: &str = include_str!("../tidewater_stall/full_adapter.wgsl");

    #[test]
    fn complete_stall_uses_previous_rigid_model_and_rejects_changed_coverage() {
        let (vertex, fragment) = derive(ADAPTER, ADAPTER).unwrap();
        assert!(vertex.contains("get_previous_world_from_local(input.instance)"));
        assert!(fragment.contains("input.world,input.tidewater_previous_P,1.0,1.0"));
        assert!(!fragment.contains("return vec4f(shadeSurface("));
        assert!(derive(ADAPTER, &format!("{ADAPTER}\nfn changed() {{ discard; }}")).is_err());
        assert!(
            derive(
                &ADAPTER.replace("@location(5) uv2", "@location(6) uv2"),
                ADAPTER
            )
            .is_err()
        );
        assert!(derive(ADAPTER, &ADAPTER.replace("surface.alpha);", "1.0);")).is_err());
    }
}
