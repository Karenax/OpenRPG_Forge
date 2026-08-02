@tool
extends EditorPlugin

const MAP_SCRIPT := preload("res://addons/forge_forest_map/ForgeForestMap.gd")
const SCATTER_SCRIPT := preload("res://addons/forge_forest_map/core/ForgeBiomeScatter.gd")
const ENTRY_SCRIPT := preload("res://addons/forge_forest_map/core/ForgeBiomeScatterEntry.gd")
const FOG_SCRIPT := preload("res://addons/forge_forest_map/core/ForgeBiomeFog.gd")
const CATALOG_RELATIVE_PATH := "../../asset_manifests/catalogs/cc0_3d_all.json"
const IMPORT_ROOT := "res://forest_maps/imported"
const ALLOWED_IMPORT_EXTENSIONS := [
    "gltf", "glb", "obj", "mtl", "bin", "png", "jpg", "jpeg", "webp", "tga", "exr", "hdr",
    "txt", "md", "json"
]

var _current_map: ForgeForestMap
var _panel: VBoxContainer
var _paint_toggle: CheckButton
var _layer_option: OptionButton
var _mode_option: OptionButton
var _brush_size: SpinBox
var _brush_hardness: SpinBox
var _brush_opacity: SpinBox
var _resolution_option: OptionButton
var _status_label: Label
var _asset_layer_option: OptionButton
var _asset_path_label: Label
var _mask_dialog: FileDialog
var _asset_dialog: FileDialog
var _catalog_dialog: Window
var _catalog_search: LineEdit
var _catalog_list: ItemList
var _catalog_status: Label
var _catalog_import_button: Button
var _catalog_assets: Array = []
var _stroke_active := false
var _stroke_before: Image
var _stroke_working: Image
var _last_painted_pixel := Vector2i(-1, -1)
var _hover_screen_position := Vector2.ZERO
var _hover_world_position: Variant = null


func _enter_tree() -> void:
    var icon := get_editor_interface().get_editor_theme().get_icon("Node3D", "EditorIcons")
    add_custom_type("ForgeBiomeScatterEntry", "Resource", ENTRY_SCRIPT, icon)
    add_custom_type("ForgeBiomeScatter", "Node3D", SCATTER_SCRIPT, icon)
    add_custom_type("ForgeBiomeFog", "Node", FOG_SCRIPT, icon)
    add_custom_type("ForgeForestMap", "Node3D", MAP_SCRIPT, icon)
    _build_panel()
    _build_dialogs()
    set_input_event_forwarding_always_enabled()
    set_force_draw_over_forwarding_enabled()
    print("OPENRPG_FORGE_FOREST_MAP_PLUGIN_LOADED")


func _exit_tree() -> void:
    remove_custom_type("ForgeForestMap")
    remove_custom_type("ForgeBiomeFog")
    remove_custom_type("ForgeBiomeScatter")
    remove_custom_type("ForgeBiomeScatterEntry")
    if _panel != null:
        remove_control_from_bottom_panel(_panel)
        _panel.queue_free()
    for dialog in [_mask_dialog, _asset_dialog, _catalog_dialog]:
        if dialog != null:
            dialog.queue_free()


func _handles(object: Object) -> bool:
    return object is ForgeForestMap


func _edit(object: Object) -> void:
    _current_map = object as ForgeForestMap
    _refresh_state()


func _make_visible(visible: bool) -> void:
    if _panel != null:
        _panel.visible = visible
    if visible:
        _refresh_state()


func _forward_3d_gui_input(camera: Camera3D, event: InputEvent) -> int:
    if event is InputEventMouse:
        _hover_screen_position = event.position
        _hover_world_position = _intersect_map_plane(camera, event.position) if _can_paint() else null
        update_overlays()
    if not _can_paint():
        return AFTER_GUI_INPUT_PASS
    if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
        if event.pressed:
            if _begin_stroke():
                _paint_at(camera, event.position)
                return AFTER_GUI_INPUT_STOP
        elif _stroke_active:
            _end_stroke()
            return AFTER_GUI_INPUT_STOP
    if event is InputEventMouseMotion and _stroke_active:
        _paint_at(camera, event.position)
        return AFTER_GUI_INPUT_STOP
    return AFTER_GUI_INPUT_PASS


func _forward_3d_draw_over_viewport(overlay: Control) -> void:
    if not _can_paint() or _hover_world_position == null:
        return
    var color := _layer_color(_layer_option.get_selected_id())
    if _mode_option.selected == 1:
        color = Color(1.0, 0.25, 0.25, 0.95)
    overlay.draw_arc(
        _hover_screen_position,
        maxf(float(_brush_size.value) * 6.0, 8.0),
        0.0,
        TAU,
        48,
        color,
        2.0,
        true
    )
    overlay.draw_circle(_hover_screen_position, 3.0, color)


func _build_panel() -> void:
    _panel = VBoxContainer.new()
    _panel.name = "Carte forêt"
    _panel.add_theme_constant_override("separation", 8)
    var title := Label.new()
    title.text = "CARTE FORÊT FORGE"
    title.tooltip_text = "Masque RGBA versionné : forêt, fleurs, rochers et zones interdites."
    _panel.add_child(title)

    var paint_row := HBoxContainer.new()
    _panel.add_child(paint_row)
    _paint_toggle = CheckButton.new()
    _paint_toggle.text = "Peindre dans la vue 3D"
    _paint_toggle.toggled.connect(func(_enabled: bool) -> void: update_overlays())
    paint_row.add_child(_paint_toggle)
    _layer_option = OptionButton.new()
    for layer in range(ForgeForestMap.PaintLayer.FORBIDDEN + 1):
        _layer_option.add_item(_layer_name(layer), layer)
    _layer_option.item_selected.connect(_on_layer_selected)
    paint_row.add_child(_layer_option)
    _mode_option = OptionButton.new()
    _mode_option.add_item("Peindre")
    _mode_option.add_item("Effacer")
    paint_row.add_child(_mode_option)

    _brush_size = _make_spinbox(0.25, 128.0, 0.25, 5.0, "Rayon en mètres")
    _brush_hardness = _make_spinbox(0.0, 1.0, 0.05, 0.45, "Dureté")
    _brush_opacity = _make_spinbox(0.0, 1.0, 0.05, 1.0, "Opacité")
    paint_row.add_child(_brush_size)
    paint_row.add_child(_brush_hardness)
    paint_row.add_child(_brush_opacity)

    var map_row := HBoxContainer.new()
    _panel.add_child(map_row)
    _resolution_option = OptionButton.new()
    for resolution in [256, 512, 1024, 2048]:
        _resolution_option.add_item("%s × %s" % [resolution, resolution], resolution)
    _resolution_option.selected = 1
    map_row.add_child(_resolution_option)
    var create_button := Button.new()
    create_button.text = "Créer le masque"
    create_button.pressed.connect(_on_create_mask_pressed)
    map_row.add_child(create_button)
    var save_button := Button.new()
    save_button.text = "Enregistrer la carte"
    save_button.pressed.connect(_on_save_map_pressed)
    map_row.add_child(save_button)

    var asset_separator := HSeparator.new()
    _panel.add_child(asset_separator)
    var asset_row := HBoxContainer.new()
    _panel.add_child(asset_row)
    var asset_label := Label.new()
    asset_label.text = "Modèle du calque"
    asset_row.add_child(asset_label)
    _asset_layer_option = OptionButton.new()
    for layer in [ForgeForestMap.PaintLayer.FOREST, ForgeForestMap.PaintLayer.FLOWERS, ForgeForestMap.PaintLayer.ROCKS]:
        _asset_layer_option.add_item(_layer_name(layer), layer)
    _asset_layer_option.item_selected.connect(func(_index: int) -> void: _refresh_asset_path())
    asset_row.add_child(_asset_layer_option)
    var resource_button := Button.new()
    resource_button.text = "Ressource du projet…"
    resource_button.pressed.connect(_on_choose_resource_pressed)
    asset_row.add_child(resource_button)
    var catalog_button := Button.new()
    catalog_button.text = "Bibliothèque Forge…"
    catalog_button.pressed.connect(_open_catalog)
    asset_row.add_child(catalog_button)
    _asset_path_label = Label.new()
    _asset_path_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
    _asset_path_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
    asset_row.add_child(_asset_path_label)

    _status_label = Label.new()
    _status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    _panel.add_child(_status_label)
    add_control_to_bottom_panel(_panel, "Carte forêt")
    _panel.visible = false


func _build_dialogs() -> void:
    _mask_dialog = FileDialog.new()
    _mask_dialog.access = FileDialog.ACCESS_RESOURCES
    _mask_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
    _mask_dialog.filters = PackedStringArray(["*.png ; Masque de carte PNG"])
    _mask_dialog.file_selected.connect(_on_mask_path_selected)
    get_editor_interface().get_base_control().add_child(_mask_dialog)

    _asset_dialog = FileDialog.new()
    _asset_dialog.access = FileDialog.ACCESS_RESOURCES
    _asset_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
    _asset_dialog.filters = PackedStringArray([
        "*.tscn,*.scn,*.glb,*.gltf,*.obj,*.mesh ; Modèles 3D Forge"
    ])
    _asset_dialog.file_selected.connect(_on_asset_resource_selected)
    get_editor_interface().get_base_control().add_child(_asset_dialog)

    _catalog_dialog = Window.new()
    _catalog_dialog.title = "Bibliothèque d’assets Forge"
    _catalog_dialog.size = Vector2i(820, 600)
    _catalog_dialog.min_size = Vector2i(640, 420)
    _catalog_dialog.visible = false
    var margin := MarginContainer.new()
    margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    margin.add_theme_constant_override("margin_left", 16)
    margin.add_theme_constant_override("margin_top", 16)
    margin.add_theme_constant_override("margin_right", 16)
    margin.add_theme_constant_override("margin_bottom", 16)
    _catalog_dialog.add_child(margin)
    var content := VBoxContainer.new()
    margin.add_child(content)
    _catalog_search = LineEdit.new()
    _catalog_search.placeholder_text = "Rechercher arbres, fleurs, rochers…"
    _catalog_search.text_changed.connect(func(_text: String) -> void: _filter_catalog())
    content.add_child(_catalog_search)
    _catalog_list = ItemList.new()
    _catalog_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
    _catalog_list.item_selected.connect(func(_index: int) -> void: _refresh_catalog_selection())
    _catalog_list.item_activated.connect(func(_index: int) -> void: _import_selected_catalog_asset())
    content.add_child(_catalog_list)
    _catalog_status = Label.new()
    _catalog_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
    content.add_child(_catalog_status)
    var buttons := HBoxContainer.new()
    buttons.alignment = BoxContainer.ALIGNMENT_END
    content.add_child(buttons)
    var close_button := Button.new()
    close_button.text = "Fermer"
    close_button.pressed.connect(func() -> void: _catalog_dialog.hide())
    buttons.add_child(close_button)
    _catalog_import_button = Button.new()
    _catalog_import_button.text = "Importer et utiliser"
    _catalog_import_button.disabled = true
    _catalog_import_button.pressed.connect(_import_selected_catalog_asset)
    buttons.add_child(_catalog_import_button)
    get_editor_interface().get_base_control().add_child(_catalog_dialog)


func _make_spinbox(minimum: float, maximum: float, step: float, value: float, tooltip: String) -> SpinBox:
    var spin := SpinBox.new()
    spin.min_value = minimum
    spin.max_value = maximum
    spin.step = step
    spin.value = value
    spin.custom_minimum_size = Vector2(92.0, 0.0)
    spin.tooltip_text = tooltip
    return spin


func _refresh_state() -> void:
    var has_map := _current_map != null and is_instance_valid(_current_map)
    if _paint_toggle != null:
        _paint_toggle.disabled = not has_map
    if not has_map:
        _status_label.text = "Ouvrez la scène forge_forest_map_demo.tscn puis sélectionnez ForgeForestMap."
        _asset_path_label.text = "Aucune carte sélectionnée"
        return
    var path := _current_map.get_mask_texture_path()
    _status_label.text = (
        "Calque %s · masque %s · clic gauche pour peindre."
        % [_layer_name(_layer_option.get_selected_id()), path if not path.is_empty() else "non enregistré"]
    )
    _refresh_asset_path()


func _refresh_asset_path() -> void:
    if _current_map == null or not is_instance_valid(_current_map):
        return
    var layer := _asset_layer_option.get_selected_id()
    var path := _current_map.get_layer_resource_path(layer)
    _asset_path_label.text = path if not path.is_empty() else "Primitives Forge"
    _asset_path_label.tooltip_text = _asset_path_label.text


func _on_layer_selected(index: int) -> void:
    if _current_map != null:
        _current_map.set_active_paint_layer(_layer_option.get_item_id(index))
    _refresh_state()
    update_overlays()


func _on_create_mask_pressed() -> void:
    if _current_map == null:
        return
    _mask_dialog.current_dir = "res://forest_maps"
    _mask_dialog.current_file = "%s-mask.png" % _current_map.map_id
    _mask_dialog.popup_centered_ratio(0.65)


func _on_mask_path_selected(path: String) -> void:
    if _current_map == null:
        return
    if _current_map.create_mask_texture_file(path, _resolution_option.get_selected_id()):
        get_editor_interface().get_resource_filesystem().scan()
        _status_label.text = "Masque créé : %s" % path
    else:
        _status_label.text = "Impossible de créer le masque : %s" % path


func _on_save_map_pressed() -> void:
    if _current_map == null:
        return
    if _current_map.save_map_definition():
        _status_label.text = "Carte enregistrée : %s" % _current_map.definition_path
    else:
        _status_label.text = "Échec de l’enregistrement de la carte."


func _on_choose_resource_pressed() -> void:
    if _current_map == null:
        return
    _asset_dialog.current_dir = "res://"
    _asset_dialog.popup_centered_ratio(0.75)


func _on_asset_resource_selected(path: String) -> void:
    if _current_map == null:
        return
    var resource := ResourceLoader.load(path)
    if resource != null and _current_map.set_layer_resource(_asset_layer_option.get_selected_id(), resource):
        _status_label.text = "Modèle remplacé par %s" % path
        _refresh_asset_path()
    else:
        _status_label.text = "La ressource choisie n’est pas un modèle 3D compatible."


func _can_paint() -> bool:
    return (
        _current_map != null
        and is_instance_valid(_current_map)
        and _paint_toggle.button_pressed
        and not _current_map.get_mask_texture_path().is_empty()
    )


func _begin_stroke() -> bool:
    if not _can_paint():
        return false
    _stroke_before = _current_map.get_mask_image_copy()
    _stroke_working = _stroke_before.duplicate()
    _stroke_active = true
    _last_painted_pixel = Vector2i(-1, -1)
    return true


func _end_stroke() -> void:
    if not _stroke_active:
        return
    _stroke_active = false
    if _stroke_before.get_data() == _stroke_working.get_data():
        return
    var undo_redo := get_undo_redo()
    undo_redo.create_action("Peindre %s" % _layer_name(_layer_option.get_selected_id()))
    undo_redo.add_do_method(_current_map, "set_mask_image", _stroke_working, true)
    undo_redo.add_undo_method(_current_map, "set_mask_image", _stroke_before, true)
    undo_redo.commit_action()


func _paint_at(camera: Camera3D, screen_position: Vector2) -> void:
    var world_position := _intersect_map_plane(camera, screen_position)
    if world_position == null:
        return
    var local_position := _current_map.to_local(world_position as Vector3)
    var uv := _current_map.local_to_mask_uv(local_position)
    var pixel := Vector2i(
        int(round(uv.x * float(_stroke_working.get_width() - 1))),
        int(round(uv.y * float(_stroke_working.get_height() - 1)))
    )
    if pixel == _last_painted_pixel:
        return
    _last_painted_pixel = pixel
    _current_map.set_active_paint_layer(_layer_option.get_selected_id())
    if _current_map.paint_mask_circle_on_image(
        _stroke_working,
        local_position,
        _brush_size.value,
        1.0 if _mode_option.selected == 0 else 0.0,
        _brush_hardness.value,
        _brush_opacity.value
    ):
        _current_map.preview_mask_image(_stroke_working)


func _intersect_map_plane(camera: Camera3D, screen_position: Vector2) -> Variant:
    if _current_map == null:
        return null
    var origin := camera.project_ray_origin(screen_position)
    var direction := camera.project_ray_normal(screen_position)
    var plane_origin := _current_map.global_position
    var plane_normal := (_current_map.global_basis.orthonormalized() * Vector3.UP).normalized()
    var denominator := plane_normal.dot(direction)
    if absf(denominator) < 0.0001:
        return null
    var distance := plane_normal.dot(plane_origin - origin) / denominator
    return null if distance < 0.0 else origin + direction * distance


func _open_catalog() -> void:
    if _current_map == null:
        return
    if _catalog_assets.is_empty():
        _load_catalog()
    _filter_catalog()
    _catalog_dialog.popup_centered()


func _load_catalog() -> void:
    var catalog_path := ProjectSettings.globalize_path("res://%s" % CATALOG_RELATIVE_PATH)
    if not FileAccess.file_exists(catalog_path):
        _catalog_status.text = "Catalogue introuvable : %s" % catalog_path
        return
    var parsed := JSON.parse_string(FileAccess.get_file_as_string(catalog_path))
    if not parsed is Dictionary or not parsed.has("assets"):
        _catalog_status.text = "Catalogue Forge invalide."
        return
    _catalog_assets = parsed["assets"]


func _filter_catalog() -> void:
    _catalog_list.clear()
    var query := _catalog_search.text.strip_edges().to_lower()
    var shown := 0
    for index in range(_catalog_assets.size()):
        var asset: Dictionary = _catalog_assets[index]
        var haystack := "%s %s %s" % [
            asset.get("name", ""),
            " ".join(asset.get("tags", [])),
            " ".join(asset.get("categories", []))
        ]
        if not query.is_empty() and not haystack.to_lower().contains(query):
            continue
        _catalog_list.add_item("%s  ·  %s  ·  %s" % [
            asset.get("name", asset.get("asset_id", "asset")),
            asset.get("source", "source"),
            asset.get("license", "licence inconnue")
        ])
        _catalog_list.set_item_metadata(_catalog_list.item_count - 1, index)
        shown += 1
        if shown >= 300:
            break
    _catalog_status.text = "%s résultat(s) affiché(s) sur %s assets." % [shown, _catalog_assets.size()]
    _catalog_import_button.disabled = true


func _refresh_catalog_selection() -> void:
    var selected := _catalog_list.get_selected_items()
    _catalog_import_button.disabled = selected.is_empty()
    if selected.is_empty():
        return
    var asset: Dictionary = _catalog_assets[_catalog_list.get_item_metadata(selected[0])]
    _catalog_status.text = "%s · %s · %s" % [
        asset.get("asset_id", ""), asset.get("license", ""), asset.get("source_page", "")
    ]


func _import_selected_catalog_asset() -> void:
    var selected := _catalog_list.get_selected_items()
    if selected.is_empty() or _current_map == null:
        return
    var asset: Dictionary = _catalog_assets[_catalog_list.get_item_metadata(selected[0])]
    if not bool(asset.get("license_verified", false)):
        _catalog_status.text = "Import refusé : licence non vérifiée."
        return
    var model_relative := _catalog_model_relative_path(asset)
    var vault_root := _asset_vault_root()
    var source_model := vault_root.path_join(model_relative)
    if model_relative.is_empty() or not FileAccess.file_exists(source_model):
        _catalog_status.text = (
            "Asset catalogué mais non installé dans %s. Configurez OPENRPG_ASSET_VAULT ou téléchargez-le."
            % vault_root
        )
        return
    var slug := _sanitize_slug(str(asset.get("asset_id", "asset")))
    var target_res_dir := IMPORT_ROOT.path_join(slug)
    var target_absolute := ProjectSettings.globalize_path(target_res_dir)
    if not _copy_asset_directory(source_model.get_base_dir(), target_absolute):
        _catalog_status.text = "Échec de la copie de l’asset dans le projet."
        return
    _write_provenance(target_absolute.path_join("provenance.json"), asset)
    var target_resource := target_res_dir.path_join(source_model.get_file())
    _catalog_status.text = "Import Godot en cours : %s" % target_resource
    get_editor_interface().get_resource_filesystem().scan()
    await _wait_for_import()
    var resource := ResourceLoader.load(target_resource)
    if resource == null or not _current_map.set_layer_resource(_asset_layer_option.get_selected_id(), resource):
        _catalog_status.text = "Import terminé, mais le modèle n’est pas chargeable : %s" % target_resource
        return
    _catalog_status.text = "Asset utilisé : %s" % asset.get("name", asset.get("asset_id", "asset"))
    _status_label.text = _catalog_status.text
    _refresh_asset_path()


func _catalog_model_relative_path(asset: Dictionary) -> String:
    var extracted_root := str(asset.get("extracted_root", ""))
    var models: Array = asset.get("model_files", [])
    if not extracted_root.is_empty() and not models.is_empty():
        return extracted_root.path_join(str(models[0]))
    for file_value in asset.get("files", []):
        var path := str(file_value.get("path", ""))
        if path.get_extension().to_lower() in ["gltf", "glb", "obj"]:
            return path
    return ""


func _asset_vault_root() -> String:
    var configured := OS.get_environment("OPENRPG_ASSET_VAULT")
    if not configured.is_empty():
        return configured
    return ProjectSettings.globalize_path("res://../../../OpenRPG_AssetVault")


func _copy_asset_directory(source_dir: String, target_dir: String) -> bool:
    if DirAccess.make_dir_recursive_absolute(target_dir) != OK:
        return false
    var directory := DirAccess.open(source_dir)
    if directory == null:
        return false
    directory.list_dir_begin()
    var name := directory.get_next()
    while not name.is_empty():
        if name != "." and name != ".." and not directory.is_link(name):
            var source := source_dir.path_join(name)
            var target := target_dir.path_join(name)
            if directory.current_is_dir():
                if not _copy_asset_directory(source, target):
                    directory.list_dir_end()
                    return false
            elif name.get_extension().to_lower() in ALLOWED_IMPORT_EXTENSIONS:
                if DirAccess.copy_absolute(source, target) != OK:
                    directory.list_dir_end()
                    return false
        name = directory.get_next()
    directory.list_dir_end()
    return true


func _write_provenance(path: String, asset: Dictionary) -> void:
    var record := asset.duplicate(true)
    record["forge_import"] = {
        "layer": _layer_name(_asset_layer_option.get_selected_id()),
        "map_id": _current_map.map_id
    }
    var file := FileAccess.open(path, FileAccess.WRITE)
    if file != null:
        file.store_string(JSON.stringify(record, "  ") + "\n")
        file.close()


func _wait_for_import() -> void:
    var filesystem := get_editor_interface().get_resource_filesystem()
    for _attempt in range(100):
        if not filesystem.is_scanning():
            return
        await get_tree().create_timer(0.1).timeout


func _layer_name(layer: int) -> String:
    match layer:
        ForgeForestMap.PaintLayer.FOREST:
            return "Forêt"
        ForgeForestMap.PaintLayer.CLEARING:
            return "Clairière"
        ForgeForestMap.PaintLayer.FLOWERS:
            return "Fleurs"
        ForgeForestMap.PaintLayer.ROCKS:
            return "Rochers"
        ForgeForestMap.PaintLayer.FORBIDDEN:
            return "Zone interdite"
    return "Inconnue"


func _layer_color(layer: int) -> Color:
    match layer:
        ForgeForestMap.PaintLayer.FOREST:
            return Color(0.2, 0.85, 0.3, 0.95)
        ForgeForestMap.PaintLayer.CLEARING:
            return Color(0.95, 0.78, 0.28, 0.95)
        ForgeForestMap.PaintLayer.FLOWERS:
            return Color(0.95, 0.35, 0.8, 0.95)
        ForgeForestMap.PaintLayer.ROCKS:
            return Color(0.65, 0.7, 0.78, 0.95)
        ForgeForestMap.PaintLayer.FORBIDDEN:
            return Color(1.0, 0.18, 0.18, 0.95)
    return Color.WHITE


func _sanitize_slug(value: String) -> String:
    var result := ""
    for character in value.to_lower():
        var code := character.unicode_at(0)
        if (code >= 97 and code <= 122) or (code >= 48 and code <= 57):
            result += character
        else:
            result += "-"
    while result.contains("--"):
        result = result.replace("--", "-")
    result = result.trim_prefix("-").trim_suffix("-")
    return "asset" if result.is_empty() else result
