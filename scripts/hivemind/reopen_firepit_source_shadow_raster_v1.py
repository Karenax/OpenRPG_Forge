"""Reopen source raster state, matching atlas allocations and actual depth pixels."""
from pathlib import Path
import datetime,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-raster-reopen-v1';CAP=W/'firepit-source-shadow-raster-v4'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
 prep=read(CAP/'PREPARED.v1.json');pins=prep['input_pins']
 for p,h in pins.items():assert sha(p)==h,p
 protocol=read(CAP/'DEPTH-PROTOCOL.v1.json');assert protocol['atlas_width']==protocol['atlas_height']==1024
 assert sha(protocol['compute'])==protocol['compute_sha256']
 rows=[read(CAP/f'SOURCE-SHADOW-RASTER-{i}.json') for i in range(2)];assert rows[0]==rows[1]
 data=rows[0];assert data['request_count']==4 and data['reversed_z'] and data['backend']=='Direct3D11'
 allocations=json.loads(data['matching_allocations_json']);assert len(allocations)==1 and allocations[0]['created'] and allocations[0]['depth_stencil_format']=='D32_SFloat'
 assert allocations[0]['width']==allocations[0]['height']==data['declared_width']==data['declared_height']==1024
 assert data['atlas_declared_depth_bits']=='Depth32' and data['slope_bias']==[2.5]*4
 requests=json.loads(data['requests_json']);assert len(requests)==4 and [r['slopeBias'] for r in requests]==data['slope_bias']
 files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'DEPTH-PROTOCOL.v1.json',Path(protocol['compute'])]
 statistics=[]
 for view in range(2):
  path=CAP/f'SOURCE-SHADOW-DEPTH-{view}.float32';raw=path.read_bytes();assert len(raw)==1024*1024*4
  values=struct.unpack('<1048576f',raw);assert all(math.isfinite(v) and 0<=v<=1 for v in values)
  tiles=[]
  for ty in range(2):
   for tx in range(2):
    tile=[v for y in range(ty*512,(ty+1)*512) for v in values[y*1024+tx*512:y*1024+(tx+1)*512]]
    tiles.append({'tile_xy':[tx,ty],'nonzero_samples':sum(v!=0 for v in tile),'minimum':min(tile),'maximum':max(tile)})
  statistics.append({'view':view,'gpu_samples':len(values),'tiles':tiles,'sha256':sha(path)});files.extend([path,CAP/f'SOURCE-SHADOW-RASTER-{view}.json'])
  for relative in [f'images/{view}.png',f'linear-{view}.rgba16f']:
   p=CAP/relative;q=W/'firepit-shadow-source-reference-v8'/relative;assert sha(p)==sha(q);files.extend([p,q])
 hdrp=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
 files.extend([hdrp/'Lighting/Shadow/HDShadowUtils.cs',hdrp/'Lighting/Shadow/HDShadowAtlas.cs',hdrp/'Lighting/Light/HDGpuLightsBuilder.LightLoop.cs'])
 ROOT.mkdir();result={'status':'source_cascade_raster_requests_allocation_format_and_gpu_depth_pixels_verified','source_requests_verified':4,'source_views_verified':2,'source_gpu_depth_samples_verified':2097152,'source_input_pins_verified':len(pins),'source_dependencies_verified':len(prep['dependency_paths']),'original_png_and_linear_views_byte_identical':True,'source_format':'D32_SFloat','source_atlas_size':[1024,1024],'reversed_z':True,'unity_global_depth_bias_constant':1,'actual_request_slope_bias':[2.5]*4,'candidate_native_reversed_depth_bias':{'constant':-1,'slope_scale':-2.5,'clamp':0.0},'native_raster_bias_gpu_verified':False,'bound_global_texture_observed':False,'depth_capture_identity':'unique created allocation matching exact atlas descriptor name and dimensions','statistics':statistics,'full_import':False,'scene_fidelity_verified':False,'complete_assets_delivered_increment':0,'editor_visual_verified':False,'input_pins':{str(p):sha(p) for p in files},'source_current_input_pins':pins,'official_depth_convention_reference':'https://docs.unity3d.com/Manual/SL-PlatformDifferences.html','next_action':'Install an explicit orthographic shadow raster policy with depth format guard and pipeline cache key; verify actual native descriptors and copied shadow depth against these source GPU pixels, then compare original rendered views.','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_current_input_pins']}))
if __name__=='__main__':main()
