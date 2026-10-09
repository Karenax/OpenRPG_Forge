"""Compare code after an explicit, narrowly defined licence-header normalization."""
from pathlib import Path
import datetime
import hashlib
import json

ROOT=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v4')
SOURCE=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v2/source/builtin/CGIncludes')
HEADER='// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)\n'
def sha(p):
    with Path(p).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    target=ROOT/'SOURCE-CODE-COMPARISON.v1.json';assert not target.exists()
    receipt=ROOT/'RESULT.v1.json';prior=json.loads(receipt.read_text('utf-8-sig'))
    pins={str(receipt):sha(receipt),str(Path(__file__).resolve()):sha(__file__)};rows=[]
    for row in prior['installed_include_differences']:
        original=SOURCE/row['file'];installed=Path(row['installed_copy'])
        assert sha(original)==row['official_archive_sha256'] and sha(installed)==row['installed_sha256']
        a=original.read_text('utf-8-sig');b=installed.read_text('utf-8-sig')
        assert a.startswith(HEADER), 'Unexpected leading source header'
        normalized_a=a[len(HEADER):].lstrip('\n');normalized_b=b.lstrip('\n')
        assert normalized_a==normalized_b,'Code differs after explicit header normalization: '+row['file']
        pins[str(original)]=sha(original);pins[str(installed)]=sha(installed)
        rows.append(dict(file=row['file'],normalized_code_sha256=hashlib.sha256(normalized_a.encode('utf-8')).hexdigest(),
            raw_original_sha256=sha(original),raw_installed_sha256=sha(installed),code_equal_after_normalization=True))
    assert len(rows)==55 and all(sha(p)==d for p,d in pins.items())
    result=dict(checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        input_pins=pins,include_code_bodies_verified=55,rows=rows,
        normalization=['UTF-8 BOM handling','CRLF to LF text decoding','exact leading Unity MIT comment','leading blank lines'],
        original_headers_and_licences_retained=True,original_and_installed_files_modified=False,
        raw_byte_equality_claimed=False,compiled_builtin_shader_equivalence_qualified=False,
        gpu_rendered=False,full_import=False)
    target.write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(include_code_bodies_verified=55,receipt=str(target),full_import=False)))
if __name__=='__main__':main()
