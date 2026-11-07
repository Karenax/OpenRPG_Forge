"""Recapture native BC evidence in the existing Forge wire protocol, without rewriting old receipts."""
from pathlib import Path
S = Path(__file__).resolve().parent
BASE = S / 'run_house_compressed_storage_v641.py'
CS = S / 'candidates/HouseCompressedStorageV641.cs'
candidate = S / 'candidates/HouseWireStorageV668.cs'
source = CS.read_text('utf-8-sig').replace('OpenRPGHouseCompressedStorageV641', 'OpenRPGHouseWireStorageV668')
old = 'public bool native_decoded_mips_equal,restored_decoded_mips_equal,importer_restored_equal,meta_bytes_restored_equal;'
new = old + 'public string settings_before,settings_restored,error="";public int width,height,mip_count,whole_bytes,filter_mode,wrap_u,wrap_v,wrap_w,aniso;public float mip_bias;public Level[] mips;public bool settings_restored_equal,gpu_samples_bit_exact,private_readability_override;'
assert source.count(old) == 1
source = source.replace(old,new)
source = source.replace('public Row[] textures;', 'public Row[] textures,rows;')
anchor = 'byte[] metaBytes=File.ReadAllBytes(meta);Verify(t,shader,item);'
assert source.count(anchor) == 1
source = source.replace(anchor, '''byte[] metaBytes=File.ReadAllBytes(meta);
if(readable)throw new Exception("Wire protocol requires originally unreadable importer");
row.settings_before=json;row.width=t.width;row.height=t.height;row.mip_count=t.mipmapCount;
row.filter_mode=(int)t.filterMode;row.wrap_u=(int)t.wrapModeU;row.wrap_v=(int)t.wrapModeV;row.wrap_w=(int)t.wrapModeW;row.aniso=t.anisoLevel;row.mip_bias=t.mipMapBias;
row.private_readability_override=true;Verify(t,shader,item);''')
anchor = 'row.restored_decoded_mips_equal=true;rows.Add(row);'
assert source.count(anchor) == 1
source = source.replace(anchor, '''row.restored_decoded_mips_equal=true;
row.settings_restored=EditorJsonUtility.ToJson(importer,true);row.settings_restored_equal=row.importer_restored_equal;
row.gpu_samples_bit_exact=row.native_decoded_mips_equal&&row.restored_decoded_mips_equal;
row.mips=row.levels;row.whole_bytes=File.ReadAllBytes(row.whole_path).Length;rows.Add(row);''')
source = source.replace('textures=rows.ToArray()', 'textures=rows.ToArray(),rows=rows.ToArray()')
if candidate.exists():
    assert candidate.read_text('utf-8-sig') == source
else:
    candidate.write_text(source, 'utf-8')
code = BASE.read_text('utf-8-sig')
changes = {
    'house-compressed-storage-v641':'house-wire-native-storage-v668',
    'HouseCompressedStorageV641.cs':'HouseWireStorageV668.cs',
    'OpenRPGHouseCompressedStorageV641.Export':'OpenRPGHouseWireStorageV668.Export',
    'house-dxc-surface-reference-v638/HELPER-OWNERSHIP.v1.json':'house-native-prefab-geometry-v651/HELPER-OWNERSHIP.v1.json',
    "len(audit['helpers'])==30":"len(audit['helpers'])==36",
    'house-dxc-surface-reference-v638/PREPARED.v1.json':'house-native-prefab-geometry-v651/PREPARED.v1.json',
}
for old,new in changes.items():
    assert old in code,old
    code=code.replace(old,new)
exec(compile(code,str(BASE)+'#native-wire-protocol-recapture','exec'),globals())
