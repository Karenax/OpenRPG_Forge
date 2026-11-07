"""Explain every private SDK serialization change by exact reverse comparison."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork'); I=W/'medieval-torch-private-material-audit-v148/RESULT.v1.json'; O=W/'medieval-torch-private-material-classification-v149'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not O.exists();audit=json.loads(I.read_text('utf-8'));rows=[]
    for p,h in audit['input_pins'].items():assert sha(Path(p))==h,p
    for row in audit['material_changes']:
        before=Path(row['before_path']).read_text('utf-8-sig').replace('\r\n','\n');work=Path(row['path']).read_text('utf-8-sig').replace('\r\n','\n')
        added=['  m_Parent: {fileID: 0}\n','  m_ModifiedSerializedProperties: 0\n','  m_LockedProperties: \n','  m_AllowLocking: 1\n']+[f'    - {k}: {v}\n' for k,v in {'_DstBlend2':0,'_ExcludeFromTUAndAA':0,'_MaterialTypeMask':2,'_PerPixelSorting':0}.items()]
        for line in added:
            assert line not in before and work.count(line)==1,line
            work=work.replace(line,'',1)
        replacements=[('  version: 12\n','  version: 13\n'),('  m_Shader: {fileID: -6465566751694194690, guid: fc8e31a3d73f370561906623bcff4912,\n    type: 3}\n','  m_Shader: {fileID: -6465566751694194690, guid: fc8e31a3d73f370561906623bcff4912, type: 3}\n')]+[(f'    - {k}: {a}\n',f'    - {k}: {b}\n') for k,a,b in [('_StencilWriteMaskDepth',8,9),('_StencilWriteMaskGBuffer',14,15),('_StencilWriteMaskMV',40,41)]]
        for a,b in replacements:
            assert before.count(a)==1 and work.count(b)==1,(row['path'],a)
            work=work.replace(b,a,1)
        assert work==before,'Unexplained material change: '+row['path']
        rows.append(dict(path=row['path'],before_sha256=row['before_sha256'],after_sha256=row['after_sha256'],exact_reverse_comparison_passed=True,added_sdk_fields=[x.strip() for x in added],replacements=[dict(before=a.strip(),after=b.strip()) for a,b in replacements],authored_properties_texture_references_and_keywords_unchanged=True))
    O.mkdir();result=dict(status='three_private_sdk_material_migrations_exactly_classified',finished_utc=datetime.now(timezone.utc).isoformat(),materials=rows,source_version_parity_qualified=False,material_rendering_verified=False,full_import=False,audit_sha256=sha(I),classifier_sha256=sha(Path(__file__)))
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({'classified':len(rows),'full_import':False}))
    note='\n## L06 Torche — migrations privées classifiées v149\n\nLes trois matériaux M_Wood_A, M_Metal et M_BasicTextured ont passé une comparaison inverse exacte : version SDK 12→13, champs de sérialisation ajoutés, reformatage de référence shader, quatre propriétés SDK et trois masques de stencil. Aucune autre propriété, référence de texture ou keyword modifié. La fidélité visuelle et la parité de version restent non qualifiées. [Preuve](../medieval-torch-private-material-classification-v149/RESULT.v1.json).\n\n'
    p=W/'SUIVI_LOTS_HIVEMIND/AVANCEMENT_ACTUEL.md';old=p.read_bytes();(O/'AVANCEMENT_ACTUEL.before.md').write_bytes(old);p.write_text(note+old.decode('utf-8-sig'),'utf-8')
if __name__=='__main__':main()
