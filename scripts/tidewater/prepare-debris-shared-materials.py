"""Resolve exact Debris Village roles against an acquired shared material library.

This prepares dependencies, not a complete Debris package or scene render.
Usage: DEBRIS_PACKAGE BUILDING_PACKAGE NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    debris_root, building_root, output = map(lambda value: Path(value).resolve(), sys.argv[1:])
    assert not output.exists()
    assert all(not output.is_relative_to(root) for root in [debris_root, building_root])
    debris_bytes = (debris_root / "package.json").read_bytes()
    building_bytes = (building_root / "package.json").read_bytes()
    debris, building = json.loads(debris_bytes), json.loads(building_bytes)
    assert debris["schema"] == "forge.tidewater.debris-package"
    assert building["schema"] == "forge.tidewater.building-package"
    assert debris["source_commit"] == building["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"

    def read(root, manifest, reference):
        name = reference["file"]
        assert Path(name).name == name
        expected = {key: reference[key] for key in ["bytes", "sha256"]}
        assert manifest["files"][name] == expected
        raw = (root / name).read_bytes()
        assert len(raw) == expected["bytes"] and digest(raw) == expected["sha256"]
        return raw

    contracts = json.loads(read(debris_root, debris, debris["materials"]))["source_contracts"]
    library_bytes = read(building_root, building, building["material_library"])
    library = json.loads(library_bytes)
    shader = read(building_root, building, library["shader"]).decode().replace("\r\n", "\n")
    shared = {}
    for role in ["wood", "hard", "fabric"]:
        contract = contracts[role]
        assert not contract["defines"]
        assert not contract["transparent"] and contract["depth_write"]
        assert contract["side"] == ("double" if role == "fabric" else "front")
        labels = [entry["texture"]["label"] for entry in contract["bindings"].values()]
        matches = [value for value in library["materials"].values() if value["textures"] == labels]
        assert len(matches) == 1, f"{role} has no unique shared texture contract"
        parameters = matches[0]
        kind = parameters["kind"]
        branch_start = shader.index(f"#ifdef VILLAGE_KIND_{kind}\n")
        branch_end = shader.index("\n#endif", branch_start)
        branch = shader[branch_start:branch_end]
        for stage in ["vertex", "surface"]:
            original = read(debris_root, debris, contract["stages"][stage]).decode().replace("\r\n", "\n")
            assert original in branch, f"{role} source {stage} program differs"
        modules = []
        for module in contract["modules"]:
            raw = read(debris_root, debris, module["code"])
            code = raw.decode().replace("\r\n", "\n")
            if module["name"] == "common":
                # Same bounded adaptation as prepare-village-probe.py: keep common math
                # and the normal-map helper; Bevy owns the camera/depth bindings.
                start = code.index("fn perturbNormalByMap(")
                end = code.index("\n}", start) + 2
                code = code.split("// ---- depth (reversed-Z")[0] + code[start:end]
            assert code in branch, f"{role} module {module['name']} differs"
            modules.append({"name": module["name"], "source_sha256": digest(raw)})
        assert parameters["textures"] == [entry["texture"]["label"] for entry in contract["bindings"].values()]
        fields = {name: entry["value"] for name, entry in contract["uniforms"]["fields"].items()}
        rgb = lambda name: fields[name]
        assert len(rgb("color")) == len(rgb("emissive")) == 3
        assert parameters["color"] == rgb("color") + [fields["opacity"]]
        assert parameters["emissive"] == rgb("emissive") + [fields["emissiveIntensity"]]
        assert parameters["surface"] == [fields[name] for name in ["roughness", "metalness", "alphaTest", "envMapIntensity"]]
        bindings = []
        for slot, (name, binding) in enumerate(contract["bindings"].items(), 1):
            assert f"@binding({slot}) var {name}:texture_2d<f32>;" in branch
            label = binding["texture"]["label"]
            texture = library["textures"][label]
            assert [texture["width"], texture["height"]] == [binding["texture"][axis] for axis in ["width", "height"]]
            expected = building["files"][texture["file"]]
            read(building_root, building, {"file": texture["file"], **expected})
            bindings.append({"binding": slot, "source_name": name, "texture": label,
                             "file": texture["file"], **expected})
        shared[role] = {"source_name": contract["name"], "parameters": parameters, "textures": bindings,
                        "modules": modules}

    result = {"schema": "forge.tidewater.debris-shared-material-dependencies", "version": 1,
              "source_commit": debris["source_commit"], "debris_package_sha256": digest(debris_bytes),
              "building_package_sha256": digest(building_bytes), "material_library_sha256": digest(library_bytes),
              "preparation_sha256": digest(Path(__file__).read_bytes()),
              "shader": library["shader"], "shared": shared,
              "unresolved_roles": ["nature", "pebbles", "scanned"],
              "qualification": "Exact vertex/surface bodies, uniform values, texture slots/dimensions and retained dependency bytes only; no scene draw or visual parity"}
    output.mkdir()
    (output / "dependencies.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "shared_roles": list(shared), "unresolved_roles": result["unresolved_roles"]}))


if __name__ == "__main__":
    main()
