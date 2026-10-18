// SPDX-License-Identifier: MIT
// Static reprojection follows Tidewater MeshShader/ScannedDebris at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Scanned auxiliary coverage preserves the acquired LOD dithering and instance ABI.
use super::{fragments, lighting_imports, require, through};

pub(super) fn derive(vertex: &str, fragment: &str) -> Result<(String, String), &'static str> {
    for source in [vertex, fragment] {
        require(source, "@location(4) vIData:vec4f,", 1)?;
        require(
            source,
            "@binding(106) var<storage,read> scanInstances:array<vec4f>;",
            1,
        )?;
        require(
            source,
            "if ( ! lodFadeVisible( in.pixel, iData.z, iData.w > 0.5 ) ) { discard; }",
            1,
        )?;
        if source.contains("tidewater_temporal_packet")
            || source.contains("tidewater_previous_P")
            || source.contains("var<uniform> refractionCapture:")
        {
            return Err("already derived or incompatible Scanned auxiliary source");
        }
    }
    require(vertex, "@vertex", 1)?;
    require(vertex, "fn vertex(input:Input)->Output {", 1)?;
    require(vertex, "sourceVertex(&v,&o);", 1)?;
    require(
        vertex,
        "v.iData=scanInstances[mesh_functions::get_tag(input.instance)];",
        1,
    )?;
    require(
        vertex,
        "o.P=(mesh_functions::get_world_from_local(input.instance)*vec4f(v.position,1.0)).xyz;",
        1,
    )?;
    require(fragment, "@fragment", 1)?;
    let fragment = fragments(
        fragment,
        "fn fragment(vs:Output,@builtin(front_facing) front:bool)->@location(0) vec4f {",
        1,
        |body| {
            require(body, "sourceSurface(input,&s);", 1)?;
            let prefix = through(body, "if s.alpha < mat.alphaTest { discard; }")?;
            if body[prefix.len()..].contains("discard") {
                return Err("unrecognized Scanned post-surface coverage");
            }
            // ScannedDebris explicitly uses staticVelocity. Its camera-selected
            // LOD records do not represent a previous authored mesh model.
            Ok(format!(
                "{prefix}\nreturn tidewater_auxiliary_world(vs.P,vs.P,1.0,1.0,vec4f(0.0));\n"
            ))
        },
    )?;
    Ok((
        lighting_imports(vertex, 1)?,
        lighting_imports(&fragment, 1)?,
    ))
}
