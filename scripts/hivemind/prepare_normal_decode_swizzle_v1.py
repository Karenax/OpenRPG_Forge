"""Test all components of WGSL normal-profile swizzles without editing pinned code."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
from datetime import datetime, timezone

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'normal-decode-swizzle-candidate-v1'
assert not ROOT.exists()
ROOT.mkdir()
main = R / 'crates/forge_assets/src/source_graph_bundle.rs'
original = main.read_text(encoding='utf-8')
old = '''                let channel = tail
                    .chars()
                    .next()
                    .and_then(|c| "xyzw".find(c))
                    .ok_or(SourceGraphContractError::InvalidShader)?;
                if group > 1 || self.normal_decode_modes[group * 4 + channel].is_none() {
                    return Err(SourceGraphContractError::UnqualifiedNormalDecode);
                }'''
new = '''                // A vector read consumes every selected profile, not just its
                // first component. Reject unknown members and dynamic selectors.
                let member: String = tail
                    .chars()
                    .take_while(|c| c.is_ascii_alphanumeric() || *c == '_')
                    .collect();
                if member.is_empty() || member.len() > 4 {
                    return Err(SourceGraphContractError::InvalidShader);
                }
                if group > 1 {
                    return Err(SourceGraphContractError::UnqualifiedNormalDecode);
                }
                for component in member.chars() {
                    let channel = "xyzw"
                        .find(component)
                        .ok_or(SourceGraphContractError::InvalidShader)?;
                    if self.normal_decode_modes[group * 4 + channel].is_none() {
                        return Err(SourceGraphContractError::UnqualifiedNormalDecode);
                    }
                }'''
assert original.count(old) == 1
candidate = original.replace(old, new)
start = candidate.index('    #[test]\n    fn integrity_admission_refuses_an_unqualified_normal_shader_read()')
end = candidate.index('    #[test]', start + 12)
test = candidate[start:end]
test = test.replace('integrity_admission_refuses_an_unqualified_normal_shader_read', 'integrity_admission_requires_every_swizzled_normal_profile')
test = test.replace('fn decode() -> f32 { return source_graph.normal_decode_modes[1].w; }', 'fn decode() -> vec3<f32> { return source_graph.normal_decode_modes[0].xyz; }')
tail = '''        contract.normal_decode_modes[7] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Ok(()));
        assert_eq!(contract.normal_decode_modes[0], None);'''
replacement = '''        contract.normal_decode_modes[0] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Err(SourceGraphContractError::UnqualifiedNormalDecode));
        contract.normal_decode_modes[1] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Err(SourceGraphContractError::UnqualifiedNormalDecode));
        contract.normal_decode_modes[2] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Ok(()));
        contract.normal_decode_modes[0] = None;
        assert_eq!(contract.validate_integrity(&[bytes]), Err(SourceGraphContractError::UnqualifiedNormalDecode));
        assert_eq!(contract.normal_decode_modes[3], None);'''
assert tail in test
test = test.replace(tail, replacement)
candidate = candidate[:end] + test + candidate[end:]
candidate = candidate.replace('source_graph_bundle/imported_storage.rs', 'imported_storage.candidate.rs').replace('source_graph_bundle/scalar_parameters.rs', 'scalar_parameters.rs')
(ROOT / 'source_graph_bundle.candidate.rs').write_text(candidate, encoding='utf-8')
files = {'imported_storage.candidate.rs': main.parent / 'source_graph_bundle/imported_storage.rs',
         'scalar_parameters.rs': main.parent / 'source_graph_bundle/scalar_parameters.rs',
         'source_material_parameters.rs': main.parent / 'source_material_parameters.rs'}
pins = {str(main): hashlib.sha256(main.read_bytes()).hexdigest()}
for name, path in files.items():
    (ROOT / name).write_bytes(path.read_bytes())
    pins[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
(ROOT / 'PREPARED.v1.json').write_text(json.dumps({'source_pins': pins, 'prepared_utc': datetime.now(timezone.utc).isoformat(), 'shared_sources_modified': False}, indent=2), encoding='utf-8')
helper = (S / 'check_townsmith_texture_set_v1.py').read_text(encoding='utf-8')
helper = helper.replace('townsmith-s-paint-texture-set-check-v1', 'normal-decode-swizzle-check-v1').replace('native-texture-identity-contract-candidate-v3', 'normal-decode-swizzle-candidate-v1')
target = S / 'check_normal_decode_swizzle_v1.py'
assert not target.exists()
target.write_text(helper, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
