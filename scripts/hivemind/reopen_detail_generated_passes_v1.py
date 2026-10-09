"""Extract actual generated pass headers and resolve only captured material properties."""
from pathlib import Path
import datetime,hashlib,json,re
W=Path('F:/OpenRPG_ImportWork');CAPTURE=W/'detail-native-generated-passes-v2';ROOT=W/'detail-generated-pass-state-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def parse(source,properties):
    # Keep quoted strings intact; remove only comments before locating pass headers.
    token=re.compile(r'"(?:\\.|[^"\\])*"|/\*.*?\*/|//[^\n]*',re.S)
    text=token.sub(lambda m:m.group(0) if m.group(0).startswith('"') else '',source)
    result={}
    for match in re.finditer(r'\bPass\s*\{',text):
        end=text.find('HLSLPROGRAM',match.end());assert end>=0
        header=text[match.end():end]
        name=re.search(r'\bName\s+"([^"]+)"',header);assert name is not None
        label=name.group(1);assert label not in result
        commands=[]
        for line in header.splitlines():
            command=re.match(r'^\s*(Blend|Cull|ZWrite|ZTest|ZClip|ColorMask|AlphaToMask)\s+(.+?)\s*$',line)
            if command is None:continue
            expression=command.group(2);refs=re.findall(r'\[([^\]]+)\]',expression)
            commands.append(dict(command=command.group(1),expression=expression,
                captured_properties={r:properties[r] for r in refs if r in properties},
                unresolved_properties=[r for r in refs if r not in properties]))
        light=re.search(r'"LightMode"\s*=\s*"([^"]+)"',header)
        result[label]=dict(name=label,light_mode=light.group(1) if light else None,
            header=header,commands=commands,contains_stencil=bool(re.search(r'\bStencil\s*\{',header)))
    return result

def main():
    assert not ROOT.exists()
    terminal=read(CAPTURE/'RUN-RESULT.v1.json')
    assert terminal['status']=='actual_shaderlab_and_native_pass_queries_captured' and terminal['actual_unity_exit_code']==0
    path=CAPTURE/'source-generated.shader';assert sha(path)==terminal['generated_shader_sha256']
    native_path=W/'detail-native-material-state-v1/NATIVE-RESULT.v1.json';native=read(native_path)['cases'][0]
    props={p['name']:p['native_float'] for p in native['properties'] if p['type'] in ['Float','Range','Int']}
    source=path.read_text('utf-8');passes=parse(source,props)
    expected={p['name'] for p in native['passes']};assert expected.issubset(passes)
    for name in ['Forward','DepthOnly','ShadowCaster']:assert name in passes
    forward=passes['Forward'];cmd={c['command']:c for c in forward['commands'] if c['command']!='ColorMask'}
    assert cmd['Cull']['captured_properties']=={'_CullModeForward':0.0}
    assert cmd['ZWrite']['captured_properties']=={'_ZWrite':1.0}
    assert cmd['ZTest']['captured_properties']=={'_ZTestDepthEqualForOpaque':3.0}
    mask=[c for c in forward['commands'] if c['command']=='ColorMask']
    assert all(re.search(r'\s[12]$',c['expression']) for c in mask),mask
    assert len(mask)==2
    queries=read(CAPTURE/'NATIVE-RESULT.v1.json')['queries']
    original=read(W/'detail-original-render-contracts-v1/RESULT.v1.json')['rows'][0]
    pins=dict(read(CAPTURE/'PREPARED.v1.json')['input_pins'])
    for p in [path,native_path,CAPTURE/'RUN-RESULT.v1.json',CAPTURE/'NATIVE-RESULT.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
    for p,h in pins.items():assert sha(Path(p))==h,p
    ROOT.mkdir()
    result=dict(status='actual_generated_pass_headers_and_captured_values_reopened',generated_passes=passes,
        native_pass_queries=queries,original_disabled_serialized_labels=original['source_disabled_shader_passes'],
        unresolved_generated_properties=sorted({ref for row in passes.values() for command in row['commands'] for ref in command['unresolved_properties']}),
        installed_hdrp_forward_depth_compare=3,installed_hdrp_forward_cull=0,installed_hdrp_forward_zwrite=True,
        primary_color_mask_not_overridden=True,primary_default_channels='RGBA',
        primary_default_documentation='https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-render-state-commands/color-mask',
        secondary_attachment_masks_preserved=mask,source_pass_label_resolution_qualified=False,
        original_hdrp_version_qualified=False,stencil_role_mapping_qualified=False,pipeline_state_admission_complete=False,
        renderer_installed=False,gpu_rendered=False,full_import=False,input_pins=pins,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(generated_passes=len(passes),native_passes=len(expected),forward_depth_compare=3,secondary_masks=len(mask),full_import=False)))
if __name__=='__main__':main()
