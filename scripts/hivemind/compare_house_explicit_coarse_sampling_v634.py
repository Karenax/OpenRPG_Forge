"""Test source-equivalent coarse sampling derivatives; candidate, not promotion."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_implicit_surface_gpu_v622.py')
code=recipe.read_text('utf-8-sig').replace("O=W/'house-implicit-surface-gpu-comparison-v622'","O=W/'house-explicit-coarse-sampling-comparison-v634'").replace("N=W/'house-implicit-surface-reference-v621'","N=W/'house-hdrp-bias-surface-reference-v623'").replace('openrpg-house-implicit-gpu-v622','openrpg-house-coarse-sampling-v634')
needle="    target=O/'src'/(guid+'.wgsl');"
addition='''    start=0;replacements=0
    while True:
        a=text.find('textureSampleBias(',start)
        if a<0:break
        first=a+len('textureSampleBias(');depth=0;parts=[];last=first
        for b in range(first,len(text)):
            char=text[b]
            if char=='(':depth+=1
            elif char==')':
                if depth==0:parts.append(text[last:b]);break
                depth-=1
            elif char==',' and depth==0:parts.append(text[last:b]);last=b+1
        assert len(parts)==4 and parts[3]=='i.texture_mip_bias'
        t,s,uv,bias=parts
        new=f'textureSampleGrad({t},{s},{uv},dpdxCoarse({uv})*exp2({bias}),dpdyCoarse({uv})*exp2({bias}))'
        text=text[:a]+new+text[b+1:];start=a+len(new);replacements+=1
    assert replacements==3
'''+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('for p in [Path(__file__),tp,mp,','for p in [Path(__file__),recipe,tp,mp,')
code=code.replace('Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification','Candidate explicit coarse derivatives vs native source implicit sampling, bias zero fixture; no material promotion or arbitrary bias qualification')
code=code.replace('implicit_sampling_verified=failed_total==0','implicit_sampling_verified=False,coarse_candidate_matches_fixture=failed_total==0')
exec(compile(code,str(recipe)+'#explicit-coarse-sampling-candidate','exec'),globals())
