import unittest
from stage_source_support_pair_consolidation import consolidate


class ConsolidationTests(unittest.TestCase):
    def test_pipeline_light_variant_preserves_original_and_requires_evidence(self):
        h={'id':'h','source_file':'hdr.prefab','source_sha256':'a'*64,'source_meta_file':'same.meta',
           'source_meta_sha256':'c'*64,'preview_sha256':'h'}
        u=dict(h,id='u',source_file='urp.prefab',source_sha256='b'*64,preview_sha256='u')
        pair={'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':False,'declared_meta_sha_equal':True,
              'different_fields':sorted(k for k in set(h)|set(u) if h.get(k)!=u.get(k)),'pipeline_light_only':True}
        result=consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_pipeline_light=True)
        alias=result['assets'][0]['consolidated_sources'][0]
        self.assertEqual(alias['original_detail'],h)
        self.assertEqual(alias['source_variation'],{'kind':'pipeline_light_only'})
        pair['pipeline_light_only']=False
        with self.assertRaisesRegex(ValueError,'not qualified'):
            consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_pipeline_light=True)

    def test_lod_collider_variant_preserves_original_and_requires_checked_relation(self):
        h={'id':'h','source_file':'hdr.prefab','source_sha256':'a'*64,'source_meta_file':'same.meta',
           'source_meta_sha256':'c'*64,'preview_sha256':'h'}
        u=dict(h,id='u',source_file='urp.prefab',source_sha256='b'*64,preview_sha256='u')
        pair={'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':False,'declared_meta_sha_equal':True,
              'different_fields':sorted(k for k in set(h)|set(u) if h.get(k)!=u.get(k)),'lod_collider_only':True}
        result=consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_lod_collider=True)
        alias=result['assets'][0]['consolidated_sources'][0]
        self.assertEqual(alias['original_detail'],h)
        self.assertEqual(alias['source_variation'],{'kind':'lod_collider_only'})
        pair['lod_collider_only']=False
        with self.assertRaisesRegex(ValueError,'not qualified'):
            consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_lod_collider=True)

    def test_measured_source_retains_richer_owner_and_original_variant(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s','source_meta_sha256':'m',
           'preview_sha256':'h','readiness':{'geometry_measured':True},'source_lod_level':1,
           'source_components':[{'lod':{'level':1},'mesh_sha256':'a'*64}],'measurement':{'vertices':12}}
        u={'id':'u','source_file':'same','source_meta_file':'same.meta','source_sha256':'s','source_meta_sha256':'m',
           'preview_sha256':'u','readiness':{'geometry_measured':False},'original_lod_level':1,'material_bindings':['wood']}
        def pair(a,b):
            return {'hdrp_id':'u','urp_id':'h','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        out=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_measured_source=True)
        self.assertEqual(out['assets'][0]['measurement'],{'vertices':12})
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],u)
        changed=dict(u,original_lod_level=0)
        with self.assertRaisesRegex(ValueError,'relation differs'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_measured_source=True)
        no_group=dict(h,source_lod_level=0,source_components=[{'lod':None,'mesh_sha256':'a'*64}])
        zero=dict(u,original_lod_level=0)
        result=consolidate({'assets':[no_group,zero]},[pair(no_group,zero)],distinct_previews=True,preserve_measured_source=True)
        self.assertEqual(result['assets'][0]['id'],'h')

    def test_light_prefab_mode_preserves_original_and_rejects_geometry_change(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s','source_meta_sha256':'m',
           'source_prefab_sha256':'a'*64,'source_prefab_meta_sha256':'c'*64,'preview_sha256':'h',
           'source_prefab_file':'hdr/a.prefab','source_prefab_meta_file':'hdr/a.prefab.meta',
           'source_components':[{'mesh_source':'Assets/Pack/HDRP(Default)/mesh.fbx','lod':0}],
           'source_materials':[{'shader':'hdrp'}]}
        u=dict(h,id='u',source_prefab_sha256='b'*64,preview_sha256='u',source_prefab_file='urp/a.prefab',
               source_prefab_meta_file='urp/a.prefab.meta',source_components=[{'mesh_source':'Assets/Pack/URP/mesh.fbx','lod':0}],
               source_materials=[{'shader':'urp'}])
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k)),
                    'prefab_pipeline_light_only':True}
        result=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_light_prefab=True)
        self.assertEqual(result['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        changed=dict(u,source_components=[{'mesh_source':'Assets/Pack/URP/mesh.fbx','lod':1}])
        with self.assertRaisesRegex(ValueError,'geometry or source differs'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_light_prefab=True)

    def test_provenance_mode_preserves_names_and_rejects_different_sources(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview_sha256':'p','names':{'fr':'Ancien'},
           'source_prefab_asset_path':'Assets/HDRP/a.prefab'}
        u=dict(h,id='u',names={'fr':'Retenu'},source_prefab_asset_path='Assets/URP/a.prefab')
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        result=consolidate({'assets':[h,u]},[pair(h,u)],preserve_provenance_details=True)
        self.assertEqual(result['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        changed=dict(u,source_file='different')
        with self.assertRaisesRegex(ValueError,'do not share'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],preserve_provenance_details=True)
        changed=dict(u,material_bindings=['different'])
        with self.assertRaisesRegex(ValueError,'outside permitted'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],preserve_provenance_details=True)

    def test_source_reuse_requires_correct_donor_and_retains_metadata(self):
        h={'id':'h','source_file':'same.prefab','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview_sha256':'p','support_kind':'original',
           'source_reuse':{'donor_id':'u','donor_source_file':'same.prefab','donor_source_meta_file':'same.meta',
                           'geometry_signature_identical':True,'meta_bytes_identical':True,'prefab_bytes_identical':True,
                           'target_geometry_signature':{'mesh':'example'}}}
        u={k:v for k,v in h.items() if k!='source_reuse'};u.update(id='u',support_kind='retained')
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        result=consolidate({'assets':[h,u]},[pair(h,u)],preserve_source_reuse=True)
        self.assertEqual(result['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        bad=dict(h,source_reuse=dict(h['source_reuse'],donor_id='other'))
        with self.assertRaisesRegex(ValueError,'reuse evidence'):
            consolidate({'assets':[bad,u]},[pair(bad,u)],preserve_source_reuse=True)

    def test_lod_variant_mode_preserves_both_original_sources_and_details(self):
        h={'id':'h','source_file':'hdr/file.prefab','source_sha256':'a'*64,'source_meta_file':'hdr/file.meta',
           'source_meta_sha256':'c'*64,'preview_sha256':'x','source_lod_screen_relative_height':0.6}
        u=dict(h,id='u',source_file='urp/file.prefab',source_sha256='b'*64,source_meta_file='urp/file.meta',
               preview_sha256='y',source_lod_screen_relative_height=0.3)
        pair={'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':False,'declared_meta_sha_equal':True,
              'different_fields':sorted(k for k in set(h)|set(u) if h.get(k)!=u.get(k)), 'lod_thresholds_only':True}
        result=consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_lod_variants=True)
        self.assertEqual(result['assets'][0]['source_file'],'urp/file.prefab')
        self.assertEqual(result['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        pair['lod_thresholds_only']=False
        with self.assertRaisesRegex(ValueError,'not qualified'):
            consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_lod_variants=True)

    def test_geometry_detail_mode_preserves_records_and_rejects_mesh_or_lod_changes(self):
        comp={'mesh_guid':'guid','mesh_sha256':'a'*64,'mesh_name':'mesh','mesh_fileid':4300000,
              'lod':{'level':0},'import_settings':{'scale':1},'material_guids':['wood']}
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s','source_meta_sha256':'m',
           'preview_sha256':'old','source_geometry':{'lod_level':0,'components':[comp]},'support_kind':'old'}
        richer=dict(comp); richer.pop('material_guids'); richer['mesh_resolution']='resolved'
        u=dict(h,id='u',preview_sha256='new',support_kind='new',source_geometry={'lod_level':0,'components':[richer],'name':'mesh'})
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        result=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_geometry_details=True)
        self.assertEqual(result['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        self.assertEqual(result['assets'][0]['source_geometry'],u['source_geometry'])
        bad=dict(richer,mesh_sha256='b'*64)
        changed=dict(u,source_geometry={'lod_level':0,'components':[bad]})
        with self.assertRaisesRegex(ValueError,'geometry details differ'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_geometry_details=True)
        changed=dict(u,source_geometry={'lod_level':1,'components':[richer]})
        with self.assertRaisesRegex(ValueError,'geometry details differ'):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_geometry_details=True)

    def test_prefab_mode_checks_geometry_and_preserves_material_sources(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s','source_meta_sha256':'m',
           'source_prefab_sha256':'a'*64,'source_prefab_meta_sha256':'b'*64,'preview_sha256':'h',
           'source_prefab_file':'hdr/prefab','source_prefab_meta_file':'hdr/prefab.meta',
           'source_components':[{'mesh_source':'Assets/Pack/HDRP(Default)/mesh.fbx','mesh_sha256':'same','lod':0}],
           'source_materials':[{'shader':'hdrp'}]}
        u=dict(h,id='u',preview_sha256='u',source_prefab_file='urp/prefab',source_prefab_meta_file='urp/prefab.meta',
               source_components=[{'mesh_source':'Assets/Pack/URP/mesh.fbx','mesh_sha256':'same','lod':0}],
               source_materials=[{'shader':'urp'}])
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        out=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_prefab_components=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        self.assertEqual(out['assets'][0]['source_materials'],u['source_materials'])
        for changed in (dict(u,source_prefab_sha256='c'*64),dict(u,source_components=[{'mesh_source':'Assets/Pack/URP/mesh.fbx','mesh_sha256':'same','lod':1}]),dict(u,source_file='other')):
            with self.assertRaises(ValueError):consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_prefab_components=True)

    def test_material_bindings_preserve_both_records_and_refuse_geometry_changes(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview':'h.png','preview_sha256':'h',
           'material_bindings':[{'shader':'hdrp'}],'source_geometry':{'triangles':12}}
        u=dict(h,id='u',preview='u.png',preview_sha256='u',material_bindings=[{'shader':'urp'}])
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        with self.assertRaises(ValueError):consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True)
        out=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_material_bindings=True)
        self.assertEqual(out['assets'][0]['material_bindings'],u['material_bindings'])
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        same_preview=dict(u,preview='h.png',preview_sha256='h')
        out=consolidate({'assets':[h,same_preview]},[pair(h,same_preview)],distinct_previews=True,preserve_material_bindings=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        self.assertNotIn('preserve_distinct_preview',out['assets'][0]['consolidated_sources'][0])
        for changed in (dict(u,source_file='other'),dict(u,source_geometry={'triangles':13}),dict(u,material_bindings='bad')):
            with self.assertRaises(ValueError):consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_material_bindings=True)
        with self.assertRaises(ValueError):consolidate({'assets':[h,u]},[pair(h,u)],preserve_material_bindings=True)

    def test_same_preview_mode_preserves_metadata_and_refuses_distinct_content(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview':'one.png','preview_sha256':'p','preview_status':'old',
           'descriptions':{'en':'old'},'material_bindings':['wood']}
        u=dict(h,id='u',preview_status='new',descriptions={'en':'new'})
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        with self.assertRaises(ValueError):consolidate({'assets':[h,u]},[pair(h,u)])
        out=consolidate({'assets':[h,u]},[pair(h,u)],preserve_same_preview_metadata=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        for changed in (dict(u,preview_sha256='different'),dict(u,source_file='other'),dict(u,material_bindings=['stone'])):
            with self.assertRaises(ValueError):consolidate({'assets':[h,changed]},[pair(h,changed)],preserve_same_preview_metadata=True)
        with self.assertRaises(ValueError):consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_same_preview_metadata=True)

    def test_taxonomy_mode_preserves_legacy_and_rejects_other_differences(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview':'h.png','preview_sha256':'h',
           'taxonomy':{'family':'old'},'material_bindings':['wood']}
        u=dict(h,id='u',preview='u.png',preview_sha256='u',taxonomy={'family':'new'})
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        with self.assertRaises(ValueError):
            consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True)
        out=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_taxonomy=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        for changed in (dict(u,source_file='other'),dict(u,material_bindings=['stone'])):
            with self.assertRaises(ValueError):
                consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_taxonomy=True)

    def test_preview_metadata_mode_retains_original_and_rejects_material_difference(self):
        h={'id':'h','source_file':'same','source_meta_file':'same.meta','source_sha256':'s',
           'source_meta_sha256':'m','preview':'h.png','preview_sha256':'h','preview_status':'old',
           'preview_provenance':{'renderer':'first'},'material_bindings':['wood']}
        u=dict(h,id='u',preview='u.png',preview_sha256='u',preview_status='new',
               preview_provenance={'renderer':'second'})
        def pair(a,b):
            return {'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                    'different_fields':sorted(k for k in set(a)|set(b) if a.get(k)!=b.get(k))}
        with self.assertRaises(ValueError):
            consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True)
        out=consolidate({'assets':[h,u]},[pair(h,u)],distinct_previews=True,preserve_preview_metadata=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        self.assertEqual(out['assets'][0]['preview_provenance'],u['preview_provenance'])
        changed=dict(u,material_bindings=['stone'])
        with self.assertRaises(ValueError):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_preview_metadata=True)
        changed=dict(u,source_file='other')
        with self.assertRaises(ValueError):
            consolidate({'assets':[h,changed]},[pair(h,changed)],distinct_previews=True,preserve_preview_metadata=True)

    def test_distinct_text_is_preserved_only_with_same_source_paths(self):
        h={'id':'p.h','source_file':'shared','source_meta_file':'shared.meta','source_sha256':'s','source_meta_sha256':'m',
           'preview':'h.png','preview_sha256':'h','descriptions':{'en':'HDRP'},'reason':'reason H'}
        u={'id':'p.u','source_file':'shared','source_meta_file':'shared.meta','source_sha256':'s','source_meta_sha256':'m',
           'preview':'u.png','preview_sha256':'u','descriptions':{'en':'URP'},'reason':'reason U'}
        pair={'hdrp_id':'p.h','urp_id':'p.u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
              'different_fields':['id','preview','preview_sha256','descriptions','reason']}
        with self.assertRaisesRegex(ValueError,'differs'):
            consolidate({'assets':[h,u]},[pair],distinct_previews=True)
        out=consolidate({'assets':[h,u]},[pair],distinct_previews=True,preserve_distinct_text=True)
        self.assertEqual(out['assets'][0]['reason'],'reason U')
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)
        u2=dict(u,source_file='other');pair2=dict(pair,different_fields=pair['different_fields']+['source_file'])
        with self.assertRaisesRegex(ValueError,'identical source paths'):
            consolidate({'assets':[h,u2]},[pair2],distinct_previews=True,preserve_distinct_text=True)

    def test_preserves_original_record_and_selects_urp(self):
        h = {'id':'p.h','source_sha256':'s','source_meta_sha256':'m','preview_sha256':'i','license':'x'}
        u = {'id':'p.u','source_sha256':'s','source_meta_sha256':'m','preview_sha256':'i','license':'x'}
        pair = {'hdrp_id':'p.h','urp_id':'p.u','declared_source_sha_equal':True,
                'declared_meta_sha_equal':True,'different_fields':['id']}
        out = consolidate({'assets':[h,u]}, [pair])
        self.assertEqual(len(out['assets']), 1)
        self.assertEqual(out['assets'][0]['id'], 'p.u')
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'], h)
        self.assertNotIn('consolidated_sources', u)

    def test_refuses_unapproved_difference(self):
        pair = {'hdrp_id':'a','urp_id':'b','declared_source_sha_equal':True,
                'declared_meta_sha_equal':True,'different_fields':['taxonomy']}
        with self.assertRaisesRegex(ValueError, 'differs'):
            consolidate({'assets':[{'id':'a'},{'id':'b'}]}, [pair])

    def test_different_previews_preserve_original_detail_and_both_paths(self):
        h={'id':'p.h','source_sha256':'s','source_meta_sha256':'m','preview':'h.png','preview_sha256':'h'}
        u={'id':'p.u','source_sha256':'s','source_meta_sha256':'m','preview':'u.png','preview_sha256':'u'}
        pair={'hdrp_id':'p.h','urp_id':'p.u','declared_source_sha_equal':True,
              'declared_meta_sha_equal':True,'different_fields':['id','preview','preview_sha256']}
        with self.assertRaises(ValueError):consolidate({'assets':[h,u]},[pair])
        out=consolidate({'assets':[h,u]},[pair],distinct_previews=True)
        self.assertEqual(out['assets'][0]['preview'],'u.png')
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail'],h)


if __name__ == '__main__':
    unittest.main()
