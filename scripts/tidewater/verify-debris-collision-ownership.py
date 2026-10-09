"""Verify exact emitter ownership and absence of perturbation to Debris source data.

Usage: FIRST REPEAT BASELINE NEW_RECEIPT
"""
import hashlib
import json
import math
from pathlib import Path
import runpy
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    first, repeat, baseline, receipt = map(Path, sys.argv[1:])
    assert not receipt.exists()
    files = lambda root: {path.name: sha(path.read_bytes()) for path in root.iterdir() if path.is_file()}
    acquired = files(first)
    assert acquired == files(repeat)
    before = files(baseline)
    assert set(acquired) == set(before) | {"collision-ownership.json"}
    assert all(acquired[name] == digest for name, digest in before.items())
    verify = runpy.run_path(str(Path(__file__).with_name("verify-debris-source-payloads.py")))["verify"]
    payload_result = verify(first, baseline)
    assert payload_result == verify(repeat, baseline)
    geometry = json.loads((first / "ownership.json").read_bytes())
    collision_bytes = (first / "colliders.json").read_bytes()
    collisions = json.loads(collision_bytes)
    document = json.loads((first / "collision-ownership.json").read_bytes())
    assert document["schema"] == "forge.tidewater.debris-collision-ownership" and document["version"] == 1
    assert document["source_commit"] == geometry["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
    assert document["colliders_sha256"] == sha(collision_bytes)
    expected = {owner["source_id"]: "procedural" for owner in geometry["owners"]}
    expected.update({owner["source_id"]: "scanned" for owner in geometry["scanned_instances"]})
    assert len(expected) == len(document["owners"]) == 946
    seen_owners, seen_collisions, reconstructed = set(), set(), []
    counts = {"procedural": 0, "scanned": 0}
    colliding_owners = {"procedural": 0, "scanned": 0}
    for owner in document["owners"]:
        identity, category = owner["source_id"], owner["category"]
        assert identity not in seen_owners and expected[identity] == category
        seen_owners.add(identity)
        if owner["collisions"]:
            colliding_owners[category] += 1
        for event in owner["collisions"]:
            assert event["owner_source_id"] == identity
            shape, index = event["shape"], event["index"]
            assert shape in ["box", "cylinder"] and isinstance(index, int) and index >= 0
            key = (shape, index)
            assert key not in seen_collisions
            seen_collisions.add(key)
            assert event["basis"] == ("source-scan-return-object-to-log-collider" if category == "scanned"
                                       else "source-count-emission-boundary")
            value = collisions["boxes" if shape == "box" else "cylinders"][index]
            if shape == "box":
                assert all(math.isfinite(value[group][axis]) for group in ["center", "half"] for axis in ["x", "y", "z"])
                assert all(value["half"][axis] > 0 for axis in ["x", "y", "z"])
                assert math.isfinite(value["rotY"])
                assert math.isclose(value["cos"], math.cos(value["rotY"]), abs_tol=1e-12)
                assert math.isclose(value["sin"], math.sin(value["rotY"]), abs_tol=1e-12)
                if category == "scanned":
                    assert value["tag"] == "log"
            else:
                assert category == "procedural"
                assert all(math.isfinite(value[key]) for key in ["x", "z", "radius", "yMin", "yMax"])
                assert value["radius"] > 0 and value["yMax"] > value["yMin"]
            counts[category] += 1
            reconstructed.append(event)
    assert seen_owners == set(expected)
    assert seen_collisions == ({("box", i) for i in range(len(collisions["boxes"]))}
                              | {("cylinder", i) for i in range(len(collisions["cylinders"]))})
    assert len(document["events"]) == len(reconstructed) == 111
    assert {json.dumps(event, sort_keys=True) for event in document["events"]} == {
        json.dumps(event, sort_keys=True) for event in reconstructed}
    result = {"status": "passed", "source_commit": document["source_commit"],
              "collision_ownership_sha256": acquired["collision-ownership.json"],
              "colliders_sha256": sha(collision_bytes), "owners": len(expected),
              "collisions_by_category": counts, "colliding_owners": colliding_owners,
              "source_payloads_unchanged": True, "repeat_identical": True, "payload_verification": payload_result,
              "limits": ["Emitter association and retained source primitives only",
                         "No local-frame collider conversion, Rapier registration, gameplay or Studio qualification"]}
    receipt.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
