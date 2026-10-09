"""Reopen the common scene mount against unchanged Unity surface references."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_firepit_surface_packed_hdr_control_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-packed-hdr-control','firepit-surface-common-sampling')
source=source.replace('hardware_packed_hdr_output_control=True,',
    'hardware_packed_hdr_output_control=True,source_sampling_common_mount=True,')
source=source.replace("files.extend([raw,meta,", "files.extend([CAP/'SAMPLING-POLICY.json',CAP/'SAMPLING-REFUSALS.json',raw,meta,")
exec(compile(source,str(BASE),'exec'),globals())
