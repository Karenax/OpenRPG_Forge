"""Capture raw albedo, parallax UV and coarse/fine derivatives; diagnostic only."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_hdrp_bias_surface_reference_v623.py')
prefix=parent.read_text('utf-8-sig').split('exec(compile(code,')[0]
exec(compile(prefix,str(parent)+'#prepare-recipe','exec'),globals())
code=code.replace('house-hdrp-bias-surface-reference-v623','house-sampling-diagnostic-v625').replace('HouseImplicitSurfaceV623','HouseSamplingDiagnosticV625')
code=code.replace("ap=W/'house-implicit-surface-reference-v621/HELPER-OWNERSHIP.v1.json'","ap=W/'house-hdrp-bias-surface-reference-v623/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==24","len(audit['helpers'])==25")
needle="    copies=read(W/'house-native-texture-mips-v619/PREPARED.v1.json')['private_copies']"
addition='''    uv='_ParallaxOcclusionMapping_e1b7d9fa42a44d408365caf53a373575_ParallaxUVs_1_Vector2'
    raw='_SampleTexture2D_ac49fe421e9c4ef98fb4a04982571774_RGBA_0_Vector4'
    assert shader.count('return surface;')==1
    diagnostic=f"""
    if(_ProbeMode>0.5 && _ProbeMode<1.5){{surface.NormalTS={raw}.xyz;surface.Metallic={raw}.w;}}
    if(_ProbeMode>1.5 && _ProbeMode<2.5){{surface.Smoothness={uv}.x;surface.Occlusion={uv}.y;}}
    if(_ProbeMode>2.5 && _ProbeMode<3.5){{surface.Emission=float3(ddx({uv}),ddy({uv}).x);surface.Metallic=ddy({uv}).y;}}
    if(_ProbeMode>3.5){{surface.BentNormal=float3(ddx_fine({uv}),ddy_fine({uv}).x);surface.Metallic=ddy_fine({uv}).y;}}
    return surface;
    """
    shader=shader.replace('return surface;',diagnostic).replace('float4(r.Emission,0)','float4(r.Emission,r.Metallic)').replace('float4(r.BentNormal,0)','float4(r.BentNormal,r.Metallic)')
    pins[str(parent)]=sha(parent)
'''+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace("['base_color_alpha','normal_metallic','smoothness_occlusion_depth','emission','bent_normal','pixel_position_uv_orientation_marker']","['base_color_alpha','raw_albedo_rgba','parallax_uv_depth_padding','coarse_uv_derivatives','fine_uv_derivatives','pixel_position_uv_orientation_marker']")
exec(compile(code,str(parent)+'#sampling-diagnostic','exec'),globals())
