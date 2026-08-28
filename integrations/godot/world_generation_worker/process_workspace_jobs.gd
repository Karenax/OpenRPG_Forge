extends SceneTree

const READY_FRAME_LIMIT := 900

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var runner: Node = null
    var backend: Node = null
    for _frame_index in READY_FRAME_LIMIT:
        await process_frame
        runner = root.get_node_or_null("OpenRPGWorldGenerationJobs")
        backend = root.get_node_or_null("OpenRPGWorldGenerationBackend")
        var flow_plugin := FlowPlugin.get_instance()
        if runner != null and backend != null and ClassDB.class_exists("Terrain3D") and \
                ResourceLoader.exists("res://addons/flow_nodes_editor/flow_node.gd") and \
                flow_plugin != null and flow_plugin.is_node_ready() and \
                flow_plugin.nodes_factory != null and flow_plugin.nodes_factory.node_types.size() > 0:
            break
    if runner == null or backend == null:
        _fail("The OpenRPG worker autoloads did not become ready.")
        return
    if not ClassDB.class_exists("Terrain3D"):
        _fail("The pinned Terrain3D provider did not become ready.")
        return
    if not ResourceLoader.exists("res://addons/flow_nodes_editor/flow_node.gd"):
        _fail("The pinned graph PCG provider did not become ready.")
        return
    var ready_flow_plugin := FlowPlugin.get_instance()
    if ready_flow_plugin == null or not ready_flow_plugin.is_node_ready() or \
            ready_flow_plugin.nodes_factory == null or ready_flow_plugin.nodes_factory.node_types.is_empty():
        _fail("The pinned graph PCG editor provider did not finish initialization.")
        return

    var result_variant: Variant = await runner.call("process_pending_jobs")
    if typeof(result_variant) != TYPE_DICTIONARY:
        _fail("The durable job runner returned no structured result.")
        return
    var result := result_variant as Dictionary
    if not bool(result.get("succeeded", false)):
        _fail("One or more durable jobs failed: %s" % JSON.stringify(result))
        return
    print(
        "OPENRPG_FORGE_WORLD_GENERATION_GODOT_WORKER_PASS processed=%d failed=%d directory=%s" % [
            int(result.get("processed", 0)),
            int(result.get("failed", 0)),
            str(result.get("directory", "unknown")),
        ])
    await _shutdown_editor_plugins(backend)
    quit(0)

func _fail(message: String) -> void:
    push_error(message)
    print("OPENRPG_FORGE_WORLD_GENERATION_GODOT_WORKER_FAILED reason=%s" % message.replace(" ", "_"))
    quit(1)

func _shutdown_editor_plugins(backend: Node) -> void:
    if not Engine.is_editor_hint():
        return
    if backend.has_method("release_forge_world_generation_editor_state"):
        backend.call("release_forge_world_generation_editor_state")
    for _frame_index in 3:
        await process_frame
    # Godot Flow owns editor controls that must be released before SceneTree teardown.
    # Disable it while those controls are still valid so its late save callback cannot
    # dereference an already freed graph dock.
    if EditorInterface.is_plugin_enabled("flow_nodes_editor"):
        EditorInterface.set_plugin_enabled("flow_nodes_editor", false)
    for _frame_index in 3:
        await process_frame
