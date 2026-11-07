"""Preserve authored UV1 in a private Townsmith material GPU control."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
builder = S / 'build_townsmith_source_uv1_v36.py'
assert not builder.exists()
q = (S / 'build_townsmith_normal_roughness_draw_v4.py').read_text(encoding='utf-8')
for old, new in [
    ('townsmith-paint-door-normal-roughness-carrier-v4', 'townsmith-source-uv1-carrier-v36'),
    ('townsmith-paint-door-native-normal-roughness-draw-v4', 'townsmith-native-source-uv1-v36'),
    ('verify_source_glb_normal_roughness_draw', 'verify_source_glb_source_uv1_v36'),
    ('queue_source_glb_normal_roughness_draw_v4', 'queue_source_glb_source_uv1_v36'),
    ('SM_Door-Single-B.normal-roughness-control.candidate.glb', 'SM_Door-Single-B.source-uv1-control.candidate.glb'),
]:
    assert old in q
    q = q.replace(old, new)
fix = '''
def preserve_source_uv1(stage_text):
    assert stage_text.count('textureSample(t2,s2,vec2<f32>(0.0,0.0))') == 2
    stage_text = stage_text.replace('textureSample(t2,s2,vec2<f32>(0.0,0.0))', 'textureSample(t2,s2,source_uv1.xy)')
    replacements = [
        ('fn evaluate_source_material(source_uv:vec4<f32>,', 'fn evaluate_source_material(source_uv:vec4<f32>,source_uv1:vec4<f32>,'),
        (' source_uv0:vec4<f32>,\\n target_world_position:', ' source_uv0:vec4<f32>,\\n source_uv1:vec4<f32>,\\n target_world_position:'),
        ('evaluate_source_material(input.source_uv0,absolute_position,', 'evaluate_source_material(input.source_uv0,input.source_uv1,absolute_position,'),
        ('SourceMaterialHostInputs(in.source_uv0,in.world_position.xyz,', 'SourceMaterialHostInputs(in.source_uv0,in.source_uv1,in.world_position.xyz,'),
        ('out.source_uv0=vertex.source_uv0;', 'out.source_uv0=vertex.source_uv0;\\n out.source_uv1=vertex.source_uv1;'),
        ('out.source_uv0=frame.source_uv0;', 'out.source_uv0=frame.source_uv0;\\n out.source_uv1=frame.source_uv1;'),
        ('source_graph_second_uv_requires_source_qualification();', 'out.uv_b=vertex.source_uv1.xy;'),
    ]
    for old,new in replacements:
        assert stage_text.count(old)==1, (old, stage_text.count(old))
        stage_text=stage_text.replace(old,new)
    for name, location in [('SourceGraphVertex',12),('SourceGraphForwardOutput',11),('SourceGraphPrepassOutput',11)]:
        start=stage_text.index('struct '+name+' {')
        end=stage_text.index('\\n}',start)
        body=stage_text[start:end]
        assert body.count('@location(10) source_uv0:vec4<f32>,')==1
        body=body.replace('@location(10) source_uv0:vec4<f32>,','@location(10) source_uv0:vec4<f32>,\\n @location('+str(location)+') source_uv1:vec4<f32>,')
        stage_text=stage_text[:start]+body+stage_text[end:]
    return stage_text
text=preserve_source_uv1(text)
'''
anchor = "text = stage.decode('utf-8')"
assert q.count(anchor) == 1
q = q.replace(anchor, anchor + '\n' + fix)
# Preserve and correct both stages, including the same original binary prefix.
anchor = "doc['buffers'][0]['byteLength'] = len(binary)"
addition = '''
prepass_ref=contract['prepass']
prepass_view=doc['bufferViews'][prepass_ref['buffer_view']]
prepass_bytes=original_binary[prepass_view.get('byteOffset',0):prepass_view.get('byteOffset',0)+prepass_view['byteLength']]
assert hashlib.sha256(prepass_bytes).hexdigest()==prepass_ref['sha256']
prepass_derived=preserve_source_uv1(prepass_bytes.decode('utf-8')).encode('utf-8')
prepass_index=len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(prepass_derived)))
binary.extend(prepass_derived)
contract['prepass']=dict(buffer_view=prepass_index,sha256=hashlib.sha256(prepass_derived).hexdigest())
while len(binary)%4:binary.append(0)
'''
assert q.count(anchor) == 1
q = q.replace(anchor, addition + '\n' + anchor)
anchor = 'pins = {str(path): sha(path) for path in paths}'
q = q.replace(anchor, "subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe','--edition','2024',str(EXAMPLE)],check=True)\n" + anchor)
q = q.replace("status='source_normal_roughness_only_private_control_prepared'", "status='preserved_source_uv1_normal_roughness_control_prepared'")
compile(q, str(builder), 'exec')
builder.write_text(q, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(builder)], check=True)
