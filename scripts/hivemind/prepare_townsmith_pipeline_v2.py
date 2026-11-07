"""Retain v1 failure and use the installed wgpu 29 error-scope guard API."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
base = S / 'candidates/SourceTownsmithPipelineAdmissionV1.rs'
candidate = S / 'candidates/SourceTownsmithPipelineAdmissionV2.rs'
assert not candidate.exists()
source = base.read_text(encoding='utf-8')
assert source.count('device.push_error_scope(wgpu::ErrorFilter::Validation);') == 1
source = source.replace('device.push_error_scope(wgpu::ErrorFilter::Validation);', 'let error_scope=device.push_error_scope(wgpu::ErrorFilter::Validation);')
assert source.count('device.pop_error_scope()') == 1
source = source.replace('device.pop_error_scope()', 'error_scope.pop()')
candidate.write_text(source, encoding='utf-8')
controller = S / 'check_townsmith_native_pipeline_v2.py'
assert not controller.exists()
source = (S / 'check_townsmith_native_pipeline_v1.py').read_text(encoding='utf-8')
source = source.replace('townsmith-s-paint-native-pipeline-v1', 'townsmith-s-paint-native-pipeline-v2').replace('SourceTownsmithPipelineAdmissionV1.rs', 'SourceTownsmithPipelineAdmissionV2.rs')
compile(source, str(controller), 'exec')
controller.write_text(source, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(controller)], check=True)
