from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
source=(S/'queue_townsmith_paint_generated_source_v2.py').read_text(encoding='utf-8')
source=source.replace('townsmith-s-paint-sdk-generated-source-v2','townsmith-s-paint-native-textures-v1')
source=source.replace('SourceTownsmithPaintGeneratedV2','SourceTownsmithPaintTexturesV1')
source=source.replace('OpenRPGSourceTownsmithPaintGeneratedV2','OpenRPGSourceTownsmithPaintTexturesV1')
anchor='ROOT.mkdir();HELPER.write_bytes(CS.read_bytes())'
assert source.count(anchor)==1
source=source.replace(anchor,'''ROOT.mkdir();HELPER.write_bytes(CS.read_bytes())
    texture_inventory=W/"townsmith-s-paint-texture-inventory-v1/RESULT.v1.json";inventory=read(texture_inventory)
    for p,h in inventory["input_pins"].items():assert sha(p)==h,p
    pins[str(texture_inventory)]=sha(texture_inventory)
    input_path=ROOT/"TEXTURES-INPUT.v1.json";input_path.write_text(json.dumps({"rows":[{"guid":r["guid"],"authored_texture_index":r["authored_texture_index"]} for r in inventory["rows"]]},indent=2),encoding="utf-8")
    pins[str(input_path)]=sha(input_path)''')
source=source.replace('townsmith_paint_sdk_generation_running','townsmith_paint_native_texture_capture_running')
source=source.replace('townsmith_paint_sdk_generation_refusal_retained','townsmith_paint_native_texture_capture_refusal_retained')
old='assert native["status"]=="original_shadergraph_sdk_source_generated" and native["native_shader_supported"] and not native["native_shader_errors"]'
assert source.count(old)==1
source=source.replace(old,'''assert len(native["rows"])==10 and all(len(r["mips"])==r["mipmap_count"] for r in native["rows"])
        assert not native["source_importer_modified"]
        storage_pins={m["file"]:sha(Path(m["file"])) for r in native["rows"] for m in r["mips"] if m["native_readback_ok"]}''')
old='state.update(status="townsmith_paint_sdk_source_generated_inputs_unchanged",source_inputs_unchanged=True,generated_source_sha256=sha(Path(native["generated_source"])),material_render_qualified=False,complete_assets_delivered_increment=0)'
assert source.count(old)==1
source=source.replace(old,'''state.update(status="townsmith_paint_native_texture_profiles_captured_inputs_unchanged",source_inputs_unchanged=True,native_receipt_sha256=sha(ROOT/"NATIVE-RESULT.v1.json"),native_readback_complete=native["native_readback_complete"],native_mip_pins=storage_pins,material_render_qualified=False,complete_assets_delivered_increment=0)''')
path=S/'queue_townsmith_paint_texture_capture_v1.py'
assert not path.exists()
path.write_text(source,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(path),'prepare'],check=True)
