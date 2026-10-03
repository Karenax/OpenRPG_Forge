"""Combine independently acquired public physics proofs without inheriting PASS."""
import argparse
import importlib.util
import hashlib
import json
from pathlib import Path

CAMPAIGN_SOURCE_BYTES = Path(__file__).read_bytes()
CAMPAIGN_SOURCE_SHA256 = hashlib.sha256(CAMPAIGN_SOURCE_BYTES).hexdigest()
reader_path = Path(__file__).with_name("Check-Gate3PhysicsCausalEvidence.py")
READER_SOURCE_BYTES = reader_path.read_bytes()
READER_SOURCE_SHA256 = hashlib.sha256(READER_SOURCE_BYTES).hexdigest()
spec = importlib.util.spec_from_file_location("gate3_physics_causal", Path(__file__).with_name("Check-Gate3PhysicsCausalEvidence.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
if hashlib.sha256(reader_path.read_bytes()).hexdigest() != READER_SOURCE_SHA256:
    raise ValueError("Causal reader source changed while importing")

CAPABILITIES = {
    "push": ("push",),
    "throw": ("throw",),
    "impact_damage": ("impact_damage",),
    "fall_or_trap": ("fall_or_trap",),
    "fire": ("fire", "fire_heat_damage"),
    "water_extinguish": ("water_extinguish",),
    "destructible": ("destructible",),
    "danger_ai": ("danger_ai",),
}

# Existing causal observations remain available for partial runs. Completed
# Save/new-process Resume rows additionally require the durable object state.
PERSISTENCE_CHECKS = {
    "throw": ("throw_object_state_saved_resumed",),
    "impact_damage": ("impact_source_state_saved_resumed",),
    "fall_or_trap": ("trap_object_states_saved_resumed",),
    "destructible": ("trap_object_states_saved_resumed",),
}


def campaign(paths):
    assessments = [module.assess(path) for path in paths]
    identities = {(a["candidate_sha256"], a["authored_checkpoint_sha256"], a["authored_head_sha256"]) for a in assessments}
    gameplay_sources = {sha for a in assessments for sha in a["save_gameplay_source_hashes"]}
    if len(identities) != 1 or len(gameplay_sources) != 1:
        raise ValueError("All campaign proofs must use one exact candidate/archive/head/gameplay source")
    capability_rows = {}
    for name, required in CAPABILITIES.items():
        observed = [a["ledger"] for a in assessments if all(a["checks"].get(key) for key in required)]
        complete = [a["ledger"] for a in assessments if all(a["checks"].get(key)
                    for key in (*required, *PERSISTENCE_CHECKS.get(name, ())))
                    and a["checks"]["flow_completed_and_clean"]
                    and a["checks"]["source_preserved"]
                    and a["checks"]["loaded_public_scripts_provenance_verified"]
                    and a["checks"]["public_saves_committed_coherently"]]
        capability_rows[name] = {"observed_in_any_run": bool(observed), "observed_ledgers": observed,
                                 "completed_saved_resumed_run": bool(complete), "complete_ledgers": complete}
    quest = [a["ledger"] for a in assessments if a["checks"]["environmental_quest_completed_saved_resumed"]
             and a["checks"]["flow_completed_and_clean"] and a["checks"]["source_preserved"]
             and a["checks"]["loaded_public_scripts_provenance_verified"]
             and a["checks"]["public_saves_committed_coherently"]]
    candidate, archive, head = next(iter(identities))
    return {"candidate_sha256": candidate, "authored_checkpoint_sha256": archive,
            "authored_head_sha256": head, "gameplay_source_sha256": next(iter(gameplay_sources)),
            "capabilities": capability_rows, "environmental_quest_complete_ledgers": quest,
            "physics_campaign_observed_complete": all(v["completed_saved_resumed_run"] for v in capability_rows.values()) and bool(quest),
            "gate_qualified": False, "human_visual_review": False, "navigation_claim": False,
            "limitations": ["A partial run can contain valid causal observations; only clean completed Save/new-process Resume flows satisfy the campaign completion row.",
                            "Fire requires heat damage, not ignition alone. Whole Gate 3, human and navigation decisions are separate."]}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("ledgers", nargs="+")
    parser.add_argument("--output")
    args = parser.parse_args()
    result = campaign(args.ledgers)
    if hashlib.sha256(Path(__file__).read_bytes()).hexdigest() != CAMPAIGN_SOURCE_SHA256 or hashlib.sha256(reader_path.read_bytes()).hexdigest() != READER_SOURCE_SHA256:
        raise ValueError("An evidence reader source changed while combining proofs")
    if args.output:
        copies = []
        for name, data, sha in (("campaign", CAMPAIGN_SOURCE_BYTES, CAMPAIGN_SOURCE_SHA256), ("causal", READER_SOURCE_BYTES, READER_SOURCE_SHA256)):
            copy = Path(args.output).with_name(f"{name}-reader-{sha}.py")
            copy.write_bytes(data)
            copies.append({"kind": name, "source_copy_path": str(copy.resolve()), "sha256_at_load": sha})
        result["loaded_reader_sources"] = copies
        Path(args.output).write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"physics_campaign_observed_complete": result["physics_campaign_observed_complete"],
                      "capabilities": {name: {k: row[k] for k in ("observed_in_any_run", "completed_saved_resumed_run")} for name, row in result["capabilities"].items()},
                      "output": args.output}, ensure_ascii=False))
