"""Stage pinned imports for Studio's same-origin browser transport."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil

FISH_STAND_PROVENANCE = (
    "fish-stand/source/LICENSE-Tidewater.txt",
    "fish-stand/source/FishStand.js",
    "fish-stand/source/StallKit.js",
    "fish-stand/source/props/CREDITS.md",
)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load_module(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def safe_relative(name):
    path = Path(name)
    return bool(name) and not path.is_absolute() and all(part not in ("", ".", "..") for part in path.parts)


def verified_file(root, name, entry):
    assert safe_relative(name)
    path = (root / name).resolve()
    assert path.is_relative_to(root.resolve()) and path.is_file()
    data = path.read_bytes()
    assert len(data) == entry["bytes"]
    assert digest(data) == entry["sha256"]
    return path


def fish_stand_entries(bundle, expected, building_pin=None):
    """Return the 53 manifest-pinned stand, Joe and fish reader inputs."""
    preview = load_module("package-preview-scene")
    preview.verify(bundle, expected)
    raw = (bundle / "preview-scene.json").read_bytes()
    manifest = json.loads(raw)
    if building_pin is not None:
        assert manifest["building_package_sha256"] == building_pin, "FishStand bundle belongs to a different building package"
    descriptor_name = "fish-stand/fish-stand.json"
    descriptor_path = verified_file(bundle, descriptor_name, manifest["files"][descriptor_name])
    descriptor = json.loads(descriptor_path.read_bytes())
    geometry = descriptor["geometry"]
    names = [descriptor_name]
    names.extend("fish-stand/" + item["file"] for item in geometry["attributes"].values())
    names.append("fish-stand/" + geometry["indices"]["file"])
    names.extend("fish-stand/" + item["file"] for item in descriptor["textures"].values())
    names.extend(FISH_STAND_PROVENANCE)
    content_name = "fish-stand-content/content.json"
    content = json.loads(verified_file(bundle, content_name, manifest["files"][content_name]).read_bytes())
    assert content["schema"] == "forge.tidewater.fish-stand-content-source-export"
    assert content["source"]["commit"] == manifest["source_commit"]
    assert len(content["source_files"]) == 11
    names.append(content_name)
    for relative, declared in content["source_files"].items():
        name = "fish-stand-content/" + relative
        assert safe_relative(relative) and manifest["files"][name] == declared
        names.append(name)
    fish_name = "fish-stand-fish/fish.json"
    fish = json.loads(verified_file(bundle, fish_name, manifest["files"][fish_name]).read_bytes())
    assert fish["schema"] == "forge.tidewater.fish-stand-fish-source-export"
    assert fish["source"]["commit"] == manifest["source_commit"]
    assert len(fish["fish"]) == 5
    names.append(fish_name)
    for item in fish["fish"]:
        species = item["species"]
        assert species in ("jack", "redSnapper", "yellowtail", "grunt", "mullet")
        for payload in item["geometry"].values():
            if not isinstance(payload, dict):
                continue
            assert safe_relative(payload["file"])
            name = f"fish-stand-fish/{species}/{payload['file']}"
            assert manifest["files"][name] == {key: payload[key] for key in ("bytes", "sha256")}
            names.append(name)
    names.append("scene/joe.glb")
    assert len(names) == 53 and len(set(names)) == 53
    selected = [(name, verified_file(bundle, name, manifest["files"][name])) for name in names]
    return raw, selected


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("expected")
    parser.add_argument("destination", type=Path)
    parser.add_argument("supplemental", nargs="*", type=Path)
    parser.add_argument("--fish-stand-bundle", type=Path)
    parser.add_argument("--fish-stand-sha256")
    parser.add_argument("--ocean-package-root", type=Path)
    parser.add_argument("--ocean-package-sha256")
    parser.add_argument("--water-surface-package-root", type=Path)
    parser.add_argument("--water-surface-package-sha256")
    parser.add_argument("--shore-sim-package-root", type=Path)
    parser.add_argument("--shore-sim-package-sha256")
    parser.add_argument("--ground-bounce-package-root", type=Path)
    parser.add_argument("--ground-bounce-package-sha256")
    parser.add_argument("--boardwalk-package-root", type=Path)
    parser.add_argument("--boardwalk-package-sha256")
    parser.add_argument("--pier-package-root", type=Path)
    parser.add_argument("--pier-package-sha256")
    parser.add_argument("--plaza-package-root", type=Path)
    parser.add_argument("--plaza-package-sha256")
    parser.add_argument("--source-local-lights-root", type=Path)
    parser.add_argument("--source-local-lights-sha256")
    args = parser.parse_args(argv)
    assert bool(args.fish_stand_bundle) == bool(args.fish_stand_sha256)
    assert bool(args.ocean_package_root) == bool(args.ocean_package_sha256)
    assert bool(args.water_surface_package_root) == bool(args.water_surface_package_sha256)
    assert bool(args.shore_sim_package_root) == bool(args.shore_sim_package_sha256)
    assert bool(args.ground_bounce_package_root) == bool(args.ground_bounce_package_sha256)
    assert bool(args.boardwalk_package_root) == bool(args.boardwalk_package_sha256)
    assert bool(args.pier_package_root) == bool(args.pier_package_sha256)
    assert bool(args.plaza_package_root) == bool(args.plaza_package_sha256)
    assert bool(args.source_local_lights_root) == bool(args.source_local_lights_sha256)
    assert len(args.supplemental) in (0, 2, 4, 6)

    raw = (args.source / "package.json").read_bytes()
    assert len(args.expected) == 64 and digest(raw) == args.expected
    manifest = json.loads(raw)
    assert manifest["schema"] == "forge.tidewater.building-package" and manifest["version"] == 1
    assert not args.destination.exists(), "Choose a new staging directory"
    verified = [(name, verified_file(args.source, name, entry)) for name, entry in manifest["files"].items()]

    environment = terrain = plants = None
    if len(args.supplemental) >= 2:
        env_root, env_pin = args.supplemental[:2]
        env_bytes = (env_root / "package.json").read_bytes()
        assert digest(env_bytes) == str(env_pin)
        env_manifest = json.loads(env_bytes)
        assert env_manifest["schema"] == "forge.tidewater.environment-package" and env_manifest["version"] == 1
        environment = (str(env_pin), env_bytes, [(name, verified_file(env_root, name, entry)) for name, entry in env_manifest["files"].items()])
    if len(args.supplemental) >= 4:
        terrain_packager = load_module("package-terrain-assets")
        terrain_root, terrain_pin = args.supplemental[2:4]
        terrain_packager.verify(terrain_root, str(terrain_pin))
        terrain = (str(terrain_pin), terrain_root)
    if len(args.supplemental) == 6:
        plant_packager = load_module("package-plant-assets")
        plant_root, plant_pin = args.supplemental[4:6]
        plant_packager.verify(plant_root, str(plant_pin))
        plants = (str(plant_pin), plant_root)
    fish_stand = None
    if args.fish_stand_bundle:
        assert len(args.fish_stand_sha256) == 64
        fish_stand = (args.fish_stand_sha256, *fish_stand_entries(args.fish_stand_bundle, args.fish_stand_sha256, args.expected))
    ocean = None
    if args.ocean_package_root:
        ocean_bytes = (args.ocean_package_root / "package.json").read_bytes()
        assert digest(ocean_bytes) == args.ocean_package_sha256
        ocean_manifest = json.loads(ocean_bytes)
        assert ocean_manifest["schema"] == "forge.tidewater.ocean-input-package"
        assert ocean_manifest["version"] == 1
        ocean_files = [(name, verified_file(args.ocean_package_root, name, entry))
                       for name, entry in ocean_manifest["files"].items()]
        ocean = (args.ocean_package_sha256, ocean_bytes, ocean_files)
    water_surface = None
    if args.water_surface_package_root:
        surface_bytes = (args.water_surface_package_root / "package.json").read_bytes()
        assert digest(surface_bytes) == args.water_surface_package_sha256
        surface_manifest = json.loads(surface_bytes)
        assert surface_manifest["schema"] == "forge.tidewater.water-surface-package"
        assert surface_manifest["version"] == 1
        surface_files = [(name, verified_file(args.water_surface_package_root, name, entry))
                         for name, entry in surface_manifest["files"].items()]
        water_surface = (args.water_surface_package_sha256, surface_bytes, surface_files)
    shore_sim = None
    if args.shore_sim_package_root:
        shore_bytes = (args.shore_sim_package_root / "package.json").read_bytes()
        assert digest(shore_bytes) == args.shore_sim_package_sha256
        shore_manifest = json.loads(shore_bytes)
        assert shore_manifest["schema"] == "forge.tidewater.shore-sim-input-package"
        assert shore_manifest["version"] == 1
        shore_files = [(name, verified_file(args.shore_sim_package_root, name, entry))
                       for name, entry in shore_manifest["files"].items()]
        shore_sim = (args.shore_sim_package_sha256, shore_bytes, shore_files)
    ground_bounce = None
    if args.ground_bounce_package_root:
        bounce_bytes = (args.ground_bounce_package_root / "package.json").read_bytes()
        assert digest(bounce_bytes) == args.ground_bounce_package_sha256
        bounce_manifest = json.loads(bounce_bytes)
        assert bounce_manifest["schema"] == "forge.tidewater.ground-bounce-input-package"
        assert bounce_manifest["version"] == 1
        bounce_files = [(name, verified_file(args.ground_bounce_package_root, name, entry))
                        for name, entry in bounce_manifest["files"].items()]
        ground_bounce = (args.ground_bounce_package_sha256, bounce_bytes, bounce_files)
    boardwalk = None
    if args.boardwalk_package_root:
        boardwalk_bytes = (args.boardwalk_package_root / "package.json").read_bytes()
        assert digest(boardwalk_bytes) == args.boardwalk_package_sha256
        boardwalk_manifest = json.loads(boardwalk_bytes)
        assert boardwalk_manifest["schema"] == "forge.tidewater.boardwalk-package"
        assert boardwalk_manifest["version"] == 1
        assert boardwalk_manifest["source_commit"] == manifest["source_commit"]
        assert boardwalk_manifest["building_material_library_sha256"] == digest((args.source / "materials.json").read_bytes())
        boardwalk_files = [(name, verified_file(args.boardwalk_package_root, name, entry))
                           for name, entry in boardwalk_manifest["files"].items()]
        boardwalk = (args.boardwalk_package_sha256, boardwalk_bytes, boardwalk_files)
    pier = None
    if args.pier_package_root:
        pier_bytes = (args.pier_package_root / "package.json").read_bytes()
        assert digest(pier_bytes) == args.pier_package_sha256
        pier_manifest = json.loads(pier_bytes)
        assert pier_manifest["schema"] == "forge.tidewater.pier-package"
        assert pier_manifest["version"] == 1
        assert pier_manifest["source_commit"] == manifest["source_commit"]
        assert pier_manifest["building_material_library_sha256"] == digest((args.source / "materials.json").read_bytes())
        pier_files = [(name, verified_file(args.pier_package_root, name, entry))
                      for name, entry in pier_manifest["files"].items()]
        pier = (args.pier_package_sha256, pier_bytes, pier_files)
    plaza = None
    if args.plaza_package_root:
        plaza_bytes = (args.plaza_package_root / "package.json").read_bytes()
        assert digest(plaza_bytes) == args.plaza_package_sha256
        plaza_manifest = json.loads(plaza_bytes)
        assert plaza_manifest["schema"] == "forge.tidewater.plaza-package"
        assert plaza_manifest["version"] == 1
        assert plaza_manifest["source_commit"] == manifest["source_commit"]
        assert plaza_manifest["building_material_library_sha256"] == digest((args.source / "materials.json").read_bytes())
        plaza_files = [(name, verified_file(args.plaza_package_root, name, entry))
                       for name, entry in plaza_manifest["files"].items()]
        plaza = (args.plaza_package_sha256, plaza_bytes, plaza_files)

    local_lights = None
    if args.source_local_lights_root:
        light_bytes = (args.source_local_lights_root / "manifest.json").read_bytes()
        assert len(light_bytes) <= 2 * 1024 * 1024
        assert digest(light_bytes) == args.source_local_lights_sha256
        light_manifest = json.loads(light_bytes)
        assert light_manifest["schema"] == "forge.source-local-lights.package"
        assert light_manifest["version"] == 1
        assert light_manifest["source_commit"] == manifest["source_commit"]
        assert 0 < len(light_manifest["files"]) <= 4096
        light_files = [(name, verified_file(args.source_local_lights_root, name, entry))
                       for name, entry in light_manifest["files"].items()]
        local_lights = (args.source_local_lights_sha256, light_bytes, light_files)

    root = args.destination / "forge-imports"
    package = root / args.expected
    package.mkdir(parents=True)
    for name, path in verified:
        shutil.copyfile(path, package / name)
    (package / "package.json").write_bytes(raw)
    if environment:
        env_pin, env_bytes, env_files = environment
        env_destination = root / env_pin
        env_destination.mkdir()
        (env_destination / "package.json").write_bytes(env_bytes)
        for name, path in env_files:
            shutil.copyfile(path, env_destination / name)
    if terrain:
        terrain_pin, terrain_root = terrain
        terrain_destination = root / terrain_pin
        terrain_destination.mkdir()
        terrain_manifest = json.loads((terrain_root / "package.json").read_bytes())
        for name in [*terrain_manifest["files"], "package.json"]:
            shutil.copyfile(terrain_root / name, terrain_destination / name)
        terrain_packager.verify(terrain_destination, terrain_pin)
    if plants:
        plant_pin, plant_root = plants
        plant_destination = root / plant_pin
        plant_destination.mkdir()
        plant_manifest = json.loads((plant_root / "package.json").read_bytes())
        for name in [*plant_manifest["files"], "package.json"]:
            shutil.copyfile(plant_root / name, plant_destination / name)
        plant_packager.verify(plant_destination, plant_pin)
    if fish_stand:
        fish_pin, fish_manifest, fish_files = fish_stand
        fish_destination = root / fish_pin
        fish_destination.mkdir()
        (fish_destination / "preview-scene.json").write_bytes(fish_manifest)
        for name, path in fish_files:
            target = fish_destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
    if ocean:
        ocean_pin, ocean_manifest, ocean_files = ocean
        ocean_destination = root / ocean_pin
        ocean_destination.mkdir()
        (ocean_destination / "package.json").write_bytes(ocean_manifest)
        for name, path in ocean_files:
            shutil.copyfile(path, ocean_destination / name)
    if water_surface:
        surface_pin, surface_manifest, surface_files = water_surface
        surface_destination = root / surface_pin
        surface_destination.mkdir()
        (surface_destination / "package.json").write_bytes(surface_manifest)
        for name, path in surface_files:
            shutil.copyfile(path, surface_destination / name)
    if shore_sim:
        shore_pin, shore_manifest, shore_files = shore_sim
        shore_destination = root / shore_pin
        shore_destination.mkdir()
        (shore_destination / "package.json").write_bytes(shore_manifest)
        for name, path in shore_files:
            shutil.copyfile(path, shore_destination / name)
    if ground_bounce:
        bounce_pin, bounce_manifest, bounce_files = ground_bounce
        bounce_destination = root / bounce_pin
        bounce_destination.mkdir()
        (bounce_destination / "package.json").write_bytes(bounce_manifest)
        for name, path in bounce_files:
            shutil.copyfile(path, bounce_destination / name)
    if boardwalk:
        boardwalk_pin, boardwalk_manifest, boardwalk_files = boardwalk
        boardwalk_destination = root / boardwalk_pin
        boardwalk_destination.mkdir()
        (boardwalk_destination / "package.json").write_bytes(boardwalk_manifest)
        for name, path in boardwalk_files:
            shutil.copyfile(path, boardwalk_destination / name)
    if pier:
        pier_pin, pier_manifest, pier_files = pier
        pier_destination = root / pier_pin
        pier_destination.mkdir()
        (pier_destination / "package.json").write_bytes(pier_manifest)
        for name, path in pier_files:
            shutil.copyfile(path, pier_destination / name)
    if plaza:
        plaza_pin, plaza_manifest, plaza_files = plaza
        plaza_destination = root / plaza_pin
        plaza_destination.mkdir()
        (plaza_destination / "package.json").write_bytes(plaza_manifest)
        for name, path in plaza_files:
            shutil.copyfile(path, plaza_destination / name)
    if local_lights:
        light_pin, light_manifest, light_files = local_lights
        light_destination = root / light_pin
        light_destination.mkdir()
        (light_destination / "manifest.json").write_bytes(light_manifest)
        for name, path in light_files:
            target = light_destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
    (root / "buildings.json").write_text(json.dumps({
        "schema": "forge.imported-building-library", "version": 1, "package_sha256": args.expected,
        **({"environment_sha256": environment[0]} if environment else {}),
        **({"terrain_sha256": terrain[0]} if terrain else {}),
        **({"plants_sha256": plants[0]} if plants else {}),
        **({"fish_stand_bundle_sha256": fish_stand[0]} if fish_stand else {}),
        **({"ocean_sha256": ocean[0]} if ocean else {}),
        **({"water_surface_sha256": water_surface[0]} if water_surface else {}),
        **({"shore_sim_sha256": shore_sim[0]} if shore_sim else {}),
        **({"ground_bounce_sha256": ground_bounce[0]} if ground_bounce else {}),
        **({"boardwalk_sha256": boardwalk[0]} if boardwalk else {}),
        **({"pier_sha256": pier[0]} if pier else {}),
        **({"plaza_sha256": plaza[0]} if plaza else {}),
        **({"source_local_lights_sha256": local_lights[0]} if local_lights else {}),
    }, indent=2), encoding="utf-8")
    print(json.dumps({"package_sha256": args.expected, "payloads": len(verified),
                      "fish_stand_payloads": len(fish_stand[2]) if fish_stand else 0,
                      "source_local_light_payloads": len(local_lights[2]) if local_lights else 0,
                      "site_root": str(args.destination)}))


if __name__ == "__main__":
    main()
