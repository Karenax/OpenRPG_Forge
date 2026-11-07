"""Compare diagnostic channels; instrumented output never qualifies the material."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_implicit_surface_gpu_v622.py')
code=recipe.read_text('utf-8-sig')
code=code.replace("O=W/'house-implicit-surface-gpu-comparison-v622'","O=W/'house-sampling-diagnostic-comparison-v626'").replace("N=W/'house-implicit-surface-reference-v621'","N=W/'house-sampling-diagnostic-v625'").replace('openrpg-house-implicit-gpu-v622','openrpg-house-sampling-diagnostic-v626')
needle="    target=O/'src'/(guid+'.wgsl');"
addition='''    import re
    assert 'let n2=vec4<f32>(pom2.uv,0.0,0.0);' in text and 'let n3=textureSampleBias(t1,s1,' in text
    pattern=r'return SourceMaterialOutputs\\([^;]+;'
    matches=re.findall(pattern,text);assert len(matches)==1
    diagnostic=matches[0].replace('return SourceMaterialOutputs','var probe=SourceMaterialOutputs')+"""
    if(frame.scale_mode.z>0.5 && frame.scale_mode.z<1.5){probe.normal_ts=n3.xyz;probe.metallic=n3.w;}
    if(frame.scale_mode.z>1.5 && frame.scale_mode.z<2.5){probe.smoothness=n2.x;probe.occlusion=n2.y;}
    if(frame.scale_mode.z>2.5 && frame.scale_mode.z<3.5){probe.emission=vec3<f32>(dpdxCoarse(n2.xy),dpdyCoarse(n2.xy).x);probe.metallic=dpdyCoarse(n2.xy).y;}
    if(frame.scale_mode.z>3.5){probe.bent_normal=vec3<f32>(dpdxFine(n2.xy),dpdyFine(n2.xy).x);probe.metallic=dpdyFine(n2.xy).y;}
    return probe;
    """
    text=text.replace(matches[0],diagnostic).replace('vec4<f32>(r.emission,0)','vec4<f32>(r.emission,r.metallic)').replace('vec4<f32>(r.bent_normal,0)','vec4<f32>(r.bent_normal,r.metallic)')
'''+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('for p in [Path(__file__),tp,mp,','for p in [Path(__file__),recipe,tp,mp,')
code=code.replace('house_implicit_surface_gpu_comparison_pass','house_sampling_diagnostic_channels_match').replace('house_implicit_surface_gpu_mismatches_retained','house_sampling_diagnostic_mismatches_retained').replace('implicit_sampling_verified=failed_total==0','implicit_sampling_verified=False,diagnostic_only=True')
code=code.replace('Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification','Diagnostic raw albedo, parallax UV and coarse/fine derivatives; instrumented shader is not material qualification')
exec(compile(code,str(recipe)+'#sampling-diagnostic','exec'),globals())
