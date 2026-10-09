"""Source spherical border formula control; dither remains explicitly unqualified."""
from pathlib import Path
base=Path(__file__).with_name('prepare_firepit_sphere_selection_v1.py')
source=base.read_text('utf-8').replace('firepit-source-sphere-selection-case-v1','firepit-source-sphere-transition-case-v2')
marker='    before=\'let index=shadows::get_cascade_index(light_id,view_z);\''
addition='''    helper+='\\nfn private_source_sphere_alpha(index:u32,position:vec4<f32>) -> f32 {\\n'
    helper+=' let border=array<f32,4>('+literal(native[20:24])+')[index];if(border<=0.0){return 0.0;}\\n'
    helper+=' let rws=vec3<f32>(-position.x,position.y-1024.0,position.z)-vec3<f32>('+literal(eye)+');\\n'
    helper+=' let spheres=array<vec4<f32>,4>('+','.join('vec4<f32>('+literal(row)+')' for row in spheres)+');\\n'
    helper+=' let delta=rws-spheres[index].xyz;let dist2=dot(delta,delta);let relative=dist2/spheres[index].w;\\n'
    helper+=' let alpha=clamp((relative-(1.0-border))/border,0.0,1.0);\\n'
    helper+=' let t=clamp((border-1.0)/(0.7-1.0),0.0,1.0);let sharp=8.0+512.0*t*t*(3.0-2.0*t);\\n'
    helper+=' let facing=dot(normalize(-rws),delta/sqrt(dist2));return mix(alpha,0.0,clamp(facing*sharp,0.0,1.0));\\n}\\n'
'''
assert source.count(marker)==1
source=source.replace(marker,addition+marker)
old="        return text.replace(before,after)+helper"
new='''        old_fade=""" if(index+1u<light.num_cascades){
  let far=light.cascades[index].far_bound;let near=(1.0-light.cascades_overlap_proportion)*far;
  if(-view_z>=near){let next=source_tent7_directional_cascade(light_id,index+1u,position,normal);value=mix(value,next,(-view_z-near)/(far-near));}
 }"""
        new_fade=""" let alpha=private_source_sphere_alpha(index,position);
 if(alpha>0.0){
  var next=1.0;
  if(index+1u<light.num_cascades){next=source_tent7_directional_cascade(light_id,index+1u,position,normal);}
  value=mix(value,next,alpha);
 }"""
        assert text.count(old_fade)==1
        return text.replace(before,after).replace(old_fade,new_fade)+helper'''
assert source.count(old)==1
source=source.replace(old,new).replace("'existing_view_depth_overlap_fade_retained':True","'existing_view_depth_overlap_fade_retained':False").replace("'single_variable_selection_control':True","'source_spherical_border_formula_replayed':True,'source_dither_replayed':False,'source_border_coefficients_from_gpu':list(native[20:24])")
source=source.replace('private_measured_source_sphere_selection_control_prepared','private_source_sphere_and_border_formula_blend_control_prepared')
exec(compile(source,str(base),'exec'),globals())
