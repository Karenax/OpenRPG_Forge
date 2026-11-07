// Native photometric emitters retain Bevy's lighting, shadow and texture helpers.
// Cluster iteration and input mapping follow bevy_pbr 0.19.1 pbr_functions.wgsl
// (Bevy contributors, MIT OR Apache-2.0). No source-scene intensity conversion.
#import bevy_pbr::pbr_types::PbrInput as NativePbrInput
#import bevy_pbr::lighting as lighting
#import bevy_pbr::lighting::{LAYER_BASE, LAYER_CLEARCOAT}
#import bevy_pbr::pbr_functions::{calculate_diffuse_color, calculate_F0_dielectric}
#import bevy_pbr::mesh_view_bindings as nativeView
#import bevy_pbr::mesh_view_types as mesh_view_types
#import bevy_pbr::clustered_forward as nativeClusters
#import bevy_pbr::shadows as shadows
#import bevy_pbr::mesh_types::MESH_FLAGS_SHADOW_RECEIVER_BIT
#ifdef CONTACT_SHADOWS
#ifdef DEPTH_PREPASS
#import bevy_pbr::pbr_functions::calculate_contact_shadow
#endif
#endif

fn nativeLocalLighting(input:NativePbrInput)->vec3f {
    var surface:lighting::LightingInput;
    let roughness=input.material.perceptual_roughness;
    let NdotV=max(dot(input.N,input.V),0.0001);
    surface.layers[LAYER_BASE].NdotV=NdotV;
    surface.layers[LAYER_BASE].N=input.N;
    surface.layers[LAYER_BASE].R=reflect(-input.V,input.N);
    surface.layers[LAYER_BASE].perceptual_roughness=roughness;
    surface.layers[LAYER_BASE].roughness=lighting::perceptualRoughnessToRoughness(roughness);
    surface.P=input.world_position.xyz;
    surface.V=input.V;
    surface.diffuse_color=calculate_diffuse_color(input.material.base_color.rgb,
        input.material.metallic,input.material.specular_transmission,input.material.diffuse_transmission);
    surface.metallic=input.material.metallic;
    surface.F0_dielectric=calculate_F0_dielectric(input.material.reflectance);
    surface.F0_metallic=input.material.base_color.rgb;
    surface.F_ab=lighting::F_AB(roughness,NdotV);
#ifdef STANDARD_MATERIAL_CLEARCOAT
    surface.layers[LAYER_CLEARCOAT].NdotV=max(dot(input.clearcoat_N,input.V),0.0001);
    surface.layers[LAYER_CLEARCOAT].N=input.clearcoat_N;
    surface.layers[LAYER_CLEARCOAT].R=reflect(-input.V,input.clearcoat_N);
    surface.layers[LAYER_CLEARCOAT].perceptual_roughness=input.material.clearcoat_perceptual_roughness;
    surface.layers[LAYER_CLEARCOAT].roughness=lighting::perceptualRoughnessToRoughness(input.material.clearcoat_perceptual_roughness);
    surface.clearcoat_strength=input.material.clearcoat;
#endif
#ifdef STANDARD_MATERIAL_ANISOTROPY
    surface.anisotropy=input.anisotropy_strength;
    surface.Ta=input.anisotropy_T;
    surface.Ba=input.anisotropy_B;
#endif
    let view_z=dot(vec4f(nativeView::view.view_from_world[0].z,nativeView::view.view_from_world[1].z,
        nativeView::view.view_from_world[2].z,nativeView::view.view_from_world[3].z),input.world_position);
    let cluster=nativeClusters::view_fragment_cluster_index(input.frag_coord.xy,view_z,input.is_orthographic);
    let ranges=nativeClusters::unpack_clusterable_object_index_ranges(cluster);
    var result=vec3f(0.0);
    for(var i=ranges.first_point_light_index_offset;i<ranges.first_reflection_probe_index_offset;i++) {
        let id=nativeClusters::get_clusterable_object_id(i);
        let light=nativeView::clustered_lights.data[id];
        let point=i<ranges.first_spot_light_index_offset;
#ifdef LIGHTMAP
        let diffuse=(light.flags & mesh_view_types::POINT_LIGHT_FLAGS_AFFECTS_LIGHTMAPPED_MESH_DIFFUSE_BIT)!=0u;
#else
        let diffuse=true;
#endif
        var shadow=1.0;
        if (input.flags & MESH_FLAGS_SHADOW_RECEIVER_BIT)!=0u {
            if (light.flags & mesh_view_types::POINT_LIGHT_FLAGS_SHADOWS_ENABLED_BIT)!=0u {
                if point {
                    shadow=shadows::fetch_point_shadow(id,input.world_position,input.world_normal,input.frag_coord.xy);
                } else {
                    shadow=shadows::fetch_spot_shadow(id,input.world_position,input.world_normal,light.shadow_map_near_z,input.frag_coord.xy);
                }
            }
#ifdef CONTACT_SHADOWS
#ifdef DEPTH_PREPASS
            if shadow>0.0 && nativeView::contact_shadows_settings.linear_steps>0u
                && (light.flags & mesh_view_types::POINT_LIGHT_FLAGS_CONTACT_SHADOWS_ENABLED_BIT)!=0u {
                let L=normalize(light.position_radius.xyz-input.world_position.xyz);
                shadow*=calculate_contact_shadow(input.world_position.xyz,input.frag_coord.xy,L,nativeView::contact_shadows_settings.linear_steps);
            }
#endif
#endif
        }
        if point {
            result+=lighting::point_light(id,&surface,diffuse,true)*shadow;
        } else {
            result+=lighting::spot_light(id,&surface,diffuse)*shadow;
        }
    }
    return nativeView::view.exposure*result;
}
