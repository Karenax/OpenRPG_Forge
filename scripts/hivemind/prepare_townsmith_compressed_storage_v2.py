"""Repair the readability setter in the initialized isolated project; retain V1."""
from pathlib import Path
import sys,subprocess
S=Path(__file__).parent
source=(S/'candidates/SourceTownsmithCompressedStorageV1.cs').read_text(encoding='utf-8')
source=source.replace('SourceTownsmithCompressedStorageV1','SourceTownsmithCompressedStorageV2')
old='if(texture==null||importer==null||texture.isReadable||importer.isReadable)throw new Exception("Exact unreadable imported source required");'
assert source.count(old)==1
source=source.replace(old,'''if(texture==null||importer==null)throw new Exception("Exact imported source required");
    // V1's JsonOverwrite did not apply this native importer property.
    // Restore it explicitly before taking a fresh baseline in the same isolated copy.
    if(texture.isReadable||importer.isReadable) {
     importer.isReadable=false;importer.SaveAndReimport();texture=AssetDatabase.LoadAssetAtPath<Texture2D>(row.asset_path);
    }
    if(texture.isReadable||importer.isReadable)throw new Exception("Unreadable baseline repair failed");''')
old='EditorJsonUtility.FromJsonOverwrite(originalJson,importer);importer.SaveAndReimport();'
assert source.count(old)==1
source=source.replace(old,'EditorJsonUtility.FromJsonOverwrite(originalJson,importer);importer.isReadable=false;importer.SaveAndReimport();')
p=S/'candidates/SourceTownsmithCompressedStorageV2.cs';assert not p.exists();p.write_text(source,encoding='utf-8')
controller=(S/'queue_townsmith_compressed_storage_v1.py').read_text(encoding='utf-8')
controller=controller.replace("ROOT=W/'townsmith-s-paint-compressed-storage-v1';PROJECT=ROOT/'project'", "ROOT=W/'townsmith-s-paint-compressed-storage-v2';PROJECT=W/'townsmith-s-paint-compressed-storage-v1/project'")
controller=controller.replace('SourceTownsmithCompressedStorageV1','SourceTownsmithCompressedStorageV2')
start=controller.index(' ROOT.mkdir();PROJECT.mkdir();copies={}')
end=controller.index(" for name in ['SourceTownsmithCompressedStorageV2.cs'",start)
controller=controller[:start]+''' ROOT.mkdir();assert PROJECT.is_dir();copies={}
 for row in selected:
  target=PROJECT/row['source_path'];assert sha(target)==row['source_sha256'];copies[str(target)]=sha(target)
 for folder in ['ProjectSettings','Packages']:
  for p in (PROJECT/folder).iterdir():
   if p.is_file():pins[str(p)]=sha(p)
 prior=W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json';state=read(prior)
 assert state['actual_exit_code']==0 and state['originals_unchanged']
 pins[str(prior)]=sha(prior)
''' +controller[end:]
p=S/'queue_townsmith_compressed_storage_v2.py';assert not p.exists();p.write_text(controller,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(p)],check=True)
