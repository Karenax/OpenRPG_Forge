// SPDX-License-Identifier: MIT
// Static world reprojection follows Tidewater MeshShader/PebbleField at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Strict Pebble auxiliary coverage; preserves the occupied location-5 source varying.
use super::{fragments, lighting_imports, require, through};

pub(super) fn derive(vertex: &str, fragment: &str) -> Result<(String, String), &'static str> {
    for source in [vertex, fragment] {
        require(source, "@location(5) vPebble:vec4f,", 1)?;
        require(
            source,
            "@binding(106) var<storage,read> pebbleCells:PebbleCells;",
            1,
        )?;
        if source.contains("tidewater_temporal_packet")
            || source.contains("tidewater_previous_P")
            || source.contains("var<uniform> refractionCapture:")
        {
            return Err("already derived or incompatible Pebble auxiliary source");
        }
    }
    require(vertex, "@vertex", 1)?;
    require(vertex, "fn vertex(input:Input)->Output {", 1)?;
    require(vertex, "sourceVertex(&v,&o);", 1)?;
    require(
        vertex,
        "v.aSlot=input.vdata; v.iCell=pebbleCells.cells[mesh_functions::get_tag(input.instance)];",
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
                return Err("unrecognized Pebble post-surface coverage");
            }
            // Source staticVelocity reprojects the same current world position.
            // Camera-following cell indices are transient; they are not previous mesh models.
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
