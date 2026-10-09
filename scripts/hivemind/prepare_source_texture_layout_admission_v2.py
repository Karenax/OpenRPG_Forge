"""Repair the isolated layout probe; retain V1 and all common source files."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
rust = (S / 'candidates/SourceTextureLayoutAdmissionV1.rs').read_text(encoding='utf-8')
for label, entries in [('actual-existing-source-graph-layout', 'entries'),
                       ('proposed-ten-source-textures-layout', 'proposed')]:
    old = 'device.wgpu_device().create_bind_group_layout(&BindGroupLayoutDescriptor{label:Some("' + label + '"),entries:&' + entries + '})'
    new = 'device.create_bind_group_layout(Some("' + label + '"), &' + entries + ')'
    assert rust.count(old) == 1
    rust = rust.replace(old, new)
candidate = S / 'candidates/SourceTextureLayoutAdmissionV2.rs'
assert not candidate.exists()
candidate.write_text(rust, encoding='utf-8')
controller = (S / 'check_source_texture_layout_admission_v1.py').read_text(encoding='utf-8')
controller = controller.replace('source-texture-layout-admission-v1', 'source-texture-layout-admission-v2')
controller = controller.replace('SourceTextureLayoutAdmissionV1.rs', 'SourceTextureLayoutAdmissionV2.rs')
path = S / 'check_source_texture_layout_admission_v2.py'
assert not path.exists()
path.write_text(controller, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(path)], check=True)
