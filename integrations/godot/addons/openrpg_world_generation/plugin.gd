@tool
extends EditorPlugin

const BACKEND_AUTOLOAD_NAME := "OpenRPGWorldGenerationBackend"
const BACKEND_AUTOLOAD_PATH := "res://addons/openrpg_world_generation/openrpg_world_generation_backend.gd"
const RUNNER_AUTOLOAD_NAME := "OpenRPGWorldGenerationJobs"
const RUNNER_AUTOLOAD_PATH := "res://addons/openrpg_world_generation/world_generation_job_runner.gd"
const WORKSPACE_SETTING := "openrpg/world_generation/workspace_root"
const AUTO_PROCESS_SETTING := "openrpg/world_generation/auto_process_jobs"
const POLL_SECONDS_SETTING := "openrpg/world_generation/poll_seconds"

var _added_backend_autoload := false
var _added_runner_autoload := false

func _enter_tree() -> void:
    _define_setting(WORKSPACE_SETTING, "res://", TYPE_STRING)
    _define_setting(AUTO_PROCESS_SETTING, false, TYPE_BOOL)
    _define_setting(POLL_SECONDS_SETTING, 1.0, TYPE_FLOAT)
    if not ProjectSettings.has_setting("autoload/" + BACKEND_AUTOLOAD_NAME):
        add_autoload_singleton(BACKEND_AUTOLOAD_NAME, BACKEND_AUTOLOAD_PATH)
        _added_backend_autoload = true
    if not ProjectSettings.has_setting("autoload/" + RUNNER_AUTOLOAD_NAME):
        add_autoload_singleton(RUNNER_AUTOLOAD_NAME, RUNNER_AUTOLOAD_PATH)
        _added_runner_autoload = true

func _exit_tree() -> void:
    if _added_runner_autoload and _owns_autoload(RUNNER_AUTOLOAD_NAME, RUNNER_AUTOLOAD_PATH):
        remove_autoload_singleton(RUNNER_AUTOLOAD_NAME)
    if _added_backend_autoload and _owns_autoload(BACKEND_AUTOLOAD_NAME, BACKEND_AUTOLOAD_PATH):
        remove_autoload_singleton(BACKEND_AUTOLOAD_NAME)

func _owns_autoload(name: String, expected_path: String) -> bool:
    var key := "autoload/" + name
    return ProjectSettings.has_setting(key) and \
        str(ProjectSettings.get_setting(key, "")).trim_prefix("*") == expected_path

func _define_setting(key: String, default_value: Variant, value_type: int) -> void:
    if not ProjectSettings.has_setting(key):
        ProjectSettings.set_setting(key, default_value)
    ProjectSettings.add_property_info({
        "name": key,
        "type": value_type,
    })
    ProjectSettings.set_initial_value(key, default_value)
    ProjectSettings.save()
