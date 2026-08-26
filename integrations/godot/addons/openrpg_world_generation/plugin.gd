@tool
extends EditorPlugin

const AUTOLOAD_NAME := "OpenRPGWorldGenerationBackend"
const AUTOLOAD_PATH := "res://addons/openrpg_world_generation/openrpg_world_generation_backend.gd"

func _enter_tree() -> void:
    if not ProjectSettings.has_setting("autoload/" + AUTOLOAD_NAME):
        add_autoload_singleton(AUTOLOAD_NAME, AUTOLOAD_PATH)

func _exit_tree() -> void:
    if ProjectSettings.has_setting("autoload/" + AUTOLOAD_NAME):
        remove_autoload_singleton(AUTOLOAD_NAME)
