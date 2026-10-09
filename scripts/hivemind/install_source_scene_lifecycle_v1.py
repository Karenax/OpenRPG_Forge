"""Install tested production candidates with preserved preimages and exact hashes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-lifecycle-install-v2'
A=R/'apps/forge_studio/src/authoring_assets';MAIN=A.parent/'authoring_assets.rs'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,data):(ROOT/name).write_text(json.dumps(data,indent=2),'utf-8')
def main():
    assert not ROOT.exists();ROOT.mkdir()
    br=W/'studio-source-bootstrap-native-v1';rr=W/'studio-source-runtime-native-v1'
    bootstrap=read(br/'RUN-RESULT.v1.json');runtime=read(rr/'RUN-RESULT.v1.json')
    assert bootstrap['bootstrap_tests_passed']==3 and runtime['runtime_tests_passed']==5 and runtime['actual_exit_code']==0
    for p,d in runtime['input_pins'].items():assert sha(p)==d,p
    bs=br/'source_graph_bootstrap.rs';rs=rr/'source_graph_runtime.rs'
    bootstrap_text=bs.read_text('utf-8');runtime_text=rs.read_text('utf-8').split('#[cfg(test)] mod runtime_tests {')[0]
    assert not (A/'source_graph_bootstrap.rs').exists() and not (A/'source_graph_runtime.rs').exists()
    main_before=MAIN.read_bytes();preview=A/'preview.rs';preview_before=preview.read_bytes()
    (ROOT/'authoring_assets.preimage.rs').write_bytes(main_before);(ROOT/'preview.preimage.rs').write_bytes(preview_before)
    schedule='''
/// The authoring viewport owns one current/previous Bevy clock. Paused time
/// remains paused; no wall-clock or captured asset timestamp is introduced.
fn capture_viewport_clock(time:Res<Time>,mut clock:ResMut<SourceClock>){
 clock.current_seconds=time.elapsed_secs();
 clock.previous_seconds=time.elapsed_secs()-time.delta_secs();
}
pub(super) fn register(app:&mut App){
 app.insert_resource(SourceClock{current_seconds:0.,previous_seconds:0.});
 app.init_resource::<SourceSceneResidents>();
 app.add_systems(PostUpdate,(capture_viewport_clock,update).chain().after(bevy::transform::TransformSystems::Propagate));
}
'''
    main_text=main_before.decode('utf-8');needle='mod source_graph_context;';assert main_text.count(needle)==1
    main_text=main_text.replace(needle,needle+'\n#[allow(dead_code, reason = "Initial source host contexts await preview/placement routing")]\nmod source_graph_bootstrap;\n#[allow(dead_code, reason = "Shared lifecycle installed before preview/placement routing")]\nmod source_graph_runtime;')
    preview_text=preview_before.decode('utf-8');needle='    super::placements::register(app);';assert preview_text.count(needle)==1
    preview_text=preview_text.replace(needle,'    super::source_graph_runtime::register(app);\n'+needle)
    save('PREPARED.v1.json',dict(sub_batch='L01.INTEGRATION.01',prepared_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),bs,rs,br/'RUN-RESULT.v1.json',rr/'RUN-RESULT.v1.json',MAIN,preview]},bootstrap_candidate_sha256=sha(bs),runtime_tested_production_sha256=hashlib.sha256(runtime_text.encode()).hexdigest(),new_clock_schedule=schedule,preimages_preserved=True,full_import=False))
    (A/'source_graph_bootstrap.rs').write_text(bootstrap_text,'utf-8')
    (A/'source_graph_runtime.rs').write_text(runtime_text+schedule,'utf-8')
    MAIN.write_bytes(main_text.replace('\r\n','\n').encode('utf-8'))
    preview.write_bytes(preview_text.replace('\r\n','\n').encode('utf-8'))
    save('INSTALLED.v1.json',dict(status='tested_bootstrap_lifecycle_installed_clock_schedule_checks_pending',finished_utc=datetime.now(timezone.utc).isoformat(),installed_pins={str(p):sha(p) for p in [A/'source_graph_bootstrap.rs',A/'source_graph_runtime.rs',MAIN,preview]},preimages_preserved=True,preview_source_route_connected=False,placement_source_route_connected=False,gpu_qualified=False,studio_verified=False,full_import=False))
    print('Installed tested production regions and shared viewport clock; route and schedule qualification pending.')
if __name__=='__main__':main()
