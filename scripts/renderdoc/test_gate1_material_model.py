"""Synthetic bound-state checks; these fixtures are never GPU evidence."""

import copy
import struct
import unittest

from gate1_material_model import validate_document


def constant_block(bind_set, binding, size, values=()):
    raw = bytearray(size)
    for offset, numbers in values:
        struct.pack_into("<" + "f" * len(numbers), raw, offset, *numbers)
    return {"set": bind_set, "binding": binding, "size": size,
            "raw_hex": raw.hex(), "bound": {"resource": "ResourceId::100"}}


def texture(resource, mip=0):
    return {"resource": "ResourceId::" + str(resource), "first_mip": mip}


def resource_binding(bind_set, binding, resource, mip=0):
    return {"set": bind_set, "binding": binding, "array_index": 0,
            "statically_unused": False, "bound": texture(resource, mip)}


def draw(role, event_id, entry="fragment"):
    return {"role": role, "event_id": event_id, "entry": entry,
            "indices": 3, "instances": 1, "constant_blocks": [],
            "resources": [], "outputs": [], "blend": {}}


def fixture(weather="clear"):
    rain = weather == "rain"
    wetness = [0.78 if rain else 0, 0.32, 0.72, 0]
    terrain = draw("terrain", 10)
    terrain["constant_blocks"] = [constant_block(3, 13, 112, [(96, wetness)])]
    vegetation = draw("vegetation", 20)
    vegetation["constant_blocks"] = [constant_block(3, 13, 160, [(128, wetness)])]
    water = draw("water", 30)
    water["constant_blocks"] = [
        constant_block(3, 13, 80, [(12, [0]), (32, [0.12, 5.5, 0.72, 0.62]),
                                  (64, [0.84, 0.16, 1.33, 0.72])]),
        constant_block(3, 0, 192, [(120, [0.18, 3.2, 1.33])]),
    ]
    water["outputs"] = [texture(1)]
    water["resources"] = [resource_binding(0, 15, 3), resource_binding(1, 1, 4)]
    bloom = []
    for i in range(16):
        entry = "downsample_first" if i == 0 else "downsample" if i < 8 else "upsample"
        item = draw("bloom", 40 + i, entry)
        item["resources"] = [resource_binding(
            0, 0, 1 if i == 0 else 2, max(i - 1, 0) if i < 8 else 15 - i)]
        item["outputs"] = [texture(1 if i == 15 else 2,
                                   i if i < 8 else max(14 - i, 0))]
        item["constant_blocks"] = [constant_block(0, 2, 48)] if i == 0 or i >= 8 else []
        item["blend"] = {"factor": [0.08, 0.08, 0.08, 1],
                         "attachments": [{"state": {"enabled": True}}]}
        bloom.append(item)
    tone = draw("tonemapping", 60)
    tone["constant_blocks"] = [constant_block(0, 0, 784, [
        (672, [1.08 if rain else 1.06] * 3), (688, [1] * 3),
        (744, [-0.35 if rain else 0]), (752, [0.86 if rain else 1.04]),
    ])]
    tone["resources"] = [resource_binding(0, 1, 1)]
    return {
        "schema": "forge-gate1-material-replay/v1",
        "capture": {"run": "synthetic-" + weather, "weather": weather,
                    "sha256": "a" * 64, "parent_receipt_sha256": "b" * 64,
                    "runtime_source_commit": "c" * 40, "runtime_ledger_sha256": "d" * 64,
                    "quality": "High", "window": {"physical_width": 1920, "physical_height": 1080},
                    "pipeline": "Vulkan", "degraded": False},
        "draws": [terrain, vegetation, water] + bloom + [tone],
        "textures": [
            {"resource": "ResourceId::3", "width": 1920, "height": 1080,
             "usage": [{"event_id": 29, "usage": "ResourceUsage.CopyDst"}]},
            {"resource": "ResourceId::4", "cubemap": True, "mips": 8},
        ],
        "qualification": {"pixel_debug_performed": False, "human_review_performed": False,
                          "same_process_weather_reversal": False, "browser_observed": False,
                          "routes_closed": [], "gate1_transition_authorized": False},
    }


def role(document, name):
    return next(item for item in document["draws"] if item["role"] == name)


def msaa_fixture(weather="clear"):
    document = fixture(weather)
    role(document, "water")["outputs"].insert(0, texture(5))
    document["textures"].extend([
        {"resource": "ResourceId::5", "usage": [
            {"event_id": 35, "usage": "ResourceUsage.ResolveSrc"}]},
        {"resource": "ResourceId::1", "usage": [
            {"event_id": 35, "usage": "ResourceUsage.ResolveDst"}]},
    ])
    return document


def replace_floats(block, offset, values):
    raw = bytearray.fromhex(block["raw_hex"])
    struct.pack_into("<" + "f" * len(values), raw, offset, *values)
    block["raw_hex"] = raw.hex()


class MaterialBoundStateTests(unittest.TestCase):
    def assert_rejected(self, document, message):
        with self.assertRaisesRegex(ValueError, message):
            validate_document(document)

    def test_clear_and_rain_pass_only_bound_state_without_mutating_input(self):
        for weather in ("clear", "rain"):
            with self.subTest(weather=weather):
                document = fixture(weather)
                original = copy.deepcopy(document)
                result = validate_document(document)
                self.assertEqual(result["status"], "PASS_BOUND_STATE_ONLY")
                self.assertEqual(result["run"], "synthetic-" + weather)
                self.assertEqual(result["events"], {"water": 30, "terrain": 10,
                                                   "vegetation": 20, "tonemapping": 60})
                self.assertEqual(len(result["bloom_events"]), 16)
                self.assertAlmostEqual(result["wetness"]["terrain"][0],
                                       0.78 if weather == "rain" else 0, places=6)
                self.assertAlmostEqual(result["grading_exposure"],
                                       -0.35 if weather == "rain" else 0, places=6)
                self.assertEqual(result["qualification"], original["qualification"])
                self.assertEqual(document, original)

    def test_wrong_wetness_or_nonfinite_values_are_rejected_for_both_materials(self):
        for name, offset in (("terrain", 96), ("vegetation", 128)):
            for value in (0, -0.1, float("nan"), float("inf")):
                with self.subTest(role=name, value=value):
                    document = fixture("rain")
                    replace_floats(role(document, name)["constant_blocks"][0], offset, [value])
                    self.assert_rejected(document, "Unexpected bound values")

    def test_truncated_constant_buffer_is_rejected(self):
        document = fixture()
        bound = role(document, "terrain")["constant_blocks"][0]
        bound["raw_hex"] = bound["raw_hex"][:-2]
        self.assert_rejected(document, "Wrong constant buffer length")

    def test_wrong_or_duplicate_constant_binding_is_rejected(self):
        for mutation in ("set", "binding", "size", "duplicate"):
            with self.subTest(mutation=mutation):
                document = fixture()
                blocks = role(document, "vegetation")["constant_blocks"]
                if mutation == "duplicate":
                    blocks.append(copy.deepcopy(blocks[0]))
                else:
                    blocks[0][mutation] += 1
                self.assert_rejected(document, "Missing or duplicate constant block")

    def test_null_constant_resource_is_rejected(self):
        document = fixture()
        role(document, "terrain")["constant_blocks"][0]["bound"]["resource"] = "ResourceId::0"
        self.assert_rejected(document, "Unbound constant buffer")

    def test_bloom_entry_chain_is_checked(self):
        document = fixture()
        role(document, "bloom")["entry"] = "upsample"
        self.assert_rejected(document, "Wrong Bloom chain")

    def test_msaa_resolve_to_composed_hdr_passes_clear_and_rain(self):
        for weather in ("clear", "rain"):
            with self.subTest(weather=weather):
                document = msaa_fixture(weather)
                self.assertEqual(validate_document(document)["status"], "PASS_BOUND_STATE_ONLY")
                bloom = [item for item in document["draws"] if item["role"] == "bloom"]
                self.assertEqual([len(item["constant_blocks"]) for item in bloom],
                                 [1] + [0] * 7 + [1] * 8)

    def test_missing_mismatched_or_out_of_window_resolve_is_rejected(self):
        for mutation in ("missing", "different_event", "late", "early", "wrong_usage"):
            with self.subTest(mutation=mutation):
                document = msaa_fixture()
                source, destination = document["textures"][-2:]
                if mutation == "missing":
                    destination["usage"] = []
                elif mutation == "different_event":
                    destination["usage"][0]["event_id"] = 36
                elif mutation in ("late", "early"):
                    event_id = 40 if mutation == "late" else 30
                    source["usage"][0]["event_id"] = event_id
                    destination["usage"][0]["event_id"] = event_id
                else:
                    source["usage"][0]["usage"] = "ResourceUsage.CopySrc"
                self.assert_rejected(document, "No exact water MSAA resolve before Bloom")

    def test_ordinary_downsample_cannot_claim_unused_constants(self):
        document = fixture()
        document["draws"][4]["constant_blocks"] = [constant_block(0, 2, 48)]
        self.assert_rejected(document, "Ordinary downsample should not use constants")

    def test_bloom_resource_chain_is_checked(self):
        for edge in ("resources", "outputs"):
            with self.subTest(edge=edge):
                document = fixture()
                item = document["draws"][4]
                target = item[edge][0]
                if edge == "resources":
                    target = target["bound"]
                target["resource"] = "ResourceId::99"
                self.assert_rejected(document, "Broken Bloom")

    def test_bloom_mip_chain_is_checked(self):
        for edge in ("resources", "outputs"):
            with self.subTest(edge=edge):
                document = fixture()
                target = document["draws"][11][edge][0]
                if edge == "resources":
                    target = target["bound"]
                target["first_mip"] += 1
                self.assert_rejected(document, "Wrong Bloom .* mip")

    def test_disabled_upsampling_blend_is_rejected(self):
        document = fixture()
        document["draws"][11]["blend"]["attachments"][0]["state"]["enabled"] = False
        self.assert_rejected(document, "Bloom blend disabled")

    def test_unbounded_or_malformed_blend_factors_are_rejected(self):
        for factor in ([-0.01, 0, 0, 1], [1.01, 0, 0, 1],
                       [float("nan"), 0, 0, 1], [float("inf"), 0, 0, 1], [0, 0, 0]):
            with self.subTest(factor=factor):
                document = fixture()
                document["draws"][11]["blend"]["factor"] = factor
                self.assert_rejected(document, "Unbounded Bloom blend")

    def test_wrong_final_blend_is_rejected(self):
        document = fixture()
        document["draws"][-2]["blend"]["factor"] = [0.2, 0.2, 0.2, 1]
        self.assert_rejected(document, "Unexpected bound values")

    def test_missing_or_late_transmission_copy_is_rejected(self):
        for usage in ([], [{"event_id": 30, "usage": "ResourceUsage.CopyDst"}],
                      [{"event_id": 29, "usage": "ResourceUsage.CopySrc"}]):
            with self.subTest(usage=usage):
                document = fixture()
                document["textures"][0]["usage"] = usage
                self.assert_rejected(document, "No transmission copy before water")

    def test_transmission_extent_and_filtered_ibl_are_checked(self):
        for field, value, index, error in (("width", 1280, 0, "Wrong transmission extent"),
                                           ("cubemap", False, 1, "Missing mipmapped IBL cubemap"),
                                           ("mips", 1, 1, "Missing mipmapped IBL cubemap")):
            with self.subTest(field=field):
                document = fixture()
                document["textures"][index][field] = value
                self.assert_rejected(document, error)

    def test_missing_unused_or_null_texture_binding_is_rejected(self):
        for mutation, error in (("binding", "Missing used texture binding"),
                                ("statically_unused", "Missing used texture binding"),
                                ("null", "Null texture")):
            with self.subTest(mutation=mutation):
                document = fixture()
                bound = role(document, "water")["resources"][0]
                if mutation == "null":
                    bound["bound"]["resource"] = "ResourceId::0"
                else:
                    bound[mutation] = True if mutation == "statically_unused" else 99
                self.assert_rejected(document, error)

    def test_scope_cannot_claim_human_pixel_browser_reversal_or_gate_results(self):
        for field in fixture()["qualification"]:
            with self.subTest(field=field):
                document = fixture()
                document["qualification"][field] = ["R1-025"] if field == "routes_closed" else True
                self.assert_rejected(document, "Scope was overstated")

    def test_duplicate_event_is_rejected(self):
        document = fixture()
        document["draws"][0]["event_id"] = document["draws"][1]["event_id"]
        self.assert_rejected(document, "Duplicate draw event")

    def test_wrong_replay_or_weather_profile_is_rejected(self):
        for field, value, error in (("pipeline", "D3D12", "Invalid replay"),
                                    ("degraded", True, "Invalid replay"),
                                    ("weather", "snow", "Unsupported weather")):
            with self.subTest(field=field):
                document = fixture()
                document["capture"][field] = value
                self.assert_rejected(document, error)

    def test_low_quality_or_wrong_resolution_is_rejected(self):
        document = fixture()
        document["capture"]["quality"] = "Low"
        self.assert_rejected(document, "Only native High is covered")
        document = fixture()
        document["capture"]["window"]["physical_width"] = 1280
        self.assert_rejected(document, "Wrong capture resolution")

    def test_capture_parent_ledger_and_runtime_identities_are_required(self):
        for field in ("sha256", "parent_receipt_sha256", "runtime_ledger_sha256", "runtime_source_commit"):
            for value in ("", "A" * 64, "g" * 64, "c" * 39):
                with self.subTest(field=field, value=value):
                    document = fixture()
                    document["capture"][field] = value
                    self.assert_rejected(document, "Invalid source identity")

    def test_tonemapping_must_consume_composed_hdr_with_correct_grade(self):
        document = fixture()
        role(document, "tonemapping")["resources"][0]["bound"]["resource"] = "ResourceId::99"
        self.assert_rejected(document, "Tonemapping does not read composed HDR")
        document = fixture("rain")
        replace_floats(role(document, "tonemapping")["constant_blocks"][0], 744, [0])
        self.assert_rejected(document, "Unexpected bound values")

    def test_draw_count_empty_draws_and_pass_order_are_checked(self):
        document = fixture()
        document["draws"].pop(4)
        self.assert_rejected(document, "Unexpected selected draw count")
        document = fixture()
        role(document, "bloom")["indices"] = 0
        self.assert_rejected(document, "Empty draw")
        document = fixture()
        role(document, "tonemapping")["event_id"] = 39
        self.assert_rejected(document, "Wrong pass order")


if __name__ == "__main__":
    unittest.main()
