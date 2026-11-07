"""Resolve preserved extracted sources through their inventory and Vault archive."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_palm_native_contract_v523.py')
code=recipe.read_text('utf-8').replace('v523','v524').replace('V523','V524')
old="""    root=source
    for part in Path(card['source_asset_path']).parts:root=root.parent"""
new="""    manifest_path=W/'hivemind-run/pirate-island/source.v1.json'
    manifest=read(manifest_path)
    assert manifest['private_only'] and manifest['license']=='LicenseRef-Unity-Asset-Store-EULA'
    root=Path(manifest['source_root'])
    assert sha(root/card['source_asset_path'])==card['source_sha256']
    archive=V/'originals/unity-purchased/hivemind/pirate-island'/Path(manifest['archive_path']).name
    assert sha(archive)==manifest['archive_sha256']
    original_hashes={str(manifest_path.parent/r['stored_path']):r['sha256'] for r in manifest['files']}"""
assert code.count(old)==1
code=code.replace(old,new)
code=code.replace('Path(__file__),base,controlled,helper,request,auditpath', 'recipe,Path(__file__),manifest_path,archive,base,controlled,helper,request,auditpath')
old="""        pins[str(original)]=sha(original)
        copies.append"""
new="""        pins[str(original)]=sha(original)
        assert pins[str(original)]==original_hashes[str(original)],str(original)
        copies.append"""
assert code.count(old)==1
code=code.replace(old,new)
exec(compile(code,str(recipe)+'#manifest-and-vault-provenance','exec'),globals())
