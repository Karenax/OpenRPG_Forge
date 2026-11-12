"""Prepare source-card consolidation without publishing or deleting payloads.

Both complete original records remain in the staged document. Reader and
importer support for consolidated_sources is required before publication.
"""
import argparse
import copy
import json
import re
from pathlib import Path

import vault_storage as storage
from inventory_source_support_variant_pairs import classify
from stage_source_support_relinks import digest_file, norm
from audit_source_prefab_differences import summarize_diff
from compare_unity_light_prefabs import compare as compare_light_prefabs
from compare_unity_lod_collider_variants import compare as compare_lod_collider

PROVENANCE_DIFFERENCES = {
    'id', 'pack_id', 'source_asset_path', 'source_file', 'source_meta_file',
    'nested_archive_provenance', 'source_direct_archive_is_nested', 'source_direct_archive_sha256',
    'source_archive', 'source_archive_bytes', 'source_archive_sha256', 'source_ir_evidence',
    'source_manifest_path', 'source_manifest_sha256', 'source_queue_path', 'source_queue_sha256',
    'evidence', 'tags', 'preview', 'preview_origin', 'preview_views',
}
PREVIEW_METADATA_DIFFERENCES = {'descriptions','reason','preview_capture','preview_status',
    'preview_visual_review','original_unity_thumbnail','preview_provenance','preview_ready',
    'preview_note','preview_forge_render','preview_kind'}
PREFAB_COMPONENT_DIFFERENCES = {'names','reason_details','source_components','source_materials',
    'source_prefab_asset_path','source_prefab_file','source_prefab_meta_file'}
GEOMETRY_DETAIL_DIFFERENCES = {'descriptions','reason','taxonomy','support_kind','names','source_geometry'}
MEASURED_SOURCE_DIFFERENCES = {'descriptions','full_import','material_bindings','measurement','measurement_evidence',
    'names','original_lod_level','placeable','preview_evidence','preview_forge_render','preview_origin',
    'preview_status','preview_visual_review','readiness','reason','runtime_equivalence','source_components',
    'source_lod_level','source_materials','support_kind','taxonomy','unported_components'}


def measured_source_pair(hdrp, urp):
    lod=hdrp.get('source_lod_level');components=hdrp.get('source_components')
    return (isinstance(lod,int) and lod==urp.get('original_lod_level')
            and isinstance(hdrp.get('readiness'),dict) and hdrp['readiness'].get('geometry_measured') is True
            and isinstance(urp.get('readiness'),dict) and urp['readiness'].get('geometry_measured') is False
            and isinstance(components,list) and bool(components)
            and all(isinstance(c,dict) and ((isinstance(c.get('lod'),dict) and c['lod'].get('level')==lod)
                    or (c.get('lod') is None and lod==0))
                    and storage.valid_hex(c.get('mesh_sha256'),64) for c in components)
            and isinstance(hdrp.get('source_file'),str) and hdrp['source_file']==urp.get('source_file')
            and isinstance(hdrp.get('source_meta_file'),str) and hdrp['source_meta_file']==urp.get('source_meta_file'))


def compatible_geometry_details(source, target):
    """Require the same underlying source and each declared LOD mesh identity."""
    a, b = source.get('source_geometry'), target.get('source_geometry')
    if not isinstance(a,dict) or not isinstance(b,dict): return False
    left, right = a.get('components'), b.get('components')
    if not isinstance(left,list) or not left or not isinstance(right,list) or len(left)!=len(right): return False
    if a.get('lod_level') is None or a.get('lod_level') != b.get('lod_level'): return False
    fields = ('mesh_guid','mesh_sha256','mesh_name','mesh_fileid','lod','import_settings')
    for x, y in zip(left,right):
        if not isinstance(x,dict) or not isinstance(y,dict): return False
        if any(k not in x or k not in y or x[k] != y[k] for k in fields): return False
        if not storage.valid_hex(x['mesh_sha256'],64): return False
    return (isinstance(source.get('source_file'),str) and source['source_file']==target.get('source_file')
            and isinstance(source.get('source_meta_file'),str) and source['source_meta_file']==target.get('source_meta_file'))


def verified_source_reuse(source, target):
    reuse=source.get('source_reuse')
    if not isinstance(reuse,dict):return False
    if any(reuse.get(k) is not True for k in ('geometry_signature_identical','meta_bytes_identical','prefab_bytes_identical')):return False
    if reuse.get('donor_id')!=target.get('id'):return False
    if not isinstance(reuse.get('target_geometry_signature'),dict):return False
    return (isinstance(source.get('source_file'),str) and source['source_file']==target.get('source_file')==reuse.get('donor_source_file')
            and isinstance(source.get('source_meta_file'),str) and source['source_meta_file']==target.get('source_meta_file')==reuse.get('donor_source_meta_file')
            and source.get('preview_sha256')==target.get('preview_sha256'))


def normalized_pipeline_paths(value):
    if isinstance(value,dict):return {key:normalized_pipeline_paths(item) for key,item in value.items()}
    if isinstance(value,list):return [normalized_pipeline_paths(item) for item in value]
    if isinstance(value,str):
        return re.sub(r'(?i)(?<=/)(?:hdrp(?:\s*\(default\))?|urp(?:\s*\(default\))?)(?=/)','PIPELINE',value)
    return value


def identical_prefab_components(source,target):
    return (isinstance(source.get('source_components'),list) and isinstance(target.get('source_components'),list)
        and normalized_pipeline_paths(source['source_components'])==normalized_pipeline_paths(target['source_components'])
        and isinstance(source.get('source_materials'),list) and isinstance(target.get('source_materials'),list)
        and all(storage.valid_hex(source.get(key),64) and source[key]==target.get(key)
                for key in ('source_prefab_sha256','source_prefab_meta_sha256')))


def consolidate(catalog, pairs, distinct_previews=False, preserve_distinct_text=False, preserve_preview_metadata=False, preserve_taxonomy=False, preserve_same_preview_metadata=False, preserve_material_bindings=False, preserve_prefab_components=False, preserve_geometry_details=False, preserve_lod_variants=False, preserve_source_reuse=False, preserve_provenance_details=False, preserve_light_prefab=False, preserve_measured_source=False, preserve_lod_collider=False, preserve_pipeline_light=False):
    if preserve_pipeline_light and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab,preserve_measured_source,preserve_lod_collider))):
        raise ValueError('pipeline light mode requires preserved previews and no other mode')
    if preserve_lod_collider and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab,preserve_measured_source))):
        raise ValueError('LOD collider mode requires preserved previews and no other mode')
    if preserve_measured_source and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab))):
        raise ValueError('measured source mode requires preserved previews and no other mode')
    if preserve_light_prefab and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details))):
        raise ValueError('light prefab mode requires preserved previews and no other mode')
    if preserve_provenance_details and any((distinct_previews,preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse)):
        raise ValueError('provenance details mode must be used alone')
    if preserve_source_reuse and any((distinct_previews,preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants)):
        raise ValueError('source reuse mode must be used alone')
    if preserve_lod_variants and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details))):
        raise ValueError('LOD variant mode requires preserved previews and no other mode')
    if preserve_geometry_details and (not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components))):
        raise ValueError('geometry details mode requires preserved previews and no other mode')
    if preserve_same_preview_metadata and (distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy):
        raise ValueError('same-preview mode must be used alone')
    if preserve_material_bindings and (not distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy or preserve_same_preview_metadata):
        raise ValueError('material mode requires distinct previews and no other mode')
    if preserve_prefab_components and (not distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy or preserve_same_preview_metadata or preserve_material_bindings):
        raise ValueError('prefab mode requires distinct previews and no other mode')
    result = copy.deepcopy(catalog)
    cards = result['assets']
    by_id = {c['id']: c for c in cards}
    if len(by_id) != len(cards):
        raise ValueError('duplicate source identity')
    retired = set()
    for pair in pairs:
        old, keep = pair['hdrp_id'], pair['urp_id']
        if old == keep or old not in by_id or keep not in by_id or old in retired or keep in retired:
            raise ValueError('missing or overlapping identities')
        source, target = by_id[old], by_id[keep]
        if source.get('consolidated_sources') or target.get('consolidated_sources'):
            raise ValueError('existing source aliases require separate migration')
        actual_differences = sorted(k for k in set(source) | set(target) if source.get(k) != target.get(k))
        if actual_differences != sorted(pair['different_fields']):
            raise ValueError('pair comparison differs from source cards')
        allowed = PROVENANCE_DIFFERENCES | ({'preview_sha256'} if distinct_previews else set())
        if preserve_pipeline_light:
            allowed |= PREVIEW_METADATA_DIFFERENCES | {'source_sha256','material_bindings'}
            if pair.get('pipeline_light_only') is not True or source.get('source_sha256')==target.get('source_sha256'):
                raise ValueError('source differences not qualified as pipeline lights')
            if 'material_bindings' in pair['different_fields'] and (not isinstance(source.get('material_bindings'),list) or not isinstance(target.get('material_bindings'),list)):
                raise ValueError('different material bindings must be lists')
        if preserve_lod_collider:
            allowed |= PREVIEW_METADATA_DIFFERENCES | {'source_sha256','material_bindings'}
            if pair.get('lod_collider_only') is not True or source.get('source_sha256')==target.get('source_sha256'):
                raise ValueError('source differences not qualified as LOD and collider labels')
            if 'material_bindings' in pair['different_fields'] and (not isinstance(source.get('material_bindings'),list) or not isinstance(target.get('material_bindings'),list)):
                raise ValueError('different material bindings must be lists')
        if preserve_measured_source:
            allowed |= MEASURED_SOURCE_DIFFERENCES
            if not measured_source_pair(target,source):raise ValueError('measured source relation differs')
        if preserve_light_prefab:
            allowed |= PREVIEW_METADATA_DIFFERENCES | PREFAB_COMPONENT_DIFFERENCES | {'source_prefab_sha256'}
            if (pair.get('prefab_pipeline_light_only') is not True
                or not isinstance(source.get('source_components'),list) or not source['source_components']
                or normalized_pipeline_paths(source['source_components'])!=normalized_pipeline_paths(target.get('source_components'))
                or not isinstance(source.get('source_materials'),list) or not isinstance(target.get('source_materials'),list)
                or not storage.valid_hex(source.get('source_prefab_meta_sha256'),64)
                or source.get('source_prefab_meta_sha256')!=target.get('source_prefab_meta_sha256')
                or not isinstance(source.get('source_file'),str) or source.get('source_file')!=target.get('source_file')
                or not isinstance(source.get('source_meta_file'),str) or source.get('source_meta_file')!=target.get('source_meta_file')):
                raise ValueError('light prefab geometry or source differs')
        if preserve_provenance_details:
            allowed |= {'names','descriptions','preview_provenance','source_prefab_asset_path','source_names_preserved','source_visual_classification'}
            if not (isinstance(source.get('source_file'),str) and source['source_file']==target.get('source_file')
                    and isinstance(source.get('source_meta_file'),str) and source['source_meta_file']==target.get('source_meta_file')):
                raise ValueError('provenance variants do not share source files')
        if preserve_source_reuse:
            allowed |= {'descriptions','names','reason','source_reuse','support_kind','taxonomy'}
            if not verified_source_reuse(source,target): raise ValueError('source reuse evidence differs')
        if preserve_lod_variants:
            allowed |= {'source_sha256','source_lod_screen_relative_height','taxonomy'}
            if pair.get('lod_thresholds_only') is not True or source.get('source_sha256') == target.get('source_sha256'):
                raise ValueError('source differences not qualified as LOD thresholds only')
        if preserve_geometry_details:
            allowed |= GEOMETRY_DETAIL_DIFFERENCES
            if not compatible_geometry_details(source,target): raise ValueError('geometry details differ')
        if preserve_taxonomy:
            if not distinct_previews: raise ValueError('taxonomy mode requires preserved previews')
            allowed.add('taxonomy')
        if preserve_material_bindings:
            allowed |= PREVIEW_METADATA_DIFFERENCES | {'material_bindings'}
            if not isinstance(source.get('material_bindings'),list) or not isinstance(target.get('material_bindings'),list):
                raise ValueError('material bindings must be lists')
        if preserve_prefab_components:
            allowed |= PREVIEW_METADATA_DIFFERENCES | PREFAB_COMPONENT_DIFFERENCES
            if not identical_prefab_components(source,target):raise ValueError('prefab components or hashes differ')
        if preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy or preserve_same_preview_metadata or preserve_material_bindings or preserve_prefab_components:
            if not distinct_previews and not preserve_same_preview_metadata:raise ValueError('distinct-text mode requires preserved previews')
            if preserve_preview_metadata or preserve_same_preview_metadata: allowed |= PREVIEW_METADATA_DIFFERENCES
            if preserve_distinct_text: allowed |= {'descriptions','reason'}
            if (not isinstance(source.get('source_file'),str) or not isinstance(source.get('source_meta_file'),str)
                    or source.get('source_file')!=target.get('source_file')
                    or source.get('source_meta_file')!=target.get('source_meta_file')):
                raise ValueError('distinct-text mode requires identical source paths')
        if ((not (preserve_lod_variants or preserve_lod_collider or preserve_pipeline_light) and not pair['declared_source_sha_equal']) or not pair['declared_meta_sha_equal']
                or set(pair['different_fields']) - allowed):
            raise ValueError('pair differs outside permitted provenance fields')
        for key in (('source_sha256', 'source_meta_sha256') if distinct_previews else
                    ('source_sha256', 'source_meta_sha256', 'preview_sha256')):
            if source.get(key) != target.get(key) and not ((preserve_lod_variants or preserve_lod_collider or preserve_pipeline_light) and key == 'source_sha256'):
                raise ValueError('source or preview hash differs')
        alias = {'retired_id': old, 'original_detail': source}
        if preserve_lod_variants:
            alias['source_variation'] = {'kind':'lod_thresholds_only'}
        if preserve_lod_collider:
            alias['source_variation'] = {'kind':'lod_collider_only'}
        if preserve_pipeline_light:
            alias['source_variation'] = {'kind':'pipeline_light_only'}
        if distinct_previews and source.get('preview_sha256') != target.get('preview_sha256'):
            alias['preserve_distinct_preview'] = True
        target['consolidated_sources'] = [alias]
        retired.add(old)
    result['assets'] = [c for c in cards if c['id'] not in retired]
    return result


def prepare(vault, distinct_previews=False, preserve_distinct_text=False, preserve_preview_metadata=False, preserve_taxonomy=False, preserve_same_preview_metadata=False, preserve_material_bindings=False, preserve_prefab_components=False, preserve_geometry_details=False, preserve_lod_variants=False, preserve_source_reuse=False, preserve_provenance_details=False, preserve_light_prefab=False, preserve_measured_source=False, preserve_lod_collider=False, preserve_pipeline_light=False):
    vault = vault.resolve(strict=True)
    source_ptr = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    model_ptr = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    source_before, model_before = source_ptr.read_bytes(), model_ptr.read_bytes()
    pointer = json.loads(source_before)
    raw = storage.confined(vault, vault/pointer['path']).read_bytes()
    if storage.sha(raw) != pointer['sha256']:
        raise ValueError('source catalog SHA changed')
    catalog = json.loads(raw)
    allowed = PROVENANCE_DIFFERENCES | ({'preview_sha256'} if distinct_previews else set())
    if preserve_pipeline_light:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab,preserve_measured_source,preserve_lod_collider)):raise ValueError('invalid pipeline light mode')
        allowed |= PREVIEW_METADATA_DIFFERENCES | {'source_sha256','material_bindings'}
    if preserve_lod_collider:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab,preserve_measured_source)):raise ValueError('invalid LOD collider mode')
        allowed |= PREVIEW_METADATA_DIFFERENCES | {'source_sha256','material_bindings'}
    if preserve_measured_source:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details,preserve_light_prefab)):raise ValueError('invalid measured source mode')
        allowed |= MEASURED_SOURCE_DIFFERENCES
    if preserve_light_prefab:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse,preserve_provenance_details)):raise ValueError('invalid light prefab mode')
        allowed |= PREVIEW_METADATA_DIFFERENCES | PREFAB_COMPONENT_DIFFERENCES | {'source_prefab_sha256'}
    if preserve_provenance_details:
        if any((distinct_previews,preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants,preserve_source_reuse)):raise ValueError('invalid provenance details mode')
        allowed |= {'names','descriptions','preview_provenance','source_prefab_asset_path','source_names_preserved','source_visual_classification'}
    if preserve_source_reuse:
        if any((distinct_previews,preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details,preserve_lod_variants)):raise ValueError('invalid source reuse mode')
        allowed |= {'descriptions','names','reason','source_reuse','support_kind','taxonomy'}
    if preserve_lod_variants:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components,preserve_geometry_details)): raise ValueError('invalid LOD variant mode')
        allowed |= {'source_sha256','source_lod_screen_relative_height','taxonomy'}
    if preserve_geometry_details:
        if not distinct_previews or any((preserve_distinct_text,preserve_preview_metadata,preserve_taxonomy,preserve_same_preview_metadata,preserve_material_bindings,preserve_prefab_components)): raise ValueError('invalid geometry details mode')
        allowed |= GEOMETRY_DETAIL_DIFFERENCES
    if preserve_distinct_text and preserve_preview_metadata:raise ValueError('choose a single metadata mode')
    if preserve_same_preview_metadata:
        if distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy:raise ValueError('same-preview mode must be used alone')
        allowed |= PREVIEW_METADATA_DIFFERENCES
    if preserve_material_bindings:
        if not distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy or preserve_same_preview_metadata:raise ValueError('material mode requires distinct previews and no other mode')
        allowed |= PREVIEW_METADATA_DIFFERENCES | {'material_bindings'}
    if preserve_prefab_components:
        if not distinct_previews or preserve_distinct_text or preserve_preview_metadata or preserve_taxonomy or preserve_same_preview_metadata or preserve_material_bindings:raise ValueError('prefab mode requires distinct previews and no other mode')
        allowed |= PREVIEW_METADATA_DIFFERENCES | PREFAB_COMPONENT_DIFFERENCES
    if preserve_taxonomy:
        if not distinct_previews: raise ValueError('taxonomy mode requires preserved previews')
        allowed.add('taxonomy')
    if preserve_distinct_text or preserve_preview_metadata:
        if not distinct_previews:raise ValueError('distinct-text mode requires preserved previews')
        allowed |= PREVIEW_METADATA_DIFFERENCES if preserve_preview_metadata else {'descriptions','reason'}
    by_id = {c['id']:c for c in catalog['assets']}
    pairs = [p for p in classify(catalog['assets'])
             if (p['declared_source_sha_equal'] or preserve_lod_variants or preserve_lod_collider or preserve_pipeline_light) and p['declared_meta_sha_equal']
             and not (set(p['different_fields']) - allowed)
             and (not distinct_previews or preserve_geometry_details or preserve_lod_variants or preserve_material_bindings or preserve_light_prefab or preserve_measured_source or preserve_lod_collider or preserve_pipeline_light or 'preview_sha256' in p['different_fields'])
             and (not preserve_measured_source or measured_source_pair(by_id[p['hdrp_id']],by_id[p['urp_id']]))
             and (not preserve_light_prefab or ('source_prefab_sha256' in p['different_fields']
                  and isinstance(by_id[p['hdrp_id']].get('source_components'),list)
                  and by_id[p['hdrp_id']]['source_components']
                  and normalized_pipeline_paths(by_id[p['hdrp_id']]['source_components'])==normalized_pipeline_paths(by_id[p['urp_id']].get('source_components'))
                  and by_id[p['hdrp_id']].get('source_file')==by_id[p['urp_id']].get('source_file')
                  and by_id[p['hdrp_id']].get('source_meta_file')==by_id[p['urp_id']].get('source_meta_file')))
             and (not preserve_provenance_details or (bool({'names','source_prefab_asset_path','source_visual_classification'} & set(p['different_fields']))
                  and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                  and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                  and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                  and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))
             and (not preserve_source_reuse or ('source_reuse' in p['different_fields']
                  and verified_source_reuse(by_id[p['hdrp_id']],by_id[p['urp_id']])))
             and (not preserve_geometry_details or ('source_geometry' in p['different_fields']
                  and compatible_geometry_details(by_id[p['hdrp_id']],by_id[p['urp_id']])))
             and (not preserve_prefab_components or ('source_components' in p['different_fields']
                  and identical_prefab_components(by_id[p['hdrp_id']],by_id[p['urp_id']])
                  and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                  and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                  and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                  and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))
             and (not preserve_material_bindings or ('material_bindings' in p['different_fields']
                  and isinstance(by_id[p['hdrp_id']].get('material_bindings'),list)
                  and isinstance(by_id[p['urp_id']].get('material_bindings'),list)
                  and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                  and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                  and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                  and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))
             and (not preserve_same_preview_metadata or
                  (bool(PREVIEW_METADATA_DIFFERENCES & set(p['different_fields']))
                   and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                   and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                   and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                   and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))
             and (not preserve_taxonomy or ('taxonomy' in p['different_fields']
                  and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                  and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                  and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                  and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))
             and (not (preserve_distinct_text or preserve_preview_metadata) or
                  (bool((PREVIEW_METADATA_DIFFERENCES if preserve_preview_metadata else {'descriptions','reason'}) & set(p['different_fields']))
                   and isinstance(by_id[p['hdrp_id']].get('source_file'),str)
                   and isinstance(by_id[p['hdrp_id']].get('source_meta_file'),str)
                   and by_id[p['hdrp_id']]['source_file']==by_id[p['urp_id']]['source_file']
                   and by_id[p['hdrp_id']]['source_meta_file']==by_id[p['urp_id']]['source_meta_file']))]
    if preserve_measured_source:
        pairs=[dict(p,hdrp_id=p['urp_id'],urp_id=p['hdrp_id']) for p in pairs]
    if preserve_light_prefab:
        qualified=[];prefab_cache={}
        for pair in pairs:
            texts=[]
            for ident in (pair['hdrp_id'],pair['urp_id']):
                card=by_id[ident];rel=card.get('source_prefab_file');norm(rel);path=storage.confined(vault,vault/rel)
                pin=card.get('source_prefab_sha256')
                if not storage.valid_hex(pin,64) or path.stat().st_size>2*1024*1024:break
                if path not in prefab_cache:
                    with storage.locked_source(path,share_delete=False) as stream:raw_prefab=stream.read(2*1024*1024+1)
                    if storage.sha(raw_prefab)!=pin:raise ValueError('prefab SHA mismatch: '+ident)
                    try:prefab_cache[path]=(pin,raw_prefab.decode('utf-8-sig'))
                    except UnicodeDecodeError:break
                cached_pin,text=prefab_cache[path]
                if cached_pin!=pin:raise ValueError('conflicting prefab pin')
                texts.append(text)
            if len(texts)==2:
                try:result=compare_light_prefabs(*texts)
                except ValueError:continue
                if result['equal_outside_pipeline_lights']:
                    pair['prefab_pipeline_light_only']=True;qualified.append(pair)
        pairs=qualified
    if preserve_lod_variants or preserve_lod_collider or preserve_pipeline_light:
        qualified=[];source_cache={}
        for pair in pairs:
            if pair['declared_source_sha_equal']: continue
            texts=[]
            for ident in (pair['hdrp_id'],pair['urp_id']):
                card=by_id[ident];rel=card.get('source_file');norm(rel);path=storage.confined(vault,vault/rel)
                pin=card.get('source_sha256')
                if not storage.valid_hex(pin,64) or path.stat().st_size>2*1024*1024: break
                if path not in source_cache:
                    with storage.locked_source(path,share_delete=False) as stream: raw_source=stream.read(2*1024*1024+1)
                    if storage.sha(raw_source)!=pin: raise ValueError('source SHA mismatch: '+ident)
                    try: source_cache[path]=(pin,raw_source.decode('utf-8-sig'))
                    except UnicodeDecodeError: break
                cached_pin,text=source_cache[path]
                if cached_pin!=pin: raise ValueError('conflicting source pin')
                texts.append(text)
            if len(texts)==2 and (preserve_pipeline_light or (texts[0].count('\n')<=10000 and texts[1].count('\n')<=10000)):
                if preserve_lod_variants:
                    result=summarize_diff(*texts);matching=result['equal_except_lod_thresholds'] and not result['normalized_lines_equal']
                elif preserve_lod_collider:matching=compare_lod_collider(*texts)
                else:
                    try:matching=compare_light_prefabs(*texts)['equal_outside_pipeline_lights']
                    except ValueError:matching=False
                if matching:
                    key='lod_thresholds_only' if preserve_lod_variants else 'lod_collider_only' if preserve_lod_collider else 'pipeline_light_only'
                    pair[key]=True;qualified.append(pair)
        pairs=qualified
    if not pairs:
        raise ValueError('no strictly matched source cards')
    cards = {c['id']: c for c in catalog['assets']}
    cache = {}
    for pair in pairs:
        for ident in (pair['hdrp_id'], pair['urp_id']):
            card = cards[ident]
            fields=[('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256'), ('preview', 'preview_sha256')]
            if preserve_prefab_components or preserve_light_prefab:fields += [('source_prefab_file','source_prefab_sha256'),('source_prefab_meta_file','source_prefab_meta_sha256')]
            for field, pin_field in fields:
                path, sha = card.get(field), card.get(pin_field)
                if path is None and sha is None:
                    continue
                if not isinstance(path, str) or not storage.valid_hex(sha, 64):
                    raise ValueError('missing path/hash: '+ident+'/'+field)
                norm(path)
                path = storage.confined(vault, vault/path)
                if path not in cache:
                    cache[path] = digest_file(path)
                if cache[path] != sha:
                    raise ValueError('source payload SHA mismatch: '+ident+'/'+field)
    staged = consolidate(catalog, pairs, distinct_previews, preserve_distinct_text, preserve_preview_metadata, preserve_taxonomy, preserve_same_preview_metadata, preserve_material_bindings, preserve_prefab_components, preserve_geometry_details, preserve_lod_variants, preserve_source_reuse, preserve_provenance_details, preserve_light_prefab, preserve_measured_source, preserve_lod_collider, preserve_pipeline_light)
    if source_ptr.read_bytes() != source_before or model_ptr.read_bytes() != model_before:
        raise ValueError('registry changed')
    encoded = (json.dumps(staged, sort_keys=True, ensure_ascii=False, indent=2)+'\n').encode('utf8')
    plan = {'schema': 'openrpg.source-support-pair-consolidation-stage.v1', 'state': 'prepared_only',
            'source_registry_sha256': storage.sha(source_before), 'asset_registry_sha256': storage.sha(model_before),
            'old_catalog_sha256': storage.sha(raw), 'new_catalog_sha256': storage.sha(encoded),
            'pairs': pairs, 'payloads_verified': len(cache),
            'limitations': 'Not published; source alias reader/importer compatibility and Unity behavior unqualified.'}
    if distinct_previews:
        plan['distinct_previews'] = True
    if preserve_distinct_text:
        plan['preserve_distinct_text'] = True
    if preserve_preview_metadata:
        plan['preserve_preview_metadata'] = True
    if preserve_taxonomy:
        plan['preserve_taxonomy'] = True
    if preserve_same_preview_metadata:
        plan['preserve_same_preview_metadata'] = True
    if preserve_material_bindings:
        plan['preserve_material_bindings'] = True
    if preserve_prefab_components:
        plan['preserve_prefab_components'] = True
    if preserve_geometry_details:
        plan['preserve_geometry_details'] = True
    if preserve_lod_variants:
        plan['preserve_lod_variants'] = True
    if preserve_source_reuse:
        plan['preserve_source_reuse'] = True
    if preserve_provenance_details:
        plan['preserve_provenance_details'] = True
    if preserve_light_prefab:
        plan['preserve_light_prefab'] = True
    if preserve_measured_source:
        plan['preserve_measured_source'] = True
    if preserve_lod_collider:
        plan['preserve_lod_collider'] = True
    if preserve_pipeline_light:
        plan['preserve_pipeline_light'] = True
    return encoded, plan


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', required=True, type=Path)
    p.add_argument('--stage', required=True, type=Path)
    p.add_argument('--distinct-previews', action='store_true')
    p.add_argument('--preserve-distinct-text', action='store_true')
    p.add_argument('--preserve-preview-metadata', action='store_true')
    p.add_argument('--preserve-taxonomy', action='store_true')
    p.add_argument('--preserve-same-preview-metadata', action='store_true')
    p.add_argument('--preserve-material-bindings', action='store_true')
    p.add_argument('--preserve-prefab-components', action='store_true')
    p.add_argument('--preserve-geometry-details', action='store_true')
    p.add_argument('--preserve-lod-variants', action='store_true')
    p.add_argument('--preserve-source-reuse', action='store_true')
    p.add_argument('--preserve-provenance-details', action='store_true')
    p.add_argument('--preserve-light-prefab', action='store_true')
    p.add_argument('--preserve-measured-source', action='store_true')
    p.add_argument('--preserve-lod-collider', action='store_true')
    p.add_argument('--preserve-pipeline-light', action='store_true')
    a = p.parse_args()
    root = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    dest = a.stage.resolve(strict=False)
    if not dest.is_relative_to(root) or dest.exists():
        p.error('new stage must be under the asset conversion workspace')
    raw, plan = prepare(a.vault, a.distinct_previews, a.preserve_distinct_text, a.preserve_preview_metadata, a.preserve_taxonomy, a.preserve_same_preview_metadata, a.preserve_material_bindings, a.preserve_prefab_components, a.preserve_geometry_details, a.preserve_lod_variants, a.preserve_source_reuse, a.preserve_provenance_details, a.preserve_light_prefab, a.preserve_measured_source, a.preserve_lod_collider, a.preserve_pipeline_light)
    dest.mkdir()
    (dest/'source-support.v1.json').write_bytes(raw)
    (dest/'plan.json').write_text(json.dumps(plan, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(json.dumps({'state':plan['state'], 'pairs':len(plan['pairs']), 'payloads_verified':plan['payloads_verified']}))
