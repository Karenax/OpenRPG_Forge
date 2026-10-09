"""Bind the two Village prefabs to their 36 genuine successful mesh derivatives.

The parent bank refused a later Blood meta. Its successful cases are projected
explicitly; no broader bank or imported asset is promoted by this assembly.
"""
from pathlib import Path
import datetime,hashlib,json,subprocess,sys
from assemble_nested_prefab_bindings import bind_renderer,identity,reflected_column_major
from convert_closed_nested_geometry import reopen_streams

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'rocks-firepit-scene-bindings-v1'
BANK=W/'nested-prefab-captured-geometry-derivatives-v3'
HIERARCHY=W/'hivemind-prefab-instance-source-closure-v1'
PACK='hivemind.native-american-village'

def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    assert not ROOT.exists()
    bank_path=BANK/'RUN-RESULT.v1.json';bank=read(bank_path)
    assert bank['finished_utc'] and bank['status']=='refused_or_partial'
    assert 'realistic-blood-vfx' in bank['error']
    selected=[r for r in bank['rows'] if r['pack_id']==PACK]
    assert len(selected)==36
    proof_path=HIERARCHY/'HIERARCHY-REOPEN.v1.json';proof=read(proof_path)
    assert proof['source_and_capture_pins_unchanged_before_after']
    capture_path=HIERARCHY/'projects/native-american-village.HIERARCHY-RESULT.v1.json'
    assert sha(capture_path)==proof['proof_pins'][str(capture_path)]
    capture=read(capture_path);assert not capture['failed'] and not capture['purchased_scripts_copied_or_executed']
    helper=Path(__file__).with_name('assemble_nested_prefab_bindings.py')
    pins={str(p):sha(p) for p in [bank_path,proof_path,capture_path,helper,Path(__file__).resolve()]}
    native_root=W/'unity-nested-prefab-static-geometry-capture-v1/native-american-village'
    format_path=native_root/'SOURCE-FORMAT-REOPEN.normalized-before-native.v2.json'
    geometry_path=native_root/'EXPORT-REOPEN.normalized-before-native.v2.json'
    format_proof=read(format_path);geometry_proof=read(geometry_path)
    assert format_proof['original_sources_unchanged'] and format_proof['source_hierarchy_mesh_formats_match']
    assert format_proof['verified_source_meshes']==geometry_proof['source_geometry_captures_verified']==36
    assert sha(geometry_path)==format_proof['geometry_reopen_sha256']
    manifest_path=native_root/'manifest.json';export_path=native_root/'export/RESULT.json'
    assert sha(manifest_path)==geometry_proof['manifest_sha256']
    assert sha(export_path)==geometry_proof['unity_result_sha256']
    manifest=read(manifest_path);export=read(export_path)
    assert export['failed']==0 and export['matched']==36 and len(export['cases'])==len(manifest['cases'])==36
    migration_path=W/'native-village-isolated-importer-migration-v1/MIGRATION-REOPEN.v1.json'
    migration=read(migration_path)
    assert migration['original_sources_unchanged'] and migration['preexisting_geometry_settings_unchanged']
    assert migration['native_geometry_reopening_sha256']==sha(geometry_path)
    assert migration['source_format_reopening_sha256']==sha(format_path)
    prepared_path=native_root/'PREPARED.normalized-before-native.v2.json'
    assert sha(prepared_path)==migration['prepared_capture_sha256']
    prepared=read(prepared_path)
    for item in prepared['copies']:
        for path_key,hash_key in [('original','original_sha256'),('original_meta','original_meta_sha256'),
            ('project_copy','project_copy_sha256'),('project_meta_copy','project_meta_copy_sha256')]:
            p=item[path_key];h=item[hash_key]
            if p==migration['copy']['project_meta_copy']:h=migration['isolated_meta_after_sha256']
            assert sha(Path(p))==h,p;pins[p]=h
    for p in [format_path,geometry_path,manifest_path,export_path,migration_path,prepared_path,
        Path(__file__).with_name('convert_closed_nested_geometry.py')]:pins[str(p)]=sha(p)
    index={};asset_ids=set();streams=0
    for row in selected:
        assert row['native_capture_terminal_verified'] and row['derived_streams_reopened']>0
        directory=Path(row['derivative']);p=directory/'GEOMETRY.v1.json'
        assert directory.resolve().is_relative_to(BANK.resolve())
        assert sha(p)==row['geometry_receipt_sha256']
        receipt=read(p);assert receipt['original_streams_unchanged']
        # The generic converter deliberately leaves this leaf flag false. The
        # closed native capture, source-format proof and later REOPEN close it.
        assert receipt['source_capture_terminal_verified'] is False
        ordinal=row['capture_ordinal'];metadata=export['cases'][ordinal];request=manifest['cases'][ordinal]
        assert request['asset_id']==metadata['asset_id']==row['asset_id']
        assert row['source_format_receipt_sha256']==sha(format_path)
        assert row['source_geometry_receipt_sha256']==sha(geometry_path)
        assert metadata==receipt['metadata']
        raw={name:(native_root/'export'/f'{ordinal:03}'/name).read_bytes()
            for name in geometry_proof['rows'][ordinal]['buffers']}
        for name,h in geometry_proof['rows'][ordinal]['buffers'].items():
            source=native_root/'export'/f'{ordinal:03}'/name
            assert sha(source)==h;pins[str(source)]=h
        outputs={name:(directory/name).read_bytes() for name in receipt['derived_streams']}
        assert reopen_streams(raw,outputs,metadata)==row['derived_streams_reopened']
        pins[str(p)]=sha(p)
        reopened=directory/'REOPEN.v1.json';assert read(reopened)==row;pins[str(reopened)]=sha(reopened)
        for source,h in receipt['source_stream_pins'].items():
            assert sha(Path(source))==h,source;pins[source]=h
        for name,description in receipt['derived_streams'].items():
            path=directory/name;assert path.resolve().is_relative_to(directory.resolve())
            assert sha(path)==description['sha256'];pins[str(path)]=description['sha256'];streams+=1
        key=(row['source_mesh_identity']['guid'],str(row['source_mesh_identity']['file_id']))
        assert key not in index;index[key]=row
        asset_ids.update(a['asset_id'] for a in row['prefab_instance_aliases'])
    assert len(asset_ids)==2
    cases=[c for c in capture['cases'] if c['asset_id'] in asset_ids];assert len(cases)==2
    output=[]
    for case in cases:
        bindings=[];deferred=[]
        for node in case['nodes']:
            for renderer in node['renderers']:
                if not renderer.get('static_mesh_adapter_applicable'):
                    deferred.append(dict(node_path=node['path'],renderer=renderer,reason='requires_type_specific_adapter'));continue
                key=identity(renderer['mesh']['source']);assert key in index
                binding=bind_renderer(case['asset_id'],node,renderer,index[key])
                assert not binding['material_draw_mapping_requires_native_qualification']
                bindings.append(binding)
        nodes=[dict(source_node=node,target_local_matrix_column_major=reflected_column_major(node['source_local_matrix_row_major']),
            target_source_world_matrix_column_major=reflected_column_major(node['source_local_to_world_matrix_row_major'])) for node in case['nodes']]
        materials=sorted({identity(m)[0] for b in bindings for m in b['original_material_slots']})
        output.append(dict(asset_id=case['asset_id'],pack_id=PACK,nodes=nodes,static_renderer_bindings=bindings,
            deferred_renderers=deferred,original_lod_groups=case['lod_groups'],original_root=case['root'],material_guids=materials,
            declared_submesh_draws=sum(b['source_submesh_count'] for b in bindings),
            original_components_colliders_and_inactive_nodes_preserved=True,glb_created=False,full_import=False))
    rocks=next(r for r in output if 'sm-rocks-002' in r['asset_id'])
    firepit=next(r for r in output if 'sm-firepit-01' in r['asset_id'])
    print(json.dumps(dict(observed=[dict(asset_id=r['asset_id'],nodes=len(r['nodes']),draws=r['declared_submesh_draws'],materials=len(r['material_guids']),deferred=len(r['deferred_renderers'])) for r in output])),flush=True)
    assert len(rocks['nodes'])==86 and len(rocks['static_renderer_bindings'])==57 and rocks['declared_submesh_draws']==92 and len(rocks['material_guids'])==8
    assert len(firepit['nodes'])==9 and len(firepit['static_renderer_bindings'])==4 and firepit['declared_submesh_draws']==4 and len(firepit['material_guids'])==1
    assert len(firepit['deferred_renderers'])==2
    effect_materials={identity(m)[0] for d in firepit['deferred_renderers'] for m in d['renderer']['original_material_slots']}
    assert effect_materials=={'d10c1dc24453f8c4eb26d8ed7223d745','251f329e9f65944409f96791dedb09b0'}
    ROOT.mkdir()
    with (ROOT/'assembly-controls.stdout.log').open('xb') as out,(ROOT/'assembly-controls.stderr.log').open('xb') as err:
        code=subprocess.run([sys.executable,'-B',str(helper),'self-test'],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
    assert code==0
    controls=read(ROOT/'assembly-controls.stdout.log')['assembly_controls_passed'];assert len(controls)==7
    for p,h in pins.items():assert sha(Path(p))==h,p
    result=dict(status='two_native_prefab_scene_bindings_reopened',prefabs=2,meshes_reopened=36,derived_streams_rehashed=streams,
        rows=output,assembly_controls_passed=controls,input_pins=pins,
        successful_geometry_case_projection_only=True,parent_bank_failure_retained=bank['error'],
        original_importer_settings_parity_qualified=False,material_programs_installed=False,glb_created=False,
        gpu_render_qualified=False,studio_verified=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(prefabs=2,meshes=36,streams=streams,rocks_nodes=86,rocks_renderers=57,rocks_draws=92,rocks_materials=8,
        firepit_nodes=9,firepit_draws=4,firepit_static_materials=1,firepit_effect_materials=2,assembly_controls=len(controls),full_import=False)))

if __name__=='__main__':main()
