#import bevy_pbr::{
    pbr_functions::calculate_tbn_mikktspace,
    pbr_bindings::{normal_map_texture, normal_map_sampler},
    mesh_view_bindings::view,
}

struct SourceFragmentExtension {
    rgb_rows: array<vec4<f32>,3>,
    metallic: vec4<f32>,
    occlusion: vec4<f32>,
    roughness: vec4<f32>,
    scalar_offsets_normal_strength: vec4<f32>,
    backface: vec4<f32>,
    world_params: vec4<f32>,
    world_mask_uv: vec4<f32>,
    world_tints: array<vec4<f32>,3>,
    world_axes: array<vec4<f32>,3>,
    world_control: vec4<f32>,
}
@group(#{MATERIAL_BIND_GROUP}) @binding(100) var<uniform> source: SourceFragmentExtension;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var source_orm: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(102) var source_orm_sampler: sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(103) var source_normal: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(104) var source_blend_mask: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(105) var source_noise: texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(106) var source_noise_mask: texture_2d<f32>;

// Source route v1 supports repeat and bilinear, one mip. It does not claim
// equivalence to Unity's platform compression or imported mipmap chain.
fn source_normal_texel(index:vec2<i32>, size:vec2<i32>)->vec3<f32> {
    return textureLoad(source_normal,((index%size)+size)%size,0).rgb;
}
fn source_normal_bilinear(uv:vec2<f32>)->vec3<f32> {
    let size=vec2<i32>(textureDimensions(source_normal));
    let at=fract(uv)*vec2<f32>(size)-0.5;
    let index=vec2<i32>(floor(at));
    let blend=fract(at);
    return mix(mix(source_normal_texel(index,size),source_normal_texel(index+vec2<i32>(1,0),size),blend.x),
               mix(source_normal_texel(index+vec2<i32>(0,1),size),source_normal_texel(index+vec2<i32>(1,1),size),blend.x),blend.y);
}

fn source_world_normal(uv:vec2<f32>, world_normal:vec3<f32>, world_tangent:vec4<f32>, is_front:bool)->vec3<f32> {
    let surface_uv=source_surface_image_uv_from_target(uv);
    var encoded:vec3<f32>;
    if source.backface.z==1.0 {encoded=source_normal_bilinear(surface_uv);}
    else {encoded=textureSampleBias(normal_map_texture,normal_map_sampler,surface_uv,view.mip_bias).rgb;}
    let raw=encoded*2.0-1.0;
    let s=source.scalar_offsets_normal_strength.w;
    var n=vec3<f32>(raw.xy*s,1.0+(raw.z-1.0)*clamp(s,0.0,1.0));
    n.y*=source.backface.y;
    if !is_front {
        if source.backface.x==1.0 {n=-n;}
        if source.backface.x==2.0 {n.z=-n.z;}
    }
    return normalize(calculate_tbn_mikktspace(world_normal,world_tangent)*n);
}

// Source world-blend arithmetic. Texture decoding/sampling is supplied by the
// caller; these functions do not choose an importer format or mip policy.
fn source_projection_position(target_position:vec3<f32>, axis_x:vec3<f32>, axis_y:vec3<f32>, axis_z:vec3<f32>, period:f32)->vec3<f32> {
    let position=vec3<f32>(dot(axis_x,target_position),dot(axis_y,target_position),dot(axis_z,target_position));
    return position / -abs(period);
}

// XY, ZY, XZ are combined in source graph order, without normalized weights.
fn source_projected_rgba(xy:vec4<f32>, zy:vec4<f32>, xz:vec4<f32>, source_normal:vec3<f32>)->vec4<f32> {
    let first=xy+(zy-xy)*abs(source_normal.x);
    return first+(xz-first)*abs(source_normal.y);
}

// Receives the existing affine RGB output, before saturation/power and masks.
// Source RGB/HDR and mask weights remain unclamped throughout these equations.
fn source_world_blend_rgb(base:vec3<f32>, mask:vec3<f32>, noise_red:f32, noise_mask:vec3<f32>, red_tint:vec3<f32>, green_tint:vec3<f32>, blue_tint:vec3<f32>, saturation:f32, power:f32, contrast:f32, use_mask:bool)->vec3<f32> {
    let luminance=dot(base,vec3<f32>(0.2126729,0.7151522,0.0721750));
    let saturated=vec3<f32>(luminance)+(base-vec3<f32>(luminance))*saturation;
    let powered=pow(saturated,vec3<f32>(power));
    var color=powered;
    if use_mask {
        color=color+(red_tint-color)*mask.r;
        color=color+(green_tint-color)*mask.g;
        color=color+(blue_tint-color)*mask.b;
    }
    let midpoint=vec3<f32>(pow(0.5,2.2));
    let contrasted=(noise_mask-midpoint)*contrast+midpoint;
    let noise=vec3<f32>(noise_red)+(vec3<f32>(0.0)-vec3<f32>(noise_red))*contrasted;
    return color+(vec3<f32>(0.0)-color)*noise;
}

// Explicit base-level repeat/bilinear source route, matching float storage.
// Source importer compression and mip-chain parity remain separate checks.
fn source_image_bilinear(image:texture_2d<f32>, uv:vec2<f32>)->vec4<f32> {
    let size=vec2<i32>(textureDimensions(image));
    let at=fract(uv)*vec2<f32>(size)-0.5;
    let index=vec2<i32>(floor(at));
    let blend=fract(at);
    let a=textureLoad(image,((index%size)+size)%size,0);
    let b=textureLoad(image,(((index+vec2<i32>(1,0))%size)+size)%size,0);
    let c=textureLoad(image,(((index+vec2<i32>(0,1))%size)+size)%size,0);
    let d=textureLoad(image,(((index+vec2<i32>(1,1))%size)+size)%size,0);
    return mix(mix(a,b,blend.x),mix(c,d,blend.x),blend.y);
}

// Source Unity UV uses the image bottom-left; decoded PNG/glTF uses top-left.
fn source_image_uv_from_unity(uv:vec2<f32>)->vec2<f32> {
    return vec2<f32>(uv.x,1.0-uv.y);
}

fn source_mask_image_uv_from_target(uv:vec2<f32>, scale:vec2<f32>)->vec2<f32> {
    let source_uv=source_image_uv_from_unity(uv);
    return source_image_uv_from_unity(source_uv*scale);
}

fn source_surface_image_uv_from_target(uv:vec2<f32>)->vec2<f32> {
    let scale=source.world_mask_uv.zw;
    if all(scale==vec2<f32>(1.0)) { return uv; }
    return source_mask_image_uv_from_target(uv,scale);
}

// Source PowerNode: no clamping, source HDR values are retained.
fn source_base_color_power(base:vec3<f32>, power:f32)->vec3<f32> {
    return pow(base,vec3<f32>(power));
}

fn source_world_color(base:vec3<f32>, uv:vec2<f32>, target_position:vec3<f32>, target_normal:vec3<f32>)->vec3<f32> {
    let p=source_projection_position(target_position,source.world_axes[0].xyz,source.world_axes[1].xyz,source.world_axes[2].xyz,source.world_params.z);
    let normal=normalize(target_normal);
    let n=vec3<f32>(dot(source.world_axes[0].xyz,normal),dot(source.world_axes[1].xyz,normal),dot(source.world_axes[2].xyz,normal));
    let noise=source_projected_rgba(source_image_bilinear(source_noise,source_image_uv_from_unity(p.xy)),source_image_bilinear(source_noise,source_image_uv_from_unity(p.zy)),source_image_bilinear(source_noise,source_image_uv_from_unity(p.xz)),n);
    let noise_mask=source_projected_rgba(source_image_bilinear(source_noise_mask,source_image_uv_from_unity(p.xy)),source_image_bilinear(source_noise_mask,source_image_uv_from_unity(p.zy)),source_image_bilinear(source_noise_mask,source_image_uv_from_unity(p.xz)),n);
    let use_mask=source.world_control.z==1.0;
    var mask=vec3<f32>(0.0);
    if use_mask { mask=source_image_bilinear(source_blend_mask,source_mask_image_uv_from_target(uv,source.world_mask_uv.xy)).rgb; }
    return source_world_blend_rgb(base,mask,noise.r,noise_mask.rgb,source.world_tints[0].rgb,source.world_tints[1].rgb,source.world_tints[2].rgb,source.world_params.x,source.world_params.y,source.world_params.w,use_mask);
}
