"""Compare projection replay against original references and thresholds."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_firepit_source_shadow_context_v1.py')
source=BASE.read_text('utf-8').replace('firepit-source-shadow-context-native-v1','firepit-source-shadow-raster-native-v2')
source=source.replace('firepit-source-shadow-context-comparison-v1','firepit-source-shadow-raster-comparison-v2')
source=source.replace('firepit-source-shadow-context-case-v1','firepit-source-shadow-raster-case-v1')
exec(compile(source,str(BASE),'exec'),globals())
