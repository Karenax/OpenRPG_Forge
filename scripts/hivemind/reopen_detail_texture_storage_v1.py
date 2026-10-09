"""Independent identity, layout, mip byte and original source verification."""
from pathlib import Path
import hashlib,json,runpy
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-native-texture-storage-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def main():
    target=ROOT/'TEXTURE-REOPEN.v1.json';assert not target.exists()
    prepared=read(ROOT/'PREPARED.v1.json');run=read(ROOT/'RUN-RESULT.v1.json');storage=read(ROOT/'STORAGE-REOPEN.v1.json')
    assert run['unity_exit_code']==0 and run['textures_verified']==2 and run['finished_utc']
    native_path=ROOT/'export/RESULT.json';assert sha(native_path)==storage['native_result_sha256']
    native=read(native_path);manifest=read(ROOT/'manifest.json')
    assert native['total']==native['matched']==len(native['cases'])==len(manifest['cases'])==2 and native['failed']==0
    assert native['color_space']=='Linear' and native['graphics_device_type']=='Direct3D11'
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),ROOT/'PREPARED.v1.json',ROOT/'RUN-RESULT.v1.json',ROOT/'STORAGE-REOPEN.v1.json',ROOT/'manifest.json',native_path]}
    for p,h in prepared['input_pins'].items():assert sha(p)==h;pins[p]=h
    differences=[]
    for row in prepared['copies']:
        assert sha(row['source'])==row['sha256']
        current=sha(row['copy'])
        if current!=row['sha256']:
            assert row['is_meta'];differences.append(dict(path=row['copy'],before_sha256=row['sha256'],after_sha256=current))
        pins[row['copy']]=current
    assert differences==storage['private_meta_differences']
    verifier=runpy.run_path(prepared['verifier'],run_name='independent_detail_texture_verifier')
    rows=[];ids=set()
    for i,(request,row) in enumerate(zip(manifest['cases'],native['cases'],strict=True)):
        assert request['expected_guid'] not in ids;ids.add(request['expected_guid'])
        directory=ROOT/'export'/f'{i:03}';checked=verifier['check_case'](request,row,directory)
        assert row['graphics_format'] in ('RGBA_DXT5_SRGB','RGBA_DXT5_UNorm')
        for mip in row['mips']:
            assert mip['bytes']==((mip['width']+3)//4)*((mip['height']+3)//4)*16
        for name,digest in checked['buffers'].items():pins[str(directory/name)]=digest
        checked.update(original_texture_guid=request['expected_guid'],original_file_id=request['expected_file_id']);rows.append(checked)
    verifier['controls'](ROOT)
    assert all(sha(p)==h for p,h in pins.items())
    report=dict(schema='openrpg.imported-unity-texture-storage-reopening.v1',
        source_texture_storage_captures_verified=2,mips_verified=sum(row['mip_count'] for row in rows),
        original_sources_and_materials_unchanged=True,unity_result_sha256=sha(native_path),
        rows=rows,input_pins=pins,format_block_layout_checked=True,private_meta_differences=differences,
        original_project_color_space_qualified=False,source_texture_orientation_qualified=False,
        source_normal_decode_qualified=False,render_parity_qualified=False,editor_runtime_verified=False,full_import=False)
    target.write_text(json.dumps(report,indent=2),'utf-8')
    print(json.dumps(dict(textures_independently_reopened=2,mips_verified=report['mips_verified'],full_import=False)))
if __name__=='__main__':main()
