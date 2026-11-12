"""Prepare a small isolated Unity project using textures from overlay manifests."""
import argparse
import hashlib
import json
from pathlib import Path,PurePosixPath
import re
import shutil
import vault_storage as storage
from unity_shared_overlay import inventory_tree


CSHARP=r'''using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable] public class SharedGuidCase { public string guid; public string path; public string kind; }
[Serializable] public class SharedDependencyCase { public string material; public string texture; public string kind; }
[Serializable] public class SharedGuidCases { public SharedGuidCase[] entries; public SharedDependencyCase[] dependencies; }
[Serializable] public class SharedGuidResult { public int checked_count; public int dependency_count; public int failures; public string[] errors; }
public static class SharedUnityGuidProbe {
    public static void Run() {
        var errors = new System.Collections.Generic.List<string>();
        var cases = JsonUtility.FromJson<SharedGuidCases>(File.ReadAllText("Assets/Editor/ExpectedGuids.json"));
        foreach (var item in cases.entries) {
            var found = AssetDatabase.GUIDToAssetPath(item.guid);
            if (found != item.path) errors.Add(item.guid + ": GUIDToAssetPath=" + found + ", expected=" + item.path);
            if (AssetDatabase.AssetPathToGUID(item.path) != item.guid) errors.Add(item.guid + ": reverse GUID mismatch");
            if (item.kind == "model" || item.kind == "prefab") {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(item.path) == null) errors.Add(item.guid + ": model did not load");
            } else if (item.kind == "material") {
                if (AssetDatabase.LoadAssetAtPath<Material>(item.path) == null) errors.Add(item.guid + ": material did not load");
            } else if (AssetDatabase.LoadAssetAtPath<Texture2D>(item.path) == null) errors.Add(item.guid + ": texture did not load");
        }
        if (cases.dependencies != null) foreach (var item in cases.dependencies) {
            if (item.kind == "graph") {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.material) == null) errors.Add(item.material + ": shader graph did not load");
            } else if (item.kind == "prefab") {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(item.material) == null) errors.Add(item.material + ": prefab did not load");
            } else if (AssetDatabase.LoadAssetAtPath<Material>(item.material) == null) errors.Add(item.material + ": material did not load");
            var deps = AssetDatabase.GetDependencies(item.material, false);
            if (Array.IndexOf(deps, item.texture) < 0) errors.Add(item.material + ": missing texture dependency " + item.texture);
        }
        var result = new SharedGuidResult { checked_count = cases.entries.Length,
            dependency_count = cases.dependencies == null ? 0 : cases.dependencies.Length,
            failures = errors.Count, errors = errors.ToArray() };
        File.WriteAllText("guid-probe-result.json", JsonUtility.ToJson(result, true));
        Debug.Log("SHARED_GUID_PROBE " + JsonUtility.ToJson(result));
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }
}
'''


def prepare(vault,manifests,audit_path,project,work_root,per_group=2,include_materials=False,include_graphs=False,package_roots=None,editor_version=None,include_prefabs=False):
    vault=vault.resolve(strict=True);work_root=work_root.resolve(strict=True)
    if project.parent.resolve(strict=True)!=work_root or project.exists():raise ValueError('project must be a new direct work-root child')
    audit_raw=audit_path.read_bytes();audit=json.loads(audit_raw);cases=[];sources=[];materials={};dependencies=set()
    for group,manifest_path in manifests:
        if not group.isalnum():raise ValueError('invalid probe group')
        raw=manifest_path.read_bytes();doc=json.loads(raw)
        if (doc.get('schema')!='openrpg.unity-shared-overlay.v1' or audit['comparison_sha256'] not in
                [doc['comparison_sha256'],*doc.get('additional_comparison_sha256',[])]):
            raise ValueError('overlay manifest and GUID audit differ')
        root=PurePosixPath(doc['source_root']);rows={x['logical'].casefold():x for x in doc['files']};choices=[]
        for item in audit['images']:
            old=PurePosixPath(item['hdrp'])
            if not old.is_relative_to(root):continue
            logical=old.relative_to(root).as_posix();image=rows.get(logical.casefold());meta=rows.get((logical+'.meta').casefold())
            if Path(logical).is_absolute() or '..' in Path(logical).parts:raise ValueError('invalid logical path')
            if image is None or meta is None:raise ValueError('overlay entry missing')
            choices.append((image['bytes'],logical,item['guid'],image,meta,item))
        selected=sorted(choices) if per_group is None else sorted(choices)[:per_group]
        for _,logical,guid,image,meta,item in selected:
            if any(x['guid']==guid for x in cases):raise ValueError('duplicate selected GUID')
            asset='Assets/Probe/'+group+'/'+logical
            suffix=Path(logical).suffix.lower()
            if suffix not in ('.png','.tga','.fbx','.prefab','.mat'):raise ValueError('unsupported probe asset')
            kind={'.fbx':'model','.prefab':'prefab','.mat':'material'}.get(suffix,'texture')
            cases.append({'guid':guid,'path':asset,'kind':kind});sources.append((asset,image,meta))
            if include_materials or include_graphs or include_prefabs:
                for ref in item['references']:
                    source_path=PurePosixPath(ref['path'])
                    suffix=source_path.suffix.lower()
                    if (ref['side']!='hdrp' or not source_path.is_relative_to(root)
                            or not ((include_materials and suffix=='.mat') or (include_graphs and suffix=='.shadergraph') or (include_prefabs and suffix=='.prefab'))):continue
                    rel=source_path.relative_to(root).as_posix()
                    if Path(rel).is_absolute() or '..' in Path(rel).parts:raise ValueError('invalid material path')
                    material=rows.get(rel.casefold());material_meta=rows.get((rel+'.meta').casefold())
                    if material is None or material_meta is None:raise ValueError('material or metadata absent from overlay')
                    material_path='Assets/Probe/'+group+'/'+rel
                    value=(material,material_meta)
                    if material_path in materials and materials[material_path]!=value:raise ValueError('conflicting material')
                    materials[material_path]=value;dependencies.add((material_path,asset,'graph' if suffix=='.shadergraph' else 'prefab' if suffix=='.prefab' else 'material'))
        if include_graphs:
            for row in doc['files']:
                rel=row['logical']
                if Path(rel).suffix.lower() not in ('.shadergraph','.shadersubgraph'):continue
                meta=rows.get((rel+'.meta').casefold())
                if meta is None:raise ValueError('shader graph metadata absent from overlay')
                graph_path='Assets/Probe/'+group+'/'+rel
                value=(row,meta)
                if graph_path in materials and materials[graph_path]!=value:raise ValueError('conflicting graph')
                materials[graph_path]=value
        if not choices or (per_group is not None and len(choices)<per_group):raise ValueError('not enough GUID candidates')
        if manifest_path.read_bytes()!=raw:raise ValueError('manifest changed')
    if audit_path.read_bytes()!=audit_raw:raise ValueError('GUID audit changed')
    project.mkdir();(project/'Assets/Editor').mkdir(parents=True);(project/'Packages').mkdir();(project/'ProjectSettings').mkdir()
    total=0
    sources.extend((path,rows[0],rows[1]) for path,rows in sorted(materials.items()))
    for asset,image,meta in sources:
        for suffix,row in (('',image),('.meta',meta)):
            path=storage.confined(vault,vault/row['vault_path']);dest=project/(asset+suffix)
            dest.parent.mkdir(parents=True,exist_ok=True);h=hashlib.sha256();size=0
            with storage.locked_source(path,share_delete=False) as src,dest.open('xb') as out:
                while block:=src.read(4*1024*1024):h.update(block);size+=len(block);out.write(block)
            if h.hexdigest()!=row['sha256'] or size!=row['bytes'] or storage.sha(dest.read_bytes())!=row['sha256']:
                raise ValueError('probe copy SHA changed')
            total+=size
    (project/'Assets/Editor/SharedUnityGuidProbe.cs').write_text(CSHARP,encoding='utf8')
    deps=[{'material':material,'texture':texture,'kind':kind} for material,texture,kind in sorted(dependencies)]
    (project/'Assets/Editor/ExpectedGuids.json').write_text(json.dumps({'entries':cases,'dependencies':deps},indent=2),encoding='utf8')
    package_manifest={'dependencies':{}}
    if include_graphs:
        required={'com.unity.shadergraph','com.unity.render-pipelines.core','com.unity.searcher'}
        optional={'com.unity.render-pipelines.high-definition','com.unity.render-pipelines.high-definition-config',
                  'com.unity.render-pipelines.universal','com.unity.render-pipelines.universal-config',
                  'com.unity.visualeffectgraph'}
        if not package_roots or not required <= set(package_roots) or set(package_roots)-required-optional:
            raise ValueError('invalid shader graph packages')
        for name,path in package_roots.items():
            if isinstance(path,str):
                if not re.fullmatch(r'\d+\.\d+\.\d+',path):raise ValueError('invalid package version')
                package_manifest['dependencies'][name]=path
                continue
            if not path.is_absolute():raise ValueError('absolute local package path required')
            path=storage.confined(Path(path.anchor),path);inventory_tree(path)
            shutil.copytree(path,project/'Packages'/name,symlinks=False)
            package_manifest['dependencies'][name]='file:'+name
    (project/'Packages/manifest.json').write_text(json.dumps(package_manifest,indent=2)+'\n',encoding='utf8')
    version=editor_version or ('2021.3.5f1' if include_graphs else '6000.5.8f1')
    (project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: '+version+'\n',encoding='utf8')
    return {'state':'prepared_only','project':str(project),'cases':cases,'dependencies':deps,'source_bytes':total,
        'audit_sha256':storage.sha(audit_raw),'limitations':'A small sample, not a full Unity project or render qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','gothic','gladiator','audit','project','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--all-textures',action='store_true')
    p.add_argument('--include-materials',action='store_true')
    p.add_argument('--include-graphs',action='store_true')
    p.add_argument('--unity6-graphs',action='store_true')
    p.add_argument('--unity6-full-pipelines',action='store_true')
    a=p.parse_args();work=Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if a.output.resolve(strict=False).parent!=work or a.output.exists():p.error('output must be new and in conversion workspace')
    packages=None
    if a.unity6_graphs and not a.include_graphs:p.error('--unity6-graphs requires --include-graphs')
    if a.unity6_full_pipelines and not a.unity6_graphs:p.error('--unity6-full-pipelines requires --unity6-graphs')
    if a.include_graphs:
        base=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages' if a.unity6_graphs else
                  'F:/Unity/2021.3.5f1/Editor/Data/Resources/PackageManager/BuiltInPackages')
        packages={'com.unity.shadergraph':base/'com.unity.shadergraph',
                  'com.unity.render-pipelines.core':base/'com.unity.render-pipelines.core',
                  'com.unity.searcher':('4.9.3' if a.unity6_graphs else Path('F:/OpenRPG_ImportWork/hivemind-sdk12-matching-editor-preparation-v1546/project/Library/PackageCache/com.unity.searcher@4.9.1'))}
        if a.unity6_full_pipelines:
            for name in ('com.unity.render-pipelines.high-definition','com.unity.render-pipelines.high-definition-config',
                         'com.unity.render-pipelines.universal','com.unity.render-pipelines.universal-config',
                         'com.unity.visualeffectgraph'):
                packages[name]=base/name
    result=prepare(a.vault,[('gothic',a.gothic),('gladiator',a.gladiator)],a.audit,a.project,work,
                   per_group=None if a.all_textures else 2,include_materials=a.include_materials,
                   include_graphs=a.include_graphs,package_roots=packages,
                   editor_version='6000.5.8f1' if a.unity6_graphs else None)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'cases':len(result['cases']),
                      'dependencies':len(result['dependencies']),'source_bytes':result['source_bytes']}))
