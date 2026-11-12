import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import vault_storage as storage
from unity_shared_overlay import UnitySourceResolver

SCRIPT=Path(__file__).resolve().parents[1]/'hivemind/run_gladiator_native_geometry_v380.py'
spec=importlib.util.spec_from_file_location('gladiator_geometry_copy_test',SCRIPT)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)


class GladiatorCopyTests(unittest.TestCase):
    def test_copies_shared_asset_and_guid_meta_without_old_files(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';package=vault/'pack';hdr=package/'source/Assets/HDRP'
            hdr.mkdir(parents=True);(vault/'shared').mkdir();project=root/'project';project.mkdir()
            values={'a.png':b'pixels','a.png.meta':b'guid: 123\n'};files=[];pins={}
            for name,data in values.items():
                (vault/'shared'/name).write_bytes(data);logical='Art/'+name
                files.append({'logical':logical,'vault_path':'shared/'+name,'sha256':storage.sha(data),'bytes':len(data)})
                pins[str(hdr/'Art'/name)]=storage.sha(data)
            doc={'schema':'openrpg.unity-shared-overlay.v1','source_root':'pack/source/Assets/HDRP','files':files}
            manifest=root/'manifest.json';manifest.write_text(json.dumps(doc),encoding='utf8')
            previous=module.resolver
            try:
                module.resolver=UnitySourceResolver(vault,manifest,storage.sha(manifest.read_bytes()))
                audit={'resolved_files':[{'path':str(hdr/'Art/a.png')}],'input_pins':pins}
                used,copies=module.copy_audited_sources(audit,package,project)
                self.assertEqual(len(copies),2);self.assertEqual(used,pins)
                for name,data in values.items():self.assertEqual((project/'Assets/HDRP/Art'/name).read_bytes(),data)
                (vault/'shared/a.png').write_bytes(b'wrong')
                with self.assertRaises(ValueError):module.copy_audited_sources(audit,package,project)
            finally:module.resolver=previous


if __name__=='__main__':unittest.main()
