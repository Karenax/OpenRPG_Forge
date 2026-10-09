"""Repair only the private fixture path; inherit the already-passed asset tests."""
from pathlib import Path
import hashlib,json,time,sys

BASE=Path(__file__).with_name('queue_scalar_transport_native_v5.py')
W=Path('F:/OpenRPG_ImportWork')
PARENT=W/'source-graph-scalar-transport-native-v5/RUN-RESULT.v1.json'
OVERLAY=W/'source-graph-private-boundary-v1/OVERLAY-INTAKE.v1.json'
WAIT=W/'detail-native-material-state-v1/RUN-RESULT.v1.json'

def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def load():
    parent=read(PARENT)
    assert parent['finished_utc'] and parent['status']=='refused_or_unverified'
    assert parent['tests'][0]['package']=='forge_assets' and parent['tests'][0]['exit_code']==0
    assert len(parent['tests'][0]['passed_tests'])==26 and parent['tests'][1]['exit_code']==101
    old_prepared=W/'source-graph-scalar-transport-native-v5/PREPARED.v1.json'
    for p,h in read(old_prepared)['input_pins'].items():assert sha(Path(p))==h,p
    overlay=read(OVERLAY);fixture_root=Path(overlay['base_snapshot_root'])
    fixture_pins={str(fixture_root/p):h for p,h in overlay['external_fixture_sha256'].items()}
    for p,h in fixture_pins.items():assert sha(Path(p))==h,p
    text=BASE.read_text('utf-8-sig')
    pairs=[("ROOT=W/'source-graph-scalar-transport-native-v5'","ROOT=W/'source-graph-scalar-transport-native-v6'"),
        ("OPENRPG_PRIVATE_SOURCE_FIXTURES='F:/OpenRPG_ImportWork/source-unlit-canonical-snapshot-v1'",
         'OPENRPG_PRIVATE_SOURCE_FIXTURES='+repr(str(fixture_root))),
        ("for package in ['forge_assets','forge_engine']:","for package in ['forge_engine']:"),
        ('rows=[]','rows=[dict(parent["tests"][0], inherited_from_receipt=str(PARENT), inherited_receipt_sha256=sha(PARENT))]'),
        ('pins={str(p):sha(p) for p in files}','pins={str(p):sha(p) for p in files}\n    pins.update(fixture_pins)\n    pins.update({str(p):sha(p) for p in [BASE,PARENT,OVERLAY,old_prepared]})')]
    for a,b in pairs:
        assert text.count(a)==1,a;text=text.replace(a,b)
    ns=dict(__name__='scalar_transport_fixed',__file__=str(Path(__file__).resolve()),parent=parent,PARENT=PARENT,
        BASE=BASE,OVERLAY=OVERLAY,fixture_pins=fixture_pins,old_prepared=old_prepared)
    exec(compile(text,str(BASE),'exec'),ns)
    return ns

def main():
    ns=load()
    if len(sys.argv)>1 and sys.argv[1]=='run':
        ns['save']('RUN-RESULT.v1.json',dict(status='waiting_for_owned_detail_unity_before_engine_test',full_import=False))
        while not read(WAIT).get('finished_utc'):time.sleep(15)
        ns['run']()
    else:ns['prepare']()

if __name__=='__main__':main()
