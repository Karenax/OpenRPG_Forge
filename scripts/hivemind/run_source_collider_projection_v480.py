"""Replay actual source projection and Rapier context using the warm dependency build."""
from pathlib import Path

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
parent = R / 'scripts/hivemind/run_gladiator_rapier_boxes_v475.py'
probe = R / 'scripts/hivemind/source_collider_projection_probe_v480.rs'
projection = R / 'crates/forge_engine/src/source_collider_projection.rs'
hierarchy = R / 'crates/forge_engine/src/source_graph_scene.rs'
code = parent.read_text('utf-8').replace('gladiator-rapier-box-query-v475', 'source-collider-projection-query-v480')
needle = "(O/'src/main.rs').write_text(source,'utf-8')"
addition = (
    "source=source.replace('*.5','*0.5')\n"
    "source=source.replace('let boxes=source_colliders::prepare(doc,basis).unwrap();',"
    "'let boxes=source_colliders::prepare(doc,basis).unwrap();exercise_projection(&glb,&native,basis);')\n"
    "source=source.replace('\"product_rapier_patch_used\":true,',"
    "'\"product_rapier_patch_used\":true,\"composed_context_queries_verified\":true,\"context_points\":boxes.len()*63,\"context_rays\":boxes.len()*18,\"projection_ownership_and_removal_verified\":true,')\n"
    "source+='\\n#[allow(dead_code)] #[path=\"' + " + repr(hierarchy.as_posix()) + " + '\"] mod source_graph_scene;\\n'\n"
    "source+='#[allow(dead_code)] #[path=\"' + " + repr(projection.as_posix()) + " + '\"] mod source_collider_projection;\\n'\n"
    "source+=Path(" + repr(str(probe)) + ").read_text('utf-8')\n" + needle
)
assert code.count(needle) == 1
code = code.replace(needle, addition)
needle = "pins={str(p):sha(p) for p in files}"
code = code.replace(needle, "files.extend([Path(" + repr(str(parent)) + "),Path(" + repr(str(probe)) + "),Path(" + repr(str(projection)) + "),Path(" + repr(str(hierarchy)) + ")])\n" + needle)
code = code.replace('three_common_rapier_boxes_match_63_unity_points_and_18_rays', 'five_actual_source_projection_contexts_verified')
code = code.replace('common_rapier_box_refusal_retained', 'source_projection_context_refusal_retained')
exec(compile(code, str(parent), 'exec'), {'__file__': __file__, '__name__': '__main__'})
