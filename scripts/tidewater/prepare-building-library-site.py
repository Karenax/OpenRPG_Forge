"""Stage a pinned building package for Studio's same-origin browser transport."""
import hashlib
import json
from pathlib import Path
import shutil
import sys


def main():
    source, expected, destination = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
    raw = (source / 'package.json').read_bytes()
    assert len(expected) == 64 and hashlib.sha256(raw).hexdigest() == expected
    manifest = json.loads(raw)
    assert manifest['schema'] == 'forge.tidewater.building-package'
    assert manifest['version'] == 1
    assert not destination.exists(), 'Choose a new staging directory'
    verified = []
    for name, entry in manifest['files'].items():
        path = (source / name).resolve()
        assert path.parent == source.resolve() and path.is_file()
        data = path.read_bytes()
        assert len(data) == entry['bytes']
        assert hashlib.sha256(data).hexdigest() == entry['sha256']
        verified.append((name, path))
    environment = None
    if len(sys.argv) in (6, 8, 10):
        env_root, env_pin = Path(sys.argv[4]), sys.argv[5]
        env_bytes = (env_root / 'package.json').read_bytes()
        assert hashlib.sha256(env_bytes).hexdigest() == env_pin
        env_manifest = json.loads(env_bytes)
        assert env_manifest['schema'] == 'forge.tidewater.environment-package'
        assert env_manifest['version'] == 1
        env_files = []
        for name, entry in env_manifest['files'].items():
            path = (env_root / name).resolve()
            assert path.parent == env_root.resolve() and path.is_file()
            data = path.read_bytes()
            assert len(data) == entry['bytes'] and hashlib.sha256(data).hexdigest() == entry['sha256']
            env_files.append((name, path))
        environment = (env_pin, env_bytes, env_files)
    else:
        assert len(sys.argv) == 4
    terrain = None
    if len(sys.argv) in (8, 10):
        import importlib.util
        spec = importlib.util.spec_from_file_location('terrain_packager', Path(__file__).with_name('package-terrain-assets.py'))
        packager = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(packager)
        terrain_root, terrain_pin = Path(sys.argv[6]), sys.argv[7]
        packager.verify(terrain_root, terrain_pin)
        terrain = (terrain_pin, terrain_root)
    plants = None
    if len(sys.argv) == 10:
        import importlib.util
        spec = importlib.util.spec_from_file_location('plant_packager', Path(__file__).with_name('package-plant-assets.py'))
        plant_packager = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(plant_packager)
        plant_root, plant_pin = Path(sys.argv[8]), sys.argv[9]
        plant_packager.verify(plant_root, plant_pin)
        plants = (plant_pin, plant_root)
    root = destination / 'forge-imports'  
    package = root / expected
    package.mkdir(parents=True)
    for name, path in verified:
        shutil.copyfile(path, package / name)
    (package / 'package.json').write_bytes(raw)
    if environment:
        env_pin, env_bytes, env_files = environment
        env_destination = root / env_pin
        env_destination.mkdir()
        (env_destination / 'package.json').write_bytes(env_bytes)
        for name, path in env_files:
            shutil.copyfile(path, env_destination / name)
    if terrain:
        terrain_pin, terrain_root = terrain
        terrain_destination = root / terrain_pin
        terrain_destination.mkdir()
        terrain_manifest = json.loads((terrain_root / 'package.json').read_bytes())
        for name in [*terrain_manifest['files'], 'package.json']:
            shutil.copyfile(terrain_root / name, terrain_destination / name)
        packager.verify(terrain_destination, terrain_pin)
    if plants:
        plant_pin, plant_root = plants
        plant_destination = root / plant_pin
        plant_destination.mkdir()
        plant_manifest = json.loads((plant_root / 'package.json').read_bytes())
        for name in [*plant_manifest['files'], 'package.json']:
            shutil.copyfile(plant_root / name, plant_destination / name)
        plant_packager.verify(plant_destination, plant_pin)
    (root / 'buildings.json').write_text(json.dumps({
        'schema': 'forge.imported-building-library',
        'version': 1,
        'package_sha256': expected,
        **({'environment_sha256': environment[0]} if environment else {}),
        **({'terrain_sha256': terrain[0]} if terrain else {}),
        **({'plants_sha256': plants[0]} if plants else {}),
    }, indent=2), encoding='utf-8')
    print(json.dumps({'package_sha256': expected, 'payloads': len(verified),
                      'site_root': str(destination)}))


if __name__ == '__main__':
    main()
