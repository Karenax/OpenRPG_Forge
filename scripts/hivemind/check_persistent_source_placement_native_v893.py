"""Test actual placement ownership and pose code; no renderer or GUI claim.

The standalone harness supplies only the unrelated UI schedule labels and a
preview cancellation hook. It imports the actual physics and placement modules.
"""
from pathlib import Path

recipe = Path(__file__).with_name('check_source_resident_release_v871.py')
code = recipe.read_text('utf-8').replace('source-resident-release-tests-v871', 'persistent-source-placement-tests-v893')
code = code.replace('openrpg-source-resident-release-v871', 'openrpg-persistent-source-placement-v893')
code = code.replace("'runtime']", "'runtime','physics','placement']")
code = code.replace("cargo += '[lib]", "cargo = cargo.replace('[patch.crates-io]', 'forge_project={path=\"'+(R/'crates/forge_project').as_posix()+'\"}\\n[patch.crates-io]')\ncargo += '[lib]")
code = code.replace("(O/'src/lib.rs').write_text(source,'utf-8')", """source += '''
mod preview {
    #[derive(bevy::prelude::SystemSet,Debug,Clone,Copy,PartialEq,Eq,Hash)]
    pub enum PreviewSystems { Requests }
    pub fn cancel() {}
}
mod placements {
    #[derive(bevy::prelude::SystemSet,Debug,Clone,Copy,PartialEq,Eq,Hash)]
    pub enum PlacementSystems { Projection }
}
'''
(O/'src/lib.rs').write_text(source,'utf-8')""")
code = code.replace("'source_graph_runtime::tests::'", "'source_graph_placement::tests::'")
code = code.replace("'2 passed; 0 failed'", "'4 passed; 0 failed'")
code = code.replace("'release_preserves_source_hierarchy_and_foreign_children_and_is_idempotent',\n    'vanished_root_releases_an_owned_child_that_lost_its_parent'", """'vanished_root_cleans_detached_owned_nodes_and_physics_only',
    'invalid_pose_clears_derivatives_preserves_author_identity_and_refuses',
    'pose_composes_parent_rotation_and_nonuniform_scale_without_decomposition',
    'object_local_mapping_cannot_replace_explicit_world_mapping'""")
code = code.replace('actual_source_runtime_owned_release_and_orphan_cleanup_verified', 'actual_persistent_source_ownership_and_pose_tests_verified')
code = code.replace('two_actual_module_tests_executed', 'four_actual_module_tests_executed')
# Reuse the live native job's cache; Cargo serializes access while that job runs.
code = code.replace("str(W/'persistent-source-placement-tests-v893/target')", "str(W/'source-resident-release-tests-v871/target')")
exec(compile(code, str(recipe)+'#actual-persistent-placement-tests', 'exec'), globals())

