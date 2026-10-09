@group(0) @binding(0) var atlas:texture_depth_2d_array;
@group(0) @binding(1) var<storage,read_write> output:array<f32>;
@compute @workgroup_size(8,8,1) fn main(@builtin(global_invocation_id) id:vec3<u32>) {
 let dimensions=textureDimensions(atlas);let layers=textureNumLayers(atlas);
 if(id.x>=dimensions.x||id.y>=dimensions.y||id.z>=layers){return;}
 output[(id.z*dimensions.y+id.y)*dimensions.x+id.x]=textureLoad(atlas,vec2<i32>(id.xy),i32(id.z),0);
}
