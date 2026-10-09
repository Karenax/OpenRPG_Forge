"""Compose Detail's ten original surface outputs using its measured-default ABI."""
from pathlib import Path
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
HERE=Path(__file__).resolve().parent
BASE=HERE/'build_delta_native_surface_interfaces_v1.py'
PARENT=W/'detail-native-surface-selected-parent-v1'
ROOT=W/'detail-native-surface-interfaces-v1'

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def once(t,a,b):
    assert t.count(a)==1,a
    return t.replace(a,b)

def main():
    assert not PARENT.exists() and not ROOT.exists()
    candidate_path=W/'detail-normal-mode2-bevy-candidate-v1/RESULT.v1.json'
    c=read(candidate_path)
    normal_path=W/'detail-normal-decoder-independent-reopen-v1/RESULT.v1.json'
    normal=read(normal_path)
    assert c['status']=='candidate_composed' and len(c['actual_stage_compositions'])==4
    assert normal['normal_textures_verified']==1 and normal['normal_mips_verified']==12
    p=Path(c['shader']);assert sha(p)==c['shader_sha256']
    for packet in [c]:
        for name,h in packet['input_pins'].items():assert sha(Path(name))==h,name
    PARENT.mkdir()
    selected=PARENT/'RESULT.v1.json'
    selected.write_text(json.dumps(dict(native_vertex_programs_composed=1,actual_positive_compositions=4,
        failures=[],rows=[dict(material_guid=c['material_guid'],shader=str(p),sha256=sha(p))],
        selected_from_actual_candidate=str(candidate_path),candidate_receipt_sha256=sha(candidate_path),
        normal_parity_receipt=str(normal_path),normal_parity_sha256=sha(normal_path),full_import=False),indent=2),'utf-8')
    text=BASE.read_text('utf-8-sig')
    text=once(text,"ROOT = W / 'delta-native-surface-interfaces-v1'","ROOT = W / 'detail-native-surface-interfaces-v1'")
    text=once(text,"parent_path = W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'",'parent_path = Path('+repr(str(selected))+')')
    text=once(text,"assert parent['native_vertex_programs_composed'] == 6 and not parent['failures']","assert parent['native_vertex_programs_composed'] == 1 and not parent['failures']")
    text=once(text,'requested=6,','requested=1,')
    text=once(text,"    vertex_old = read(W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4/RESULT.v1.json')",
        "    pins.update({str(p):sha(p) for p in [BASE,candidate_path,normal_path,Path(c['shader'])]})\n    vertex_old = read(W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4/RESULT.v1.json')")
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE,candidate_path=candidate_path,normal_path=normal_path,c=c))
    result=read(ROOT/'RESULT.v1.json')
    assert result['native_surface_interfaces_composed']==1 and result['actual_positive_compositions']==4 and not result['failures'],result['failures']

if __name__=='__main__':main()
