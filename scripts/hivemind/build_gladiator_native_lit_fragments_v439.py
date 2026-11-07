"""Check the active surface ABI, allowing unused shared POM helper types."""
from pathlib import Path

parent = Path(__file__).resolve().with_name('build_gladiator_native_lit_fragments_v438.py')
adaptation = parent.read_text('utf-8')
adaptation = adaptation.replace('gladiator-native-lit-fragments-v438', 'gladiator-native-lit-fragments-v439')
old = "assert 'depth_offset:' not in native"
new = "assert 'depth_offset:' not in native.split('struct SourceMaterialSurfaceOutputs {',1)[1].split('}',1)[0], 'Active surface unexpectedly exposes depth offset'"
assert adaptation.count(old) == 1
adaptation = adaptation.replace(old, new)
anchor = "exec(compile(code, str(BASE) + '#gladiator-native-lit-v438', 'exec'), globals())"
assert adaptation.count(anchor) == 1
adaptation = adaptation.replace(anchor, "code=code.replace(\"error=str(error)\", \"error=__import__('traceback').format_exc()\")\ncode=code.replace(\"pins[str(BASE)] = sha(BASE)\", \"pins[str(BASE)] = sha(BASE)\\n    pins[str(parent)] = sha(parent)\")\n" + anchor)
exec(compile(adaptation, str(parent) + '#surface-abi-v439', 'exec'), globals())
