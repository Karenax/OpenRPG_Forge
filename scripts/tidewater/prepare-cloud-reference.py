"""Derive a fixed cloud acquisition pose/wind from an existing water fixture."""
from pathlib import Path
import hashlib,json,struct,sys
assert len(sys.argv)==4
reference,scene,output=map(Path,sys.argv[1:]);assert not output.exists()
ref=json.loads(reference.read_text());fixture=json.loads((scene/'fixture.json').read_text())
assert ref['source_commit']==fixture['source']['commit']
frame=(scene/'water-frame.bin').read_bytes()
assert hashlib.sha256(frame).hexdigest()==fixture['files']['water-frame.bin'] and len(frame)==720
pose=fixture['camera_pose']
ref['camera']={'position':dict(zip(['x','y','z'],pose['position'])),
 'quaternion':dict(zip(['_x','_y','_z','_w'],pose['rotation'])),'fov':pose['fov_degrees']}
ref['global_wind']={'speed':struct.unpack_from('<f',frame,668)[0],'direction':list(struct.unpack_from('<2f',frame,688))}
ref['view']='fixed_water_cloud_acquisition'
ref['derived_from_fixture_sha256']=hashlib.sha256((scene/'fixture.json').read_bytes()).hexdigest()
output.write_text(json.dumps(ref,indent=2));print(output)
