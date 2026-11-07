"""Include measured renderer/LOD state in the versioned assembly recipe."""
from pathlib import Path
import hashlib, json, subprocess, sys
S=Path(__file__).parent
old=S/'assemble_firepit_glb_v3.py';new=S/'assemble_firepit_glb_v4.py'
assert not new.exists()
text=old.read_text('utf-8')
text='import bind_native_scene_render_state_v1 as native_state\n'+text
assert text.count('firepit-complete-static-glb-v3')==1
text=text.replace('firepit-complete-static-glb-v3','firepit-complete-static-glb-v4')
marker="        path=ROOT/(scene['asset_id']+'.candidate.glb');"
assert text.count(marker)==1
insert="""        state_review=read(native_state.REVIEW)
        assert state_review['status']=='native_lod_renderer_and_hdrp_light_identity_reopened'
        state_pins=dict(state_review['input_pins'])
        for p in [native_state.REVIEW,native_state.NATIVE,Path(native_state.__file__)]:state_pins[str(p)]=sha(p)
        for p,h in state_pins.items():assert sha(Path(p))==h,p
        counts=native_state.bind(doc,read(native_state.NATIVE))
        assert counts==dict(nodes=9,renderers=6,lod_groups=1)
        pins.update(state_pins)
"""
text=text.replace(marker,insert+marker)
marker='results.append(dict(normal_guard_packaging=normal_guard_proof,'
assert text.count(marker)==1
text=text.replace(marker,'results.append(dict(original_render_state_binding=counts,normal_guard_packaging=normal_guard_proof,')
compile(text,str(new),'exec');new.write_text(text,'utf-8')
subprocess.run([sys.executable,'-B',str(new)],check=True)
w=Path('F:/OpenRPG_ImportWork');proof=json.loads((w/'firepit-complete-static-glb-v4/RESULT.v1.json').read_text('utf-8'))
bound=json.loads((w/'firepit-native-state-conversion-v222/RESULT.v1.json').read_text('utf-8'))
assert proof['rows'][0]['sha256']==bound['target_sha256']
assert proof['rows'][0]['original_render_state_binding']==dict(nodes=9,renderers=6,lod_groups=1)
(w/'firepit-complete-static-glb-v4/RECIPE-REOPEN.v1.json').write_text(json.dumps(dict(status='assembly_recipe_native_state_output_identical_to_independent_binding',recipe=str(new),recipe_sha256=hashlib.sha256(new.read_bytes()).hexdigest(),glb_sha256=bound['target_sha256'],source_geometry_and_shaders_unchanged=True,gpu_rendered=False,full_import=False),indent=2),'utf-8')
print('Native renderer and LOD metadata now included in assembler v4; exact independently bound GLB reproduced.')
