"""Prepare a private nine-channel graph-value control; preserve original GLB bytes."""
from pathlib import Path
import hashlib
import json
import re
import struct
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-native-graph-quad-difference-carrier-v69'
assert not ROOT.exists()

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

parent = W / 'townsmith-native-graph-bits-carrier-v58/SM_Door-Single-B.graph-bits.candidate.glb'
assert sha(parent) == '18fb78bd299e4aea3f53323075be95d2003aed05a35f09691ee64a5f7b0b6401'
raw = parent.read_bytes()
n = struct.unpack_from('<I', raw, 12)[0]
doc = json.loads(raw[20:20+n])
prefix = raw[28+n:]
binary = bytearray(prefix)
c = doc['materials'][0]['extensions']['OPENRPG_source_graph']
v = doc['bufferViews'][c['forward']['buffer_view']]
stage = prefix[v.get('byteOffset', 0):v.get('byteOffset', 0)+v['byteLength']]
assert hashlib.sha256(stage).hexdigest() == c['forward']['sha256']
t = stage.decode()
offset = 0
changes = []
while True:
    m = re.search(r'textureSample\(t[0-9]+,s[0-9]+,', t[offset:])
    if m is None:
        break
    start = offset + m.start()
    opening = t.index('(', start)
    depth = 1
    end = opening + 1
    while depth:
        if t[end] == '(':
            depth += 1
        elif t[end] == ')':
            depth -= 1
        end += 1
    call = t[opening+1:end-1]
    parts = []
    level = 0
    last = 0
    for i, char in enumerate(call):
        if char == '(':
            level += 1
        elif char == ')':
            level -= 1
        elif char == ',' and level == 0:
            parts.append(call[last:i])
            last = i+1
    parts.append(call[last:])
    assert len(parts) == 3, call
    uv = parts[2]
    replacement = 'textureSampleGrad('+','.join(parts)+',(dpdxCoarse('+uv+')+dpdyFine(dpdxFine('+uv+'))),-dpdyCoarse('+uv+'))'
    changes.append(dict(before=t[start:end], after=replacement))
    t = t[:start] + replacement + t[end:]
    offset = start + len(replacement)
assert len(changes) >= 19, len(changes)
stage = t.encode()
i = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0, byteOffset=len(binary), byteLength=len(stage)))
binary.extend(stage)
c['forward'] = dict(buffer_view=i, sha256=hashlib.sha256(stage).hexdigest())
binary.extend(b'\0' * (-len(binary) % 4))
doc['buffers'][0]['byteLength'] = len(binary)
doc.setdefault('extras', {})['private_graph_quad_difference'] = dict(installed_in_product=False, full_import=False)
j = json.dumps(doc, separators=(',', ':')).encode()
j += b' ' * (-len(j) % 4)
out = struct.pack('<III', 0x46546c67, 2, 28+len(j)+len(binary)) + struct.pack('<II', len(j), 0x4e4f534a) + j + struct.pack('<II', len(binary), 0x004e4942) + binary
assert binary[:len(prefix)] == prefix
ROOT.mkdir()
glb = ROOT / 'SM_Door-Single-B.graph-quad-difference.candidate.glb'
glb.write_bytes(out)
(ROOT / 'graph-quad-difference.forward.wgsl').write_bytes(stage)
queue = S / 'queue_source_glb_graph_quad_difference_v69.py'
runner = S / 'run_townsmith_native_graph_quad_difference_v69.py'
assert not queue.exists() and not runner.exists()
qt = (S / 'queue_source_glb_graph_bits_v58.py').read_text(encoding='utf-8')
qt = qt.replace('townsmith-native-graph-bits-channel-{CHANNEL}-v58', 'townsmith-native-graph-quad-difference-channel-{CHANNEL}-v69')
qt = qt.replace('townsmith-native-graph-bits-carrier-v58/SM_Door-Single-B.graph-bits.candidate.glb', 'townsmith-native-graph-quad-difference-carrier-v69/SM_Door-Single-B.graph-quad-difference.candidate.glb')
qt = qt.replace(sha(parent), sha(glb)).replace('townsmith-native-graph-bits-carrier-v58/RESULT.v1.json', 'townsmith-native-graph-quad-difference-carrier-v69/RESULT.v1.json')
compile(qt, str(queue), 'exec')
queue.write_text(qt, encoding='utf-8')
rt = (S / 'run_townsmith_native_graph_bits_v58.py').read_text(encoding='utf-8')
rt = rt.replace('townsmith-native-graph-bits-batch-v58', 'townsmith-native-graph-quad-difference-batch-v69')
rt = rt.replace('townsmith-native-graph-bits-channel-{channel}-v58', 'townsmith-native-graph-quad-difference-channel-{channel}-v69')
rt = rt.replace('queue_source_glb_graph_bits_v58.py', queue.name)
compile(rt, str(runner), 'exec')
runner.write_text(rt, encoding='utf-8')
(ROOT / 'RESULT.v1.json').write_text(json.dumps(dict(status='private_graph_quad_difference_control_prepared', input_pins={str(p): sha(p) for p in [Path(__file__), parent, glb, queue, runner, W/'townsmith-material-quad-difference-comparison-v68/RESULT.v1.json']}, sample_call_changes=changes, original_binary_prefix_byte_equal=True, resources_unchanged=True, source_pixels_applied_to_native=False, installed_in_product=False, full_import=False), indent=2), encoding='utf-8')
subprocess.run([sys.executable, '-B', str(runner)], check=True)
print(json.dumps(dict(root=str(ROOT), sample_calls=len(changes))))
