"""Build an isolated importer candidate for explicit pipeline light variants."""
from pathlib import Path
import re

source=Path('E:/CodexData/skills/openrpg-import-assets/scripts/source_support_import.py')
dest=Path('E:/OpenRPG_Forge/.build/asset-conversion/source-support-import-pipeline-light-candidate-20261112.py')
raw=source.read_text(encoding='utf8')
compare=Path(__file__).with_name('compare_unity_light_prefabs.py').read_text(encoding='utf8')
for name in ('HEADER','SCRIPT','COMPONENT','GAMEOBJECT','ENABLED','PIPELINE_LIGHT_SCRIPTS'):
    compare=re.sub(r'\b'+name+r'\b','LIGHTVAR_'+name,compare)
compare=compare.replace('def normalized(', 'def lightvar_normalized(').replace('normalized(first)','lightvar_normalized(first)').replace('normalized(second)','lightvar_normalized(second)').replace('def compare(', 'def compare_pipeline_light(')
validation='''def validate_pipeline_light_source_variant(original: dict, owner: dict, vault: Path) -> None:
    texts = []
    for entry in (original, owner):
        path = safe_relative(vault, require(entry, "source_file"), "source_file")
        if path.stat().st_size > 2 * 1024 * 1024:
            fail("pipeline light source exceeds comparison limit")
        try:
            texts.append(path.read_bytes().decode("utf-8-sig"))
        except UnicodeDecodeError:
            fail("pipeline light source is not UTF-8")
    try:
        result = compare_pipeline_light(*texts)
    except ValueError as error:
        fail("invalid pipeline light variant: " + str(error))
    if not result["equal_outside_pipeline_lights"]:
        fail("pipeline light source differs beyond permitted fields")


'''
needle='def validate_catalog(catalog: dict, vault: Path, packs: set[str], model_ids: set[str], only_ids=None) -> dict:'
old='''            elif alias.get("source_variation") == {"kind": "lod_collider_only"}:
                validate_lod_collider_source_variant(alias["original_detail"], item, vault)
'''
new=old+'''            elif alias.get("source_variation") == {"kind": "pipeline_light_only"}:
                validate_pipeline_light_source_variant(alias["original_detail"], item, vault)
'''
variants='({"kind": "lod_thresholds_only"}, {"kind": "lod_collider_only"})'
extended='({"kind": "lod_thresholds_only"}, {"kind": "lod_collider_only"}, {"kind": "pipeline_light_only"})'
if raw.count(needle)!=1 or raw.count(old)!=1 or raw.count(variants)!=2:raise ValueError('importer structure changed')
candidate=raw.replace(needle,compare+'\n'+validation+needle).replace(old,new).replace(variants,extended)
with dest.open('x',encoding='utf8',newline='') as stream:stream.write(candidate)
print(dest)
