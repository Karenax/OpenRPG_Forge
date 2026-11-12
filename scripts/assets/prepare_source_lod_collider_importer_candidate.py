"""Build an isolated importer candidate for LOD/collider-label variants."""
from pathlib import Path
import re

source=Path('E:/CodexData/skills/openrpg-import-assets/scripts/source_support_import.py')
dest=Path('E:/OpenRPG_Forge/.build/asset-conversion/source-support-import-lod-collider-candidate-20261112.py')
raw=source.read_text(encoding='utf8')
compare=Path(__file__).with_name('compare_unity_lod_collider_variants.py').read_text(encoding='utf8')
for name in ('HEADER','SCALAR','VECTOR','COLLIDER'):
    compare=re.sub(r'\b'+name+r'\b','LODCOLLIDER_'+name,compare)
compare=compare.replace('def normalize(', 'def lodcollider_normalize(').replace('normalize(a)','lodcollider_normalize(a)').replace('normalize(b)','lodcollider_normalize(b)').replace('def compare(', 'def compare_lod_collider(')
validation='''def validate_lod_collider_source_variant(original: dict, owner: dict, vault: Path) -> None:
    texts = []
    for entry in (original, owner):
        path = safe_relative(vault, require(entry, "source_file"), "source_file")
        if path.stat().st_size > 2 * 1024 * 1024:
            fail("LOD collider source exceeds comparison limit")
        try:
            text = path.read_bytes().decode("utf-8-sig")
        except UnicodeDecodeError:
            fail("LOD collider source is not UTF-8")
        if text.count("\\n") > 10000:
            fail("LOD collider source exceeds line limit")
        texts.append(text)
    if not compare_lod_collider(*texts):
        fail("LOD collider source differs beyond permitted fields")


'''
needle='def validate_catalog(catalog: dict, vault: Path, packs: set[str], model_ids: set[str], only_ids=None) -> dict:'
old='''            if alias.get("source_variation") == {"kind": "lod_thresholds_only"}:
                validate_lod_source_variant(alias["original_detail"], item, vault)
'''
new=old+'''            elif alias.get("source_variation") == {"kind": "lod_collider_only"}:
                validate_lod_collider_source_variant(alias["original_detail"], item, vault)
'''
guard='''                    or (original.get("source_sha256") != card.get("source_sha256")
                        and item.get("source_variation") != {"kind": "lod_thresholds_only"})
                    or ("source_variation" in item and item["source_variation"] != {"kind": "lod_thresholds_only"})'''
replacement='''                    or (original.get("source_sha256") != card.get("source_sha256")
                        and item.get("source_variation") not in ({"kind": "lod_thresholds_only"}, {"kind": "lod_collider_only"}))
                    or ("source_variation" in item and item["source_variation"] not in ({"kind": "lod_thresholds_only"}, {"kind": "lod_collider_only"}))'''
if raw.count(needle)!=1 or raw.count(old)!=1 or raw.count(guard)!=1:raise ValueError('importer structure changed')
candidate=raw.replace(needle,compare+'\n'+validation+needle).replace(old,new).replace(guard,replacement)
with dest.open('x',encoding='utf8',newline='') as stream:stream.write(candidate)
print(dest)
