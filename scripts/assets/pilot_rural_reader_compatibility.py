"""Test a disposable Rural GLB copy on F using native and importer readers."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

import vault_storage as storage


def run(source, expected, parent, native, importer_dir, label='Rural'):
    source = source.resolve(strict=True)
    parent = parent.resolve(strict=True)
    if not parent.is_dir() or source.suffix.lower() != '.glb':
        raise ValueError('invalid pilot source or parent')
    work = Path(tempfile.mkdtemp(prefix='rural-reader-pilot-', dir=parent)).resolve(strict=True)
    if work.parent != parent or not work.name.startswith('rural-reader-pilot-'):
        raise ValueError('invalid pilot location')
    copied = work / 'normalized' / 'pilot.glb'
    try:
        copied.parent.mkdir()
        shutil.copyfile(source, copied)
        if hashlib.sha256(copied.read_bytes()).hexdigest() != expected:
            raise ValueError('pilot copy SHA differs')
        packer = storage.Packer(work)
        try:
            item = packer.prepare(copied, expected_sha=expected)
            if not item:
                raise ValueError('pilot source already encoded')
            packer.publish(item, native)
        finally:
            packer.close()
        native_result = json.loads(subprocess.run([str(native), 'verify', str(work), str(copied),
                                                   str(512*1024*1024)], check=True,
                                                  capture_output=True, text=True).stdout)
        if native_result.get('sha256') != expected or storage.digest(work, copied) != expected:
            raise ValueError('pilot reader SHA differs')
        sys.path.insert(0, str(importer_dir))
        os.environ['OPENRPG_ASSET_VAULT'] = str(work)
        import glb_inspect
        if glb_inspect.digest(copied) != expected:
            raise ValueError('importer reader SHA differs')
        source_pin = hashlib.sha256(source.read_bytes()).hexdigest()
        if source_pin != expected:
            raise ValueError('original changed during pilot')
        # Delete only the newly-created sandbox; never traverse a reparse point.
        for directory, dirs, files in os.walk(work, followlinks=False):
            for name in dirs + files:
                item = Path(directory) / name
                if item.is_symlink() or getattr(item.lstat(), 'st_file_attributes', 0) & 0x400:
                    raise ValueError('reparse point found in pilot; preserve for inspection')
        if work.resolve(strict=True).parent != parent:
            raise ValueError('pilot location changed')
        shutil.rmtree(work)
        return {'state':'complete', 'sha256':expected, 'native_and_python_digest':'matched',
                'generated_files_removed':True, 'vault_source_unchanged':True,
                'scope':'one disposable '+label+' copy on F; no visual or runtime qualification'}
    except BaseException:
        print('Pilot preserved for inspection: ' + str(work), file=sys.stderr)
        raise


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('source','parent','native','importer-dir','output'):
        p.add_argument('--'+name, type=Path, required=True)
    p.add_argument('--sha256',required=True)
    p.add_argument('--label',default='Rural')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.source,a.sha256,a.parent,a.native,a.importer_dir,a.label)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
