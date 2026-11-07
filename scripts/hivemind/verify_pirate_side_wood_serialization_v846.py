"""Explain the complete Wood01 private-import diff without claiming old SDK parity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W = Path('F:/OpenRPG_ImportWork')
O = W/'pirate-side-wood-serialization-v846'
guid = 'abc00000000002303923814896909187'

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

queue_path = W/'queue/pirate-island/prefabs.json'
queue = json.loads(queue_path.read_text('utf-8-sig'))
material = queue['materials'][guid]
original_path = Path(queue['source_root'])/material['source']
private_path = W/'unity-library-source-thumbnail-pirate-bench-v2/project'/material['source']
assert sha(original_path) == material['sha256']
original = original_path.read_text('utf-8-sig')
restored = private_path.read_text('utf-8-sig')
changes = [
    ('  version: 13\n', '  version: 12\n'),
    ('  m_Shader: {fileID: -6465566751694194690, guid: 1caa1cbf1210e6f46b59e4604b348b46, type: 3}\n',
     '  m_Shader: {fileID: -6465566751694194690, guid: 1caa1cbf1210e6f46b59e4604b348b46,\n    type: 3}\n'),
    ('  m_Parent: {fileID: 0}\n', ''),
    ('  m_ModifiedSerializedProperties: 0\n', ''),
    ('  m_LockedProperties: \n', ''),
    ('  m_AllowLocking: 1\n', ''),
]
added_properties = {'_DstBlend2': 0, '_ExcludeFromTUAndAA': 0,
                    '_MaterialTypeMask': 2, '_PerPixelSorting': 0, '_UseAOMap': 1}
for name, value in added_properties.items():
    changes.append((f'    - {name}: {value}\n', ''))
stencil = {'_StencilWriteMaskDepth': (8, 9),
           '_StencilWriteMaskGBuffer': (14, 15), '_StencilWriteMaskMV': (40, 41)}
for name, (before, after) in stencil.items():
    changes.append((f'    - {name}: {after}\n', f'    - {name}: {before}\n'))
for before, after in changes:
    assert restored.count(before) == 1, before
    restored = restored.replace(before, after)
assert restored == original, 'Unexplained serialized material change'
assert not O.exists()
pins = {str(p): sha(p) for p in [Path(__file__), queue_path, original_path, private_path]}
result = dict(status='wood01_entire_private_material_diff_explained', material_guid=guid,
              original_source_sha256=material['sha256'], input_pins=pins,
              added_properties_requiring_native_default_check=added_properties,
              stencil_changes=stencil, textures_keywords_and_other_serialized_values_unchanged=True,
              stencil_references_unchanged=True, hdrp_serialization_version_before=12,
              hdrp_serialization_version_after=13, native_defaults_verified=False,
              render_equivalence_qualified=False, original_sdk_pipeline_equivalence=False,
              full_import=False, finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
