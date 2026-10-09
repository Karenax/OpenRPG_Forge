"""Reconcile native material bindings with original source GUIDs; never fill missing textures."""
import datetime, hashlib, json, os
from pathlib import Path
W=Path(r"F:\OpenRPG_ImportWork")
R=W/"unity-dungeon-original-shadergraph-compiler-v1"
T=W/"SUIVI_LOTS_HIVEMIND"
def read(p): return json.loads(p.read_bytes())
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
native_path=R/"NATIVE-RESULT.v1.json"
native=read(native_path); reopen=read(R/"REOPEN.v1.json"); run=read(R/"RUN-RESULT.v2.json")
if run["unity_exit_code"]!=0 or run["independent_verifier_exit_code"]!=0 or run["verified_native_materials"]!=42: raise RuntimeError("Native compilation proof incomplete")
if sha(native_path)!=reopen["native_result_sha256"] or sha(R/"verify_native.py")!=reopen["verifier_sha256"]: raise RuntimeError("Native proof changed")
if native["captured"]!=42 or native["failed"]!=0 or len(native["cases"])!=42: raise RuntimeError("Native collection incomplete")
queue_path=W/"queue"/"dungeon-builder"/"prefabs.json"
queue=read(queue_path)
rows=[]
for case in native["cases"]:
    guid=case["material_guid"]; source=queue["materials"][guid]
    source_path=Path(queue["source_root"])/source["source"]
    if sha(source_path)!=source["sha256"]: raise RuntimeError("Original material changed")
    if case["shader_guid"]!=source["shader_guid"] or case["error"] or case["shader_has_errors"]: raise RuntimeError("Native shader identity or compilation invalid")
    props={p["name"]:p for p in case["properties"]}
    bindings=[]
    for name, reference in source["textures"].items():
        prop=props.get(name)
        requested=reference.get("guid")
        rel=queue["guid_paths"].get(requested)
        exists=bool(rel and (Path(queue["source_root"])/rel).is_file())
        observation={"property":name,"original_texture_guid":requested,"original_scale":reference.get("scale"),"original_offset":reference.get("offset"),"source_path":rel,"source_exists":exists,"native_property_present":prop is not None}
        if prop is None:
            observation["status"]="saved_property_not_exposed_by_compiled_shader"
        elif prop["type"]!="Texture":
            raise RuntimeError("Source texture property changed type")
        else:
            observation.update(native_texture_null=prop["texture_null"],native_texture_guid=prop["texture_guid"],native_texture_file_id=prop["texture_file_id"],native_texture_path=prop["texture_asset_path"],native_default_texture_name=prop["default_texture_name"])
            if requested and exists:
                if prop["texture_null"] or prop["texture_guid"]!=requested or prop["texture_asset_path"]!=rel: raise RuntimeError("Resolvable texture has changed native binding")
                observation["status"]="original_guid_and_native_binding_match"
            elif requested and not exists:
                observation["status"]="missing_original_guid_still_unresolved_native_observation_retained"
            else:
                observation["status"]="authored_empty_reference_native_observation_retained"
        bindings.append(observation)
    rows.append({"material_guid":guid,"source_material_path":str(source_path),"source_sha256":source["sha256"],"shader_guid":case["shader_guid"],"native_passes":case["passes"],"native_property_count":len(props),"bindings":bindings})
missing=[dict(material_guid=row["material_guid"],**binding) for row in rows for binding in row["bindings"] if binding["original_texture_guid"] and not binding["source_exists"]]
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
report={"schema":"openrpg.native-original-material-binding-review.v1","observed_utc":now,"native_materials_verified":42,"native_graphics_api":native["graphics_api"],"native_unity_version":native["unity_version"],"native_reopen_receipt":str(R/"REOPEN.v1.json"),"native_result_sha256":sha(native_path),"source_queue_sha256":sha(queue_path),"rows":rows,"missing_original_texture_references":missing,"missing_original_texture_guids":sorted({x["original_texture_guid"] for x in missing}),"isolated_material_files_migrated_by_unity":len(reopen["changed_isolated_native_imports"]),"source_originals_unchanged_at_native_reopen":reopen["source_originals_unchanged"],"original_materials_rehashed_now":42,"null_is_not_white_or_bump_sampling_proof":True,"texture_substitutions":0,"gpu_render_parity_qualified":False,"studio_verified":False,"new_qualified_assets":0,"full_import":False}
path=T/"L02_MATERIAUX_NATIFS_BINDINGS.v1.json"
with path.open("x",encoding="utf-8") as out: json.dump(report,out,ensure_ascii=False,indent=2)
statepath=T/"ETAT.v1.json"; original=statepath.read_bytes(); state=json.loads(original)
lot=next(b for b in state["batches"] if b["id"]=="L02")
lot["native_material_compilation"]={"status":"42_original_materials_native_passes_properties_and_source_bindings_verified","receipt":str(path),"compiled_materials":42,"missing_original_texture_guids":report["missing_original_texture_guids"],"gpu_render_parity_qualified":False,"new_qualified_assets":0}
state["updated_utc"]=now; tmp=statepath.with_suffix(f".json.{os.getpid()}.tmp")
tmp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding="utf-8")
if statepath.read_bytes()!=original: tmp.unlink(); raise RuntimeError("Tracking changed concurrently")
os.replace(tmp,statepath)
entry=f"\n## L02 — 42 matériaux natifs et bindings source — {now}\nCompilation Unity et vérificateur indépendant terminés avec code 0 ; 42 matériaux sources réhachés, bindings des textures résolubles comparés par GUID et chemin. {len(missing)} références à {len(report['missing_original_texture_guids'])} GUID manquants restent explicites : observation native null ne prouve pas une texture blanche ou bump. Zéro remplacement, zéro nouvel asset livré ; rendu Forge restant. Reçu : {path}\n"
for target in [T/"JOURNAL.md",W/"RUN_STATUS.md"]:
    with target.open("a",encoding="utf-8") as out: out.write(entry)
plan=T/"PLAN_ET_SUIVI.md"; content=plan.read_text(encoding="utf-8")
content=content.replace("compilation ShaderGraph native en cours selon dernier rapport","42 matériaux compilés dans Unity et propriétés/passes vérifiées ; bindings des textures source contrôlés, rendu Forge restant")
plan.write_text(content,encoding="utf-8")
print(json.dumps({"receipt":str(path),"materials":42,"missing_references":len(missing),"missing_guids":report["missing_original_texture_guids"],"new_qualified_assets":0}))
