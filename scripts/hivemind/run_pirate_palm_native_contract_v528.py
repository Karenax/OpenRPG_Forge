"""Reuse the owned helper; inventory importer additions without claiming source parity."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_palm_native_contract_v523.py')
prior=Path(__file__).with_name('run_pirate_palm_native_contract_v527.py')
code=recipe.read_text('utf-8').replace('v523','v528').replace('V523','V527')
code=code.replace('pirate-lod-thumbnail-batch06-v513/HELPER-OWNERSHIP.v1.json','pirate-palm-native-contract-v527/HELPER-OWNERSHIP.v1.json')
old="""    root=source
    for part in Path(card['source_asset_path']).parts:root=root.parent"""
new="""    manifest_path=W/'hivemind-run/pirate-island/source.v1.json'
    manifest=read(manifest_path)
    assert manifest['private_only'] and manifest['license']=='LicenseRef-Unity-Asset-Store-EULA'
    root=Path(manifest['source_root'])
    assert sha(root/card['source_asset_path'])==card['source_sha256']
    archive=V/'originals/unity-purchased/hivemind/pirate-island'/Path(manifest['archive_path']).name
    assert sha(archive)==manifest['archive_sha256']
    original_hashes={str(manifest_path.parent/r['stored_path']):r['sha256'] for r in manifest['files']}
    target_source_text=(root/MATERIAL).read_text('utf-8-sig')+'\\n'+(root/GRAPH).read_text('utf-8-sig')"""
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('Path(__file__),base,controlled,helper,request,auditpath','recipe,prior,Path(__file__),manifest_path,archive,base,controlled,helper,request,auditpath')
old="""    helper=PROJECT/'Assets/Editor/PiratePalmContractV527.cs';assert not helper.exists()
    helper.write_bytes(controlled.read_bytes());assert sha(helper)==sha(controlled)
    audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(controlled),sha256=sha(helper)))"""
new="""    helper=PROJECT/'Assets/Editor/PiratePalmContractV527.cs'
    assert helper.is_file() and sha(helper)==sha(controlled)
    assert any(h['private_path']==str(helper) and h['sha256']==sha(helper) for h in audit['helpers'])"""
assert code.count(old)==1;code=code.replace(old,new)
old="""        assert original.is_file(),str(original)
        pins[str(original)]=sha(original)"""
new="""        if not original.is_file():
            assert str(original) not in original_hashes,str(original)
            assert p.suffix in {'.meta','.mat'},str(p)
            meta=p if p.suffix=='.meta' else p.with_suffix(p.suffix+'.meta')
            import re
            match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)
            assert match and match[1] not in target_source_text,str(p)
            copies.append(dict(source=None,copy=str(p),source_sha256=None,private_before_sha256=sha(p),kind='private_importer_addition_not_referenced_by_target_material_or_graph',original_source_claimed=False,material_fidelity_qualified=False,guid=match[1]))
            continue
        pins[str(original)]=sha(original)
        assert pins[str(original)]==original_hashes[str(original)],str(original)"""
assert code.count(old)==1;code=code.replace(old,new)
exec(compile(code,str(recipe)+'#owned-helper-and-explicit-unqualified-importer-additions','exec'),globals())
