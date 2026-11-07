"""Measure color-buffer experiment without admitting or qualifying altered assets."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-vertex-color-measurements-v930'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    run_path=W/'mountain-vertex-color-controls-run-v929/RUN-RESULT.v1.json';prep_path=W/'mountain-vertex-color-controls-v928/PREPARED.v1.json'
    run,prep=read(run_path),read(prep_path)
    assert run['status']=='native_diagnostic_vertex_color_controls_finished_no_image_admission' and run['unity_exit_code']==0 and not run['input_changes'] and not run['refused_cases'] and run['all_views_repeat_exact']
    pins={str(p):sha(p) for p in [run_path,prep_path,Path(__file__).resolve()]}
    for p,d in run['output_pins'].items():assert sha(p)==d,p;pins[p]=d
    rows=[];sheet=Image.new('RGB',(512,1680),(35,35,35));draw=ImageDraw.Draw(sheet)
    for index in range(3):
        cases=prep['cases'][6*index:6*index+6];reports=[read(c['output']) for c in cases]
        assert all(not reports[i]['diagnostic_white_vertex_colors'] for i in [0,1,2,5]) and all(reports[i]['diagnostic_white_vertex_colors'] for i in [3,4])
        original_ids=reports[1]['selected_mesh_identities'];assert all(reports[i]['selected_mesh_identities']==original_ids for i in [0,2,5])
        assert reports[3]['selected_mesh_identities']==reports[4]['selected_mesh_identities']==['diagnostic-white-vertex-colors-clone-of:'+identity for identity in original_ids]
        assert all(r['selected_mesh_vertex_counts']==reports[1]['selected_mesh_vertex_counts'] for r in reports)
        for view in range(2):
            source,diagnostic=reports[1]['images'][view],reports[3]['images'][view]
            assert source['raw_sha256']==reports[2]['images'][view]['raw_sha256']==reports[5]['images'][view]['raw_sha256']
            assert diagnostic['raw_sha256']==reports[4]['images'][view]['raw_sha256']
            arrays=[np.fromfile(r['raw_path'],dtype='<f2').reshape(512,512,4).astype(np.float32) for r in [source,diagnostic]]
            assert all(np.isfinite(a).all() for a in arrays)
            delta=np.abs(arrays[0][:,:,:3]-arrays[1][:,:,:3])
            rows.append(dict(id=cases[0]['source_card_id'],view=view,raw_equal=source['raw_sha256']==diagnostic['raw_sha256'],differing_rgb_pixels=int(np.count_nonzero(np.any(delta!=0,axis=2))),max_rgb_absolute_delta=float(delta.max()),mean_rgb_absolute_delta=float(delta.mean()),source_image=source['path'],diagnostic_only_image=diagnostic['path']))
            y=(2*index+view)*280;draw.text((5,y+2),Path(cases[0]['prefab_path']).stem+' view '+str(view),fill='white');draw.text((5,y+14),'source                      DIAGNOSTIC WHITE COLOR CLONE',fill='white')
            for column,row in enumerate([source,diagnostic]):
                with Image.open(row['path']) as png:sheet.paste(png.convert('RGB').resize((256,256)),(256*column,y+24))
    O.mkdir(exist_ok=False);image=O/'comparison.png';sheet.save(image)
    result=dict(status='three_source_vs_diagnostic_white_color_controls_measured',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,comparison_image=str(image),comparison_image_sha256=sha(image),different_views=sum(not r['raw_equal'] for r in rows),input_pins=pins,source_repeat_exact=True,diagnostic_repeat_exact=True,altered_images_admissible=False,source_repair_applied=False,root_cause_qualified=False,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],different_views=result['different_views'],rows=[{k:r[k] for k in ['id','view','differing_rgb_pixels','max_rgb_absolute_delta']} for r in rows])))
if __name__=='__main__':main()
