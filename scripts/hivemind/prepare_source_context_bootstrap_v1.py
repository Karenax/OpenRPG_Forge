"""Stage initial frame construction from the admitted hierarchy, not guessed poses."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'studio-source-context-bootstrap-v1'
CODE=r'''//! Initial contexts for the shared source host, before entity attachment.
//! Resident materials replace the initial decoder fields with admitted modes.
//! No registration, reveal, LOD selection or GPU qualification occurs here.
use super::source_graph_context::{HostFrame,ObjectBasis};
use bevy::prelude::*;
use forge_assets::source_graph_glb::SourceGraphGlb;
use forge_engine::{source_graph::SourceGraphUniforms,source_graph_scene_geometry::{SourceGraphSceneGeometry,ScenePrimitiveRoute}};
use std::collections::BTreeMap;

pub(super) fn initial_contexts(
    glb:&SourceGraphGlb<'_>,
    scene:&SourceGraphSceneGeometry,
    target_world_from_root:Mat4,
    frame:&HostFrame,
) -> Result<BTreeMap<(usize,usize),SourceGraphUniforms>,String> {
    let basis=ObjectBasis::from_document(glb.document())?
        .ok_or("Declared object mapping required for initial source frames")?;
    // The scene reader already admits parent-first, original local transforms.
    // Reject changed order/identity instead of silently using an identity parent.
    let mut globals=BTreeMap::new();
    for node in &scene.hierarchy.nodes {
        if globals.contains_key(&node.source_node) {
            return Err("Duplicate node in initial source hierarchy".into());
        }
        let parent=match node.parent_source_node {
            Some(index)=>*globals.get(&index).ok_or("Initial source parent absent or out of order")?,
            None=>target_world_from_root,
        };
        let declared=node.local_matrix;
        let composed=parent*declared;
        // Check through the same frame builder used after transform propagation.
        // Decoders are admitted by source_graph_resources, never chosen here.
        frame.uniform(basis,composed,[Vec4::ZERO;2])?;
        globals.insert(node.source_node,composed);
    }
    let mut contexts=BTreeMap::new();
    for primitive in &scene.instances {
        if let ScenePrimitiveRoute::SourceGraph{material}=primitive.route {
            let pose=*globals.get(&primitive.source_node).ok_or("Initial draw node absent")?;
            contexts.entry((primitive.source_node,material))
                .or_insert(frame.uniform(basis,pose,[Vec4::ZERO;2])?);
        }
    }
    if contexts.is_empty(){return Err("No source material instances in initial scene".into());}
    Ok(contexts)
}
'''
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists(),'Preserve candidate and receipts';ROOT.mkdir()
    candidate=ROOT/'source_graph_bootstrap.rs';candidate.write_text(CODE,'utf-8')
    files=[Path(__file__).resolve(),candidate,R/'apps/forge_studio/src/authoring_assets/source_graph_context.rs',R/'apps/forge_studio/src/authoring_assets/source_graph_resources.rs',R/'crates/forge_engine/src/source_graph_scene.rs',R/'crates/forge_engine/src/source_graph_scene_geometry.rs']
    result=dict(status='initial_context_candidate_prepared_native_tests_pending',prepared_utc=datetime.now(timezone.utc).isoformat(),candidate=str(candidate),input_pins={str(p):sha(p) for p in files},installed_in_product=False,native_tests_passed=False,gpu_rendered=False,studio_verified=False,full_import=False,next='After current native tests terminate, test actual hierarchy composition and presentation-offset cancellation; then wire shared host mount for preview and placements.')
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(result,indent=2)+'\n','utf-8')
    print(json.dumps(dict(status=result['status'],candidate=str(candidate))))
if __name__=='__main__':main()
