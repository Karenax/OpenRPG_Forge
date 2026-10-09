"""Preserve the first refusal and extend foliage kernels to original UV0-UV3."""
import hashlib
import json
from pathlib import Path


def main():
    source_path = Path(__file__).resolve().with_name("retry_delta_foliage_wgsl.py")
    proof_path = Path(r"F:\OpenRPG_ImportWork\nested-prefab-delta-foliage-wgsl-retry-v1\RUN-RESULT.v1.json")
    proof = json.loads(proof_path.read_text("utf-8"))
    expected = proof["source_pins"][str(source_path)]
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != expected:
        raise RuntimeError("First foliage retry source changed")
    text = source_path.read_text("utf-8")
    changes = [
        ("nested-prefab-delta-foliage-wgsl-retry-v1", "nested-prefab-delta-foliage-wgsl-retry-v2"),
        ("nested-prefab-delta-foliage-wgsl-v2", "nested-prefab-delta-foliage-wgsl-v3"),
        ('compile(adapted, "foliage-emitter", "exec")', '''
    old_uv_node="if op=='UVNode':assert p['m_OutputChannel']==0;expr='i.uv0';dim=4"
    new_uv_node="if op=='UVNode':channel=p['m_OutputChannel'];assert isinstance(channel,int) and 0<=channel<=3, 'Original UV channel out of range';expr='i.uv'+str(channel);dim=4;input_requirements.append({'source_opcode':'UVNode','channel':channel,'field':'uv'+str(channel),'original_four_components_required':True})"
    old_uv_slot="if kind=='UVMaterialSlot':channel=ss.get('m_Channel',0);assert channel==0;return 'i.uv0'"
    new_uv_slot="if kind=='UVMaterialSlot':channel=ss.get('m_Channel',0);assert isinstance(channel,int) and 0<=channel<=3, 'Original UV slot channel out of range';input_requirements.append({'source_slot':kind,'channel':channel,'field':'uv'+str(channel),'original_four_components_required':True});return 'i.uv'+str(channel)"
    for before,after in [(old_uv_node,new_uv_node),(old_uv_slot,new_uv_slot)]:
        if adapted.count(before)!=1:raise RuntimeError('Original UV insertion point changed')
        adapted=adapted.replace(before,after)
    uv_field="('uv0',4),"
    if adapted.count(uv_field)!=1:raise RuntimeError('Original UV ABI changed')
    adapted=adapted.replace(uv_field,"('uv0',4),('uv1',4),('uv2',4),('uv3',4),")
    compile(adapted, "foliage-emitter", "exec")'''),
        ('pins[str(emitter_path)] = sha(emitter_path)',
         'pins[str(emitter_path)] = sha(emitter_path)\n    pins[str(source_path)] = expected\n    pins[str(proof_path)] = sha(proof_path)'),
    ]
    for before, after in changes:
        required_count = 2 if before == "nested-prefab-delta-foliage-wgsl-v2" else 1
        if text.count(before) != required_count:
            raise RuntimeError("Foliage retry configuration changed")
        text = text.replace(before, after)
    namespace = {"__name__": "delta_foliage_uv_retry", "__file__": str(Path(__file__).resolve()),
                 "source_path": source_path, "expected": expected, "proof_path": proof_path}
    exec(compile(text, str(source_path), "exec"), namespace)
    namespace["main"]()


if __name__ == "__main__":
    main()
