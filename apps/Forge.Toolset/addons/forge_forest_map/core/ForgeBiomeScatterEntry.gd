## Adapted from GamesNotDeveloped/godot-forest-demo at commit
## 623212ffeb4efbf4c95323a4a6a57e726be4f6e9 (CC BY 4.0).
## See ../licenses/godot-forest-demo-CC-BY-4.0.txt and THIRD_PARTY_NOTICES.md.
@tool
class_name ForgeBiomeScatterEntry
extends Resource


func _emit_if_changed(current_value: Variant, next_value: Variant) -> Variant:
	if current_value == next_value:
		return current_value
	emit_changed()
	return next_value


@export var mesh: Mesh:
	set(value):
		mesh = _emit_if_changed(mesh, value)

@export var mesh_scene: PackedScene:
	set(value):
		mesh_scene = _emit_if_changed(mesh_scene, value)

@export var billboard_mesh: Mesh:
	set(value):
		billboard_mesh = _emit_if_changed(billboard_mesh, value)

@export var billboard_scene: PackedScene:
	set(value):
		billboard_scene = _emit_if_changed(billboard_scene, value)

@export_range(0.0, 1000.0, 0.01, "or_greater") var probability: float = 1.0:
	set(value):
		probability = _emit_if_changed(probability, maxf(value, 0.0))

@export_range(0.0, 100000.0, 0.1, "or_greater") var billboard_lod_distance: float = 60.0:
	set(value):
		billboard_lod_distance = _emit_if_changed(billboard_lod_distance, maxf(value, 0.0))

@export var scale_min: Vector3 = Vector3.ONE:
	set(value):
		scale_min = _emit_if_changed(scale_min, value)

@export var scale_max: Vector3 = Vector3.ONE:
	set(value):
		scale_max = _emit_if_changed(scale_max, value)
