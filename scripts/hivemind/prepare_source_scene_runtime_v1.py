"""Stage shared host ownership/frame updates while product sources are under test."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-scene-runtime-v1'
CODE=r'''//! Shared transient ownership for preview and persisted placement render roots.
//! Persistence remains in ManualPlacement; this holds only renderer handles.
//! No implicit clock, material replacement, reveal or LOD selection.
use bevy::prelude::*;
use std::collections::BTreeMap;
use super::{source_graph_context::{HostFrame,time_parameters},source_graph_host::SourceSceneHost};

#[derive(Resource,Clone,Copy)]
pub(super) struct SourceClock { pub current_seconds:f32,pub previous_seconds:f32 }
#[derive(Clone,Copy)]
pub(super) struct RuntimePolicy {
 pub source_world_from_target_world:Mat4,
 pub source_absolute_world_from_target_world:Mat4,
 pub global_mip_bias:f32,
 pub source_float_min:f32,
}
impl RuntimePolicy {
 fn frame(&self,clock:SourceClock)->Result<HostFrame,String>{
  Ok(HostFrame{source_world_from_target_world:self.source_world_from_target_world,
   source_absolute_world_from_target_world:self.source_absolute_world_from_target_world,
   time_parameters:time_parameters(clock.current_seconds)?,previous_time_parameters:time_parameters(clock.previous_seconds)?,
   global_mip_bias:self.global_mip_bias,source_float_min:self.source_float_min})
 }
}
struct Resident { host:SourceSceneHost,policy:RuntimePolicy }
#[derive(Resource,Default)]
pub(super) struct SourceSceneResidents { scenes:BTreeMap<Entity,Resident> }
#[derive(Component,Debug,Clone,PartialEq,Eq)]
pub(super) enum SourceFrameStatus { Updated,Refused(String) }

/// A successful mount owns its strong handles until this root is removed.
/// Do not remove borrowed assets or cache entries from Assets explicitly.
pub(super) fn retain(world:&mut World,host:SourceSceneHost,policy:RuntimePolicy)->Result<(),String>{
 world.init_resource::<SourceSceneResidents>();
 if world.resource::<SourceSceneResidents>().scenes.contains_key(&host.root){
  return Err("A source host already owns this render root".into());
 }
 if world.get::<super::source_graph_scene::SourceSceneAttached>(host.root).is_none(){
  return Err("Source root is not attached".into());
 }
 world.resource_mut::<SourceSceneResidents>().scenes.insert(host.root,Resident{host,policy});Ok(())
}
/// Schedule after TransformSystems::Propagate, before render extraction.
/// The common viewport must explicitly supply its current/previous clock.
pub(super) fn update(world:&mut World){
 if !world.contains_resource::<SourceSceneResidents>(){return;}
 let clock=world.get_resource::<SourceClock>().copied();
 world.resource_scope(|world,residents:Mut<SourceSceneResidents>|{
  let mut residents=residents;
  residents.scenes.retain(|root,resident|{
   // Despawned roots release handles naturally through Bevy's asset lifecycle.
   if world.get_entity(*root).is_err(){return false;}
   let result=clock.ok_or_else(||"Explicit source viewport clock unavailable".to_owned())
    .and_then(|clock|resident.policy.frame(clock))
    .and_then(|frame|resident.host.update_runtime_frame(world,&frame));
   match result{
    Ok(_)=>{world.entity_mut(*root).insert(SourceFrameStatus::Updated);}
    Err(error)=>{world.entity_mut(*root).insert((Visibility::Hidden,SourceFrameStatus::Refused(error)));}
   }
   true
  });
 });
}
'''
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists();ROOT.mkdir();candidate=ROOT/'source_graph_runtime.rs';candidate.write_text(CODE,'utf-8')
    files=[Path(__file__).resolve(),candidate,*[R/'apps/forge_studio/src/authoring_assets'/n for n in ['source_graph_host.rs','source_graph_context.rs','source_graph_frame.rs','source_graph_scene.rs']]]
    receipt=dict(status='shared_source_host_lifecycle_candidate_prepared',prepared_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in files},candidate=str(candidate),installed_in_product=False,native_tests_passed=False,gpu_rendered=False,editor_qualified=False,full_import=False,required_tests=['two independent root policies keep their own world mappings','current/previous clock updates actual material instances','missing/invalid clock hides root without advancing material','despawned root releases retained host handles without deleting borrowed assets'],next='Compile and test after current context/bootstrap jobs; wire into shared viewport schedule and actual preview/placement mount.')
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(receipt,indent=2)+'\n','utf-8');print(json.dumps(dict(status=receipt['status'],candidate=str(candidate))))
if __name__=='__main__':main()
