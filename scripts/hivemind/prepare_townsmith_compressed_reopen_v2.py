from pathlib import Path
S=Path(__file__).parent
source=(S/'reopen_townsmith_compressed_storage_v1.py').read_text(encoding='utf-8')
source=source.replace('townsmith-s-paint-compressed-storage-v1','townsmith-s-paint-compressed-storage-v2').replace('townsmith-s-paint-compressed-storage-reopen-v1','townsmith-s-paint-compressed-storage-reopen-v2')
p=S/'reopen_townsmith_compressed_storage_v2.py';assert not p.exists();p.write_text(source,encoding='utf-8')
source=(S/'compose_townsmith_paint_native_resources_v1.py').read_text(encoding='utf-8')
source=source.replace('townsmith-s-paint-compressed-storage-v1','townsmith-s-paint-compressed-storage-v2').replace('townsmith-s-paint-compressed-storage-reopen-v1','townsmith-s-paint-compressed-storage-reopen-v2')
p=S/'compose_townsmith_paint_native_resources_v2.py';assert not p.exists();p.write_text(source,encoding='utf-8')
