import copy
import unittest
import json
import struct

from audit_witch_source_choices import matching_sources, select, triangle_positions


def sample():
    card={'license':'private','source_prefab':{'sha256':'a'*64},
          'source_fbx':[{'guid':'guid','sha256':'b'*64}],
          'measurement':{'vertices':3,'triangles':1,'bounds':[[0,0,0],[1,1,1]]}}
    material={'name':'Bell__PBR_DIAGNOSTIC','doubleSided':True}
    main={'materials':[material]}
    variant={'materials':[{'name':'Bell','doubleSided':False}]}
    return card,copy.deepcopy(card),main,variant


class WitchChoiceTests(unittest.TestCase):
    def test_matching_source_selects_double_sided_main(self):
        a,b,main,variant=sample();matching_sources(a,b)
        self.assertEqual(select(main,variant)['keep_side'],'main')

    def test_mismatch_is_refused(self):
        for change in ('prefab','fbx','bounds','vertices','license'):
            a,b,_,_=sample()
            if change=='prefab':b['source_prefab']['sha256']='c'*64
            if change=='fbx':b['source_fbx'][0]['sha256']='c'*64
            if change=='bounds':b['measurement']['bounds'][1][0]=2
            if change=='vertices':b['measurement']['vertices']=4
            if change=='license':b['license']='other'
            with self.subTest(change=change),self.assertRaises(ValueError):matching_sources(a,b)

    def test_material_or_animation_difference_requires_review(self):
        _,_,main,variant=sample()
        variant['materials'][0]['name']='different'
        with self.assertRaises(ValueError):select(main,variant)

    def test_numeric_material_suffix_is_allowed_without_ignoring_controls(self):
        _,_,main,variant=sample();variant['materials'][0]['name']='Bell.001'
        self.assertEqual(select(main,variant)['keep_side'],'main')
        variant['materials'][0]['alphaMode']='BLEND'
        with self.assertRaises(ValueError):select(main,variant)

    def test_triangle_winding_and_vertex_duplication(self):
        def glb(points,indices):
            binary=b''.join(struct.pack('<3f',*x) for x in points)+bytes(indices)
            binary+=b'\0'*(-len(binary)%4)
            doc={'buffers':[{'byteLength':len(binary)}],
                 'bufferViews':[{'buffer':0,'byteOffset':0,'byteLength':len(points)*12},
                                {'buffer':0,'byteOffset':len(points)*12,'byteLength':len(indices)}],
                 'accessors':[{'bufferView':0,'componentType':5126,'type':'VEC3','count':len(points)},
                              {'bufferView':1,'componentType':5121,'type':'SCALAR','count':len(indices)}],
                 'meshes':[{'primitives':[{'attributes':{'POSITION':0},'indices':1}]}]}
            text=json.dumps(doc).encode();text+=b' '*(-len(text)%4)
            return struct.pack('<III',0x46546c67,2,28+len(text)+len(binary))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(binary),0x004e4942)+binary
        points=[(0,0,0),(1,0,0),(0,1,0)]
        first=triangle_positions(glb(points,[0,1,2]))
        self.assertEqual(first,triangle_positions(glb(points+[points[0]],[3,1,2])))
        self.assertNotEqual(first,triangle_positions(glb(points,[0,2,1])))
        _,_,main,variant=sample();variant['animations']=[{}]
        with self.assertRaises(ValueError):select(main,variant)


if __name__=='__main__':unittest.main()
