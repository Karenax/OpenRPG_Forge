"""Correct diagnostic taps to the L1 branch active with LayerBlend=false."""
from pathlib import Path
base_recipe=Path(__file__).with_name('run_house_sampling_diagnostic_v625.py')
text=base_recipe.read_text('utf-8-sig')
for old,new in {
 'house-sampling-diagnostic-v625':'house-active-sampling-diagnostic-v628',
 'HouseSamplingDiagnosticV625':'HouseActiveSamplingDiagnosticV628',
 "ap=W/'house-hdrp-bias-surface-reference-v623/HELPER-OWNERSHIP.v1.json'":"ap=W/'house-sampling-diagnostic-v625/HELPER-OWNERSHIP.v1.json'",
 "len(audit['helpers'])==25":"len(audit['helpers'])==26",
 '_ParallaxOcclusionMapping_e1b7d9fa42a44d408365caf53a373575_ParallaxUVs_1_Vector2':'_ParallaxOcclusionMapping_b30810784af04338acbc6c489bc06f66_ParallaxUVs_1_Vector2',
 '_SampleTexture2D_ac49fe421e9c4ef98fb4a04982571774_RGBA_0_Vector4':'_SampleTexture2D_96dbc6fa98d747edadaa11818f16eb47_RGBA_0_Vector4',
 'pins[str(parent)]=sha(parent)':'pins[str(parent)]=sha(parent);pins[str(base_recipe)]=sha(base_recipe)',
}.items():
    assert old in text,old;text=text.replace(old,new)
exec(compile(text,str(base_recipe)+'#active-L1-taps','exec'),globals())
