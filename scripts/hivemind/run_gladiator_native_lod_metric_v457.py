"""Capture actual Unity camera/LOD decisions for the five static source prefabs."""
from pathlib import Path

parent=Path(__file__).resolve().with_name('run_gladiator_component_state_v447.py')
code=parent.read_text('utf-8')
code=code.replace('gladiator-native-component-state-v447','gladiator-native-lod-metric-v457')
code=code.replace("parent=S/'queue_rocks_firepit_native_component_state_v1.py'", "parent=S/'queue_rocks_firepit_native_lod_metric_v1.py'")
start=code.index("code=code.replace('public ComponentState[] lights;'")
end=code.index("helper=PROJECT/",start)
code=code[:start]+'''code=code.replace('OpenRPGRocksFirePitNativeLODMetricV1','OpenRPGGladiatorNativeLODMetricV457')
# Source scripts remain absent. Instantiate only the previously proved static
# hierarchy; explicitly refuse MonoBehaviour before cloning.
code=code.replace('var clone=UnityEngine.Object.Instantiate(original);', 'if(original.GetComponentsInChildren<MonoBehaviour>(true).Any())throw new Exception("Purchased script component refused");var originalComponents=original.GetComponentsInChildren<Component>(true);var originalSnapshots=originalComponents.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();var clone=UnityEngine.Object.Instantiate(original);')
code=code.replace('finally{UnityEngine.Object.DestroyImmediate(clone);}', 'finally{UnityEngine.Object.DestroyImmediate(clone);for(int q=0;q<originalComponents.Length;q++)if(originalSnapshots[q]!=EditorJsonUtility.ToJson(originalComponents[q],true))throw new Exception("Source components changed during LOD measurement");}')
old_heights='foreach(float height in new[]{0.01f,0.03f,0.1f,0.18f,0.25f,0.35f,0.6f})'
new_heights='foreach(float height in new[]{0.01f,0.03f,0.1f,0.18f,0.25f,0.35f,0.6f}.Concat(group.GetLODs().SelectMany(l=>new[]{l.screenRelativeTransitionHeight*0.999f,l.screenRelativeTransitionHeight*1.001f})))'
assert code.count(old_heights)==1
code=code.replace(old_heights,new_heights)
'''+code[end:]
code=code.replace('GladiatorComponentStateV447.cs','GladiatorNativeLODMetricV457.cs')
code=code.replace('OPENRPG_COMPONENT_STATE_OUTPUT','OPENRPG_LOD_METRIC_OUTPUT')
code=code.replace('OpenRPGGladiatorComponentStateV447.Capture.Export','OpenRPGGladiatorNativeLODMetricV457.Capture.Export')
old="assert len(native['prefabs'])==5 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==17\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==5"
new="expected_samples=sum(3*(7+2*len(r['lod_groups'][0]['levels'])) for r in bridge['rows'])\n    assert expected_samples==207 and len(native['samples'])==expected_samples"
assert old in code
code=code.replace(old,new)
code=code.replace('five_prefab_renderer_lod_and_collider_states_captured_review_pending','five_prefab_native_camera_lod_metrics_captured_review_pending')
code=code.replace('gladiator_component_capture_prepared','gladiator_native_lod_metric_prepared').replace('gladiator_native_component_capture_running','gladiator_native_camera_lod_metric_running').replace('gladiator_component_state_refusal_retained','gladiator_native_lod_metric_refusal_retained')
exec(compile(code,str(parent)+'#native-camera-lod-v457','exec'),globals())
