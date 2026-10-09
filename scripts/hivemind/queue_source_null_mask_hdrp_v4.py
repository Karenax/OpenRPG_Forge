"""Measure the precisely pinned native-effective target, retaining authored bytes.

This stage measures the null-binding behavior in HDRP 17; it does not qualify
the migration or the original full prefab. v2 rendering and v3 refusals remain.
"""
from pathlib import Path
import ast
import importlib.util
import json
import sys

HERE=Path(__file__).resolve().parent
BASE=HERE/'queue_source_null_mask_hdrp_v2.py'
spec=importlib.util.spec_from_file_location('null_mask_v2_retained',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
ROOT=Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v4')
DIAG=ROOT.parent/'unity-source-null-mask-hdrp-v3/TARGET-MIGRATION-DIAGNOSIS.v1.json'
DIAGNOSIS=json.loads(DIAG.read_text('utf-8-sig'))
original_retain=probe.retain_private_context


def retain_context(origin,target,digest):
    meta=Path(str(origin)+'.meta')
    if origin.suffix.lower()!='.mat' or 'abc00000000012210656512993269337' not in meta.read_text('utf-8-sig'):
        return original_retain(origin,target,digest)
    assert not DIAGNOSIS['migration_behavior_qualified']
    assert probe.namespace['sha'](origin)==DIAGNOSIS['input_pins'][str(origin)]==digest
    native_digest=probe.namespace['sha'](target)
    assert native_digest==DIAGNOSIS['input_pins'][str(target)]
    # Accept only this exact retained effective byte sequence, never a generic waiver.
    retained=DIAG.parent/'preserved-target-migration'
    assert probe.namespace['sha'](retained/'authored.mat')==digest
    assert probe.namespace['sha'](retained/'native-effective.mat')==native_digest
    directory=ROOT/'preserved-private-context'/digest
    directory.mkdir(parents=True,exist_ok=False)
    for before,after in [('authored.mat','original.mat'),('native-effective.mat','private.mat'),('difference.patch','difference.patch')]:
        (directory/after).write_bytes((retained/before).read_bytes())
    probe.MIGRATIONS.append(dict(original_path=str(origin),private_path=str(target),
        original_sha256=digest,private_sha256=native_digest,preserved_directory=str(directory),
        target_material=True,context_qualified_as_original=False,
        native_effective_binding_probe_only=True,migration_diagnosis=str(DIAG)))
    return native_digest


def configure():
    probe.ROOT=ROOT
    namespace=probe.namespace
    namespace.update(ROOT=ROOT,__file__=str(Path(__file__).resolve()),retain_private_context=retain_context)
    original=probe.BASE.read_text('utf-8-sig')
    for name in ('prepare','run'):
        node=next(n for n in ast.parse(original).body if isinstance(n,ast.FunctionDef) and n.name==name)
        text=ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpStableV4Probe.Export')
        text=text.replace('SourceNullMaskHdrpProbe.cs','SourceNullMaskHdrpStableV4Probe.cs')
        if name=='prepare':
            old="assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)"
            assert text.count(old)==1
            text=text.replace(old,'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
        if name=='run':
            marker="report = read(ROOT / 'NATIVE.v1.json')"
            assert text.count(marker)==1
            text=text.replace(marker,marker+"\n        assert report['diagnostic_camera_postprocess_disabled'] and report['diagnostic_camera_exposure_disabled']")
        exec(compile(text,str(Path(__file__).resolve()),'exec'),namespace)
    original_save=namespace['save']
    def scoped_save(name,data):
        if name=='PREPARED.v1.json':
            data.update(target_material_and_geometry_require_original_hashes=False,
                geometry_requires_original_hashes=True,native_effective_target_requires_exact_diagnosed_hash=True,
                native_material_migration_qualified=False,original_prefab_render_qualified=False)
            data['input_pins'][str(DIAG)]=namespace['sha'](DIAG)
            data['input_pins'][str(BASE)]=namespace['sha'](BASE)
        if name in {'RUN-RESULT.v1.json','COMPARISON.v1.json'}:
            data.update(native_effective_target_binding_probe_only=True,
                native_material_migration_qualified=False,original_prefab_render_qualified=False)
        original_save(name,data)
    namespace['save']=scoped_save
    return namespace


if __name__=='__main__':
    namespace=configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':namespace['run']()
    else:
        assert not ROOT.exists()
        helper=HERE/'candidates/SourceNullMaskHdrpStableV4Probe.cs'
        assert not helper.exists()
        source=(HERE/'candidates/SourceNullMaskHdrpStableProbe.cs').read_text('utf-8-sig')
        helper.write_text(source.replace('OpenRPGSourceNullMaskHdrpStableProbe','OpenRPGSourceNullMaskHdrpStableV4Probe'),'utf-8')
        namespace['HELPER']=helper
        namespace['prepare']()
