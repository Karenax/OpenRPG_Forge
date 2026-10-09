"""Record only terminal material stages in the existing batch tracking files."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"


def read(path):
    return json.loads(path.read_text("utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def replace_if_unchanged(path, previous_hash, text):
    temporary = path.with_name(path.name + ".delta-materials.tmp")
    if temporary.exists():
        raise RuntimeError("Previous tracking temporary file preserved")
    temporary.write_text(text, "utf-8")
    if sha(path) != previous_hash:
        raise RuntimeError("Tracking changed concurrently; replacement refused")
    os.replace(temporary, path)


def main():
    preparation_path = W / "nested-prefab-delta-material-preparation-v1/RUN-RESULT.v1.json"
    graph_path = W / "nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json"
    topology_path = W / "nested-prefab-delta-material-topology-controller-v1/RUN-RESULT.v1.json"
    preparation, graphs, topology = [read(p) for p in [preparation_path, graph_path, topology_path]]
    if (preparation["status"] != "delta_prefab_material_sources_reopened"
            or topology["status"] != "delta_material_active_topology_verified"
            or graphs["materials"] != 25 or not graphs["all_input_pins_unchanged"]
            or len(graphs["actual_parser_controls_passed"]) != 6):
        raise RuntimeError("Terminal delta material proofs required")
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state_path = T / "ETAT.v1.json"
    previous = sha(state_path)
    state = read(state_path)
    batch = next(b for b in state["batches"] if b["id"] == "L04")
    stage = {"status": "source_properties_and_active_topology_verified",
             "prefabs": 11, "source_materials": 25, "unique_payloads_and_meta_reopened": 218,
             "source_graphs": 6, "parser_controls_passed": 6,
             "compiled_topologies": 25, "source_preparation_receipt": str(preparation_path),
             "graph_properties_receipt": str(graph_path), "active_topology_receipt": str(topology_path),
             "unresolved_texture": {"material": "M_FirePit_A_01", "property": "_NoiseMask",
                                    "guid": "abc00000000007232662921548558196",
                                    "declared_in_source_graph": True},
             "native_compiler_liveness_qualified": False,
             "gpu_rendered": False, "studio_verified": False, "full_import": False}
    kernel_path = W / "nested-prefab-delta-material-wgsl-v1/RESULT.v1.json"
    if kernel_path.exists():
        kernels = read(kernel_path)
        if kernels.get("original_sources_and_helpers_unchanged"):
            stage["wgsl"] = {"status": kernels["status"], "receipt": str(kernel_path),
                             "requested": kernels["requested_materials"],
                             "naga_validated": kernels["naga_validated_materials"],
                             "refusals": kernels["failed_materials"], "failures": kernels["failures"]}
    batch["delta_prefab_materials"] = stage
    state["updated_utc"] = stamp
    state["goal_complete"] = False
    replace_if_unchanged(state_path, previous, json.dumps(state, ensure_ascii=False, indent=2))
    text = ("\n\n## L04 — matériaux des onze nouveaux prefabs — " + stamp + "\n\n"
            "25 matériaux supplémentaires : 218 fichiers source et métadonnées réouverts, "
            "25 bindings de propriétés / six graphes source, six contrôles de parseur réussis, "
            "25 topologies actives compilées avec codes 0 et garde finale inchangée. "
            "Lecteurs existants conservés ; seuls chemins de configuration adaptés. "
            "Reçus : `" + str(preparation_path) + "` et `" + str(topology_path) + "`.\n\n"
            "M_FirePit_A_01 conserve une texture _NoiseMask absente des manifestes extraits ; "
            "la propriété est déclarée dans son graphe source. Aucun défaut de remplacement. "
            "Liveness native, rendu, GLB final et Studio restent non qualifiés. "
            "Les 32 prefabs précédents ne sont pas refaits.\n")
    if "wgsl" in stage:
        text += ("\nWGSL : " + str(stage["wgsl"]["naga_validated"]) + "/25 kernels validés "
                 "par Naga ; " + str(stage["wgsl"]["refusals"]) + " refus conservés. Reçu : `" +
                 str(kernel_path) + "`. Ce contrôle ne qualifie pas le rendu GPU.\n")
    for name in ["PLAN_ET_SUIVI.md", "JOURNAL.md"]:
        path = T / name
        prior = sha(path)
        replace_if_unchanged(path, prior, path.read_text("utf-8-sig") + text)
    print(json.dumps(stage, ensure_ascii=False))


if __name__ == "__main__":
    main()
