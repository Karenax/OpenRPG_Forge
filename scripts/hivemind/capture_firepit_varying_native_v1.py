"""Render original interpolated UV and TBN diagnostics in measured HDR format."""
from pathlib import Path
BASE_VARYING = Path(__file__).with_name('capture_firepit_surface_packed_hdr_control_v1.py')
source = BASE_VARYING.read_text('utf-8').replace('firepit-surface-packed-hdr-control', 'firepit-varying-native')
source = source.replace('firepit-surface-instance-fix-case-v1', 'firepit-varying-native-case-v1')
source = source.replace('unchanged surface shader and geometry', 'diagnostic varying shader and unchanged geometry')
exec(compile(source,str(BASE_VARYING),'exec'),globals())
