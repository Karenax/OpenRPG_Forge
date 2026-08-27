extends SceneTree

func _initialize() -> void:
    await process_frame
    var version := Engine.get_version_info()
    if int(version.get("major", 0)) != 4 or int(version.get("minor", 0)) != 7 or \
            int(version.get("patch", 0)) != 1:
        _fail("Qualification requires Godot 4.7.1, received %s." % version)
        return

    var required_classes := ["Terrain3D", "Terrain3DData"]
    var missing := required_classes.filter(
        func(candidate: String) -> bool: return not ClassDB.class_exists(candidate))
    if not missing.is_empty():
        _fail("Provider classes did not load: %s" % missing)
        return

    var terrain: Object = ClassDB.instantiate("Terrain3D")
    var executor_script := load("res://addons/flow_nodes_editor/flow_node.gd") as Script
    var graph_executor: Object = executor_script.new() if executor_script != null and executor_script.can_instantiate() else null
    var baker_script := load("res://addons/openrpg_world_generation/neutral_world_baker.gd") as Script
    var baker: Object = baker_script.new() if baker_script != null and baker_script.can_instantiate() else null
    var runner_script := load("res://addons/openrpg_world_generation/world_generation_job_runner.gd") as Script
    var runner: Node = runner_script.new() as Node if runner_script != null and runner_script.can_instantiate() else null
    if not (terrain is Node3D) or not (graph_executor is Node3D):
        _fail("Provider classes loaded but could not be instantiated as Node3D.")
        return
    if not (baker is RefCounted):
        _fail("Neutral world baker could not be instantiated.")
        return
    if runner == null:
        _fail("World-generation job runner could not be instantiated.")
        return
    var terrain_evidence: Dictionary = runner.call("_provider_evidence_for_step",
        { "stage": "terrain", "backend": "terrain3D" }, { "succeeded": true })
    var graph_evidence: Dictionary = runner.call("_provider_evidence_for_step",
        { "stage": "biomes", "backend": "pcgGodot" }, { "succeeded": true })
    if str(terrain_evidence.get("providerKey", "")) != "terrain3d" or \
            str(graph_evidence.get("providerKey", "")) not in ["godot-flow", "gf-pcgodot"] or \
            str(terrain_evidence.get("evidenceSha256", "")).length() != 64 or \
            str(graph_evidence.get("evidenceSha256", "")).length() != 64:
        _fail("Installed provider receipt did not produce verified stage evidence: terrain=%s graph=%s" % [
            terrain_evidence, graph_evidence])
        return

    print("WORLD_GENERATION_PROVIDER_LOAD_PASS godot=4.7.1 terrain3d=1 flow_graph_node_3d=1 provider_receipt=1")
    terrain.free()
    graph_executor.free()
    runner.free()
    quit(0)

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
