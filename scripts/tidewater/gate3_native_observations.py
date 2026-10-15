"""Strict reader of native gameplay observations, without simulation or state edits."""
import hashlib
import copy
import json
import math
from pathlib import Path
import re

MAX_OBSERVATION_BYTES = 64 * 1024 * 1024 + 4096  # bounded terminal marker
SOURCE_SHA256 = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
CAPABILITIES = ("push", "throw", "impact_damage", "fall_or_trap", "fire", "fire_danger_ai",
                "water_extinguish", "destructible", "danger_ai", "rpg_commands",
                "fire_retreat_ack_1000ms")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def strict_json(data):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, f"Duplicate JSON key: {key}")
            result[key] = value
        return result

    def number(value):
        result = float(value)
        require(math.isfinite(result), "Non-finite JSON number")
        return result

    def constant(value):
        raise ValueError(f"Non-standard JSON constant: {value}")

    return json.loads(data, object_pairs_hook=pairs, parse_float=number, parse_constant=constant)


def integer(value):
    return type(value) is int and value >= 0


def position(value):
    require(isinstance(value, dict) and all(k in value for k in ("x", "y", "z")),
            "Missing physical position")
    require(all(type(value[k]) in (int, float) and math.isfinite(value[k]) for k in ("x", "y", "z")),
            "Invalid physical position")
    return [float(value[k]) for k in ("x", "y", "z")]


def distance(left, right):
    return math.dist(position(left), position(right))


def events(items, kind):
    return [event for event in items if event.get("kind") == kind]


def read_rows(path, archive_hash, source_hash, scenario_hash=None):
    require(path.is_file() and path.stat().st_size <= MAX_OBSERVATION_BYTES,
            "Native observations missing or over quota")
    raw = path.read_bytes()
    require(raw.endswith(b"\n"), "Native observations end in a truncated line")
    rows = []
    for line in raw.splitlines():
        require(line and len(line) <= 4 * 1024 * 1024, "Empty or over-size observation line")
        rows.append(strict_json(line.decode("utf-8")))
    require(len(rows) >= 3 and rows[0].get("kind") == "boot" and rows[-1].get("kind") == "exit",
            "Native observations lack their boot or terminal marker")
    boot = rows[0]
    require(integer(boot.get("pid")) and boot["pid"] > 0,
            "Native process identity is absent")
    require(isinstance(boot.get("resumed"), bool) and isinstance(boot.get("state"), dict),
            "Native initial state or resume mode is absent")
    require(not boot["resumed"] or isinstance(boot.get("saved"), dict),
            "A resumed native process has no saved-state identity")
    require(boot.get("resume_refused") is None, "Native session booted after a refused Resume")
    functional = boot.get("functional_hash")
    require(isinstance(functional, str) and re.fullmatch(r"[0-9a-f]{64}", functional),
            "Missing gameplay functional identity")
    tick = boot["state"].get("tick")
    sequence = boot["state"].get("sequence")
    require(integer(tick) and integer(sequence), "Invalid initial game-state revision")
    elapsed = 0.0
    frame_tick = None
    scenario_plan, scenario_steps, completed_steps = None, None, []
    for index, row in enumerate(rows):
        require(row.get("schema") == "forge.native-gameplay.observation" and type(row.get("version")) is int and row["version"] == 1
                and row.get("qualification") == "observations_only" and row.get("pid") == boot["pid"]
                and row.get("archive_hash") == archive_hash and row.get("source_hash") == source_hash
                and row.get("functional_hash") == functional,
                "Native observation identity or archive/source differs")
        kind = row.get("kind")
        require(kind in ("boot", "frame", "command", "scenario", "exit")
                and (kind != "boot" or index == 0) and (kind != "exit" or index == len(rows) - 1),
                "Repeated or misplaced native lifecycle marker")
        if kind == "boot":
            continue
        now = row.get("elapsed_ms")
        require(type(now) in (int, float) and math.isfinite(now) and now >= elapsed,
                "Invalid or backwards native observation clock")
        elapsed = now
        if kind == "scenario":
            plan_hash = row.get("plan_hash")
            require(isinstance(plan_hash, str) and re.fullmatch(r"[0-9a-f]{64}", plan_hash)
                    and (scenario_plan is None or scenario_plan == plan_hash)
                    and (scenario_hash is None or scenario_hash == plan_hash),
                    "Native scenario differs from its pinned plan")
            scenario_plan = plan_hash
            require(integer(row.get("step")) and row.get("status") in
                    ("prepared", "completed", "input_submitted", "save_copied"),
                    "Native scenario failed or has an invalid lifecycle marker")
            if row["status"] == "prepared":
                require(scenario_steps is None and row["step"] == 0
                        and integer(row.get("detail", {}).get("steps"))
                        and 1 <= row["detail"]["steps"] <= 128, "Repeated or invalid prepared scenario")
                scenario_steps = row["detail"]["steps"]
            elif row["status"] == "completed":
                require(row["step"] == len(completed_steps), "Native scenario skipped or repeated a step")
                completed_steps.append(row["step"])
            continue
        require(integer(row.get("tick")) and row["tick"] >= tick
                and integer(row.get("sequence")) and row["sequence"] >= sequence,
                "Native game-state revision moved backwards")
        tick, sequence = row["tick"], row["sequence"]
        if kind == "frame":
            require(frame_tick is None or row["tick"] > frame_tick, "Repeated native physics frame")
            frame_tick = row["tick"]
            require(type(row.get("ready")) is bool and row.get("error") is None,
                    "Native gameplay readiness/error observation is invalid")
            require(all(isinstance(row.get(name), dict) for name in ("actors", "objects", "quests"))
                    and all(isinstance(row.get(name), list) for name in ("last_events", "navigation", "npc_motion")),
                    "Native frame is missing state or physical observations")
            if row["ready"]:
                frame = (row.get("last_frame") or {}).get("frame", {})
                require(frame.get("source_hash") == source_hash and frame.get("tick") == row["tick"],
                        "Ready observation does not carry its current canonical physics frame")
        elif kind == "command":
            require(type(row.get("accepted")) is bool and isinstance(row.get("command"), dict)
                    and row.get("storage_observation_error") is None,
                    "Native command/storage observation is invalid")
            require(row["command"].get("kind") in ("action", "save", "resume_next_launch"),
                    "Unknown native command observation")
            require((row["accepted"] and row.get("error") is None)
                    or (not row["accepted"] and isinstance(row.get("error"), str)),
                    "Native command result has inconsistent acceptance/error fields")
        elif kind == "exit":
            require(row.get("exit_success") is True and row.get("observation_error") is None
                    and row.get("simulation_error") is None,
                    "Native process or observation stream did not close successfully")
    require(scenario_plan is None or (scenario_steps is not None and len(completed_steps) == scenario_steps),
            "Native scenario ended without all of its pinned steps")
    require(scenario_hash is None or scenario_plan == scenario_hash, "Native observations omitted their requested scenario")
    return rows, hashlib.sha256(raw).hexdigest()


def fire_retreat_guards(rows):
    """Measure real exposed ignition to an acknowledged physical retreat.

    Ignition happens between accepted queueing and the first committed receipt.
    Use queue acceptance as the conservative start of the 1000 ms bound; also
    report the observed ignition timestamp separately. Input submission alone
    cannot close the guard, nor can an ACK of another movement sequence.
    """
    guards, seen = [], set()
    for index, row in enumerate(rows):
        if row.get("kind") != "frame" or row.get("ready") is not True:
            continue
        receipt = row.get("last_action_receipt") or {}
        request = receipt.get("request", {})
        action = request.get("action", {})
        key, player, fire = request.get("command_id"), request.get("actor"), action.get("object")
        if action.get("kind") != "ignite" or key in seen or not any(
                event.get("object") == fire and event.get("active") is True
                for event in events(receipt.get("events", []), "burning")):
            continue
        seen.add(key)
        exposed = any(later.get("kind") == "frame" and later.get("ready") is True
                      and any(heat.get("actor") == player and heat.get("object") == fire
                              and heat.get("line_of_sight") is True
                              for heat in (later.get("last_frame") or {}).get("frame", {}).get("heat", []))
                      and any(event.get("target") == player and event.get("contact") is False
                              and event.get("amount", 0) > 0
                              for event in (later.get("last_frame") or {}).get("events", []))
                      for later in rows[index:])
        if not exposed:
            continue  # The temporal exposure guard is not claimed for an unexposed ignition.
        commands = [previous for previous in rows[:index + 1]
                    if previous.get("kind") == "command" and previous.get("accepted") is True
                    and previous.get("sequence") == request.get("expected_sequence")
                    and previous.get("command", {}).get("kind") == "action"
                    and previous["command"].get("action") == action]
        result = {"command_id": key, "player": player, "fire": fire,
                  "ignition_observed_ms": row["elapsed_ms"], "budget_ms": 1000,
                  "native_input_ack_observed": False, "within_budget": False}
        if commands:
            result["ignition_lower_bound_ms"] = commands[0]["elapsed_ms"]
        initial_player = row.get("actors", {}).get(player, {}).get("position")
        fire_position = row.get("objects", {}).get(fire, {}).get("position")
        for input_index, submitted in enumerate(rows[index + 1:], index + 1):
            if submitted.get("kind") != "scenario" or submitted.get("status") != "input_submitted":
                continue
            detail = submitted.get("detail", {})
            axis, sequence = detail.get("move_axis"), detail.get("submitted_sequence")
            if not isinstance(axis, list) or len(axis) != 2 or not any(axis) or not integer(sequence):
                continue
            ack = next((later for later in rows[input_index + 1:]
                        if later.get("kind") == "frame" and later.get("ready") is True
                        and later.get("player", {}).get("last_applied_sequence") == sequence), None)
            if ack is None:
                continue
            movement = ack.get("player", {}).get("effective_translation", [0, 0, 0])
            away = (initial_player is not None and fire_position is not None and len(movement) == 3
                    and sum(float(movement[offset]) * (initial_player[axis_name] - fire_position[axis_name])
                            for offset, axis_name in ((0, "x"), (2, "z"))) > 0)
            if not away:
                continue
            upper_delay = ack["elapsed_ms"] - result.get("ignition_lower_bound_ms", -math.inf)
            result.update(submitted_sequence=sequence, input_submitted_ms=submitted["elapsed_ms"],
                          ack_ms=ack["elapsed_ms"], ack_tick=ack["tick"],
                          native_input_ack_observed=True, retreat_direction_away=True,
                          delay_from_observed_ignition_ms=ack["elapsed_ms"] - row["elapsed_ms"],
                          upper_delay_ms=upper_delay if math.isfinite(upper_delay) else None,
                          within_budget=(bool(commands) and 0 <= upper_delay <= 1000
                                         and ack.get("actors", {}).get(player, {}).get("vitality", 0) > 0))
            break
        guards.append(result)
    return guards


def assess(rows):
    """Report causal evidence only; an accepted queued command is not a result."""
    ready = [row for row in rows if row["kind"] == "frame" and row["ready"]]
    require(ready, "Native observations never reach canonical gameplay readiness")
    receipts = {}
    for row in ready:
        receipt = row.get("last_action_receipt")
        if receipt is None:
            continue
        request = receipt.get("request", {})
        key = request.get("command_id")
        require(isinstance(key, str) and integer(request.get("expected_sequence"))
                and receipt.get("sequence") == request["expected_sequence"] + 1
                and receipt.get("tick", math.inf) <= row["tick"], "Invalid committed native action receipt")
        require(key not in receipts or receipts[key] == receipt, "A committed action identity was reused")
        receipts[key] = receipt
    actions = list(receipts.values())
    kinds = {receipt["request"]["action"]["kind"] for receipt in actions}
    checks = dict.fromkeys(CAPABILITIES, False)
    initial = rows[0]["state"]
    for kind in ("push", "throw"):
        for receipt in actions:
            action = receipt["request"]["action"]
            obj = action.get("object")
            if action.get("kind") != kind or obj not in initial.get("objects", {}):
                continue
            impulse = any(event.get("object") == obj and any(float(v) != 0 for v in position(event["impulse"]))
                          for event in events(receipt["events"], "impulse"))
            moved = any(obj in row["objects"] and distance(row["objects"][obj]["position"],
                        initial["objects"][obj]["position"]) > 0.05 for row in ready if row["tick"] >= receipt["tick"])
            released = kind == "push" or any(row["objects"].get(obj, {}).get("released") is True
                         and row["objects"][obj].get("initiator") == receipt["request"]["actor"] for row in ready)
            checks[kind] |= impulse and moved and released
    for row in ready:
        frame_receipt = row["last_frame"]
        frame, consequence = frame_receipt["frame"], frame_receipt["events"]
        for contact in frame.get("contacts", []):
            impacts = [event for event in events(consequence, "impact")
                       if event.get("source") == contact.get("source")
                       and event.get("target") == contact.get("target") and event.get("energy_j", 0) > 0]
            checks["impact_damage"] |= bool(impacts) and contact.get("normal_speed_m_s", 0) > 0 and any(
                event.get("target") == contact.get("target") and event.get("contact") is True
                and event.get("amount", 0) > 0 for event in events(consequence, "damage"))
        destroyed = events(consequence, "destroyed")
        released = events(consequence, "released")
        matching_destruction = any(event.get("object") == contact.get("target")
                                   for event in destroyed for contact in frame.get("contacts", []))
        checks["destructible"] |= matching_destruction
        checks["fall_or_trap"] |= bool(matching_destruction and released) and any(
            obj.get("released") is True and key in initial.get("objects", {})
            and obj["position"]["y"] < initial["objects"][key]["position"]["y"] - 0.05
            for later in ready if later["tick"] >= row["tick"] for key, obj in later["objects"].items()
            if key in {event.get("object") for event in released})
        for exposure in frame.get("heat", []):
            checks["fire"] |= (exposure.get("line_of_sight") is True
                and row["objects"].get(exposure.get("object"), {}).get("burning") is True
                and any(receipt["request"]["action"].get("kind") == "ignite"
                        and receipt["request"]["action"].get("object") == exposure.get("object")
                        and receipt["tick"] <= row["tick"] for receipt in actions)
                and any(event.get("target") == exposure.get("actor") and event.get("contact") is False
                        and event.get("amount", 0) > 0 for event in events(consequence, "damage")))
        for exposure in frame.get("danger", []):
            actor = exposure.get("actor")
            body = row["actors"].get(actor, {})
            start = initial.get("actors", {}).get(actor)
            source = row["objects"].get(exposure.get("object"), {})
            motions = [motion for motion in row["npc_motion"] if motion.get("actor") == actor]
            away = (start is not None and bool(source) and sum(
                (body.get("position", {}).get(axis, 0) - start["position"][axis])
                * (start["position"][axis] - source["position"][axis]) for axis in ("x", "z")) > 0)
            fleeing_observed = (exposure.get("line_of_sight") is True and body.get("fleeing") is True
                and body.get("vitality", 0) > 0 and start is not None
                and (source.get("burning") or (source.get("released") and source.get("in_motion")))
                and distance(start["position"], body["position"]) > 0.05
                and away
                and any(m.get("grounded") is True and m.get("feedback_current") is True
                        and math.dist(m.get("effective_translation", [0, 0, 0]), [0, 0, 0]) > 0
                        for m in motions)
                and any(nav.get("actor") == actor and nav.get("feedback_tick") == row["tick"]
                        and nav.get("grounded") is True for nav in row["navigation"]))
            checks["danger_ai"] |= fleeing_observed
            checks["fire_danger_ai"] |= fleeing_observed and source.get("burning") is True and any(
                receipt["request"]["action"].get("kind") == "ignite"
                and receipt["request"]["action"].get("object") == exposure.get("object")
                and receipt["tick"] <= row["tick"] for receipt in actions) and any(
                    heat.get("object") == exposure.get("object") and heat.get("actor") == actor
                    and heat.get("line_of_sight") is True for heat in frame.get("heat", [])) and any(
                        event.get("target") == actor and event.get("contact") is False and event.get("amount", 0) > 0
                        for event in events(consequence, "damage"))
    for receipt in actions:
        action = receipt["request"]["action"]
        obj = action.get("object")
        if action.get("kind") == "extinguish":
            stopped = any(event.get("object") == obj and event.get("active") is False
                          for event in events(receipt["events"], "burning"))
            checks["water_extinguish"] |= stopped and any(
                row["tick"] > receipt["tick"] and row["objects"].get(obj, {}).get("burning") is False
                and not any(heat.get("object") == obj for heat in row["last_frame"]["frame"].get("heat", []))
                for row in ready)
    quest = any(event.get("status") == "completed" for receipt in actions
                for event in events(receipt["events"], "quest_changed"))
    combat = any(event.get("contact") is True and event.get("amount", 0) > 0
                 for receipt in actions if receipt["request"]["action"].get("kind") == "attack"
                 for event in events(receipt["events"], "damage"))
    checks["rpg_commands"] = {"pickup", "choose_dialogue", "attack"} <= kinds and quest and combat
    guards = fire_retreat_guards(rows)
    checks["fire_retreat_ack_1000ms"] = bool(guards) and all(guard["within_budget"] for guard in guards)
    return {"checks": checks, "pid": rows[0]["pid"], "resumed": rows[0]["resumed"],
            "source_hash": rows[0]["source_hash"], "functional_hash": rows[0]["functional_hash"],
            "archive_hash": rows[0]["archive_hash"], "ready_frames": len(ready),
            "committed_actions": len(actions), "last_tick": ready[-1]["tick"],
            "fire_retreat_guards": guards,
            "limits": ["Extinguish is committed by GameSession only after the pinned adapter observes water contact.",
                       "This is physics/command evidence, not GPU, human, performance or complete P01 evidence.",
                       "The native 1000 ms retreat guard uses a conservative ignition bound and actual input ACK; historical browser acquisitions remain separate."]}


def verify_saved_observations(runs, captures, validate_path):
    """Bind stable Save acknowledgements and new-process Resume to unchanged bytes."""
    available = {}
    for path in captures:
        validate_path(path)
        require(path.is_file() and path.stat().st_size <= 4 * 1024 * 1024, "Invalid saved-state capture")
        raw = path.read_bytes()
        available[hashlib.sha256(raw).hexdigest()] = (path, strict_json(raw.decode("utf-8")))
    saved_rows = [row for rows in runs for row in rows
                  if row.get("saved") is not None and (row["kind"] == "boot" or row.get("accepted") is True)]
    acknowledgements = []
    for row in saved_rows:
        meta = row["saved"]
        require(isinstance(meta, dict) and all(isinstance(meta.get(name), str)
                for name in ("path", "sha256", "state_hash", "source_hash", "functional_hash")),
                "Incomplete committed Save/Resume metadata")
        capture = available.get(meta["sha256"])
        if capture is None:
            path = Path(meta["path"])
            validate_path(path)
            require(path.is_file() and path.stat().st_size <= 4 * 1024 * 1024,
                    "A recorded saved state was not retained")
            raw = path.read_bytes()
            require(hashlib.sha256(raw).hexdigest() == meta["sha256"],
                    "Recorded Save changed; retain a byte-exact data capture after each Save")
            capture = (path, strict_json(raw.decode("utf-8")))
            available[meta["sha256"]] = capture
        path, envelope = capture
        require(envelope.get("schema") == "forge.game-save" and envelope.get("version") == 1
                and all(envelope.get(name) == meta[name] for name in ("state_hash", "source_hash", "functional_hash")),
                "Save envelope differs from native metadata")
        if row["kind"] == "command" and row["command"].get("kind") == "save":
            require(meta["source_hash"] == row["source_hash"] and meta["functional_hash"] == row["functional_hash"]
                    and envelope["state"].get("tick") == row["tick"]
                    and envelope["state"].get("sequence") == row["sequence"], "Save acknowledgement/state is incoherent")
            acknowledgements.append((row, envelope))
        if row["kind"] == "boot" and row["resumed"] and meta["source_hash"] == row["source_hash"]:
            require(envelope["state"] == row["state"], "New-process Resume changed the saved initial state")
    pairs = []
    for rows in runs:
        boot = rows[0]
        if not boot["resumed"]:
            continue
        meta = boot.get("saved") or {}
        for previous, envelope in acknowledgements:
            requested = any(row["kind"] == "command" and row.get("accepted") is True
                            and row["command"].get("kind") == "resume_next_launch"
                            and (row.get("saved") or {}).get("sha256") == meta.get("sha256")
                            for earlier in runs if earlier[0]["pid"] == previous["pid"] for row in earlier)
            if requested and previous["pid"] != boot["pid"] and previous["source_hash"] == boot["source_hash"] \
                    and previous["functional_hash"] == boot["functional_hash"] \
                    and previous["saved"]["sha256"] == meta.get("sha256") and envelope["state"] == boot["state"]:
                pairs.append({"saved_pid": previous["pid"], "resumed_pid": boot["pid"],
                              "save_sha256": meta["sha256"], "source_hash": boot["source_hash"]})
    return {"save_acknowledgements": len(acknowledgements), "new_process_resume_pairs": pairs,
            "save_new_process_resume": bool(pairs),
            "save_capture_pins": [{"path": str(path), "sha256": pin} for pin, (path, _) in available.items()],
            "state_integrity_limit": "Rust GameSession::resume verifies canonical state_hash; this reader binds the same bytes and exact initial state."}


def gameplay_document(archive):
    document = archive["head"]["corrections"]["gameplay"]
    require(document.get("schema") == "forge.gameplay" and document.get("version") == 1,
            "Pinned archive has no supported authored gameplay document")
    return document


def authored_initial_state(document):
    """Read-only expected serialized GameSession::new, before engine projection."""
    return {"sequence": 0, "tick": 0,
            "actors": {actor["id"]: {"position": actor["position"], "vitality": actor["max_vitality"],
                "stamina": document["profile"]["max_stamina"], "inventory": {}, "fleeing": False}
                for actor in document["actors"]},
            "objects": {obj["id"]: {"position": obj["position"], "rotation_xyzw": [0, 0, 0, 1],
                "initiator": None, "consumed": False, "destroyed": False,
                "open": obj["kind"].get("kind") == "door" and obj["kind"].get("initially_open") is True,
                "burning": False, "released": False, "in_motion": False} for obj in document["objects"]},
            "quests": {quest["id"]: "available" for quest in document["quests"]}, "receipts": [], "contacts": {},
            "last_frame": None, "requires_physics_revalidation": False, "tick_events_used": 0, "dialogue_cursors": {}}


def assess_p01_p04(runs, document, persistence):
    """Document-bound walkthrough facts, separate from Studio/human acceptance."""
    player = document["player"]
    linked = [place for place in document["places"] if place.get("links")]
    require(len(linked) >= 3, "P01 needs at least three linked authored places")
    place_ids = {place["id"] for place in document["places"]}
    require(all(set(place["links"]) <= place_ids for place in linked), "Authored place link is missing")
    enemies = {actor["id"] for actor in document["actors"] if actor.get("role") == "enemy"}
    require(enemies, "P01 authored document has no enemy")
    quest_ids = {quest["id"] for quest in document["quests"]}
    fresh, direct, environment = [], set(), set()
    visits = {}
    completed_runs = []
    for rows in runs:
        boot = rows[0]
        ready = [row for row in rows if row["kind"] == "frame" and row.get("ready")]
        if not boot["resumed"]:
            initial = boot["state"]
            clean = initial == authored_initial_state(document)
            fresh.append({"pid": boot["pid"], "authored_initial_state": clean})
        visits[boot["pid"]] = [place["id"] for place in linked if any(
            row.get("player", {}).get("grounded") is True
            and player in row["actors"]
            and math.hypot(row["actors"][player]["position"]["x"] - place["position"]["x"],
                           row["actors"][player]["position"]["z"] - place["position"]["z"]) <= 3
            and abs(row["actors"][player]["position"]["y"] - place["position"]["y"]) <= 2
            for row in ready)]
        action_receipts = {receipt["request"]["command_id"]: receipt for row in ready
                           if (receipt := row.get("last_action_receipt")) is not None}
        final = ready[-1]
        completed = quest_ids and all(final["quests"].get(quest) == "completed" for quest in quest_ids)
        consumed = all(final["objects"].get(obj["id"], {}).get("consumed") is True
                       for obj in document["objects"] if obj["kind"].get("kind") == "pickup"
                       and obj["kind"].get("item") in {q["required_item"] for q in document["quests"]})
        no_duplicate = all(final["actors"][player].get("inventory", {}).get(q["required_item"], 0)
                           == (q["reward_quantity"] if q.get("reward_item") == q["required_item"] else 0)
                           for q in document["quests"])
        if completed and consumed and no_duplicate:
            completed_runs.append(boot["pid"])
        for enemy in enemies:
            if final["actors"].get(enemy, {}).get("vitality", 1) != 0:
                continue
            direct_damage = any(receipt["request"]["action"].get("kind") == "attack"
                                and receipt["request"]["action"].get("target") == enemy
                                and any(e.get("target") == enemy and e.get("contact") is True and e.get("amount", 0) > 0
                                        for e in events(receipt["events"], "damage")) for receipt in action_receipts.values())
            environmental_damage = any(any(e.get("target") == enemy and e.get("amount", 0) > 0
                and ((e.get("contact") is True and any(c.get("target") == enemy
                        for c in row["last_frame"]["frame"].get("contacts", [])))
                     or (e.get("contact") is False and any(h.get("actor") == enemy and h.get("line_of_sight") is True
                        for h in row["last_frame"]["frame"].get("heat", []))))
                for e in events(row["last_frame"]["events"], "damage")) for row in ready)
            if boot["pid"] in completed_runs and not boot["resumed"]:
                if direct_damage:
                    direct.add(enemy)
                if environmental_damage and not direct_damage:
                    environment.add(enemy)
    checks = {"p01_three_places": any(len(ids) >= 3 for ids in visits.values()),
              "p01_two_approaches": bool(direct & environment),
              "p01_completed_loot_quest": bool(completed_runs),
              "p04_fresh_resume_isolation": bool(fresh) and all(item["authored_initial_state"] for item in fresh)
                  and persistence["save_new_process_resume"]}
    return {"checks": checks, "place_visits": visits, "direct_defeated_enemies": sorted(direct),
            "environment_defeated_enemies": sorted(environment), "completed_quest_pids": completed_runs,
            "fresh_initial_states": fresh, "studio_authoring_ui_verified": False,
            "studio_replay_selector_verified": False, "revision_ui_verified": False,
            "limits": ["Authored places, two causal approaches and isolated fresh/resume are machine observations.",
                       "Studio creation/replay selection, visible revision labels and human acceptance remain separate."]}


def verify_compatible_retouch(initial_rows, resumed_rows, old_archive, new_archive, fixture, captures, validate_path):
    old, new = gameplay_document(old_archive), gameplay_document(new_archive)
    first, boot = initial_rows[0], resumed_rows[0]
    require(first["pid"] != boot["pid"] and boot["resumed"] is True
            and first["source_hash"] != boot["source_hash"] and first["archive_hash"] != boot["archive_hash"]
            and first["functional_hash"] == boot["functional_hash"]
            and old["module_id"] == new["module_id"] and old["area_id"] == new["area_id"],
            "P02 must explicitly resume in a new process onto a distinct compatible authored revision")
    require(fixture.get("schema") == "forge.native-gameplay.fixture" and fixture.get("version") == 1
            and fixture.get("status") == "DATA_ONLY_APPLICATION_FIXTURE" and fixture.get("compatible_retouch") is True
            and fixture.get("application_commands_used") is True and fixture.get("source_unchanged") is True
            and fixture.get("copied_product_code") is False and fixture.get("studio_ui_verified") is False
            and fixture.get("archive_sha256") == boot["archive_hash"] and fixture.get("head_sha256") == boot["source_hash"]
            and fixture.get("source_archive_sha256") == first["archive_hash"]
            and fixture.get("source_head_sha256") == first["source_hash"]
            and fixture.get("functional_hash") == boot["functional_hash"], "P02 SDK provenance differs from the two revisions")
    receipt = fixture["authoring_receipt"]
    require(receipt["applied"]["gameplay"] == new and receipt["applied"] == new_archive["head"]["corrections"]
            and receipt["request"]["expected_hash"] == first["source_hash"]
            and receipt["resulting_hash"] == boot["source_hash"]
            and receipt["resulting_revision"] == old_archive["head"]["revision"] + 1
            and new_archive["head"]["revision"] == receipt["resulting_revision"], "P02 authoring receipt is not the actual applied revision")
    old_nodes = {node["id"]: node for dialogue in old["dialogues"] for node in dialogue["nodes"]}
    new_nodes = {node["id"]: node for dialogue in new["dialogues"] for node in dialogue["nodes"]}
    changed_text = [key for key in old_nodes if new_nodes[key]["text"] != old_nodes[key]["text"]]
    old_doors = {obj["id"]: obj for obj in old["objects"] if obj["kind"]["kind"] == "door"}
    new_doors = {obj["id"]: obj for obj in new["objects"] if obj["kind"]["kind"] == "door"}
    moved_doors = [key for key in old_doors if new_doors[key]["position"] != old_doors[key]["position"]]
    require(changed_text and moved_doors, "P02 retouch did not change dialogue and door pose")
    persistence = verify_saved_observations([initial_rows, resumed_rows], captures, validate_path)
    meta = boot["saved"]
    saved = next((strict_json(Path(pin["path"]).read_text(encoding="utf-8"))
                  for pin in persistence["save_capture_pins"] if pin["sha256"] == meta["sha256"]), None)
    require(saved is not None and meta["source_hash"] == first["source_hash"], "P02 did not load the retained old save")
    require(any(row["kind"] == "command" and row.get("accepted") is True
                and row["command"].get("kind") == "save" and (row.get("saved") or {}).get("sha256") == meta["sha256"]
                for row in initial_rows)
            and any(row["kind"] == "command" and row.get("accepted") is True
                    and row["command"].get("kind") == "resume_next_launch"
                    and row["command"].get("allow_compatible_edits") is True
                    and (row.get("saved") or {}).get("sha256") == meta["sha256"] for row in initial_rows),
            "P02 save and compatible resume were not explicitly accepted by the old process")
    expected = copy.deepcopy(saved["state"])
    expected.update(last_frame=None, tick_events_used=0, requires_physics_revalidation=True)
    for key, door in new_doors.items():
        expected["objects"][key]["position"] = door["position"]
    require(boot["state"] == expected, "P02 resume changed saved gameplay beyond documented geometry revalidation")
    require(any(row["kind"] == "frame" and row.get("ready") and row["last_frame"]["frame"]["source_hash"] == boot["source_hash"]
                for row in resumed_rows), "P02 did not revalidate physics against the retouched source")
    return {"compatible_retouch_preserved_state": True, "old_pid": first["pid"], "new_pid": boot["pid"],
            "saved_sha256": meta["sha256"], "changed_dialogue_nodes": changed_text, "moved_doors": moved_doors,
            "save_capture_pins": persistence["save_capture_pins"], "studio_ui_verified": False,
            "limits": ["SDK retouch and native compatible resume are observed; Studio retouch UI and refusal recovery are not inferred."]}
