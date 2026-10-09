"""Do not equate zero compiler exit with HDRP source pass coverage."""
import datetime,hashlib,json,os,re
from pathlib import Path
W=Path(r"F:\OpenRPG_ImportWork");R=W/"unity-dungeon-original-shadergraph-compiler-v1";T=W/"SUIVI_LOTS_HIVEMIND"
native_path=R/"NATIVE-RESULT.v1.json";native=json.loads(native_path.read_bytes())
settings=R/"project"/"ProjectSettings"/"GraphicsSettings.asset";quality=R/"project"/"ProjectSettings"/"QualitySettings.asset"
text=settings.read_text(encoding="utf-8-sig");qtext=quality.read_text(encoding="utf-8-sig")
if not re.search(r'm_CustomRenderPipeline: \{fileID: 0\}',text):raise RuntimeError("Pipeline settings changed; reobserve")
if any("fileID: 0" not in line for line in qtext.splitlines() if "customRenderPipeline:" in line):raise RuntimeError("A quality pipeline is selected; investigate")
rows=[{"material_guid":c["material_guid"],"shader_guid":c["shader_guid"],"shader_name":c["shader_name"],"passes":c["passes"],"has_only_unnamed_pass":len(c["passes"])==1 and c["passes"][0]["name"]=="<Unnamed Pass 0>"} for c in native["cases"]]
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
report={"observed_utc":now,"status":"native_properties_retained_hdrp_pass_qualification_refused", "materials_with_properties":len(rows),"only_unnamed_pass_materials":sum(x["has_only_unnamed_pass"] for x in rows),"hdrp_asset_selected_in_project":False,"native_result_sha256":hashlib.sha256(native_path.read_bytes()).hexdigest(),"settings_sha256":hashlib.sha256(settings.read_bytes()).hexdigest(),"quality_settings_sha256":hashlib.sha256(quality.read_bytes()).hexdigest(),"rows":rows,"compiler_api_called":True,"target_pipeline_pass_coverage_verified":False,"original_shader_render_parity_qualified":False,"supersedes_qualification_in":str(T/"L02_MATERIAUX_NATIFS_BINDINGS.v1.json"),"next_action":"Select the appropriate source HDRP pipeline in the isolated project and recapture supported named passes, pipeline identity and IsPassCompiled; serialize after the current Unity job.","docs":"https://docs.unity.com/en-us/engine/6000.7/script-reference/unityeditor/shaderutil/compilepass","new_qualified_assets":0,"full_import":False}
path=T/"L02_CONTROLE_PIPELINE_NATIF.v1.json"
with path.open("x",encoding="utf-8") as out:json.dump(report,out,ensure_ascii=False,indent=2)
statepath=T/"ETAT.v1.json";original=statepath.read_bytes();state=json.loads(original)
lot=next(b for b in state["batches"] if b["id"]=="L02")
lot["native_material_compilation"].update(status=report["status"],pipeline_qualification_receipt=str(path),target_pipeline_pass_coverage_verified=False,only_unnamed_pass_materials=report["only_unnamed_pass_materials"])
state["updated_utc"]=now;tmp=statepath.with_suffix(f".json.{os.getpid()}.tmp");tmp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding="utf-8")
if statepath.read_bytes()!=original:tmp.unlink();raise RuntimeError("Concurrent tracking change")
os.replace(tmp,statepath)
entry=f"\n## L02 — limite de pipeline native — {now}\nLe résultat 42 captures conserve les propriétés/bindings, mais ne qualifie pas les passes HDRP : aucun asset HDRP sélectionné dans GraphicsSettings ni QualitySettings, {report['only_unnamed_pass_materials']} matériaux avec seulement <Unnamed Pass 0>. Ancien reçu conservé, qualification corrigée et refusée. Prochaine capture : pipeline approprié, shader.isSupported, IsPassCompiled et passes nommées. Reçu : {path}\n"
for target in [T/"JOURNAL.md",W/"RUN_STATUS.md"]:
    with target.open("a",encoding="utf-8") as out:out.write(entry)
plan=T/"PLAN_ET_SUIVI.md";content=plan.read_text(encoding="utf-8").replace("42 matériaux compilés dans Unity et propriétés/passes vérifiées ; bindings des textures source contrôlés, rendu Forge restant", "42 propriétés/bindings natifs capturés ; passes HDRP non qualifiées : pipeline absent et 39 passes sans nom ; nouveau contrôle pipeline requis, rendu Forge restant")
plan.write_text(content,encoding="utf-8")
print(json.dumps({"receipt":str(path),"only_unnamed_pass_materials":report["only_unnamed_pass_materials"],"hdrp_pass_qualification":False}))
