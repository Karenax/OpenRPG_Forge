"""Retain the missing-compute preparation failure and install the trusted probe."""
from pathlib import Path
from datetime import datetime,timezone
import json,subprocess,sys,hashlib
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');old=S/'run_torch_native_materials_v159.py';target=S/'run_torch_native_materials_v160.py'
assert not target.exists()
failure=W/'medieval-torch-native-materials-v159/PREPARE-REFUSAL.v1.json';assert not failure.exists()
failure.write_text(json.dumps(dict(status='missing_trusted_mip_probe_in_private_project_preparation_refused',finished_utc=datetime.now(timezone.utc).isoformat(),unity_started=False,controller_started=False,missing='Assets/Editor/SourceImportedTextureMipLoadV1.compute',superseded_by='medieval-torch-native-materials-v160',source_sha256=hashlib.sha256(old.read_bytes()).hexdigest(),full_import=False),indent=2),'utf-8')
source=old.read_text('utf-8-sig');source=source.replace("O=W/'medieval-torch-native-materials-v159'","O=W/'medieval-torch-native-materials-v160'")
needle="material_helper=S/'candidates/TorchMaterialExportV155.cs';shutil.copyfile(material_helper,O/material_helper.name)";assert source.count(needle)==1
source=source.replace(needle,needle+"\n shutil.copyfile(S/'candidates/SourceImportedTextureMipLoadV1.compute',O/'SourceImportedTextureMipLoadV1.compute')")
needle="PROJECT/'Assets/Editor/SourceImportedTextureMipLoadV1.compute'";assert source.count(needle)==1
source=source.replace(needle,"S/'candidates/SourceImportedTextureMipLoadV1.compute',O/'SourceImportedTextureMipLoadV1.compute'")
needle="['TorchMaterialExportV155.cs','TorchCompressedStorageV159.cs']";assert source.count(needle)==1
source=source.replace(needle,"['TorchMaterialExportV155.cs','TorchCompressedStorageV159.cs','SourceImportedTextureMipLoadV1.compute']")
target.write_text(source,'utf-8');subprocess.run([sys.executable,'-B',str(target)],check=True)
