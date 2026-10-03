"""Read-only causal assessment of public Studio physics/save ledgers."""
import argparse
import hashlib
import json
import math
import re
from datetime import datetime, timezone
from pathlib import Path

READER_SOURCE_BYTES = Path(__file__).read_bytes()
READER_SOURCE_SHA256 = hashlib.sha256(READER_SOURCE_BYTES).hexdigest()
READER_SOURCE_CAPTURED_UTC = datetime.now(timezone.utc).isoformat()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def distance(a, b):
    return math.sqrt(sum((float(a[k]) - float(b[k])) ** 2 for k in ("x", "y", "z")))


def variants(events, name):
    kind = re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()
    return [event[name] if name in event else event
            for event in events if isinstance(event, dict)
            and (name in event or event.get("kind") == kind)]


def saved_documents(ledger):
    return {name: read(meta["path"]) for name, meta in ledger.get("saves", {}).items()}


def entity(gp, kind, name):
    matches = [value for value in gp[kind] if value["name"] == name]
    if len(matches) != 1:
        raise ValueError(f"Expected one {kind} named {name!r}")
    return matches[0]



def environment_before_combat_evidence(ledger, saves, ids, source_hash):
    """Use only named snapshots acquired before combat; never later union receipts."""
    scenario = ledger.get("scenario")
    if scenario not in ("EnvironmentQuest", "EnvironmentFireQuest"):
        return {"valid": False, "neutralized": False, "reason": "not an environmental quest flow"}
    kind = "impact" if scenario == "EnvironmentQuest" else "heat"
    witness_phase = "launch-after" if kind == "impact" else "fire-exposure"
    neutralized_phase = "launch-after" if kind == "impact" else "post-danger"
    before, witness, neutralized = (saves.get(name) for name in ("launch-before", witness_phase, neutralized_phase))
    bad = lambda reason: {"valid": False, "neutralized": False, "reason": reason,
                          "kind": kind, "witness_phase": witness_phase, "neutralized_phase": neutralized_phase}
    if not all((before, witness, neutralized)):
        return bad("missing required pre-combat phase")
    for doc in (before, witness, neutralized):
        state = doc["state"]
        frame = (state.get("last_frame") or {}).get("frame", {})
        if doc.get("source_hash") != source_hash or frame.get("source_hash") != source_hash or frame.get("tick") != state["tick"]:
            return bad("pre-combat source/frame tick differs")
        if any(r.get("request", {}).get("action", {}).get("kind") == "attack"
               and r["request"]["action"].get("target") == ids["guard"] for r in state.get("receipts", [])):
            return bad("combat receipt already present before neutralization")
    b, w, n = (doc["state"] for doc in (before, witness, neutralized))
    if not (b["tick"] < w["tick"] <= n["tick"] and b["sequence"] < w["sequence"] == n["sequence"]):
        return bad("pre-combat tick/sequence order differs")
    action_kind = "throw" if kind == "impact" else "ignite"
    action_object = ids["launch"] if kind == "impact" else ids["fire"]
    actions = [r for r in w.get("receipts", []) if r.get("request", {}).get("action", {}).get("kind") == action_kind
               and r["request"]["action"].get("object") == action_object and r["request"].get("actor") == ids["player"]
               and r.get("sequence") == w["sequence"] and b["tick"] < r.get("tick", -1) <= w["tick"]]
    if len(actions) != 1:
        return bad("exact new Player throw/ignite receipt missing")
    action = actions[0]
    causes = []
    if kind == "impact":
        for command_id, contact in w.get("contacts", {}).items():
            obs = contact.get("observation", {})
            if not (obs.get("command_id") == command_id and obs.get("source") in (ids["launch"], ids["load"], ids["counter"])
                    and obs.get("target") == ids["guard"] and obs.get("initiator") == ids["player"]
                    and obs.get("normal_speed_m_s", 0) > 0 and action["tick"] <= contact.get("tick", -1) <= w["tick"]):
                continue
            impacts = [e for e in variants(contact.get("events", []), "Impact") if e.get("source") == obs["source"]
                       and e.get("target") == ids["guard"] and e.get("initiator") == ids["player"] and e.get("energy_j", 0) > 0]
            damages = [e for e in variants(contact.get("events", []), "Damage") if e.get("target") == ids["guard"]
                       and e.get("contact") is True and e.get("amount", 0) > 0]
            if impacts and damages:
                causes.append({"command_id": command_id, "tick": contact["tick"], "impact": impacts, "damage": damages})
    else:
        receipt = w["last_frame"]
        heat = [e for e in receipt["frame"].get("heat", []) if e.get("object") == ids["fire"]
                and e.get("actor") == ids["guard"] and e.get("line_of_sight") is True]
        damage = [e for e in variants(receipt.get("events", []), "Damage") if e.get("target") == ids["guard"]
                  and e.get("contact") is False and e.get("amount", 0) > 0]
        burning = [e for e in variants(action.get("events", []), "Burning") if e.get("object") == ids["fire"] and e.get("active") is True]
        if heat and damage and burning and receipt["frame"]["tick"] >= action["tick"]:
            causes.append({"tick": receipt["frame"]["tick"], "heat": heat, "damage": damage})
    if not causes:
        return bad("no environmental receipt in the exact pre-combat snapshot")
    if not (b["actors"][ids["guard"]]["vitality"] > 0 and n["actors"][ids["guard"]]["vitality"] == 0):
        return bad("environment did not neutralize guardian before combat")
    files = {}
    for phase in ("launch-before", witness_phase, neutralized_phase):
        meta = ledger.get("saves", {}).get(phase, {})
        path = meta.get("path")
        if not path or not Path(path).is_file():
            return bad("exact pre-combat Save file missing")
        digest = hashlib.sha256(Path(path).read_bytes()).hexdigest()
        if (digest != meta.get("post_save_sha256") or not meta.get("save_ack_observed") or not meta.get("save_file_changed")
                or not meta.get("save_state_coherence", {}).get("coherent")):
            return bad("exact pre-combat Save hash/ack/coherence differs")
        files[phase] = {"path": path, "sha256": digest}
    return {"valid": True, "neutralized": True, "kind": kind, "witness_phase": witness_phase,
            "neutralized_phase": neutralized_phase, "files": files, "before_tick": b["tick"], "before_sequence": b["sequence"],
            "witness_tick": w["tick"], "witness_sequence": w["sequence"], "neutralized_tick": n["tick"],
            "neutralized_sequence": n["sequence"], "causal_action": action, "causes": causes,
            "no_combat_before_neutralization": True, "individual_lethal_heat_event_claim": False}

def exposure_retreat_evidence(ledger, states, ids):
    plan = ledger.get("fire_exposure_retreat")
    if not plan:
        return None
    timeline = plan.get("timeline", [])
    events = {name: [entry for entry in timeline if entry.get("event") == name]
              for name in ("ignite_click_dispatch_started", "save_click_dispatch_started",
                           "save_click_acked", "pointer_capture_guard_satisfied",
                           "retreat_keydown_acked", "retreat_arrived")}
    saved = states.get("fire-exposure", {})
    frame = (saved.get("last_frame") or {}).get("frame", {})
    actor = saved.get("actors", {}).get(ids["player"], {})
    pre = plan.get("pre_save_observation") or {}
    retreat = plan.get("retreat_observation") or {}
    delta = None
    try:
        delta = distance(actor["position"], pre)
        first = float(events["retreat_keydown_acked"][0]["since_ignite_click_ms"])
        budget = float(plan["first_key_budget_ms"])
        ordered = [events[name][0]["since_ignite_click_ms"] for name in events]
        ordered = [float(value) for value in ordered]
        timing = (all(math.isfinite(value) for value in ordered)
                  and all(a <= b for a, b in zip(ordered, ordered[1:]))
                  and 0 <= first <= budget <= 1000
                  and abs(first - float(plan["first_key_elapsed_ms"])) <= 1)
        ticks = (int(pre["tick"]) <= int(frame["tick"]) == int(saved["tick"])
                 <= int(retreat["tick"]))
    except (KeyError, TypeError, ValueError, IndexError):
        timing = ticks = False
    frame_actor = [value for value in frame.get("actors", [])
                   if value.get("actor") == ids["player"]]
    receipt = [value for value in saved.get("receipts", [])
               if value.get("sequence") == plan.get("ignite_sequence")
               and value.get("request", {}).get("actor") == ids["player"]
               and value.get("request", {}).get("action", {}).get("kind") == "ignite"
               and value["request"]["action"].get("object") == ids["fire"]
               and any(event.get("object") == ids["fire"] and event.get("active") is True
                       for event in variants(value.get("events", []), "Burning"))]
    checks = {
        "public_chronology_and_startup_budget": timing and all(events.values()),
        "save_tick_between_original_pose_and_retreat": ticks,
        "same_source_and_original_pose": (frame.get("source_hash") == plan.get("source_hash")
            and delta is not None and math.isfinite(delta) and delta <= 0.25
            and len(frame_actor) == 1 and frame_actor[0].get("position") == actor.get("position")),
        "exact_ignite_receipt": len(receipt) == 1,
        "living_player_in_real_heat": (actor.get("vitality", 0) > 0
            and any(value.get("actor") == ids["player"] and value.get("object") == ids["fire"]
                    and value.get("line_of_sight") is True for value in frame.get("heat", []))
            and any(event.get("target") == ids["player"] and event.get("contact") is False
                    and event.get("amount", 0) > 0
                    for event in variants((saved.get("last_frame") or {}).get("events", []), "Damage"))),
        "alive_after_retreat_and_transaction_resolved": (plan.get("retreat_vitality", 0) > 0
            and plan.get("save_click_dispatched") is True and plan.get("save_click_acked") is True
            and plan.get("save_resolved") is True and not plan.get("failures")),
    }
    return {"checks": checks, "valid": all(bool(value) for value in checks.values()),
            "original_pose_to_saved_distance_m": delta, "public_plan": plan}


def object_state_saved_resumed(states, object_id, expected):
    """Compare durable flags only; live pose/velocity may advance after Resume."""
    fields = ("released", "destroyed", "initiator")
    snapshots = {}
    for phase in ("after", "resume"):
        obj = states.get(phase, {}).get("objects", {}).get(object_id)
        if not isinstance(obj, dict) or any(field not in obj for field in fields):
            return {"valid": False, "object": object_id, "reason": f"missing {phase} object/field"}
        if any(type(obj[field]) is not bool for field in fields[:2]):
            return {"valid": False, "object": object_id, "reason": f"invalid {phase} boolean"}
        initiator = obj["initiator"]
        if initiator is not None and (not isinstance(initiator, str) or not re.fullmatch(
                r"[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}", initiator)):
            return {"valid": False, "object": object_id, "reason": f"invalid {phase} initiator"}
        snapshots[phase] = {field: obj[field] for field in fields}
    same = snapshots["after"] == snapshots["resume"]
    causal = all(snapshots["after"][field] == value for field, value in expected.items())
    return {"valid": same and causal, "object": object_id, "fields": snapshots,
            "expected_causal_fields": expected, "same_after_resume": same,
            "causal_state_present": causal}


def physics_object_persistence_evidence(states, ids, throw_receipts, rupture, guardian_impacts):
    launch_expected = {"initiator": ids["player"]}
    if throw_receipts:
        launch_expected["released"] = True
    objects = {
        "launch": object_state_saved_resumed(states, ids["launch"], launch_expected),
        # Destruction does not rewrite the support's initiator. Preserve its
        # actual value rather than inventing Player attribution for the target.
        "support": object_state_saved_resumed(states, ids["support"], {"destroyed": True}),
        "load": object_state_saved_resumed(states, ids["load"], {"released": True, "initiator": ids["player"]}),
    }
    impact_sources = []
    for object_id in sorted({contact["observation"]["source"] for contact in guardian_impacts}):
        expected = {"initiator": ids["player"]}
        if object_id == ids["load"] or (object_id == ids["launch"] and throw_receipts):
            expected["released"] = True
        impact_sources.append(object_state_saved_resumed(states, object_id, expected))
    return {"objects": objects, "impact_sources": impact_sources,
            "fields_compared": ["released", "destroyed", "initiator"],
            "pose_velocity_and_in_motion_compared": False,
            "checks": {
                "throw_object_state_saved_resumed": bool(throw_receipts) and objects["launch"]["valid"],
                "trap_object_states_saved_resumed": bool(rupture) and all(obj["valid"] for obj in objects.values()),
                "impact_source_state_saved_resumed": any(obj["valid"] for obj in impact_sources),
            }}


def assess(ledger_path):
    ledger = read(ledger_path)
    loaded_scripts = ledger.get("loaded_script_sources", [])
    for script in loaded_scripts:
        if hashlib.sha256(Path(script["copy_path"]).read_bytes()).hexdigest() != script["loaded_sha256"]:
            raise ValueError(f"Loaded public helper copy changed: {script['copy_path']}")
    if ledger.get("loaded_script_inputs_path") and hashlib.sha256(Path(ledger["loaded_script_inputs_path"]).read_bytes()).hexdigest() != ledger["loaded_script_inputs_sha256"]:
        raise ValueError("Loaded public script input manifest changed")
    if ledger.get("public_input_transcript_path") and hashlib.sha256(Path(ledger["public_input_transcript_path"]).read_bytes()).hexdigest() != ledger["public_input_transcript_sha256"]:
        raise ValueError("Public input transcript bytes changed")
    parent = read(ledger["authoring_ledger"])
    parent_sha = hashlib.sha256(Path(parent["saved_object_path"]).read_bytes()).hexdigest()
    if parent_sha != parent["saved_reference"]["archive_hash"]:
        raise ValueError("Authored checkpoint bytes differ from the public authoring ledger")
    if parent["candidate_sha256"] != ledger["candidate_sha256"]:
        raise ValueError("Candidate differs between authoring and public playtest")
    candidate_sha = hashlib.sha256(Path(parent["candidate"]).read_bytes()).hexdigest()
    if candidate_sha != parent["candidate_sha256"]:
        raise ValueError("Current candidate bytes differ from the public authoring/playtest pin")
    authoring_ledger_sha = hashlib.sha256(Path(ledger["authoring_ledger"]).read_bytes()).hexdigest()
    if ledger.get("authoring_ledger_sha256", authoring_ledger_sha) != authoring_ledger_sha:
        raise ValueError("Parent authoring ledger bytes changed after the public run")
    source = read(parent["saved_object_path"])
    gp = source["head"]["corrections"]["gameplay"]
    names = {
        "player": ("actors", "Joueur"),
        "guard": ("actors", "Gardien de la forêt"),
        "danger": ("actors", "Veilleur du danger"),
        "launch": ("objects", "Charge de lancer"),
        "support": ("objects", "Support piège"),
        "load": ("objects", "Charge piège"),
        "fire": ("objects", "Balise inflammable"),
        "shore": ("objects", "Feu de rive"),
        "push": ("objects", "Bloc poussable de rive"),
        "counter": ("objects", "Bloc de contrepoids"),
        "seal": ("objects", "Sceau"),
        "quest": ("quests", "Rapporter le Sceau"),
    }
    entities = {key: entity(gp, *values) for key, values in names.items()}
    ids = {key: value["id"] for key, value in entities.items()}
    saves = saved_documents(ledger)
    for name, meta in ledger.get("saves", {}).items():
        if hashlib.sha256(Path(meta["path"]).read_bytes()).hexdigest() != meta["sha256"]:
            raise ValueError(f"Public Save evidence bytes changed for {name}")
    source_hashes = {document["source_hash"] for document in saves.values()}
    if len(source_hashes) > 1:
        raise ValueError("Public Save/Resume evidence uses different gameplay source hashes")
    state = {name: doc["state"] for name, doc in saves.items()}
    exposure_retreat = exposure_retreat_evidence(ledger, state, ids)
    contacts = []
    for phase, value in state.items():
        for command_id, contact in value.get("contacts", {}).items():
            if not any(existing["command_id"] == command_id for existing in contacts):
                contacts.append({"phase": phase, "command_id": command_id, **contact})
    impacts = [contact for contact in contacts if variants(contact.get("events", []), "Impact")]
    guardian_impacts = [contact for contact in impacts if
                        contact["observation"]["source"] in (ids["launch"], ids["load"], ids["counter"])
                        and contact["observation"]["target"] == ids["guard"]
                        and contact["observation"].get("initiator") == ids["player"]
                        and any(event.get("contact") is True and event.get("amount", 0) > 0
                                for event in variants(contact["events"], "Damage"))]
    rupture = [contact for contact in impacts if
               contact["observation"]["source"] == ids["launch"]
               and contact["observation"]["target"] == ids["support"]
               and contact["observation"].get("initiator") == ids["player"]
               and any(event["object"] == ids["support"] for event in variants(contact["events"], "Destroyed"))
               and any(event["object"] == ids["load"] for event in variants(contact["events"], "Released"))]
    ai = []
    before_danger = state.get("launch-before", state.get("before", {})).get("actors", {}).get(ids["danger"])
    for phase, value in state.items():
        actor = value["actors"][ids["danger"]]
        frame = (value.get("last_frame") or {}).get("frame", {})
        if not actor.get("fleeing") or before_danger is None:
            continue
        for exposure in frame.get("danger", []):
            if exposure.get("actor") != ids["danger"] or exposure.get("line_of_sight") is not True:
                continue
            obj = value["objects"][exposure["object"]]
            if not (obj.get("burning") or (obj.get("released") and obj.get("in_motion"))):
                continue
            delta = {k: actor["position"][k] - before_danger["position"][k] for k in ("x", "z")}
            away = sum(delta[k] * (before_danger["position"][k] - obj["position"][k]) for k in ("x", "z"))
            if distance(actor["position"], before_danger["position"]) > 0.05 and away > 0:
                ai.append({"phase": phase, "actor": actor, "exposure": exposure,
                           "before": before_danger, "distance_m": distance(actor["position"], before_danger["position"]),
                           "away_dot_xz": away})
    launch_after = state.get("launch-after", {})
    def impulse_receipt(receipt, kind, object_id):
        request = receipt.get("request", {})
        return (request.get("actor") == ids["player"]
                and request.get("action", {}).get("kind") == kind
                and request["action"].get("object") == object_id
                and any(event.get("object") == object_id
                        and sum(float(event.get("impulse", {}).get(k, 0)) ** 2
                                for k in ("x", "y", "z")) > 0
                        for event in variants(receipt.get("events", []), "Impulse")))

    throw_receipts = [receipt for value in state.values() for receipt in value.get("receipts", [])
                      if impulse_receipt(receipt, "throw", ids["launch"])]
    push_receipts = [receipt for value in state.values() for receipt in value.get("receipts", [])
                     if impulse_receipt(receipt, "push", ids["push"])]
    push_before = state.get("push-before", {}).get("objects", {}).get(ids["push"])
    push_after = state.get("push-after", {}).get("objects", {}).get(ids["push"])
    push_distance = distance(push_before["position"], push_after["position"]) if push_before and push_after else None
    counter_push_receipts = [receipt for value in state.values() for receipt in value.get("receipts", [])
                            if impulse_receipt(receipt, "push", ids["counter"])]
    counter_before = state.get("counter-before", {}).get("objects", {}).get(ids["counter"])
    counter_after = state.get("counter-after", {}).get("objects", {}).get(ids["counter"])
    counter_distance = distance(counter_before["position"], counter_after["position"]) if counter_before and counter_after else None
    after = state.get("after", {})
    resume = state.get("resume", {})
    burning = state.get("shore-burning", {}).get("objects", {}).get(ids["shore"])
    samples = ledger.get("water_samples", [])
    fresh_water = len(samples) >= 4 and all(sample.get("valid") for sample in samples)
    fresh_water = fresh_water and all(samples[i]["step"] < samples[i + 1]["step"] for i in range(len(samples) - 1))
    load_drop = max((entities["load"]["position"]["y"] - value["objects"][ids["load"]]["position"]["y"]
                     for value in state.values() if value["objects"][ids["load"]].get("released")), default=0)
    fire_saved = any(value["objects"][ids["fire"]].get("burning") for value in state.values())
    heat_damage = []
    for phase, value in state.items():
        receipt = value.get("last_frame") or {}
        fire_exposure = [exposure for exposure in receipt.get("frame", {}).get("heat", [])
                         if exposure.get("object") == ids["fire"] and exposure.get("line_of_sight") is True]
        for event in variants(receipt.get("events", []), "Damage"):
            if event.get("contact") is False and event.get("amount", 0) > 0 and any(
                    exposure.get("actor") == event.get("target") for exposure in fire_exposure):
                heat_damage.append({"phase": phase, "event": event, "exposures": fire_exposure})
    completed = after and resume and all(str(value.get("quests", {}).get(ids["quest"], "")).lower() == "completed"
                                         for value in (after, resume))
    guardian_heat_damage = [damage for damage in heat_damage
                            if damage["event"].get("target") == ids["guard"]]
    # A changed vitality alone cannot attribute damage to the environment.
    # Require the actual object impact receipt or a matching heat frame/event.
    environment_precombat = environment_before_combat_evidence(ledger, saves, ids, next(iter(source_hashes)))
    environment_before_combat = environment_precombat["valid"] and environment_precombat["neutralized"]
    object_persistence = physics_object_persistence_evidence(state, ids, throw_receipts, rupture, guardian_impacts)
    coherent_saves = all(meta.get("save_file_changed") and meta.get("save_ack_observed")
                         and meta.get("save_state_coherence", {}).get("coherent")
                         for meta in ledger.get("saves", {}).values())
    checks = {
        "flow_completed_and_clean": bool(ledger.get("run1_completed") and ledger.get("run2_resume_completed")
                                        and ledger.get("owned_processes_closed") and not ledger.get("forced_process_stop")),
        "source_preserved": bool(ledger.get("source_preserved")),
        "loaded_public_scripts_provenance_verified": bool(loaded_scripts),
        "public_saves_committed_coherently": bool(saves) and coherent_saves
            and (exposure_retreat is None or exposure_retreat["valid"]),
        "push": bool(push_receipts) and push_distance is not None and push_distance > 0.05 or
                bool(counter_push_receipts) and counter_distance is not None and counter_distance > 0.05,
        "throw": bool(throw_receipts) and bool(launch_after.get("objects", {}).get(ids["launch"], {}).get("released")),
        "impact_damage": bool(guardian_impacts),
        "fall_or_trap": bool(rupture) and load_drop > 0.05,
        "fire": fire_saved or bool(burning and burning.get("burning")),
        "fire_heat_damage": bool(heat_damage),
        "fire_heat_guardian_damage": bool(guardian_heat_damage),
        "water_extinguish": bool(fresh_water and burning and burning.get("burning") and
                                 after and resume and not after["objects"][ids["shore"]]["burning"]
                                 and not resume["objects"][ids["shore"]]["burning"]),
        "destructible": bool(rupture),
        "danger_ai": bool(ai),
        "environmental_quest_completed_saved_resumed": bool(environment_before_combat and completed
                            and after["actors"][ids["guard"]]["vitality"] == 0
                            and resume["actors"][ids["guard"]]["vitality"] == 0
                            and after["objects"][ids["seal"]]["consumed"]
                            and resume["objects"][ids["seal"]]["consumed"]),
    }
    checks.update(object_persistence["checks"])
    return {"ledger": str(Path(ledger_path).resolve()), "candidate_sha256": ledger["candidate_sha256"],
            "reader_source_sha256": READER_SOURCE_SHA256, "reader_source_captured_utc": READER_SOURCE_CAPTURED_UTC,
            "loaded_script_sources": loaded_scripts,
            "authoring_ledger_sha256": authoring_ledger_sha, "save_gameplay_source_hashes": sorted(source_hashes),
            "authored_checkpoint_sha256": parent["saved_reference"]["archive_hash"],
            "authored_head_sha256": parent["saved_reference"]["head_hash"],
            "gate_qualified": False, "navigation_claim": False, "human_visual_review": False,
            "checks": checks, "environment_before_combat": environment_precombat, "guardian_impact_contacts": guardian_impacts, "rupture_contacts": rupture,
            "ai_exposure_displacements": ai, "load_drop_m": load_drop, "push_displacement_m": push_distance,
            "fire_heat_damage_events": heat_damage,
            "fire_heat_guardian_damage_events": guardian_heat_damage,
            "fire_exposure_retreat_evidence": exposure_retreat,
            "object_saved_resumed_persistence": object_persistence,
            "counter_push_displacement_m": counter_distance,
            "save_sha256": {name: hashlib.sha256(Path(meta["path"]).read_bytes()).hexdigest()
                             for name, meta in ledger.get("saves", {}).items()}}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("ledger")
    parser.add_argument("--output")
    args = parser.parse_args()
    result = assess(args.ledger)
    if hashlib.sha256(Path(__file__).read_bytes()).hexdigest() != READER_SOURCE_SHA256:
        raise ValueError("The causal reader source changed while assessing evidence")
    if args.output:
        copy = Path(args.output).with_name(f"causal-reader-{READER_SOURCE_SHA256}.py")
        copy.write_bytes(READER_SOURCE_BYTES)
        result["reader_source_copy_path"] = str(copy.resolve())
        output = json.dumps(result, indent=2, ensure_ascii=False)
        Path(args.output).write_text(output + "\n", encoding="utf-8")
    print(json.dumps({"checks": result["checks"], "output": args.output}, ensure_ascii=False))
