"""Reopen four interpolated UV/TBN roles without inferring texture gradients."""
from pathlib import Path
BASE_VARYING = Path(__file__).with_name('reopen_firepit_surface_packed_hdr_control_v1.py')
source = BASE_VARYING.read_text('utf-8').replace('firepit-surface-packed-hdr-control','firepit-varying-native')
source = source.replace('firepit-surface-instance-fix-case-v1','firepit-varying-native-case-v1')
source = source.replace("source = source.replace('source_debug_encoding_applied=True,',", "source = source.replace('source_debug_encoding_applied=True,',")
marker="if __name__ == '__main__':"
position=source.index(marker)
source=source[:position]+"source = source.replace('assert len(refs)==5','assert len(refs)==4').replace('mode=i%5','mode=i%4')\nsource = source.replace('hardware_packed_hdr_output_control=True,','hardware_packed_hdr_output_control=True,interpolated_varyings_only=True,implicit_texture_gradient_parity_qualified=False,receiver_uv_geometry_parity_expected=False,')\n"+source[position:]
exec(compile(source,str(BASE_VARYING),'exec'),globals())
