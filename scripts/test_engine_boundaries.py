"""Architecture regressions: product build inputs must be owned by the product.

Run from any directory with Python's unittest runner. No build or GPU is started.
"""
import hashlib
import importlib.util
from pathlib import Path
import re
import tempfile
import tomllib
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
EXAMPLES = ROOT / "crates/forge_tidewater/examples"
MODULE_GAME = ROOT / "apps/forge_runtime/src/module_game"


def rust_body(path):
    # Only standalone comments/blank lines differ in the legacy copies covered
    # here. The actual Rust/WGSL tokens must still be reviewed before factoring.
    return "\n".join(
        line.strip() for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.lstrip().startswith("//")
    )


def common_dependency_body(path, source):
    """Inspect code dependencies without erasing upstream notices/wire identities.

    Only the existing source codec and its two conformance fixtures may retain
    named legacy string values. They must not inspect module/map identities.
    """
    body = "\n".join(line for line in source.splitlines()
                     if not line.lstrip().startswith("//"))
    if re.search(r"\bforge_tidewater(?:_assets)?\b", body, re.I):
        raise ValueError(f"Named map crate dependency: {path}")
    for included in re.findall(
        r'(?:include(?:_str|_bytes)?!\s*\(\s*|#\[path\s*=\s*)"([^"]+)"', body
    ):
        if "tidewater" in included.lower():
            raise ValueError(f"Compiled named map input: {path}")
    relative = path.relative_to(ROOT).as_posix()
    # These two exact literals reproduce attribution already present in the
    # embedded shaders. They are not module imports or runtime selection by
    # map identity. Do not mask any other named-map text in these files.
    attribution_headers = {
        "crates/forge_engine/src/source_breaker_render.rs":
            r'const SOURCE_HEADER: &str = "// SPDX-License-Identifier: MIT\n// Adapted from Tidewater breaking lip, copyright 2026 DRG Software Solutions LLC.\n// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.\n";',
        "crates/forge_engine/src/source_spray_render.rs":
            r'const SOURCE_HEADER: &str = "// SPDX-License-Identifier: MIT\n// Adapted from Tidewater spray sprites, copyright 2026 DRG Software Solutions LLC.\n// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.\n";',
    }
    if relative in attribution_headers:
        body = body.replace(attribution_headers[relative], "", 1)
    # The old on-disk schema and licence filename are immutable compatibility
    # values. These are test fixtures, not selectors for the active module.
    legacy_fixtures = {
        "crates/forge_engine/src/source_terrain/programs.rs",
        "crates/forge_assets/tests/terrain_surface_package.rs",
    }
    if relative in legacy_fixtures:
        if re.search(r"\b(?:module_id|area_id|module_name|module_slug|map_name|slug)\b", body):
            raise ValueError(f"Legacy fixture branches on map/module identity: {path}")
        for wire_value in ('"LICENSE-Tidewater.txt"', '"forge.tidewater.terrain-package"'):
            body = body.replace(wire_value, '""')
    if relative == "crates/forge_engine/src/source_shadows/heightfield/tests.rs":
        body = body.replace(
            '#[ignore = "requires the explicitly pinned Tidewater source terrain"]',
            '#[ignore = "requires pinned reference terrain"]',
            1,
        )
    source_codec = (relative == "crates/forge_assets/src/source_surface_package.rs"
        or relative == "crates/forge_assets/src/source_terrain_package.rs"
        or relative == "crates/forge_assets/src/source_ocean_package.rs"
        or relative == "crates/forge_assets/src/source_shore_sim_package.rs"
        or relative == "crates/forge_assets/src/source_water_surface_package.rs"
        or relative == "crates/forge_assets/src/source_environment_package.rs"
        or relative.startswith("crates/forge_assets/src/source_environment_package/")
        or relative == "crates/forge_assets/src/source_vegetation_package.rs"
        or relative.startswith("crates/forge_assets/src/source_vegetation_package/")
        or relative in {"crates/forge_assets/tests/source_vegetation_package.rs",
                        "crates/forge_assets/tests/support/plant_package.rs"})
    if source_codec:
        if re.search(r"\b(?:module_id|area_id|module_name|module_slug|map_name|slug)\b", body):
            raise ValueError(f"Source codec branches on map/module identity: {path}")
        # Preserve exact admitted schema IDs, profile keys, errors and licence
        # filenames. Rewriting these would break pinned packages and provenance.
        body = re.sub(r'"(?:\\.|[^"\\])*"',
            lambda m: '""' if "tidewater" in m[0].lower() else m[0], body)
    return body.lower()


class EngineBoundaries(unittest.TestCase):
    def test_boat_projection_is_shared_with_module_adapter(self):
        engine = rust_body(ROOT / "crates/forge_engine/src/source_boat.rs")
        adapter = rust_body(ROOT / "crates/forge_tidewater/src/tidewater_boat.rs")
        self.assertIn("pub struct ResidentBoat", engine)
        self.assertIn("pub fn from_profile(", engine)
        self.assertIn("source_boat::PreparedBoat::from_profile", adapter)
        self.assertNotIn("pub struct ResidentBoat", adapter)

    def test_fish_profile_preparation_is_shared_with_studio(self):
        engine = ROOT / "crates/forge_engine/src/source_fish/host.rs"
        self.assertTrue(engine.is_file())
        self.assertIn("pub struct PreparedProfile", rust_body(engine))
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/fish_props_scene.rs")
        self.assertIn("PreparedProfile::new_selected", studio)
        self.assertIn("PreparedScene::new", studio)
        self.assertNotIn("fish_material::stage_shader(", studio)

    def test_managed_fish_uses_the_common_live_host(self):
        host = ROOT / "crates/forge_engine/src/source_fish/host/live.rs"
        self.assertTrue(host.is_file())
        common_dependency_body(host, host.read_text(encoding="utf-8"))
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_module_viewport.rs")
        game = rust_body(ROOT / "apps/forge_runtime/src/module_game/manual.rs")
        self.assertIn("fish::install(", studio)
        self.assertIn("fish_live::install(", game)
        self.assertIn("fish_scene::from_catalogs(", studio)
        self.assertIn("fish_scene::from_catalogs(", game)
        self.assertNotIn("forge_tidewater", studio)
        self.assertNotIn("forge_tidewater", game)

    def test_reference_fish_uses_common_residency(self):
        host = ROOT / "crates/forge_engine/src/source_fish/resident.rs"
        common_dependency_body(host, host.read_text(encoding="utf-8"))
        reference = rust_body(EXAMPLES / "tidewater_plant_probe/source_fish.rs")
        self.assertIn("resident::stage_instances(", reference)
        self.assertIn("live.resident.update_view(", reference)
        self.assertNotIn("group.may_contribute(", reference)
        self.assertNotIn("lod::POLICY.select(", reference)

    def test_fish_shadow_geometry_cache_is_shared(self):
        engine = ROOT / "crates/forge_engine/src/source_fish/shadow/cache.rs"
        self.assertTrue(engine.is_file())
        self.assertIn("pub struct MaskedFishCache", rust_body(engine))
        for relative in ("apps/forge_studio/src/authoring_assets/fish_props_scene.rs",
                         "apps/forge_studio/src/authoring_assets/fish_stand_scene/lod.rs"):
            source = rust_body(ROOT / relative)
            self.assertIn("shadow::cache::MaskedFishCache", source)
            self.assertNotIn("shadow::masked_mesh", source)

    def test_full_stall_material_is_shared_with_studio(self):
        engine = ROOT / "crates/forge_engine/src/source_surface"
        for name in ("stall_material.rs", "stall_scene.rs", "stall_shader.rs",
                     "stall_full_library.wgsl", "stall_contact_shadow.wgsl"):
            self.assertTrue((engine / name).is_file())
        material = rust_body(engine / "stall_material.rs")
        self.assertIn("pub struct FullMaterial", material)
        self.assertIn("pub struct BasicMaterial", material)
        self.assertIn("pub struct TextureArrays", material)
        adapter = rust_body(ROOT / "crates/forge_tidewater/src/tidewater_stall/full_material.rs")
        self.assertNotIn("pub struct FullMaterial", adapter)
        self.assertIn("stall_material::prepare_render_owned", adapter)
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/fish_stand_scene/full.rs")
        self.assertIn("source_surface::stall_material", studio)
        self.assertNotIn("tidewater_stall::full_material", studio)
        basic = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/fish_stand_scene.rs")
        self.assertIn("stall_material::BasicMaterial", basic)
        self.assertNotIn("tidewater_stall::FishStandMaterial", basic)

    def test_scene_clock_acquisition_is_shared_with_studio(self):
        owner = ROOT / "crates/forge_engine/src/source_wind/acquisition.rs"
        body = common_dependency_body(owner, owner.read_text(encoding="utf-8"))
        self.assertIn("pub struct acquiredframe", body)
        self.assertIn("pub fn from_json(", body)
        adapter = ROOT / "crates/forge_tidewater/src/scene_wind.rs"
        self.assertIn("pub use forge_engine::source_wind::AcquiredFrame;", rust_body(adapter))
        reference_test = ROOT / "crates/forge_tidewater/src/scene_wind/acquisition.rs"
        self.assertNotIn("impl AcquiredFrame", rust_body(reference_test))
        for name in (
            "apps/forge_studio/src/authoring_assets/imported_scene/wind.rs",
            "apps/forge_studio/src/authoring_imported_plants/lighting.rs",
            "apps/forge_studio/src/authoring_imported_shadows.rs",
        ):
            with self.subTest(path=name):
                source = rust_body(ROOT / name)
                self.assertIn("forge_engine::source_wind::AcquiredFrame", source)
                self.assertNotIn("forge_tidewater::scene_wind", source)

    def test_native_module_entry_is_owned_by_the_common_runtime(self):
        self.assertTrue(MODULE_GAME.is_dir())
        self.assertFalse((EXAMPLES / "module_game").exists())
        manifest = (ROOT / "apps/forge_runtime/Cargo.toml").read_text(encoding="utf-8")
        self.assertNotIn("forge_tidewater", manifest.lower())
        self.assertIn('name = "forge-module"', manifest)
        library = rust_body(ROOT / "apps/forge_runtime/src/lib.rs")
        self.assertIn("pub mod module_game;", library)
        standalone = rust_body(ROOT / "apps/forge_runtime/src/bin/forge-module.rs")
        self.assertIn("forge_runtime::module_game::run(", standalone)
        reference = rust_body(EXAMPLES / "tidewater_plant_probe.rs")
        self.assertIn("use forge_runtime::module_game;", reference)
        self.assertNotIn("mod module_game;", reference)
        files = sorted(MODULE_GAME.rglob("*.rs"))
        self.assertTrue(files)
        for path in files:
            with self.subTest(path=path.relative_to(ROOT)):
                self.assertNotIn("tidewater", common_dependency_body(path, path.read_text(encoding="utf-8")))

    def test_native_building_projection_has_one_common_owner(self):
        owner = ROOT / "crates/forge_engine/src/runtime_buildings.rs"
        source = owner.read_text(encoding="utf-8")
        self.assertNotIn("tidewater", common_dependency_body(owner, source))
        self.assertIn("pub fn install(", rust_body(owner))
        self.assertIn("pub fn observe(", rust_body(owner))
        module = MODULE_GAME
        self.assertFalse((module / "buildings.rs").exists())
        self.assertNotIn("mod buildings;", rust_body(module / "mod.rs"))
        self.assertIn("forge_engine::runtime_buildings::install(", rust_body(module / "scene.rs"))
        self.assertIn("forge_engine::runtime_buildings::observe(", rust_body(module / "species.rs"))
        self.assertIn("forge_engine::runtime_buildings::observe(", rust_body(module / "evidence/collect.rs"))

    def test_native_player_static_admission_has_one_common_owner(self):
        owner = ROOT / "crates/forge_engine/src/runtime_static_admission.rs"
        source = owner.read_text(encoding="utf-8")
        self.assertNotIn("tidewater", common_dependency_body(owner, source))
        body = rust_body(owner)
        self.assertIn("pub fn install_static_admission(", body)
        self.assertIn(".after(PlayerCellStreamingSet::Request)", body)
        self.assertIn(".before(NativeGameplayReadinessSet)", body)
        self.assertIn(".before(PlayerControllerSet::Drive)", body)
        module = MODULE_GAME
        self.assertNotIn("fn hold_until_static_ready(", rust_body(module / "streamed.rs"))
        self.assertNotIn("struct StaticAdmission", rust_body(module / "streamed.rs"))
        self.assertIn("forge_engine::runtime_static_admission::install_static_admission_with_species(",
                      rust_body(module / "scene.rs"))

    def test_amphibious_locomotion_is_shared_without_map_dependency(self):
        engine = ROOT / "crates/forge_engine/src/amphibious_locomotion.rs"
        profile = ROOT / "crates/forge_engine/src/amphibious_locomotion/profile.rs"
        adapter = ROOT / "crates/forge_tidewater/src/amphibious_locomotion.rs"
        self.assertNotIn("tidewater", common_dependency_body(engine, engine.read_text(encoding="utf-8")))
        self.assertNotIn("tidewater", common_dependency_body(profile, profile.read_text(encoding="utf-8")))
        self.assertIn("pub use forge_engine::amphibious_locomotion::*;", rust_body(adapter))
        self.assertFalse((adapter.parent / "amphibious_locomotion/profile.rs").exists())

    def test_amphibious_controller_is_engine_owned_and_reference_fixtures_stay_map_owned(self):
        engine = ROOT / "crates/forge_engine/src/amphibious_player.rs"
        adapter = ROOT / "crates/forge_tidewater/src/amphibious_player.rs"
        body = common_dependency_body(engine, engine.read_text(encoding="utf-8"))
        self.assertNotIn("tidewater", body)
        self.assertIn("pub struct amphibiousplayerplugin", body)
        self.assertIn("pub use forge_engine::amphibious_player::*;", rust_body(adapter))
        self.assertIn('include!("../../forge_engine/src/amphibious_player.rs");', rust_body(adapter))
        self.assertTrue((adapter.parent / "amphibious_player_tests.rs").is_file())

    def test_module_player_uses_the_common_controller_with_or_without_water(self):
        runtime = rust_body(MODULE_GAME / "scene.rs")
        self.assertIn("forge_engine::amphibious_player::configure_player_profile(&mut profile);", runtime)
        self.assertIn("AmphibiousPlayerPlugin::new(queries, plan)?", runtime)
        self.assertIn("AmphibiousPlayerPlugin::without_water(plan)", runtime)
        self.assertIn("AmphibiousPlayerPlugin::without_water_procedural(", runtime)
        self.assertLess(runtime.index("configure_player_profile(&mut profile)"),
                        runtime.index("let water_plan = if water.is_some()"))

    def test_final_post_has_one_common_owner_and_adapter_ordering(self):
        owner = ROOT / "crates/forge_engine/src/source_final_post.rs"
        source = rust_body(owner)
        self.assertIn("pub struct PluginSourceFinal", source)
        self.assertIn("pub struct FinalPass", source)
        self.assertNotIn("tidewater", common_dependency_body(owner, source))
        self.assertFalse((EXAMPLES / "tidewater_plant_probe/source_final_post.rs").exists())
        reference = rust_body(EXAMPLES / "tidewater_plant_probe/graphics.rs")
        shared = ROOT / "crates/forge_engine/src/source_graphics.rs"
        self.assertNotIn("tidewater", common_dependency_body(shared, shared.read_text(encoding="utf-8")))
        self.assertIn("pub fn apply(", rust_body(shared))
        self.assertIn("source_graphics::install(app)?", reference)
        self.assertIn("source_graphics::apply(", reference)
        self.assertIn("forge_engine::source_graphics::install(app)?", rust_body(MODULE_GAME / "graphics/mod.rs"))
        self.assertIn("forge_engine::source_graphics::apply(", rust_body(MODULE_GAME / "graphics/runtime.rs"))
        haze = rust_body(ROOT / "crates/forge_engine/src/source_air_haze.rs")
        self.assertIn("SourceAirHazePass.before(crate::source_final_post::FinalPass)", haze)

    def test_terrain_material_cdlod_and_tile_residency_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src/source_terrain"
        adapter = ROOT / "crates/forge_tidewater/src"
        self.assertFalse((adapter / "tidewater_terrain.rs").exists())
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_terrain.rs").exists())
        self.assertFalse((adapter / "tidewater_refraction/demand.rs").exists())
        self.assertFalse((EXAMPLES / "tidewater_plant_probe/resident_tiles.rs").exists())
        self.assertIn("pub struct TerrainMaterial", rust_body(engine.with_suffix(".rs")))
        self.assertIn("pub struct PreparedTerrain", rust_body(engine / "scene.rs"))
        self.assertIn("pub struct ResidentTiles", rust_body(engine / "resident_tiles.rs"))
        self.assertIn("select_nodes(", rust_body(engine / "lod.rs"))
        self.assertIn("source_terrain::resident_tiles", rust_body(EXAMPLES / "tidewater_plant_probe/walk.rs"))
        self.assertNotIn("pub use demand", rust_body(engine.parent / "source_refraction_capture.rs"))
        codec = ROOT / "crates/forge_assets/src/source_terrain_package.rs"
        self.assertIn("pub struct TerrainPackage", rust_body(codec))
        self.assertNotIn("tidewater", common_dependency_body(codec, codec.read_text(encoding="utf-8")))
        for dependency in ['use forge_tidewater::scene;', 'if module_id == "tidewater" {}', 'if slug == "tidewater" {}']:
            with self.assertRaises(ValueError):
                common_dependency_body(codec, dependency)

    def test_source_object_geometry_and_collision_projection_are_owned_by_the_engine(self):
        owner = ROOT / "crates/forge_engine/src/source_surface"
        adapter = ROOT / "crates/forge_tidewater/src/tidewater_assets"
        self.assertIn("pub struct PreparedMeshes", rust_body(owner / "geometry.rs"))
        self.assertIn("pub fn from_parts", rust_body(owner / "geometry.rs"))
        self.assertIn("pub struct ResidentScene", rust_body(owner / "objects.rs"))
        self.assertIn("pub fn is_synchronized", rust_body(owner / "objects.rs"))
        self.assertFalse((adapter / "scene.rs").exists())
        self.assertFalse((adapter / "placements.rs").exists())
        decoding = rust_body(adapter.with_suffix(".rs"))
        for mechanism in ("pub struct PreparedMeshes", "pub struct ResidentMeshes", "pub use"):
            self.assertNotIn(mechanism, decoding)
        preparation = rust_body(owner / "package.rs")
        self.assertIn("pub fn prepare_meshes", preparation)
        self.assertIn("PreparedMeshes::from_parts(prepared)", preparation)
        self.assertNotIn("pub fn prepare_meshes", decoding)
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/imported_scene.rs")
        self.assertIn("forge_engine::source_surface::objects::PreparedScene", studio)

    def test_source_surface_execution_has_one_engine_owner(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        material = rust_body(engine / "source_surface.rs")
        self.assertIn("pub struct SourceSurfaceMaterial", material)
        self.assertIn("impl Material for SourceSurfaceMaterial", material)
        self.assertIn("pub fn neutral_scene_material", material)
        self.assertIn("pub struct SourceMeshBytes", rust_body(engine / "source_surface/mesh.rs"))
        self.assertIn("pub struct CausticSampleParams", rust_body(engine / "source_surface/underwater.rs"))
        self.assertFalse((adapter / "tidewater_mesh.rs").exists())
        self.assertFalse((adapter / "tidewater_material/flare_sun.rs").exists())
        self.assertFalse((adapter / "tidewater_material.rs").exists())
        shader_adapter = rust_body(engine / "source_surface/receivers.rs")
        for implementation in ("pub struct", "impl Material", "pub use", "fn neutral_scene_material"):
            self.assertNotIn(implementation, shader_adapter)
        self.assertIn("stage_prepared_shader(world, &source)", shader_adapter)
        relay = rust_body(engine / "source_surface/flare_sun.rs")
        self.assertIn("publish.in_set(SurfaceSunPublication)", relay)
        self.assertNotIn("lens_flare::update", relay)
        flare = rust_body(engine / "lens_flare.rs")
        self.assertIn("after(crate::source_surface::SurfaceSunPublication)", flare)
        for relative in ("lens_flare.rs", "lens_flare.wgsl", "lens_flare_visibility.wgsl"):
            self.assertFalse((adapter / relative).exists())
        self.assertNotIn("lens_flare", rust_body(adapter / "lib.rs"))

    def test_acquired_source_object_light_binding_is_owned_by_the_common_engine(self):
        owner = ROOT / "crates/forge_engine/src/source_lighting/local_lights/projection/source_object.rs"
        body = rust_body(owner)
        self.assertIn("pub struct SourceObjectLightBindings", body)
        self.assertIn("pub struct PreparedObjectLights", body)
        self.assertNotIn("ResidentScene", body)
        self.assertNotIn("BuildingPackage", body)
        adapter = rust_body(ROOT / "crates/forge_tidewater/src/tidewater_assets/local_lights.rs")
        self.assertIn("pub fn sources", adapter)
        for implementation in ("fn stage", "fn replace", "fn verify_anchor", "LightBindings"):
            self.assertNotIn(implementation, adapter)
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_assets/local_lights/attestations.rs").exists())
        for relative in (
            "apps/forge_studio/src/authoring_assets/imported_scene/local_lights.rs",
            "apps/forge_studio/src/authoring_assets/imported_buildings.rs",
            "crates/forge_tidewater/examples/verify_building_source_lights.rs",
            "crates/forge_tidewater/examples/verify_building_light_upload/source_scene.rs",
        ):
            consumer = rust_body(ROOT / relative)
            self.assertIn("SourceObjectLightBindings::from_sources", consumer)
            self.assertNotIn("BuildingLightBindings", consumer)

    def test_source_surface_lighting_and_heightfield_execution_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src"
        self.assertTrue((engine / "source_shadows/heightfield.rs").is_file())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_hill_shadow.rs").exists())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_hill_shadow/heights.rs").exists())
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_imported_terrain.rs")
        self.assertIn("forge_engine::source_shadows::heightfield", studio)
        reference = rust_body(EXAMPLES / "tidewater_plant_probe/hill_shadow.rs")
        self.assertIn("forge_engine::source_shadows::heightfield", reference)
        material = rust_body(engine / "source_vegetation/world.rs")
        self.assertIn("FORGE_SOURCE_LIGHTING", material)
        surface = (engine / "source_vegetation/world/surface.wgsl").read_text()
        self.assertIn("return forgeSourceFragment(vs,front);", surface)
        shader = rust_body(engine / "source_vegetation/world/shader.rs")
        self.assertIn("scene_uniform::prepare", shader)
        self.assertIn("source_shadows::receiver::shader_source", shader)

    def test_common_surface_scene_lifecycle_is_independent_of_the_plant_host(self):
        engine = ROOT / "crates/forge_engine/src"
        self.assertFalse((engine / "source_vegetation/scene/streamed/host/surfaces.rs").exists())
        self.assertFalse((engine / "source_vegetation/world/motion.rs").exists())
        scene = rust_body(engine / "source_lighting/surfaces/scene.rs")
        self.assertIn("pub fn new(terrain: &SampledTerrainPlan)", scene)
        self.assertIn("plants: Option<", scene)
        self.assertNotIn("runtime::State", scene)
        live = rust_body(engine / "source_lighting/surfaces/live.rs")
        for call in ("state.scene.refresh", "state.scene.animate", "super::host::sync",
                     "crate::source_lighting::frame::motion::advance"):
            self.assertIn(call, live)
        self.assertNotIn("ImportedPopulation", live)
        self.assertIn("token.covers_frame", scene)
        self.assertIn("token.completed_frame", scene)
        frame = rust_body(engine / "source_lighting/frame.rs")
        self.assertNotIn("source_vegetation::", frame)
        runtime = rust_body(engine / "source_vegetation/scene/streamed/host/runtime.rs")
        self.assertIn("surfaces::live::snapshot", runtime)
        self.assertNotIn("surfaces.animate", runtime)
        adapter = rust_body(MODULE_GAME / "environment.rs")
        self.assertIn("surfaces::live::PreparedHost", adapter)
        self.assertIn("surfaces::scene::Prepared::new", adapter)

    def test_source_local_lights_have_one_shared_owner_and_direct_consumers(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        self.assertFalse((adapter / "source_local_lights.rs").exists())
        self.assertFalse((adapter / "source_local_lights").exists())
        self.assertFalse((adapter / "tidewater_material/local_lights.rs").exists())
        common = engine / "source_lighting/local_lights"
        collector = rust_body(common.with_suffix(".rs"))
        for name in ("Collector", "Packet", "GpuPacket"):
            self.assertIn("pub struct " + name, collector)
        self.assertIn("pub trait PreparedView", collector)
        self.assertIn("pub trait TemporalSelection", collector)
        for name in ("collection.rs", "scene.rs", "frame.rs", "projection.rs", "receiver.rs", "gpu.rs",
                     "projection/placement/host.rs"):
            source = rust_body(common / name)
            self.assertNotIn("forge_tidewater", source)
            self.assertNotIn("tidewater_temporal", source)
        gpu = rust_body(common / "gpu.rs")
        for guard in ("revision <= *previous", "tag.serial <= *serial", "validate_temporal_intent",
                      "queue.write_buffer", "OffscreenSceneCamera", "TypeId::of::<P>()"):
            self.assertIn(guard, gpu)
        self.assertIn("pub struct FrameTag", rust_body(engine / "scene_camera.rs"))
        temporal = rust_body(engine / "source_temporal/frame.rs")
        for name in ("tidewater_temporal", "tidewater_temporal_frame", "tidewater_hull_mask"):
            self.assertFalse((adapter / (name + ".rs")).exists())
            self.assertNotIn("pub mod " + name, rust_body(adapter / "lib.rs"))
        self.assertNotIn("pub struct FrameTag", temporal)
        self.assertIn("local_lights::PreparedView for ViewPacket", temporal)
        self.assertIn("local_lights::TemporalSelection for Control",
                      rust_body(engine / "source_temporal/render_owned.rs"))
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/imported_scene/local_lights.rs")
        self.assertIn("forge_engine::source_lighting::local_lights", studio)
        self.assertNotIn("forge_tidewater::source_local_lights", studio)
        plants = rust_body(engine / "source_vegetation/world/shader.rs")
        self.assertIn("local_lights::receiver::plant_shader_source", plants)
        self.assertIn("local_lights::receiver::plant_shader_source",
                      rust_body(engine / "source_vegetation/captured.rs"))
        self.assertIn("+sourceLocalLights(surface,input.P,", rust_body(common / "receiver.rs"))
        self.assertIn("local_lights::gpu::BUFFER",
                      rust_body(engine / "source_vegetation/world/lighting.rs"))

    def test_autonomous_environment_declarations_reach_the_common_fenced_host(self):
        engine = ROOT / "crates/forge_engine/src"
        codec = ROOT / "crates/forge_assets/src/source_environment_package.rs"
        self.assertIn("pub struct EnvironmentPackage", rust_body(codec))
        old = rust_body(ROOT / "crates/forge_tidewater_assets/src/tidewater_environment.rs")
        self.assertNotIn("EnvironmentPackage", old)
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_environment/programs.rs").exists())
        host = rust_body(engine / "source_environment/host.rs")
        for call in ("prepared.input.stage", "source_clouds::live::submit_observer", "live::submit",
                     "render_inventory::request_scoped", "token.covers_frame"):
            self.assertIn(call, host)
        scene = rust_body(MODULE_GAME / "scene.rs")
        self.assertIn("super::environment::prepare", scene)
        self.assertIn("environment.install", scene)
        adapter = rust_body(MODULE_GAME / "environment.rs")
        self.assertIn("loaded.environment()?", adapter)
        self.assertIn("forge_engine::source_environment::host", adapter)
        self.assertNotIn("std::fs", adapter)
        contract = rust_body(MODULE_GAME / "evidence_contract.rs")
        self.assertNotIn("forge_engine::", contract)
        self.assertIn("verify_environment(expected.environment.as_ref()", contract)

    def test_environment_gpu_execution_is_shared_and_acquisitions_remain_separate(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        publication = rust_body(engine / "source_sky/publication.rs")
        for call in ("solar::Clock::new", "atmosphere::controller::advance",
                     "atmosphere::readback::latest", "FrameLighting::new"):
            self.assertIn(call, publication)
        for relative, call in (
            ("crates/forge_engine/src/source_environment/host.rs", "source_sky::publication::advance"),
            ("apps/forge_studio/src/authoring_imported_terrain/solar.rs", "publication::advance"),
            ("crates/forge_tidewater/examples/tidewater_plant_probe/display_daylight.rs", "publication::advance_uniforms"),
        ):
            consumer = rust_body(ROOT / relative)
            self.assertIn(call, consumer)
            self.assertNotIn("controller::advance(", consumer)
            self.assertNotIn("readback::latest(", consumer)
        self.assertIn("source_clouds::live::submit_solar", rust_body(engine / "source_environment/host.rs"))
        for old, new in (("tidewater_atmosphere", "source_atmosphere"),
                         ("tidewater_clouds", "source_clouds"),
                         ("tidewater_environment", "source_environment"),
                         ("tidewater_sky", "source_sky")):
            root = engine / (new + ".rs")
            if not root.exists():
                root = engine / new / "mod.rs"
            self.assertTrue(root.is_file())
            self.assertNotIn("forge_tidewater", rust_body(root))
            for relative in ("prepared.rs", "shadow.rs", "trace.rs", "live/mod.rs", "programs/encode.rs"):
                self.assertFalse((adapter / old / relative).exists())
        aggregate = rust_body(engine / "source_environment/scene.rs")
        self.assertIn("source_atmosphere::stage", aggregate)
        self.assertIn("source_clouds::stage", aggregate)
        self.assertNotIn("EnvironmentPackage", aggregate)
        parser = rust_body(engine / "source_environment/programs/package.rs")
        self.assertIn("source_commit", parser)
        self.assertIn("Input::new", parser)
        self.assertNotIn("create_compute_pipeline", parser)
        for old in ("tidewater_scene_environment.rs", "tidewater_environment.rs",
                    "tidewater_atmosphere.rs", "tidewater_clouds/mod.rs"):
            self.assertFalse((adapter / old).exists())
        package = rust_body(engine / "source_environment/package.rs")
        self.assertIn("source_atmosphere::acquisition::from_acquisition", package)
        self.assertIn("source_clouds::acquisition::generation_and_continuation", package)
        for relative in ("apps/forge_studio/src/authoring_module_resources.rs",
                         "apps/forge_runtime/src/module_game/environment.rs",
                         "crates/forge_tidewater/examples/tidewater_plant_probe/acquired_environment.rs"):
            consumer = rust_body(ROOT / relative)
            self.assertIn("forge_engine::source_environment::package::prepare", consumer)
            self.assertNotIn("forge_tidewater::tidewater_scene_environment", consumer)
        for relative in ("apps/forge_studio/src/authoring_assets/imported_environment.rs",
                         "crates/forge_tidewater/examples/verify_tidewater_live_atmosphere.rs"):
            self.assertIn("forge_engine::source_atmosphere", rust_body(ROOT / relative))

    def test_module_wind_consumes_the_reference_clock_instead_of_a_private_integrator(self):
        engine = ROOT / "crates/forge_engine/src"
        self.assertTrue((engine / "source_wind.rs").is_file())
        self.assertTrue((engine / "source_wind/vegetation.rs").is_file())
        self.assertFalse((engine / "source_shadows/casters/wind.rs").exists())
        source = rust_body(ROOT / "crates/forge_tidewater/src/scene_wind.rs")
        self.assertNotIn("pub struct WindFrame", source)
        self.assertNotIn("pub use forge_engine::source_wind::WindFrame", source)
        runtime = rust_body(engine / "source_vegetation/scene/streamed/host/runtime.rs")
        self.assertIn("crate::source_lighting::frame::motion::advance", runtime)
        self.assertNotIn("state.gust", runtime)
        self.assertNotIn("rem_euclid(8540.0)", runtime)
        self.assertIn("crate::scene_camera::primary as camera", runtime)
        self.assertIn("OffscreenSceneCamera", rust_body(engine / "scene_camera.rs"))
        motion = rust_body(engine / "source_lighting/frame/motion.rs")
        self.assertIn("candidate.advance", motion)
        self.assertIn("pose.index % 64", motion)
        reference = rust_body(EXAMPLES / "tidewater_plant_probe/walk_wind.rs")
        self.assertIn("forge_engine::source_wind::vegetation", reference)

    def test_cascade_graph_and_transient_casters_execute_in_the_common_engine(self):
        engine = ROOT / "crates/forge_engine/src/source_shadows"
        adapter = ROOT / "crates/forge_tidewater/src/tidewater_object_shadow"
        for relative in ("graph.rs", "animation.rs", "live.rs", "plants.rs",
                         "terrain.rs", "terrain/authored.rs", "plant_shader.rs"):
            self.assertTrue((engine / "casters" / relative).is_file())
            self.assertFalse((adapter / relative).exists())
        graph = rust_body(engine / "casters/graph.rs")
        self.assertIn("super::masked as fish_shadow", graph)
        self.assertIn("super::textured as scanned_shadow", graph)
        self.assertIn("crate::scene_camera::install_offscreen_extraction(app)", graph)
        self.assertNotIn("tidewater", graph.lower())
        for relative in ("masked/gpu.rs", "masked/extraction.rs", "textured/gpu.rs"):
            self.assertTrue((engine / "casters" / relative).is_file())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_fish_props/shadow/gpu.rs").exists())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_debris/scanned_shadow/gpu.rs").exists())
        collector = rust_body(adapter / "scene.rs")
        self.assertIn("ResidentScene", collector)
        self.assertNotIn("pub fn prepare", collector)
        self.assertNotIn("fn world_pose", collector)
        for relative in ("apps/forge_studio/src/authoring_imported_shadows.rs",
                         "crates/forge_tidewater/examples/tidewater_object_shadow_reload_probe.rs"):
            self.assertIn("forge_engine::source_shadows::casters", rust_body(ROOT / relative))

    def test_picking_and_sampleable_depth_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        for relative in ("picking.rs", "picking/render.rs", "picking/occluders.rs",
                         "picking/occluders/tests.rs"):
            self.assertTrue((engine / "source_vegetation" / relative).is_file())
            self.assertFalse((adapter / "tidewater_plants" / relative).exists())
        self.assertTrue((engine / "view_depth.rs").is_file())
        self.assertFalse((adapter / "view_depth.rs").exists())
        occluders = rust_body(engine / "source_vegetation/picking/occluders.rs")
        self.assertIn("pub struct Prepared<T: Material, V: Material>", occluders)
        self.assertIn("skin_for_draw", occluders)
        self.assertNotIn("forge_tidewater", occluders)
        render = rust_body(engine / "source_vegetation/picking/render.rs")
        self.assertIn("crate::scene_camera::OffscreenSceneCamera", render)
        for relative in ("apps/forge_studio/src/authoring_imported_plants/selection.rs",
                         "crates/forge_tidewater/examples/tidewater_plant_probe/picking_proof.rs"):
            source = rust_body(ROOT / relative)
            self.assertIn("forge_engine::source_vegetation::picking", source)
            self.assertNotIn("tidewater_plants::picking", source)
        self.assertIn("crate::view_depth", rust_body(engine / "ambient_occlusion.rs"))
        self.assertFalse((adapter / "ambient_occlusion.rs").exists())
        self.assertFalse((adapter / "film_grain.rs").exists())
        self.assertIn("crate::view_depth", rust_body(engine / "lens_flare.rs"))

    def test_module_grass_uses_shared_fields_resident_and_gpu_admission(self):
        engine = ROOT / "crates/forge_engine/src"
        host = rust_body(engine / "source_vegetation/scene/streamed/host.rs")
        self.assertIn("crate::source_vegetation::grass::fields::PreparedFields::new", host)
        self.assertIn("crate::source_vegetation::grass::host::Host::stage", host)
        runtime = rust_body(engine / "source_vegetation/scene/streamed/host/runtime.rs")
        self.assertRegex(runtime, r"grass\s*\.as_ref\(\)\s*\.is_none_or\(\|grass\| grass.ready\)")
        self.assertIn("profile_manifest_sha256 != package.manifest_sha256", host)
        grass = rust_body(engine / "source_vegetation/grass/host.rs")
        for call in ("Resident::stage", "render_inventory::request_scoped",
                     "render_inventory::acquire_scoped", "token.covers_frame"):
            self.assertIn(call, grass)
        self.assertTrue((engine / "sampled_terrain_corrections.rs").is_file())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_terrain_edits.rs").exists())
        self.assertIn("pub fn source_view", rust_body(engine / "scene_camera.rs"))
        self.assertNotIn("DMat4::from_cols", rust_body(EXAMPLES / "tidewater_plant_probe/terrain_lod.rs"))

    def test_grass_and_continuous_lod_execution_have_common_owners(self):
        world = ROOT / "crates/forge_world"
        engine = ROOT / "crates/forge_engine/src/source_vegetation"
        self.assertTrue((world / "src/terrain_lod.rs").is_file())
        self.assertTrue((world / "src/terrain_lod_bounds.rs").is_file())
        for relative in ("src/cdlod.rs", "src/terrain_bounds.rs"):
            self.assertFalse((ROOT / "crates/forge_tidewater_assets" / relative).exists())
        self.assertFalse((ROOT / "crates/forge_tidewater/src/tidewater_plants/grass.rs").exists())
        grass = rust_body(engine / "grass.rs")
        for name in ("Grid", "Resident", "Prepared"):
            self.assertIn(f"pub struct {name}", grass)
        self.assertNotIn("forge.tidewater", grass)
        self.assertIn("package.grass()?", grass)
        self.assertIn("forge_world::terrain_lod::CdlodView", grass)
        for relative in ("apps/forge_studio/src/authoring_imported_plants.rs",
                         "crates/forge_tidewater/examples/tidewater_plant_probe/grass.rs",
                         "crates/forge_tidewater/examples/tidewater_plant_probe/walk_grass.rs"):
            self.assertIn("forge_engine::source_vegetation::grass", rust_body(ROOT / relative))
        reference = rust_body(EXAMPLES / "tidewater_plant_probe/grass.rs")
        self.assertNotIn("commands.spawn", reference)
        self.assertRegex(reference, r"resident\s*\.update\(")

    def test_captured_light_material_motion_and_receivers_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        for old, new in [
            ("tidewater_plants/motion.rs", "source_vegetation/motion.rs"),
            ("tidewater_material/scene_uniform.rs", "source_lighting/scene_uniform.rs"),
            ("tidewater_material/scene_buffer.rs", "source_lighting/scene_buffer.rs"),
            ("tidewater_object_shadow/receiver.rs", "source_shadows/receiver.rs"),
        ]:
            self.assertFalse((adapter / old).exists(), old)
            self.assertTrue((engine / new).is_file(), new)
        captured = rust_body(engine / "source_vegetation/captured.rs")
        self.assertIn("pub struct CapturedPlantMaterial", captured)
        self.assertIn("crate::source_lighting::scene_uniform::prepare", captured)
        self.assertIn("crate::source_shadows::receiver::install", captured)
        self.assertIn("motion::install", captured)
        self.assertFalse((adapter / "tidewater_material.rs").exists())
        material = rust_body(engine / "source_surface/receivers.rs")
        self.assertNotIn("pub struct SceneLighting", material)
        self.assertNotIn("pub struct ObjectShadowSettings", material)
        plants = rust_body(adapter / "tidewater_plants.rs")
        self.assertNotIn("pub struct PlantMaterial", plants)
        self.assertNotIn("pub use", plants)
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_imported_plants.rs")
        self.assertIn("forge_engine::source_vegetation::captured::CapturedPlantMaterial", studio)

    def test_surface_lighting_assembly_and_water_receivers_have_no_map_facade(self):
        engine = ROOT / "crates/forge_engine/src/source_surface"
        adapter = ROOT / "crates/forge_tidewater/src"
        factory = rust_body(engine / "receivers.rs")
        self.assertIn("pub fn scene_shader_source", factory)
        self.assertIn("pub fn install_scene_receivers", factory)
        self.assertIn("fn ground_bounce_shader_source", factory)
        self.assertIn("underwater::receivers::surface_shader_source", factory)
        receiver = rust_body(engine / "underwater/receivers.rs")
        self.assertIn("pub fn terrain_shader_source", receiver)
        self.assertIn("pub fn surface_shader_source", receiver)
        self.assertIn("fn refract_shadow_receiver", receiver)
        self.assertIn("pub fn install_neutral", rust_body(engine / "underwater.rs"))
        old = rust_body(ROOT / "crates/forge_engine/src/source_caustics.rs")
        for mechanism in ("fn terrain_shader_source", "fn village_shader_source", "fn refract_shadow_receiver"):
            self.assertNotIn(mechanism, old)
        lighting = rust_body(ROOT / "crates/forge_engine/src/source_underwater_lighting.rs")
        self.assertNotIn("fn install_neutral", lighting)
        self.assertNotIn("pub use", lighting)
        for old_name, new_name in (("sample.wgsl", "terrain_sample.wgsl"),
                                   ("village_lite.wgsl", "surface_lite.wgsl"),
                                   ("light_map_lookup.wgsl", "light_map_lookup.wgsl"),
                                   ("underwater_shadow.wgsl", "shadow_position.wgsl")):
            self.assertFalse((adapter / "tidewater_caustics" / old_name).exists())
            self.assertTrue((engine / "underwater/receivers" / new_name).is_file())
        for name in ("authoring_assets/imported_scene.rs", "authoring_module_viewport.rs"):
            studio = rust_body(ROOT / "apps/forge_studio/src" / name)
            self.assertIn("forge_engine::source_surface::receivers::", studio)
            self.assertNotIn("tidewater_material", studio)

    def test_optical_passes_and_water_ordering_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        for old in ("tidewater_air_haze", "tidewater_underwater", "tidewater_underwater_lighting"):
            self.assertFalse((adapter / (old + ".rs")).exists())
            new = engine / (old.replace("tidewater_", "source_") + ".rs")
            self.assertTrue(new.is_file())
        schedule = rust_body(engine / "source_water_schedule.rs")
        for name in ("OceanComputeSet", "ShoreComputeSet"):
            self.assertEqual(schedule.count("pub struct " + name), 1)
        self.assertFalse((adapter / "tidewater_ocean.rs").exists())
        self.assertNotIn("pub struct OceanComputeSet", rust_body(engine / "source_ocean.rs"))
        self.assertNotIn("pub struct ShoreComputeSet", rust_body(engine / "source_shore_sim.rs"))
        lighting = rust_body(engine / "source_underwater_lighting.rs")
        self.assertIn(".after(crate::source_water_schedule::OceanComputeSet)", lighting)
        self.assertIn(".after(crate::source_water_schedule::ShoreComputeSet)", lighting)
        fft = rust_body(EXAMPLES / "tidewater_ocean_spectrum_probe/mod.rs")
        self.assertEqual(fft.count(".in_set(forge_engine::source_water_schedule::OceanComputeSet)"), 2)
        shore = rust_body(EXAMPLES / "tidewater_shore_sim_probe/mod.rs")
        self.assertIn(".in_set(forge_engine::source_water_schedule::ShoreComputeSet)", shore)
        self.assertIn(".after(forge_engine::source_water_schedule::OceanComputeSet)", shore)
        for directory in (ROOT / "apps", ROOT / "crates/forge_tidewater"):
            for path in directory.rglob("*.rs"):
                self.assertNotRegex(rust_body(path), r"\btidewater_(?:air_haze|underwater)\b", str(path))

    def test_ocean_execution_is_shared_and_package_admission_stays_explicit(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        self.assertTrue((engine / "source_shore_field.rs").is_file())
        self.assertTrue((engine / "source_shore_sim.rs").is_file())
        for name in ("source_water_optics", "source_surface_queries", "source_refraction_capture"):
            self.assertTrue((engine / (name + ".rs")).is_file())
        for name in ("tidewater_ocean_optics", "tidewater_surface_queries", "tidewater_refraction"):
            self.assertFalse((adapter / (name + ".rs")).exists())
        self.assertTrue((engine / "source_shore_hash.wgsl").is_file())
        self.assertFalse((adapter / "tidewater_shore_sim.rs").exists())
        self.assertFalse((adapter / "tidewater_shore_field.rs").exists())
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_shore_sim.rs").exists())
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_water_surface.rs").exists())
        self.assertTrue((ROOT / "crates/forge_assets/src/source_shore_sim_package.rs").is_file())
        self.assertTrue((ROOT / "crates/forge_assets/src/source_water_surface_package.rs").is_file())
        self.assertIn("source_shore_field::bake", rust_body(engine / "source_shore_sim.rs"))
        self.assertIn("source_shore_field::bake", rust_body(engine / "source_water_optics.rs"))
        self.assertTrue((engine / "source_ocean.rs").is_file())
        self.assertTrue((engine / "source_ocean/live_clock.rs").is_file())
        self.assertFalse((adapter / "tidewater_ocean.rs").exists())
        self.assertFalse((adapter / "tidewater_ocean/live_clock.rs").exists())
        source = rust_body(engine / "source_ocean.rs")
        self.assertIn("OceanInputSource", source)
        self.assertIn(".in_set(OceanComputeSet)", source)
        self.assertNotIn("forge_tidewater", source)
        contract = rust_body(ROOT / "crates/forge_assets/src/source_ocean.rs")
        self.assertIn("pub trait OceanInputSource", contract)
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_ocean.rs").exists())
        package = rust_body(ROOT / "crates/forge_assets/src/source_ocean_package.rs")
        self.assertIn("impl OceanInputSource for OceanPackage", package)
        self.assertIn("bytes_sha256(bytes) != expected", package)
        self.assertIn("verify_inner(&files", package)

    def test_ocean_gpu_kernels_have_one_engine_owner_and_two_consumers(self):
        engine = ROOT / "crates/forge_engine/src/source_ocean"
        kernels = rust_body(engine / "kernels.rs")
        self.assertIn("pub fn encode(", kernels)
        self.assertIn("dispatch_workgroups(", kernels)
        self.assertIn("create_compute_pipeline(", kernels)
        for path in (
            engine.with_suffix(".rs"),
            EXAMPLES / "tidewater_ocean_spectrum_probe/mod.rs",
        ):
            consumer = rust_body(path)
            for shared in ("kernels::views(", "kernels::pipelines(", "kernels::encode("):
                self.assertIn(shared, consumer, str(path))
            self.assertNotIn("dispatch_workgroups(", consumer, str(path))
            self.assertNotIn("create_compute_pipeline(", consumer, str(path))
        self.assertNotIn("forge_tidewater", kernels)

    def test_water_queries_and_photon_maps_are_owned_by_the_engine(self):
        engine = ROOT / "crates/forge_engine/src"
        adapter = ROOT / "crates/forge_tidewater/src"
        for old, new in (("water_height_queries.rs", "source_water_queries.rs"),
                         ("tidewater_caustics.rs", "source_caustics.rs"),
                         ("tidewater_caustics/gpu.rs", "source_caustics/gpu.rs"),
                         ("tidewater_caustics.wgsl", "source_caustics.wgsl")):
            self.assertFalse((adapter / old).exists(), old)
            self.assertTrue((engine / new).is_file(), new)
        queries = rust_body(engine / "source_water_queries.rs")
        self.assertIn("pub struct Queries", queries)
        self.assertIn("pub const QUERY_SHADER", queries)
        self.assertIn('include_str!("source_caustics.wgsl")',
                      rust_body(engine / "source_caustics.rs"))
        for directory in (ROOT / "apps", ROOT / "crates/forge_tidewater"):
            for path in directory.rglob("*.rs"):
                body = rust_body(path)
                self.assertNotRegex(body, r"(?:crate|forge_tidewater)::(?:water_height_queries|tidewater_caustics)", str(path))

    def test_streamed_vegetation_host_and_resource_packing_have_common_owners(self):
        engine = ROOT / "crates/forge_engine/src/source_vegetation"
        adapter = ROOT / "crates/forge_tidewater/src/tidewater_plants"
        for relative in ("scene/streamed.rs", "scene/streamed/host.rs",
                         "scene/streamed/bounds.rs", "world/shader.rs", "textures.rs"):
            self.assertTrue((engine / relative).is_file(), relative)
            self.assertFalse((adapter / relative).exists(), relative)
        self.assertIn("pub struct PreparedHost", rust_body(engine / "scene/streamed/host.rs"))
        self.assertIn("pub struct WorldEnvelopes", rust_body(engine / "scene/streamed/bounds.rs"))
        for relative in ("canonical.rs", "scene/streamed/prepare.rs"):
            self.assertNotIn("65_383", rust_body(engine / relative))
        packing = ROOT / "crates/forge_assets/src/source_vegetation_package/packing.rs"
        self.assertIn("pub fn pack", rust_body(packing))
        self.assertNotIn("forge_world", rust_body(packing))
        self.assertFalse((ROOT / "crates/forge_tidewater_assets/src/tidewater_vegetation.rs").exists())
        profile = rust_body(ROOT / "crates/forge_assets/src/source_vegetation_package/profile.rs")
        self.assertIn("validate_reference_population", profile)
        self.assertIn("65_383", profile)
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_imported_plants.rs")
        self.assertIn("forge_engine::source_vegetation::scene::from_population", studio)
        self.assertNotIn("pub fn from_population", rust_body(adapter / "scene.rs"))

    def test_source_vegetation_execution_is_shared_by_reference_studio_and_modules(self):
        engine = ROOT / "crates/forge_engine/src/source_vegetation"
        adapter = ROOT / "crates/forge_tidewater/src/tidewater_plants"
        for path, names in [
            (engine.with_suffix(".rs"), ("Record", "Params", "PlantKey")),
            (engine / "world.rs", ("WorldPlantMaterial",)),
            (engine.parent / "source_lighting/frame.rs", ("Frame",)),
            (engine / "scene.rs", ("Vegetation", "ImportedPlant")),
            (engine / "scene/resident.rs", ("Resident",)),
        ]:
            body = rust_body(path)
            for name in names:
                self.assertRegex(body, rf"\bpub struct {name}\b", str(path))
        self.assertFalse((adapter / "scene/resident.rs").exists())
        self.assertFalse((adapter / "scene/resident/picking.rs").exists())
        for path, names in [
            (adapter.with_suffix(".rs"), ("Record", "Params", "PlantKey")),
            (adapter / "scene.rs", ("Vegetation", "ImportedPlant")),
        ]:
            body = rust_body(path)
            for name in names:
                self.assertNotRegex(body, rf"\bpub (?:struct|trait|type) {name}\b", str(path))
                self.assertNotRegex(body, rf"pub use [^;]*\b{name}\b", str(path))
        self.assertFalse((adapter / "world.rs").exists())
        self.assertFalse((adapter / "textures.rs").exists())
        self.assertFalse((adapter / "scene/streamed.rs").exists())
        for path in [
            ROOT / "apps/forge_studio/src/authoring_imported_plants.rs",
            EXAMPLES / "tidewater_plant_probe/vegetation_scene.rs",
        ]:
            self.assertIn("forge_engine::source_vegetation::scene::resident::Resident", rust_body(path))
        self.assertIn("crate::source_vegetation::scene::resident::Resident", rust_body(engine / "scene/streamed/resident.rs"))
        self.assertIn("forge_engine::source_vegetation::scene::streamed::host", rust_body(MODULE_GAME / "imported.rs"))

    def test_source_provenance_is_allowed_but_map_dependencies_remain_rejected(self):
        path = ROOT / "crates/forge_assets/src/source_vegetation_package.rs"
        source = '// Copyright DRG Software Solutions; upstream Tidewater.\nconst SCHEMA: &str = "forge.tidewater.plant-package";'
        self.assertNotIn("tidewater", common_dependency_body(path, source))
        for dependency in ["use forge_tidewater::scene;",
            'include_str!("../../forge_tidewater/src/scene.rs");',
            'if module_id == "tidewater" {}',
            'if slug == "tidewater" {}']:
            with self.assertRaises(ValueError):
                common_dependency_body(path, dependency)
        ordinary = ROOT / "crates/forge_engine/src/renderer.rs"
        self.assertIn("tidewater", common_dependency_body(ordinary,
            'if slug == "tidewater" {}'))

    def test_exact_attribution_and_legacy_fixture_exceptions_do_not_hide_dependencies(self):
        for name in ("source_breaker_render.rs", "source_spray_render.rs"):
            path = ROOT / "crates/forge_engine/src" / name
            source = path.read_text(encoding="utf-8")
            self.assertNotIn("tidewater", common_dependency_body(path, source))
            with self.assertRaises(ValueError):
                common_dependency_body(path, source + "\nuse forge_tidewater::scene;\n")
            self.assertIn("tidewater", common_dependency_body(path,
                source + '\nif slug == "tidewater" {}\n'))
        fixture = ROOT / "crates/forge_engine/src/source_terrain/programs.rs"
        source = fixture.read_text(encoding="utf-8")
        self.assertNotIn("tidewater", common_dependency_body(fixture, source))
        with self.assertRaises(ValueError):
            common_dependency_body(fixture, source + '\nif module_id == "tidewater" {}\n')

    def test_orchestration_candidates_are_not_compiled_inventory_inputs(self):
        spec = importlib.util.spec_from_file_location("build_validator", ROOT / "scripts/Validate-Gate3WorkingTree.py")
        validator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(validator)
        for extension in ("py", "rs", "wgsl", "cs", "json"):
            self.assertFalse(validator.is_build_input(f"scripts/hivemind/candidates/proof.{extension}"))
        self.assertTrue(validator.is_build_input("crates/forge_tidewater/examples/support/proof.rs"))
        self.assertTrue(validator.is_build_input("scripts/hivemind-other/build.py"))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "crates/probe/src/lib.rs"
            source.parent.mkdir(parents=True)
            source.write_text('include!(concat!(env!("CARGO_MANIFEST_DIR"), "/../../scripts/hivemind/candidates/proof.rs"));')
            with patch.object(validator, "SOURCE", root):
                with self.assertRaisesRegex(ValueError, "orchestration"):
                    validator.source_inventories()
                source.write_text("// Product-owned source.\n")
                (root / "Cargo.toml").write_text('[dependencies.probe]\npath = "scripts/hivemind/candidates"\n')
                with self.assertRaisesRegex(ValueError, "orchestration"):
                    validator.source_inventories()

    def test_extracted_renderer_inputs_do_not_depend_on_a_named_map(self):
        for crate in ("forge_engine", "forge_assets"):
            for path in (ROOT / "crates" / crate).rglob("*"):
                if not path.is_file():
                    continue
                self.assertNotIn("tidewater", str(path.relative_to(ROOT)).lower())
                # Legal attributions and narrative Markdown do not create renderer
                # dependencies. Keep compiled sources, manifests and data covered.
                if path.suffix in (".rs", ".wgsl", ".toml", ".json"):
                    source = common_dependency_body(path, path.read_text(encoding="utf-8"))
                    self.assertNotIn("tidewater", source, str(path))
                    self.assertNotIn("drg software solutions", source, str(path))
        for directory in ("apps", "crates"):
            for path in (ROOT / directory).rglob("*.rs"):
                source = rust_body(path)
                self.assertNotRegex(source, r"\btidewater_(?:water_query|exposure)\b", str(path))

    def test_animation_reference_is_owned_by_content_not_the_engine(self):
        root = ROOT / "crates/forge_tidewater/src/scene_wind"
        for path in root.rglob("*.rs"):
            production = rust_body(path).split("#[cfg(test)]", 1)[0]
            self.assertNotIn("tidewater", production.lower())
            self.assertNotIn("include_str!", production)
        flare = rust_body(ROOT / "crates/forge_engine/src/lens_flare.rs")
        self.assertNotIn("VillageMaterial", flare)
        self.assertNotIn("Assets<", flare)

    def test_pier_motion_uses_shared_engine_transaction(self):
        adapter = rust_body(ROOT / "crates/forge_tidewater/src/tidewater_pier_animation.rs")
        self.assertIn("forge_engine::pendulum_motion", adapter)
        self.assertIn("Resident::bind(", adapter)
        self.assertIn("self.motion.rebind(", adapter)
        self.assertIn("self.motion.advance(", adapter)
        self.assertNotIn("fn verify_pivot(", adapter)
        self.assertNotIn("get_mut::<Transform>", adapter)
        studio = rust_body(ROOT / "apps/forge_studio/src/authoring_assets/imported_scene/motion.rs")
        self.assertIn("pendulum_motion::host::collection", studio)
        self.assertIn("Collection<ResidentPierMotion>", studio)
        self.assertNotIn("instances.retain(", studio)
        self.assertNotIn("BTreeMap<", studio)

    def test_engine_local_dependency_closure_excludes_map_components(self):
        workspace = tomllib.loads((ROOT / "Cargo.toml").read_text(encoding="utf-8"))
        dependencies = workspace["workspace"]["dependencies"]
        pending = ["forge_engine"]
        visited = set()
        while pending:
            name = pending.pop()
            if name in visited:
                continue
            visited.add(name)
            self.assertNotIn("tidewater", name)
            package = dependencies[name]
            for source_file in (ROOT / package["path"] / "src").rglob("*"):
                if source_file.is_file() and source_file.suffix in (".rs", ".wgsl"):
                    source = common_dependency_body(source_file, source_file.read_text(encoding="utf-8"))
                    self.assertNotIn("tidewater", source, str(source_file))
                    self.assertNotIn("drg software solutions", source, str(source_file))
            manifest = tomllib.loads((ROOT / package["path"] / "Cargo.toml").read_text(encoding="utf-8"))
            tables = [manifest.get("dependencies", {})]
            tables.extend(target.get("dependencies", {}) for target in manifest.get("target", {}).values())
            for table in tables:
                for dep, spec in table.items():
                    if isinstance(spec, dict) and spec.get("workspace") and dep in dependencies and "path" in dependencies[dep]:
                        pending.append(dep)
                    elif isinstance(spec, dict) and "path" in spec:
                        self.fail(f"Audit direct path dependency in {name}: {dep}")
        self.assertIn("forge_assets", visited)
        adapter = tomllib.loads((ROOT / "crates/forge_tidewater/Cargo.toml").read_text(encoding="utf-8"))
        self.assertIn("forge_engine", adapter["dependencies"])
        adapter_source = rust_body(ROOT / "crates/forge_tidewater/src/lib.rs")
        self.assertNotIn("pub use forge_engine::*", adapter_source)
        data_source = rust_body(ROOT / "crates/forge_tidewater_assets/src/lib.rs")
        self.assertNotIn("pub use forge_assets::*", data_source)

    def test_rust_build_inputs_do_not_include_orchestration_candidates(self):
        violations = []
        for directory in ("apps", "crates", "tools"):
            for path in (ROOT / directory).rglob("*.rs"):
                if "vendor" in path.parts:
                    continue
                source = path.read_text(encoding="utf-8")
                for target in re.findall(
                    r'(?:include(?:_str|_bytes)?!\s*\(\s*|#\[path\s*=\s*)"([^"]+)"',
                    source,
                ):
                    if "hivemind" in target.replace("\\", "/").split("/"):
                        violations.append(f"{path.relative_to(ROOT)} -> {target}")
        self.assertEqual(violations, [], "Move compiled inputs under product ownership")

    def test_large_identical_example_implementations_are_shared(self):
        seen = {}
        copies = []
        for path in sorted(EXAMPLES.glob("*.rs")):
            body = rust_body(path)
            if len(body.splitlines()) < 100:
                continue
            digest = hashlib.sha256(body.encode()).hexdigest()
            if digest in seen:
                copies.append(f"{path.name} = {seen[digest]}")
            else:
                seen[digest] = path.name
        self.assertEqual(copies, [], "Use thin example entry points with a shared harness")


if __name__ == "__main__":
    unittest.main()
