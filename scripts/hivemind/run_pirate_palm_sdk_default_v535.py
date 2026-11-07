"""Capture the native SDK white resource used by the declared ORM default."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_palm_native_contract_v523.py')
prior=Path(__file__).with_name('run_pirate_palm_native_contract_v528.py')
code=recipe.read_text('utf-8').replace('v523','v535').replace('V523','V535')
code=code.replace('pirate-lod-thumbnail-batch06-v513/HELPER-OWNERSHIP.v1.json','pirate-palm-native-contract-v528/HELPER-OWNERSHIP.v1.json')
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
needle="    controlled=O/'PiratePalmContractV535.cs'"
addition="""    old_report='public MaterialData[] materials;'
    assert code.count(old_report)==1
    code=code.replace(old_report,'public MaterialData[] materials;public string sdk_white_base64,sdk_white_format,sdk_white_graphics_format;public int sdk_white_width,sdk_white_height,sdk_white_mips;public bool sdk_white_pixels_checked;')
    old_write='File.WriteAllText(r.generated_shader,text,new System.Text.UTF8Encoding(false));'
    assert code.count(old_write)==1
    capture='var sdkWhite=Texture2D.whiteTexture;if(sdkWhite==null||!sdkWhite.isReadable)throw new Exception("Native SDK white resource unavailable");foreach(var pixel in sdkWhite.GetPixels())if(pixel.r!=1f||pixel.g!=1f||pixel.b!=1f||pixel.a!=1f)throw new Exception("Unexpected native SDK white pixels");var sdkBytes=sdkWhite.GetRawTextureData();if(sdkBytes==null||sdkBytes.Length==0)throw new Exception("Native SDK resource bytes absent");'
    code=code.replace(old_write,capture+old_write)
    old_initializer='unity_version=Application.unityVersion,graph_path='
    assert code.count(old_initializer)==1
    code=code.replace(old_initializer,'sdk_white_base64=Convert.ToBase64String(sdkBytes),sdk_white_format=sdkWhite.format.ToString(),sdk_white_graphics_format=sdkWhite.graphicsFormat.ToString(),sdk_white_width=sdkWhite.width,sdk_white_height=sdkWhite.height,sdk_white_mips=sdkWhite.mipmapCount,sdk_white_pixels_checked=true,'+old_initializer)
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
exec(compile(code,str(recipe)+'#measured-native-sdk-resource','exec'),globals())
