extends Node


func _ready() -> void:
    var arguments := OS.get_cmdline_user_args()
    if arguments.has("--generate-forest-map-fixture"):
        _generate_fixture.call_deferred()
        return
    if arguments.has("--forest-map-capture"):
        _capture.call_deferred()
        return
    if not arguments.has("--forest-map-smoke-test"):
        return
    _run.call_deferred()


func _generate_fixture() -> void:
    var map := get_parent() as ForgeForestMap
    if map == null or not map.create_mask_texture_file(map.mask_path, 512):
        _fail("fixture generation failed")
        return
    var image := map.get_mask_image_copy()
    map.set_active_paint_layer(ForgeForestMap.PaintLayer.CLEARING)
    map.paint_mask_circle_on_image(image, Vector3.ZERO, 20.0, 1.0, 0.8, 1.0)
    map.set_active_paint_layer(ForgeForestMap.PaintLayer.FLOWERS)
    map.paint_mask_circle_on_image(image, Vector3.ZERO, 15.0, 1.0, 0.25, 0.8)
    map.set_active_paint_layer(ForgeForestMap.PaintLayer.ROCKS)
    map.paint_mask_circle_on_image(image, Vector3(-24, 0, 18), 9.0, 1.0, 0.5, 0.9)
    map.paint_mask_circle_on_image(image, Vector3(28, 0, -20), 7.0, 1.0, 0.5, 0.8)
    map.set_mask_image(image, true)
    print("OPENRPG_FORGE_FOREST_MAP_FIXTURE_PASS mask=%s definition=%s" % [
        map.mask_path, map.definition_path
    ])
    get_tree().quit(0)


func _capture() -> void:
    for _frame in range(30):
        await get_tree().process_frame
    await RenderingServer.frame_post_draw
    var image := get_viewport().get_texture().get_image()
    var path := ProjectSettings.globalize_path("res://../../../artifacts/forest-map-capture.png")
    DirAccess.make_dir_recursive_absolute(path.get_base_dir())
    if image == null or image.is_empty() or image.save_png(path) != OK:
        _fail("capture failed")
        return
    print("OPENRPG_FORGE_FOREST_MAP_CAPTURE_PASS path=%s" % path)
    get_tree().quit(0)


func _run() -> void:
    for _frame in range(12):
        await get_tree().physics_frame
    var map := get_parent() as ForgeForestMap
    if map == null:
        _fail("map root missing")
        return
    var image := map.get_mask_image_copy()
    map.set_active_paint_layer(ForgeForestMap.PaintLayer.CLEARING)
    if not map.paint_mask_circle_on_image(image, Vector3.ZERO, 8.0, 1.0, 1.0, 1.0):
        _fail("clearing stroke changed no pixels")
        return
    var center := image.get_pixel(image.get_width() / 2, image.get_height() / 2)
    if center.r > 0.01 or center.g > 0.01 or center.b > 0.01:
        _fail("clearing channels were not erased")
        return
    map.set_active_paint_layer(ForgeForestMap.PaintLayer.FORBIDDEN)
    if not map.paint_mask_circle_on_image(image, Vector3.ZERO, 4.0, 1.0, 1.0, 1.0):
        _fail("forbidden stroke changed no pixels")
        return
    map.preview_mask_image(image)
    if map.sample_forbidden_at_world_position(map.global_position) < 0.99:
        _fail("forbidden zone sampling failed")
        return
    var forest := map.get_node_or_null("Forest") as ForgeBiomeScatter
    if forest != null:
        forest.regenerate()
        await get_tree().process_frame
    var generated := forest.get_node_or_null("__biomes_generated") if forest != null else null
    if generated == null or generated.get_child_count(true) == 0:
        _fail("forest scatter generated no chunks")
        return
    print("OPENRPG_FORGE_FOREST_MAP_SMOKE_PASS chunks=%s mask=%sx%s" % [
        generated.get_child_count(true), image.get_width(), image.get_height()
    ])
    get_tree().quit(0)


func _fail(message: String) -> void:
    push_error("OPENRPG_FORGE_FOREST_MAP_SMOKE_FAILED %s" % message)
    get_tree().quit(1)
