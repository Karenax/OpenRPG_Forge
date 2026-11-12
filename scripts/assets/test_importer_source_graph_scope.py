"""Regression for the source-graph verifier subprocess path."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import tempfile
import types
import unittest
from unittest.mock import patch


class SourceGraphScopeTest(unittest.TestCase):
    def test_source_graph_invokes_qualified_verifier(self):
        inspector=Path(os.environ.get('OPENRPG_IMPORTER_SCRIPTS',str(Path.home()/'.codex/skills/openrpg-import-assets/scripts')))/'glb_inspect.py'
        spec=importlib.util.spec_from_file_location('tested_glb_inspector',inspector)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory(prefix='source-graph-regression-') as folder:
            root=Path(folder); temp=root/'.build/tmp';temp.mkdir(parents=True)
            verifier=root/'.build/cargo-target/debug/examples/verify_source_graph_carrier.exe'
            verifier.parent.mkdir(parents=True);verifier.write_bytes(b'pinned verifier')
            binary=struct.pack('<9f',0,0,0,1,0,0,0,1,0)
            doc={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(binary)}],
                 'bufferViews':[{'buffer':0,'byteLength':len(binary)}],
                 'accessors':[{'bufferView':0,'componentType':5126,'count':3,'type':'VEC3'}],
                 'meshes':[{'primitives':[{'attributes':{'POSITION':0}}]}],
                 'nodes':[{'mesh':0}],'scenes':[{'nodes':[0]}],'scene':0,
                 'extensionsUsed':['OPENRPG_source_graph']}
            text=json.dumps(doc).encode();text+=b' '*((-len(text))%4)
            raw=struct.pack('<III',0x46546c67,2,28+len(text)+len(binary))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(binary),0x004e4942)+binary
            model=root/'model.glb';model.write_bytes(raw)
            def fake_run(args,**kwargs):
                Path(args[3]).write_text(json.dumps({'schema':'forge.source-graph.carrier-reopen',
                    'carrier_sha256':hashlib.sha256(raw).hexdigest(),'geometry':[{'triangles':1}],'materials':[]}),encoding='utf-8')
                return types.SimpleNamespace(returncode=0,stderr='')
            with patch.dict(os.environ,{'OPENRPG_FORGE_ROOT':str(root)}), \
                 patch.object(module,'SOURCE_GRAPH_VERIFIER_SHA256',hashlib.sha256(verifier.read_bytes()).hexdigest()), \
                 patch.object(module.subprocess,'run',side_effect=fake_run) as run:
                result=module.inspect(model)
            self.assertTrue(result['source_graph']['verified'])
            run.assert_called_once()


if __name__=='__main__':unittest.main()
