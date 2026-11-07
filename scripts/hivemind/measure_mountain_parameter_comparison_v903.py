"""Measure actual RAW deltas and defaults; preserve all non-equivalence evidence."""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter
import hashlib
import json
import re
import numpy as np
from PIL import Image, ImageDraw

W=Path('F:/OpenRPG_ImportWork')
O=W/'mountain-native-parameter-measurements-v903'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def main():
    run_path=W/'mountain-native-parameter-comparison-run-v902/RUN-RESULT.v1.json'
    run=read(run_path)
    assert run['status']=='native_parameter_comparison_finished_requires_scope_review'
    assert run['unity_exit_code']==0 and run['all_views_repeat_exact'] and not run['refused_cases']
    prep_path=W/'mountain-native-parameter-comparison-v901/PREPARED.v1.json'
    prep=read(prep_path)
    for path,digest in run['output_pins'].items():
        assert sha(path)==digest,path
    O.mkdir(exist_ok=False)
    comparisons,defaults,card_rows=[],{},[]
    material_proof=read(W/'mountain-exact-capture-review-v897/RESULT.v1.json')
    materials={}
    for row in material_proof['source_material_changes']:
        meta=Path(row['source']+'.meta').read_text('utf-8-sig')
        guid=re.search(r'(?m)^guid: ([0-9a-f]{32})$',meta)[1]
        assert sha(row['target'])==row['migrated_sha256']
        text=Path(row['target']).read_text('utf-8-sig')
        materials[guid]={key:float(value) for key,value in re.findall(r'(?m)^    - (\S+): ([-+0-9.eE]+)$',text)}
    for index in range(47):
        request=prep['cases'][4*index+1]
        current=read(request['output'])
        original=read(prep['cases'][4*index+2]['output'])
        repeated=read(prep['cases'][4*index+3]['output'])
        assert current['selected_mesh_identities']==original['selected_mesh_identities']==repeated['selected_mesh_identities']
        assert current['selected_mesh_vertex_counts']==original['selected_mesh_vertex_counts']==repeated['selected_mesh_vertex_counts']
        assert all(row['restored_original_values'] for row in original['material_comparisons'])
        for observation in current['material_comparisons']:
            for value in observation['shader_defaults']:
                key=observation['guid']+':'+value['name']
                row=dict(material_guid=observation['guid'],property=value['name'],
                         measured_shader_default=value['value'],migrated_serialized_value=materials[observation['guid']][value['name']])
                if key in defaults:
                    assert defaults[key]==row
                defaults[key]=row
        card_metrics=[]
        for view in range(2):
            a,b,c=current['images'][view],original['images'][view],repeated['images'][view]
            assert a['raw_sha256']==c['raw_sha256']
            first=np.fromfile(a['raw_path'],dtype='<f2').reshape(512,512,4).astype(np.float32)
            second=np.fromfile(b['raw_path'],dtype='<f2').reshape(512,512,4).astype(np.float32)
            assert np.isfinite(first).all() and np.isfinite(second).all()
            delta=np.abs(first[:,:,:3]-second[:,:,:3])
            row=dict(id=request['source_card_id'],view=view,raw_exact=a['raw_sha256']==b['raw_sha256'],
                     differing_rgb_pixels=int(np.count_nonzero(np.any(delta!=0,axis=2))),
                     max_rgb_absolute_delta=float(delta.max()),mean_rgb_absolute_delta=float(delta.mean()),
                     pixels_over_0_01=int(np.count_nonzero(np.any(delta>0.01,axis=2))))
            comparisons.append(row)
            card_metrics.append(row)
        card_rows.append(dict(id=request['source_card_id'],prefab_path=request['prefab_path'],
                              requested_lod=request['requested_lod'],current_images=current['images'],
                              original_settings_images=original['images'],metrics=card_metrics))
    sheets=[]
    for page,start in enumerate(range(0,47,6)):
        selected=card_rows[start:start+6]
        sheet=Image.new('RGB',(512,280*len(selected)),(35,35,35))
        draw=ImageDraw.Draw(sheet)
        for offset,row in enumerate(selected):
            draw.text((5,offset*280+2),Path(row['prefab_path']).stem+' LOD'+str(row['requested_lod']),fill='white')
            draw.text((5,offset*280+13),'migrated                   original settings / installed SDK',fill='white')
            for column,key in enumerate(['current_images','original_settings_images']):
                with Image.open(row[key][0]['path']) as png:
                    sheet.paste(png.convert('RGB').resize((256,256)),(256*column,offset*280+24))
        path=O/f'comparison-sheet-{page:02d}.png'
        sheet.save(path)
        sheets.append(dict(path=str(path),sha256=sha(path)))
    mismatches=[row for row in defaults.values() if row['measured_shader_default']!=row['migrated_serialized_value']]
    result=dict(status='native_parameter_raw_differences_and_shader_defaults_measured',utc=datetime.now(timezone.utc).isoformat(),
                cards=47,views=94,different_raw_views=sum(not row['raw_exact'] for row in comparisons),
                differing_cards=sum(any(not m['raw_exact'] for m in row['metrics']) for row in card_rows),
                comparisons=comparisons,shader_defaults=list(defaults.values()),
                added_property_default_mismatches=mismatches,
                mismatch_counts_by_property=dict(Counter(row['property'] for row in mismatches)),
                cards_with_images=card_rows,comparison_sheets=sheets,
                input_pins={str(run_path):sha(run_path),str(prep_path):sha(prep_path),str(Path(__file__)):sha(Path(__file__))},
                original_sdk_pipeline_equivalence=False,native_parameter_equivalence_verified=False,
                agent_visual_review=False,images_admitted=0,full_import_increment=0)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({key:result[key] for key in ['status','different_raw_views','differing_cards','mismatch_counts_by_property']}))

if __name__=='__main__':
    main()
