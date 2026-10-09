"""Compare original Unity references against the private source sampling policy."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_firepit_surface_packed_hdr_control_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-packed-hdr-control','firepit-surface-force-anisotropy')
source=source.replace('firepit-surface-force-anisotropy-v1','firepit-surface-force-anisotropy-v2').replace('firepit-surface-force-anisotropy-plan-v1','firepit-surface-force-anisotropy-plan-v2')
source=source.replace('hardware_packed_hdr_output_control=True,','hardware_packed_hdr_output_control=True,source_quality_force_anisotropy_private_policy=True,source_authored_texture_values_unchanged=True,')
source=source.replace("files.extend([raw,meta,", "files.extend([CAP/'SAMPLING-POLICY.json',W/'firepit-source-no-anisotropy-control-v1/QUALITY-STATE.json',raw,meta,")
exec(compile(source,str(BASE),'exec'),globals())
