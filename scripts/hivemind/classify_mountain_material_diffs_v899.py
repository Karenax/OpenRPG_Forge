"""Explain entire serialized diffs with explicit known changes; keep unknowns."""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter
import difflib
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
O = W / 'mountain-material-serialization-review-v899'

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def normalize_shader_flow(text):
    return re.sub(r'(?m)^  m_Shader: \{([^}]+)\}',
                  lambda match: '  m_Shader: {'+' '.join(match[1].split())+'}', text)

def main():
    proof_path = W / 'mountain-exact-capture-review-v897/RESULT.v1.json'
    proof = json.loads(proof_path.read_text('utf-8-sig'))
    O.mkdir(exist_ok=False)
    rows, categories = [], Counter()
    for index, material in enumerate(proof['source_material_changes']):
        assert sha(material['source']) == material['original_sha256']
        assert sha(material['target']) == material['migrated_sha256']
        original = Path(material['source']).read_text('utf-8-sig')
        restored = Path(material['target']).read_text('utf-8-sig')
        applied = []
        known = [('  m_Parent: {fileID: 0}\n',''),
                 ('  m_ModifiedSerializedProperties: 0\n',''),
                 ('  m_LockedProperties: \n',''),('  m_AllowLocking: 1\n','')]
        added_defaults = {'_DstBlend2':0, '_ExcludeFromTUAndAA':0,
                          '_MaterialTypeMask':2, '_PerPixelSorting':0, '_UseAOMap':1}
        for name, value in added_defaults.items():
            known.append((f'    - {name}: {value}\n',''))
        for before, after in known:
            if before not in original and restored.count(before) == 1:
                restored = restored.replace(before, after)
                applied.append(dict(kind='added_serialized_field', migrated=before.strip()))
        for name, before, after in [('_StencilWriteMaskDepth',8,9),
                                     ('_StencilWriteMaskGBuffer',14,15),
                                     ('_StencilWriteMaskMV',40,41)]:
            old, new = f'    - {name}: {before}\n', f'    - {name}: {after}\n'
            if original.count(old) == 1 and restored.count(new) == 1:
                restored = restored.replace(new,old)
                applied.append(dict(kind='stencil_mask_migration', property=name, before=before, after=after))
        if original.count('  version: 12\n') == 1 and restored.count('  version: 13\n') == 1:
            restored = restored.replace('  version: 13\n','  version: 12\n')
            applied.append(dict(kind='hdrp_serialization_version', before=12, after=13))
        restored, normalized_original = normalize_shader_flow(restored), normalize_shader_flow(original)
        explained = restored == normalized_original
        remaining = ''.join(difflib.unified_diff(normalized_original.splitlines(True), restored.splitlines(True)))
        residual = O / f'{index:03d}-remaining.diff'
        residual.write_text(remaining, encoding='utf-8')
        for change in applied:
            categories[change['kind']] += 1
        rows.append(dict(source=material['source'], target=material['target'],
                         original_sha256=material['original_sha256'], migrated_sha256=material['migrated_sha256'],
                         entire_serialized_diff_explained=explained, known_changes=applied,
                         residual_diff=str(residual), residual_sha256=sha(residual),
                         native_defaults_and_stencil_equivalence_verified=False))
    result = dict(status='mountain_complete_serialized_diffs_classified_native_semantics_pending',
                  utc=datetime.now(timezone.utc).isoformat(), materials=len(rows),
                  explained_materials=sum(row['entire_serialized_diff_explained'] for row in rows),
                  unexplained_materials=sum(not row['entire_serialized_diff_explained'] for row in rows),
                  change_categories=dict(categories), rows=rows,
                  input_pins={str(proof_path):sha(proof_path), str(Path(__file__)):sha(Path(__file__))},
                  sdk_migration_render_equivalence=False, images_admitted=0, full_import_increment=0)
    (O / 'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({key:result[key] for key in ('status','materials','explained_materials','unexplained_materials','change_categories')}))

if __name__ == '__main__':
    main()
