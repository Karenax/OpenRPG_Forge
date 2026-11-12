"""Create an isolated importer candidate for explicitly checked LOD variants."""
from pathlib import Path

src=Path('E:/CodexData/skills/openrpg-import-assets/scripts/source_support_import.py')
dest=Path('E:/OpenRPG_Forge/.build/asset-conversion/source-support-import-lod-candidate-20261112.py')
raw=src.read_text(encoding='utf8')
needle='def validate_catalog(catalog: dict, vault: Path, packs: set[str], model_ids: set[str], only_ids=None) -> dict:'
function='''def validate_lod_source_variant(original: dict, owner: dict, vault: Path) -> None:
    """Confirm all source text is identical except numeric LOD thresholds."""
    pattern = re.compile(r"^(\\s*(?:-\\s*)?screenRelativeHeight:\\s*)[-+0-9.eE]+(\\s*)$")
    lines = []
    for entry in (original, owner):
        path = safe_relative(vault, require(entry, "source_file"), "source_file")
        if path.stat().st_size > 2 * 1024 * 1024:
            fail("LOD variant source exceeds comparison limit")
        try:
            text = path.read_bytes().decode("utf-8-sig").splitlines()
        except UnicodeDecodeError:
            fail("LOD variant source is not UTF-8")
        if len(text) > 10000:
            fail("LOD variant source exceeds line limit")
        lines.append(text)
    if lines[0] == lines[1] or [pattern.sub(r"\\g<1><LOD_THRESHOLD>\\g<2>", line) for line in lines[0]] != [pattern.sub(r"\\g<1><LOD_THRESHOLD>\\g<2>", line) for line in lines[1]]:
        fail("LOD variant differs beyond thresholds")


'''
old='''        for alias in item.get("consolidated_sources", []):
            validate_entry(alias["original_detail"], vault, packs, model_ids, caches)
'''
new='''        for alias in item.get("consolidated_sources", []):
            validate_entry(alias["original_detail"], vault, packs, model_ids, caches)
            if alias.get("source_variation") == {"kind": "lod_thresholds_only"}:
                validate_lod_source_variant(alias["original_detail"], item, vault)
'''
guard='''                    or any(original.get(k) != card.get(k) for k in ("source_sha256", "source_meta_sha256"))'''
replacement='''                    or original.get("source_meta_sha256") != card.get("source_meta_sha256")
                    or (original.get("source_sha256") != card.get("source_sha256")
                        and item.get("source_variation") != {"kind": "lod_thresholds_only"})
                    or ("source_variation" in item and item["source_variation"] != {"kind": "lod_thresholds_only"})'''
if raw.count(needle)!=1 or raw.count(old)!=1 or raw.count(guard)!=1:raise ValueError('importer structure changed')
candidate=raw.replace(needle,function+needle).replace(old,new).replace(guard,replacement)
with dest.open('x',encoding='utf8',newline='') as stream:stream.write(candidate)
print(dest)
