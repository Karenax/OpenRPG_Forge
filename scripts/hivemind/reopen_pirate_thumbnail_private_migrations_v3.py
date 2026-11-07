"""Reopen SDK-imported thumbnails; never equate migration to full fidelity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re
W=Path('F:/OpenRPG_ImportWork')
CAP=W/'pirate-source-thumbnail-batch-0025-0048-v1'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
TEMPLATE=W/'unity-library-source-thumbnail-house-bench-v2/project'
AUDIT=W/'pirate-private-import-change-audit-v2/RESULT.v1.json'
ROOT=W/'pirate-private-material-migrations-reopen-v3'
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def canonical(s): return re.sub(r'\s+','',s)
assert not ROOT.exists()
a=read(AUDIT); prep=read(CAP/'PREPARED.v1.json'); batch=read(CAP/'NATIVE-RESULT.v1.json'); run=read(CAP/'RUN-RESULT.v1.json')
assert a['original_sources_unchanged'] and run['actual_exit_code']==0
allowed_added={'_DstBlend2':{'0','10'},'_ExcludeFromTUAndAA':{'0'},'_MaterialID':{'1'},'_MaterialTypeMask':{'2'},'_PerPixelSorting':{'0'},'_TransmissionEnable':{'1'},'_UseAOMap':{'1'},'_AlphaRemapMax':{'1'},'_AlphaRemapMin':{'0'},'_ObjectSpaceUVMapping':{'0'},'_ObjectSpaceUVMappingEmissive':{'0'},'_TransmissionMask':{'1'},'_Contrast':{'1'},'_MaskPower':{'0.5'},'_MaskSize':{'2048'},'_UseAlpha':{'0'},'_AlphaClipping':{'0.1'}}
rows=[]; keyword_guids=set(); pins=dict(a['input_pins'])
for c in a['changes']:
    p=Path(c['path']); after=p.read_text(encoding='utf-8-sig')
    if c['extension']=='.asset':
        original=TEMPLATE/'ProjectSettings/GraphicsSettings.asset'; assert sha(original)==c['prior_sha256']
        before=original.read_text(encoding='utf-8-sig')
        pattern=r'(UnityEngine.Rendering.Universal.UniversalRenderPipeline: \{fileID: 11400000, guid: )([a-f0-9]{32})(, type: 2\})'
        x,y=re.search(pattern,before),re.search(pattern,after); assert x and y
        assert after==before[:x.start(2)]+y.group(2)+before[x.end(2):]
        pins[str(original)]=sha(original)
        rows.append(dict(path=str(p),classification='unused_urp_reference_only',original_path=str(original)))
        continue
    assert c['extension']=='.mat'
    original=Path(c['original_path']); before=original.read_text(encoding='utf-8-sig')
    assert sha(original)==c['prior_sha256'] and sha(p)==c['current_sha256']
    work=after; added=[]; stencil=[]
    for line in ['  m_Parent: {fileID: 0}\n','  m_ModifiedSerializedProperties: 0\n','  m_LockedProperties: \n','  m_AllowLocking: 1\n']:
        if line not in before:
            assert work.count(line)==1
            work=work.replace(line,'',1)
    assert before.count('  version: 13\n')==1 and work.count('  version: 13\n')==1
    for k, values in allowed_added.items():
        pattern=rf'^    - {re.escape(k)}: (.+)\n'
        b,n=re.findall(pattern,before,re.M),re.findall(pattern,work,re.M)
        if not b and n:
            assert len(n)==1 and n[0] in values,(p,k,n)
            work=re.sub(pattern,'',work,count=1,flags=re.M); added.append(dict(property=k,value=n[0]))
    for k,old,new in [('_StencilWriteMaskDepth','8','9'),('_StencilWriteMaskGBuffer','14','15'),('_StencilWriteMaskMV','40','41')]:
        oldline=f'    - {k}: {old}\n'; newline=f'    - {k}: {new}\n'
        if oldline in before and newline in work:
            work=work.replace(newline,oldline,1);stencil.append(k)
    for texture_property in ['_ORMMap','_TransmissionMaskMap','_GrayScaleMask']:
        null_texture=f'    - {texture_property}:\n        m_Texture: {{fileID: 0}}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n'
        if f'    - {texture_property}:' not in before and null_texture in work:
            work=work.replace(null_texture,'',1);added.append(dict(property=texture_property,value='null texture; scale 1; offset 0'))
    keyword_moved=False
    def keyword_sections(s):
        return re.findall(r'  m_(?:Valid|Invalid)Keywords:(.*?)(?=\n  [a-zA-Z_])',s,re.S)
    bs,ns=keyword_sections(before),keyword_sections(work)
    if bs!=ns:
        assert len(bs)==len(ns)==2
        words=lambda s:re.findall(r'^  - (\w+)$',s,re.M)
        bv,bi,nv,ni=map(words,[bs[0],bs[1],ns[0],ns[1]])
        assert [v for v in bv if v!='_ALPHATOMASK_ON']==nv
        assert sorted(bi+['_ALPHATOMASK_ON'])==sorted(ni)
        work=work.replace('  m_ValidKeywords:'+ns[0],'  m_ValidKeywords:'+bs[0],1)
        work=work.replace('  m_InvalidKeywords:'+ns[1],'  m_InvalidKeywords:'+bs[1],1)
        meta=original.with_suffix('.mat.meta');guid=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(encoding='utf-8-sig'),re.M).group(1)
        keyword_guids.add(guid); keyword_moved=True; pins[str(meta)]=sha(meta)
    assert canonical(work)==canonical(before), 'UNEXPLAINED_CHANGE '+str(p)
    rows.append(dict(path=str(p),original_path=str(original),authored_properties_and_texture_references_preserved=True,serialized_material_version=[13,13],added_sdk_properties=added,stencil_mask_changes=stencil,alpha_to_mask_keyword_moved_to_invalid=keyword_moved,full_material_fidelity_qualified=False))
assert len(rows)==4
# The two newly serialized alpha fields equal the authored graph defaults.
graph=W/'hivemind-run/pirate-island/source/Assets/Hivemind/PirateIsland/HDRP(Default)/Art/ShaderGraphs/S_Vegetation.shadergraph'
text=graph.read_text(encoding='utf-8-sig');decoder=json.JSONDecoder();offset=0;defaults={}
while offset<len(text):
    while offset<len(text) and text[offset].isspace():offset+=1
    if offset>=len(text):break
    item,offset=decoder.raw_decode(text,offset)
    name=item.get('m_OverrideReferenceName') or item.get('m_DefaultReferenceName')
    if name in ['_UseAlpha','_AlphaClipping']:defaults[name]=item['m_Value']
assert defaults['_UseAlpha'] is False and abs(defaults['_AlphaClipping']-0.1)<1e-8
pins[str(graph)]=sha(graph)
for case,request in zip(batch['cases'],prep['cases']):
    assert case['id']==request['id'] and case['captured']
    native=read(case['receipt']); assert native['source_object_unchanged'] and native['preview_scene_isolated']
    assert native['pipeline']=='UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset'
    used={t['material'].split(':')[0] for t in native['textures']}
    for renderer in native['renderers']:
        # Every material must be checked, including materials without textures.
        for material in renderer.get('materials',[]):
            if isinstance(material,str):used.add(material.split(':')[0])
            elif isinstance(material,dict):used.add(material.get('identity','').split(':')[0])
    assert not (used & keyword_guids), 'CAPTURE_DEPENDS_ON_CHANGED_ALPHA_KEYWORD'
    pins[case['receipt']]=sha(Path(case['receipt']))
    for im in native['images']:
        assert sha(Path(im['path']))==im['sha256'];pins[im['path']]=im['sha256']
for name,h in pins.items(): assert sha(Path(name))==h,name
sdk=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Editor/AssetProcessors/MaterialPostprocessor.cs'
for p in [AUDIT,Path(__file__),sdk,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
ROOT.mkdir()
result=dict(status='sdk_import_changes_classified_source_thumbnail_review_admitted',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L08.IMAGE.PIRATE.02',source_inputs_unchanged=True,private_project_byte_unchanged=False,material_changes=rows,newly_serialized_alpha_values_equal_authored_graph_defaults=defaults,alpha_keyword_changes_not_used_by_selected_cases=True,source_version_parity_qualified=False,full_material_fidelity_qualified=False,cards_captured=24,images_captured=48,original_refusal_retained=True,images_installed_in_vault=False,visual_review_qualified=False,input_pins=pins,full_import=False)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','material_changes']}))
