"""Distinguish generated private folder metadata from preserved purchased files."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_palm_native_contract_v523.py')
prior=Path(__file__).with_name('run_pirate_palm_native_contract_v524.py')
code=recipe.read_text('utf-8').replace('v523','v527').replace('V523','V527')
code=code.replace('pirate-lod-thumbnail-batch06-v513/HELPER-OWNERSHIP.v1.json','pirate-palm-native-contract-v524/HELPER-OWNERSHIP.v1.json')
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
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('Path(__file__),base,controlled,helper,request,auditpath','recipe,prior,Path(__file__),manifest_path,archive,base,controlled,helper,request,auditpath')
old="""        assert original.is_file(),str(original)
        pins[str(original)]=sha(original)"""
new="""        if not original.is_file():
            assert p.suffix=='.meta' and p.with_suffix('').is_dir(),str(original)
            assert str(original) not in original_hashes,str(original)
            copies.append(dict(source=None,copy=str(p),source_sha256=None,private_before_sha256=sha(p),kind='private_importer_generated_folder_metadata',original_source_claimed=False))
            continue
        pins[str(original)]=sha(original)
        assert pins[str(original)]==original_hashes[str(original)],str(original)"""
assert code.count(old)==1;code=code.replace(old,new)
exec(compile(code,str(recipe)+'#original-files-and-explicit-generated-folder-metadata','exec'),globals())
