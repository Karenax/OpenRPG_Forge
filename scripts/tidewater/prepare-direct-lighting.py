"""Isolate original direct BRDF/transmission; no sky/shadow or final-scene claim."""
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

upstream, source, target = (Path(v).resolve() for v in sys.argv[1:4])
pin='1438b1abfcaee3267092b75573014f4d9b4a983c'
git=lambda *args:subprocess.check_output(['git','-C',str(upstream),*args],text=True).strip()
assert git('rev-parse','HEAD')==pin and not git('status','--porcelain')
if target.exists() or target.is_relative_to(source) or target.is_relative_to(upstream):
    raise SystemExit('new destination outside inputs required')
fixture=json.loads((source/'fixture.json').read_text())
assert fixture['source']['commit']==pin and 'vegetation-scene.json' in fixture['files']
for name,expected in fixture['files'].items():
    assert Path(name).name==name
    assert hashlib.sha256((source/name).read_bytes()).hexdigest()==expected
raw=(upstream/'src/engine/render/wgsl/lighting.js').read_text()
# Copy original complete Schlick/Smith/GGX functions, without translating math.
start=raw.index('fn F_Schlick('); end=raw.index('// Charlie sheen',start)
brdf=raw[start:end]
adapter='''
// Source direct light only. Neutral visibility; environment/occlusion excluded.
fn sourceDirectLight(albedo:vec3f,roughness:f32,metalness:f32,specular:f32,
    transmission:vec3f,N:vec3f,V:vec3f,L:vec3f,lightColor:vec3f)->vec3f {
    let rough=clamp(roughness,0.03,1.0);
    let diffuseColor=albedo*(1.0-metalness);
    let specF0=mix(vec3f(0.04)*specular,albedo,metalness);
    let specF90=mix(specular,1.0,metalness);
    let irradiance=sat(dot(N,L))*lightColor;
    return irradiance*diffuseColor*INV_PI
        +irradiance*BRDF_GGX(L,V,N,specF0,specF90,rough)
        +transmission*lightColor;
}
'''
target.mkdir(parents=True)
for name in fixture['files']:shutil.copyfile(source/name,target/name)
shader=(source/'plant.wgsl').read_text()
here=Path(__file__).resolve().parent
footer=(here/'plant-probe-footer.wgsl').read_text()
old=footer[footer.index('    var pbr='):]
new='''    return vec4f(sourceDirectLight(surface.albedo,surface.roughness,surface.metalness,
        surface.specularIntensity,surface.translucency,normalize(surface.normal),input.V,
        frame.sunDir,vec3f(1.0)),1.0);
}
'''
assert shader.count(old)==3
shader=shader.replace(old,new)+'\n'+brdf+adapter
(target/'plant.wgsl').write_text(shader,encoding='utf-8')
(target/'source-direct-brdf.wgsl').write_text(brdf+adapter,encoding='utf-8')
fixture['direct_lighting_probe']={'light_color':[1,1,1],
    'sun_direction':'normalize(0.3,0.6,-0.7)', 'scope':'vegetation_direct_only',
    'source_lighting_sha256':hashlib.sha256(raw.encode()).hexdigest()}
fixture['limitations']+=['unit_direct_light_only_not_global_scene_lighting',
    'terrain_and_village_still_bevy_lighting','no_environment_or_shadow_lighting']
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(target),'status':'source_direct_light_probe_only'}))
