"""Refresh the human batch dashboard from receipts, preserving earlier history."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"

def read(path):
    return json.loads(path.read_text("utf-8-sig"))

def replace_guarded(path, before, content):
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    temporary.write_text(content, encoding="utf-8")
    if path.read_bytes() != before:
        raise RuntimeError(f"Concurrent update preserved: {path}")
    os.replace(temporary, path)

def main():
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    glb_path = W / "canonical-captured-texture-glb-integration-v1/RUN-RESULT.v3.json"
    mesh_path = W / "unity-nested-prefab-static-geometry-remaining-v2/RUN-RESULT.v3.json"
    glb = read(glb_path)
    mesh = read(mesh_path)
    packs = read(W / "unity-nested-prefab-static-geometry-remaining-v2/PROJECT-CAPTURE-RUNS.v3.json")
    verified = [p for p in packs if p.get("verified")]
    for p in verified:
        if p.get("unity_exit_code") != 0 or p.get("independent_verifier_exit_code") != 0:
            raise RuntimeError("Pack proof inconsistent")
    count = sum(p["requested_meshes"] for p in verified)
    # Controllers update these two receipts separately; refuse an inconsistent snapshot.
    if count != mesh["verified_source_meshes"]:
        raise RuntimeError("Capture receipts changing; retry after controller update")
    log = (glb_path.parent / "contract-tests.v3.log").read_text("utf-8-sig")
    contract = glb.get("contract_tests_passed", 0)
    if contract and (glb.get("contract_tests_exit_code") != 0 or f"{contract} passed; 0 failed" not in log):
        raise RuntimeError("Actual contract tests absent")
    state_path = T / "ETAT.v1.json"
    before = state_path.read_bytes()
    state = json.loads(before)
    lots = {row["id"]: row for row in state["batches"]}
    lots["L01"]["canonical_glb_texture_integration"].update(
        status=glb["status"], contract_tests_passed=contract,
        contract_tests_exit_code=glb.get("contract_tests_exit_code"),
        native_tests_executed=glb.get("native_tests_executed", False),
        studio_verified=glb.get("studio_verified", False),
        receipt_sha256=hashlib.sha256(glb_path.read_bytes()).hexdigest())
    lots["L03"]["current_native_capture"] = {
        "receipt": str(mesh_path), "verified_source_meshes": count,
        "requested_source_meshes": mesh["requested_meshes"],
        "verified_packs": verified, "current_pack": mesh.get("pack_id"),
        "current_status": mesh["status"], "geometry_converted": False,
        "full_import": False, "partial_medieval_13_excluded_from_this_count": True}
    state["updated_utc"] = stamp
    replace_guarded(state_path, before, json.dumps(state, ensure_ascii=False, indent=2))
    plan = T / "PLAN_ET_SUIVI.md"
    old = plan.read_bytes()
    oldsha = hashlib.sha256(old).hexdigest()
    archive = T / f"PLAN_HISTORIQUE_{oldsha[:16]}.md"
    if not archive.exists():
        archive.write_bytes(old)
    if archive.read_bytes() != old:
        raise RuntimeError("Plan archive differs")
    table = f"""# Conversion Hivemind — suivi par lots

État relu le {stamp}. Objectif actif, import complet **non validé**.

Fichiers livrés : `F:\\OpenRPG_AssetVault`. Travail, conversions intermédiaires et preuves : `F:\\OpenRPG_ImportWork`. État structuré : `ETAT.v1.json`. Historique des résultats : `JOURNAL.md`.

## Lots et critères de fin

| Lot | Travail | État actuel | Critère de fin |
|---|---|---|---|
| L00 | Sources, licences, métadonnées et transfert sur F: | Transfert initial vérifié : 39 725 fichiers, zéro écart SHA | Sources conservées et inventaire réconcilié ; réaudit final L09 |
| L01 | Moteur : shaders, normales, textures, chargement GLB | ACTIF : BC4 natif/Web réussi ; {contract} tests du contrat de textures réussis ; raccordement natif GLB en cours | Chargement réel et rendu dans Studio, puis placement/sauvegarde/réouverture |
| L02 | Conversion Dungeon, prefabs et LOD | 1 580 meshes et 277 textures source capturés ; contrôle HDRP en file ; références de textures manquantes à résoudre | GLB fidèles par objet logique, matériaux et rendu vérifiés |
| L03 | Conversion des autres packs statiques, pack par pack | {count}/{mesh['requested_meshes']} captures de meshes vérifiées dans cette série, {len(verified)} packs ; capture actuelle : {mesh.get('pack_id')} | Assemblage, GLB réouvert, matériaux, images, placement et runtime par pack |
| L04 | Matériaux et textures | Contrôles de couleurs/normales disponibles ; raccordement et refus restants | Source et dérivé comparés, samplers/mips/transparence fidèles, rendu vérifié |
| L05 | Rigs, animations et morphs | Capture du rig Medieval en file ; vérificateur préparé ; flags capturés | Déformations, clips et temps conservés ; GLB animé et lecture runtime vérifiés |
| L06 | Effets, particules, Blood et Dust | Blood : 46 textures/498 mips comparés, écart RGBA nul dans le domaine RAW capturé ; rendu des effets restant | Effets visibles, paramètres/durée fidèles et comportement runtime |
| L07 | Terrains, sprites, collisions et dépendances spéciales | Sources et contrôles intermédiaires conservés ; formats finaux et intégration restants | Dépendances résolues et comportement réel dans Forge |
| L08 | Bibliothèque, images, recherche, filtres et fiches | Contrôle réel partiel de 26 735 entrées ; audit intégral actif sans verdict terminal | Recherche/filtres/images/fiches, placement commun, sauvegarde et réouverture vérifiés |
| L09 | Vérification finale de toute la collection | À faire après les livraisons | Inventaire attendu/livré/refusé réconcilié ; aucune conversion obligatoire manquante |

## Ordre de travail immédiat

1. Terminer L01.3 : contrôles natifs du raccordement textures/GLB, puis Web. Les {contract} tests de contrat sont déjà réussis ; ne pas les refaire.
2. L01.4–L01.5 : raccorder les scènes au chargement et à l'aperçu réels, vérifier textures/matériaux, placement et sauvegarde. Le candidat de géométrie a réussi six tests sur 39 scènes ; ce résultat ne vaut pas qualification Studio.
3. L03 poursuit les captures déjà lancées sans duplication. Chaque pack passe ensuite par conversion/assemblage → réouverture → matériaux → indexation/images → placement/runtime.
4. File Unity conservée : captures statiques → contrôle HDRP → normale constante du shader → rig Medieval → vérification indépendante du rig. Ne pas lancer plusieurs tâches incompatibles sur le même environnement.
5. L06 et L07 s'intègrent après les capacités moteur nécessaires ; L08 accompagne chaque livraison. L09 clôture l'ensemble.

## Captures L03 réellement vérifiées dans cette série

| Pack | Meshes source vérifiés | Conversion finale |
|---|---:|---|
"""
    for p in verified:
        table += f"| {p['pack_id']} | {p['requested_meshes']} | Restante |\n"
    table += f"""
Les 13 captures statiques partielles Medieval disposent d'un reçu séparé et ne sont pas incluses dans les {count}. Le 14e mesh contient un rig : il relève de L05, sans aplatissement.

## Comment lire l'avancement

Les étapes sont distinctes : source conservée → capture vérifiée → conversion réouverte → indexation → visible dans Studio → placement et runtime validés. Les compteurs de tests, textures ou meshes ne deviennent pas des compteurs d'objets utilisables.

Le registre déclare 14 219 modèles et 12 516 fiches source, soit 26 735 entrées. Ce total n'est pas le nombre d'assets logiques entièrement validés. Aucun pourcentage global trompeur n'est calculé.

Chaque résultat annoncé indique le lot, le nouveau travail effectivement vérifié, ce qui reste et le reçu sur F:. Les états anciens restent dans `JOURNAL.md` et `{archive.name}` ; ils ne décrivent pas l'état actuel.
"""
    replace_guarded(plan, old, table)
    with (T / "JOURNAL.md").open("a", encoding="utf-8") as output:
        output.write(f"\n## Tableau de lots actualisé — {stamp}\nL01 : {contract} tests du contrat réussis, état {glb['status']}. L03 : {count}/{mesh['requested_meshes']} captures source vérifiées, {len(verified)} packs. Aucun nouvel asset runtime déclaré. Ancien plan conservé dans {archive.name}.\n")
    print(json.dumps({"plan": str(plan), "contract_tests_passed": contract,
                      "verified_source_meshes": count, "verified_packs": len(verified),
                      "full_import": False}, ensure_ascii=False))

if __name__ == "__main__":
    main()
