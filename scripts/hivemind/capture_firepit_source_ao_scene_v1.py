from pathlib import Path
BASE_AO=Path(__file__).with_name('capture_firepit_ssao_native_v1.py')
source=BASE_AO.read_text('utf-8').replace('firepit-ssao-native-v1','firepit-source-ao-scene-v1').replace('firepit-ssao-native-case-v1','firepit-source-ao-scene-case-v1')
start=source.index("    rust=rust.replace(");end=source.index('    ROOT.mkdir()',start)
source=source[:start]+'''    marker='"shared_sky_materials":5,';assert rust.count(marker)==1
    rust=rust.replace(marker,marker+'"source_ao_intensity_applied":true,"source_ao_intensity":0.5,')
'''+source[end:]
exec(compile(source,str(BASE_AO),'exec'),globals())
