"""Independently reopen identities, raw mips, importers, source hashes and samplers."""
from pathlib import Path
import json
import runpy
from verify_native_urp_material_snapshot import sha

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-delta-rocks-firepit-texture-storage-v1'

def read(path):return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    target=ROOT/'TEXTURE-REOPEN.v1.json'
    assert not target.exists()
    prepared=read(ROOT/'PREPARED.v1.json');result=read(ROOT/'RUN-RESULT.v1.json')
    assert result.get('finished_utc') and result['unity_exit_code']==0 and result['textures_verified']==10
    native_path=ROOT/'export/RESULT.json'
    assert sha(native_path)==result['native_result_sha256']
    native=read(native_path);manifest=read(ROOT/'manifest.json')
    assert native['total']==native['matched']==len(native['cases'])==len(manifest['cases'])==10
    assert native['failed']==0 and native['graphics_device_type']=='Direct3D11'
    assert native['unity_version']=='6000.5.8f1' and native['color_space']=='Linear'
    verifier=runpy.run_path(prepared['verifier'],run_name='independent_texture_verifier')
    pins={str(p):sha(p) for p in [Path(__file__),ROOT/'PREPARED.v1.json',ROOT/'RUN-RESULT.v1.json',
        ROOT/'manifest.json',native_path,Path(prepared['verifier'])]}
    private_meta={row['copied']:row for row in prepared['copies'] if row['copied'].endswith('.meta')}
    differences=[]
    for path,digest in prepared['input_pins'].items():
        if sha(path)!=digest:
            assert path in private_meta, 'Unexpected changed input: '+path
            source=Path(private_meta[path]['original'])
            assert sha(source)==digest
            differences.append(dict(path=path,original_sha256=digest,observed_sha256=sha(path)))
        else:pins[path]=digest
    rows=[]
    expected_ids=set()
    for index,(request,row) in enumerate(zip(manifest['cases'],native['cases'])):
        assert request['expected_guid'] not in expected_ids
        expected_ids.add(request['expected_guid'])
        directory=ROOT/'export'/f'{index:03}'
        checked=verifier['check_case'](request,row,directory)
        layout={'R8G8B8_UNorm':(1,1,3),'R8G8B8A8_UNorm':(1,1,4),'R8G8B8A8_SRGB':(1,1,4),
            'RGBA_DXT1_UNorm':(4,4,8),'RGBA_DXT1_SRGB':(4,4,8),
            'RGBA_DXT5_UNorm':(4,4,16),'RGBA_DXT5_SRGB':(4,4,16),
            'R_BC4_UNorm':(4,4,8)}
        assert row['graphics_format'] in layout, 'Unsupported native layout must be ported explicitly: '+row['graphics_format']
        bw,bh,block=layout[row['graphics_format']]
        for mip in row['mips']:
            expected=((mip['width']+bw-1)//bw)*((mip['height']+bh-1)//bh)*block
            assert mip['bytes']==expected, 'Native format/mip storage mismatch'
        for name,digest in checked['buffers'].items():pins[str(directory/name)]=digest
        checked.update(original_texture_guid=request['expected_guid'],original_file_id=request['expected_file_id'])
        rows.append(checked)
    # Run the existing identity, corruption and truncation controls on F.
    verifier['controls'](ROOT)
    assert all(sha(p)==h for p,h in pins.items())
    report=dict(schema='openrpg.imported-unity-texture-storage-reopening.v1',
        source_texture_storage_captures_verified=10,mips_verified=sum(r['mip_count'] for r in rows),
        original_sources_and_materials_unchanged=True,unity_result_sha256=sha(native_path),
        verifier_sha256=sha(prepared['verifier']),private_meta_serialization_differences=differences,
        format_block_layout_checked=True,rows=rows,input_pins=pins,
        original_project_color_space_qualified=False,source_texture_orientation_qualified=False,
        source_normal_decode_qualified=False,render_parity_qualified=False,editor_runtime_verified=False,full_import=False)
    with target.open('x',encoding='utf-8') as stream:json.dump(report,stream,indent=2,allow_nan=False)
    print(json.dumps(dict(textures_independently_reopened=10,mips_verified=report['mips_verified'],full_import=False)))

if __name__=='__main__':main()
