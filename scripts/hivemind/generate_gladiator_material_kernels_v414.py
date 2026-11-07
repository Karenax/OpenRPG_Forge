"""Reuse the existing node emitter on six pinned source graphs, retaining refusals."""
from pathlib import Path
import importlib.util,json,re,subprocess,traceback
from datetime import datetime,timezone
S=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('existing_wgsl_generator',S/'generate_nested_material_wgsl.py')
generator=importlib.util.module_from_spec(spec);spec.loader.exec_module(generator)
W=generator.W;O=W/'gladiator-material-kernels-v414';read=generator.read;sha=generator.sha
assert not O.exists()
top=W/'gladiator-active-material-ir-v386/RESULT.v1.json';batch=read(top)
assert batch['status']=='six_gladiator_material_topologies_compiled' and len(batch['rows'])==6
seal=W/'gladiator-native-texture-seal-v413/RESULT.v1.json';texture_seal=read(seal)
assert texture_seal['texture_count']==12
for p,h in texture_seal['output_pins'].items():assert sha(p)==h,p
closure=W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json';files=read(closure)['files']
assets={}
for f in files:
    p=Path(f['source']);assert sha(p)==f['sha256']
    if p.suffix=='.meta':
        match=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text('utf-8-sig'),re.M)
        assert match
        asset=Path(str(p)[:-5]);assets[match[1]]=[asset,p]
assert sha(generator.VALIDATOR)=='bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
inputs=[Path(__file__),S/'generate_nested_material_wgsl.py',top,seal,closure,generator.EMITTER,generator.KERNEL,generator.EXTRA,generator.VALIDATOR]
pins={str(p):sha(p) for p in inputs}
emitter=generator.EMITTER.read_text('utf-8-sig');prefix=generator.KERNEL.read_text('utf-8-sig')+'\n'+generator.EXTRA.read_text('utf-8-sig')
O.mkdir();rows=[];failures=[]
for row in batch['rows']:
    case=O/row['guid'];case.mkdir()
    try:
        irpath=Path(row['path']);assert sha(irpath)==row['sha256'];pins[str(irpath)]=row['sha256'];ir=read(irpath)
        resources=[];normals=set()
        for n in ir['nodes']:
            for entry in n['inputs']:
                value=entry['value'].get('literal')
                if not isinstance(value,dict) or 'guid' not in value:continue
                guid=value['guid'];assert guid in assets and guid in {t['guid'] for t in texture_seal['rows']}
                if guid not in resources:resources.append(guid)
                if n['opcode']=='SampleTexture2DNode' and n['source_parameters'].get('m_TextureType')==1:normals.add(guid)
        assert len(resources)==3 and len(normals)==1
        bindings=[]
        for guid in resources:
            payloads=[]
            for p in assets[guid]:
                pins[str(p)]=sha(p);payloads.append(dict(path=str(p),sha256=pins[str(p)]))
            bindings.append(dict(guid=guid,source_files=payloads,requires_normal_decode=guid in normals,normal_decode_mode_qualified=False))
        ctx=dict(ir=ir,indices={g:i for i,g in enumerate(resources)},normal_guids=normals,dims={},body=[],input_requirements=[],prefix=prefix,case=case,scalar=generator.scalar,vec=generator.vec,math=generator.math)
        exec(compile(emitter,str(generator.EMITTER),'exec'),ctx)
        output=case/'source_material.wgsl'
        with (case/'naga.stdout.log').open('xb') as out,(case/'naga.stderr.log').open('xb') as err:
            process=subprocess.run([str(generator.VALIDATOR),str(output)],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
        assert process.returncode==0,'Naga refused kernel'
        rows.append(dict(material_guid=row['guid'],name=row['name'],wgsl=str(output),wgsl_sha256=sha(output),active_ir_sha256=row['sha256'],actual_naga_exit_code=0,source_texture_bindings=bindings,input_requirements=ctx['input_requirements'],terminal_fields=ctx['terminal_fields'],source_alpha_terminal=ir['terminals']['SurfaceDescription.Alpha'],texture_count=3,render_qualified=False))
    except Exception as exc:
        (case/'failure.log').write_text(traceback.format_exc(),'utf-8');failures.append(dict(material_guid=row['guid'],error=repr(exc)))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='six_material_wgsl_kernels_naga_validated' if not failures else 'material_kernel_refusals_retained',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,failures=failures,input_pins=pins,existing_node_emitter_unchanged=True,source_inputs_unchanged=True,material_gpu_qualified=False,complete_asset_conversion_increment=0,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],kernels=len(rows),failures=failures)))
