// SPDX-License-Identifier: MIT
// Derived from Bevy 0.19.1 pbr_prepass.wgsl, Bevy Contributors.
// Source normals share exactly the forward material equation.
/*
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#import bevy_pbr::{prepass_io, pbr_prepass_functions, pbr_functions}
#import "embedded://forge_engine/shaders/source_fragment_common.wgsl"::source_world_normal

#ifdef PREPASS_FRAGMENT
@fragment
fn fragment(in:prepass_io::VertexOutput, @builtin(front_facing) is_front:bool)->prepass_io::FragmentOutput {
#ifdef VISIBILITY_RANGE_DITHER
    pbr_functions::visibility_range_dither(in.position,in.visibility_range_dither);
#endif
    pbr_prepass_functions::prepass_alpha_discard(in);
    var out:prepass_io::FragmentOutput;
#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
    out.frag_depth=in.unclipped_depth;
#endif
#ifdef NORMAL_PREPASS
#ifdef VERTEX_NORMALS
    var normal=in.world_normal;
#ifdef VERTEX_UVS_A
#ifdef VERTEX_TANGENTS
    normal=source_world_normal(in.uv,in.world_normal,in.world_tangent,is_front);
#endif
#endif
    out.normal=vec4<f32>(normal*0.5+vec3<f32>(0.5),1.0);
#endif
#endif
#ifdef MOTION_VECTOR_PREPASS
    out.motion_vector=pbr_prepass_functions::calculate_motion_vector(in.world_position,in.previous_world_position);
#endif
    return out;
}
#else
@fragment
fn fragment(in:prepass_io::VertexOutput) {
    pbr_prepass_functions::prepass_alpha_discard(in);
}
#endif
