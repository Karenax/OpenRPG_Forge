"""Compare parent-composed affine proxies of Unity capsule and sphere colliders.

This detects leads only. Unity/Bevy runtime behavior, layers, material semantics,
and nonuniform-scale treatment must be qualified separately before any removal.
"""
import argparse
import json
import math
import re
from pathlib import Path

import vault_storage as storage


def field(body, name, required=True):
    values = re.findall(r'(?m)^\s*' + re.escape(name) + r':\s*([^\r\n]+)', body)
    if len(values) != 1:
        if not values and not required: return None
        raise ValueError('missing or repeated field: '+name)
    return values[0].strip()


def vector(text, names):
    matches = dict(re.findall(r'([xyzw]):\s*([-+0-9.eE]+)', text))
    if set(matches) != set(names): raise ValueError('invalid vector')
    values = [float(matches[k]) for k in names]
    if not all(math.isfinite(x) for x in values): raise ValueError('nonfinite vector')
    return values


def file_id(text):
    match = re.fullmatch(r'\{fileID:\s*(-?\d+)\}', text)
    if not match: raise ValueError('external or invalid object reference')
    return match.group(1)


def matmul(a,b):
    return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def matrix(body):
    t=vector(field(body,'m_LocalPosition'),'xyz');s=vector(field(body,'m_LocalScale'),'xyz')
    x,y,z,w=vector(field(body,'m_LocalRotation'),'xyzw')
    if abs(x*x+y*y+z*z+w*w-1)>1e-3: raise ValueError('invalid quaternion')
    r=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
       [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
       [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    return [[r[i][j]*s[j] for j in range(3)]+[t[i]] for i in range(3)]+[[0,0,0,1]]


def affine_shapes(raw):
    blocks=re.findall(r'(?ms)^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n(.*?)(?=^--- !u!|\Z)',raw.decode('utf8'))
    transforms={};by_object={};colliders=[]
    for kind,ident,body in blocks:
        if kind=='4':
            if ident in transforms:raise ValueError('duplicate transform')
            obj=file_id(field(body,'m_GameObject'));parent=file_id(field(body,'m_Father'))
            if obj in by_object:raise ValueError('duplicate object transform')
            transforms[ident]=(matrix(body),parent);by_object[obj]=ident
        elif kind in ('135','136'):
            colliders.append((kind,body))
        elif kind in ('64','65'):
            raise ValueError('unsupported mesh or box collider')
    cache={};visiting=set()
    def world(ident):
        if ident in cache:return cache[ident]
        if ident in visiting or ident not in transforms:raise ValueError('invalid transform hierarchy')
        visiting.add(ident);local,parent=transforms[ident]
        value=local if parent=='0' else matmul(world(parent),local)
        visiting.remove(ident);cache[ident]=value;return value
    shapes=[]
    for kind,body in colliders:
        obj=file_id(field(body,'m_GameObject'))
        if obj not in by_object:raise ValueError('collider transform missing')
        m=world(by_object[obj]);center=vector(field(body,'m_Center'),'xyz');radius=float(field(body,'m_Radius'))
        if not math.isfinite(radius) or radius<=0:raise ValueError('invalid collider radius')
        center_world=[sum(m[i][j]*center[j] for j in range(3))+m[i][3] for i in range(3)]
        half=0.0;axis=[0.0,0.0,0.0]
        if kind=='136':
            direction=int(field(body,'m_Direction'));height=float(field(body,'m_Height'))
            if direction not in (0,1,2) or not math.isfinite(height) or height<=0:raise ValueError('invalid capsule')
            half=max(0.0,height/2-radius);axis=[m[i][direction]*half for i in range(3)]
        ends=sorted([[round(center_world[i]+sign*axis[i],5) for i in range(3)] for sign in (-1,1)])
        covariance=[[round(radius*radius*sum(m[i][k]*m[j][k] for k in range(3)),6) for j in range(3)] for i in range(3)]
        shapes.append({'type':'capsule' if kind=='136' else 'sphere','ends':ends,'affine_radius_matrix':covariance,
                       'enabled':field(body,'m_Enabled'),'trigger':field(body,'m_IsTrigger'),
                       'material':field(body,'m_Material',False)})
    if not shapes:raise ValueError('no supported colliders')
    return sorted(shapes,key=lambda x:json.dumps(x,sort_keys=True))


def audit(vault,evidence,local):
    vault=vault.resolve(strict=True);source=evidence.read_bytes();prior=json.loads(source)
    local_bytes=local.read_bytes();local_doc=json.loads(local_bytes)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');registry=index.read_bytes()
    if storage.sha(registry)!=local_doc['registry_sha256']:raise ValueError('registry changed')
    selected={(g['pack'],g['source_filename'],g['lod']) for g in local_doc['groups']
              if len(g['cards'][0]['colliders'])==len(g['cards'][1]['colliders'])==1}
    groups=[g for g in prior['groups'] if (g['pack'],g['source_filename'],g['lod']) in selected]
    if len(groups)!=len(selected):raise ValueError('source group mismatch')
    cache={};results=[]
    for group in groups:
        values=[]
        for card in group['cards']:
            p=storage.confined(Path(Path(card['source_path']).anchor),Path(card['source_path']))
            if p not in cache:
                raw=p.read_bytes();cache[p]=(storage.sha(raw),affine_shapes(raw))
            sha,shapes=cache[p]
            if sha!=card['source_sha256']:raise ValueError('source SHA changed')
            values.append({'id':card['id'],'source_sha256':sha,'affine_shapes':shapes})
        results.append({'pack':group['pack'],'source_filename':group['source_filename'],'lod':group['lod'],
                        'cards':values,'affine_proxy_equal':values[0]['affine_shapes']==values[1]['affine_shapes']})
    if index.read_bytes()!=registry or evidence.read_bytes()!=source or local.read_bytes()!=local_bytes:raise ValueError('input changed')
    return {'state':'audit_only','registry_sha256':storage.sha(registry),'groups':results,
            'unique_sources_read':len(cache),'limitations':'Affine geometry proxy only, not Unity physics or runtime equivalence.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','evidence','local','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.evidence,a.local)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'groups':len(result['groups']),'equal_proxies':sum(x['affine_proxy_equal'] for x in result['groups'])}))
