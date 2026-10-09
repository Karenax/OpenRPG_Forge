"""Orthographic source coordinate control: clamp UV, do not reject depth."""
from pathlib import Path
base=Path(__file__).with_name('prepare_firepit_sphere_transition_v2.py')
source=base.read_text('utf-8').replace('firepit-source-sphere-transition-case-v2','firepit-source-receiver-coordinates-case-v3')
old_coords='let local=shadows::world_to_directional_light_local(light_id,cascade_index,position);\n if(local.w==0.0){return 1.0;}'
new_coords='let projected=cascade.clip_from_world*position;\n if(projected.w==0.0){return 1.0;}\n let ndc=projected.xyz/projected.w;\n let local=vec4<f32>(clamp(ndc.xy*vec2<f32>(0.5,-0.5)+vec2<f32>(0.5),vec2<f32>(0.0),vec2<f32>(1.0)),ndc.z,1.0);'
needle='        return text.replace(before,after).replace(old_fade,new_fade)+helper'
literal=lambda value: repr(value).replace('\\','\\\\')
replacement='        assert text.count('+literal(old_coords)+')==1\n        text=text.replace('+literal(old_coords)+','+literal(new_coords)+')\n'+needle
assert source.count(needle)==1
source=source.replace(needle,replacement).replace('private_source_sphere_and_border_formula_blend_control_prepared','private_source_orthographic_coordinates_no_depth_rejection_control_prepared')
exec(compile(source,str(base),'exec'),globals())
