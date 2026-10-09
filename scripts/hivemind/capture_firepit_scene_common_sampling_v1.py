"""Same full scene fixture and shader, explicit common source scene sampling."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-scene-common-sampling-v1')
source=source.replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-source-ao-scene-v1'")
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-source-scene-instance-fix-case-v1'")
marker='    rust=rust.replace(marker,observe+marker)'
assert source.count(marker)==1
source=source.replace(marker,marker+"""
    mount='let host=source_graph_host::mount_initial(app.world_mut(),root,'
    assert rust.count(mount)==1
    rust=rust.replace(mount,'app.world_mut().entity_mut(root).insert(source_graph_resources::SceneSampling(forge_engine::source_graph::SourceGraphSceneSampling::UnityForceEnableD3d));'+mount)
""")
if __name__=='__main__':
    import sys,json,hashlib
    if sys.argv[-1]=='prepare':
        receipt=Path('F:/OpenRPG_ImportWork/firepit-surface-common-sampling-reopen-v2/RESULT.v1.json')
        measured=json.loads(receipt.read_text('utf-8-sig'))
        comparisons=[value for row in measured['rows'] for values in row['groups'].values() for value in values]
        assert len(comparisons)==20 and all(value['within_predeclared_mae'] for value in comparisons)
    exec(compile(source,str(BASE),'exec'),globals())
