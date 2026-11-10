"""Describe GLB JSON differences in matching-component pairs, with SHA guards."""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path
import vault_storage


def h(data):
    return hashlib.sha256(data).hexdigest()


def load(vault, path, expected):
    file = Path(path).resolve(strict=True)
    if not file.is_relative_to(vault):
        raise ValueError('path outside Vault')
    raw = vault_storage.read_bytes(vault, file, 512 * 1024 * 1024)
    if h(raw) != expected or raw[:4] != b'glTF':
        raise ValueError('SHA or GLB magic mismatch')
    length, kind = struct.unpack_from('<II', raw, 12)
    if kind != 0x4e4f534a:
        raise ValueError('missing JSON')
    doc = vault_storage.decode_json(raw[20:20+length])
    return doc, h(memoryview(raw)[20+length:])


def diffs(a, b, path='$'):
    if type(a) is not type(b):
        yield path + ' (type)'
    elif isinstance(a, dict):
        for key in sorted(a.keys() | b.keys()):
            name = path + '/' + key
            if key not in a or key not in b:
                yield name + ' (missing)'
            else:
                yield from diffs(a[key], b[key], name)
    elif isinstance(a, list):
        if len(a) != len(b):
            yield path + ' (length)'
        for i, (x, y) in enumerate(zip(a, b)):
            yield from diffs(x, y, path + '/' + str(i))
    elif a != b:
        yield path


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--vault',type=Path,required=True)
    p.add_argument('--comparison',type=Path,required=True)
    p.add_argument('--registry-sha',required=True)
    p.add_argument('--output',type=Path,required=True)
    args=p.parse_args()
    vault=args.vault.resolve(strict=True)
    registry=vault/'metadata/asset-import-registry.v1.json'
    if h(registry.read_bytes())!=args.registry_sha:
        raise ValueError('registry changed')
    with args.comparison.open(encoding='utf-8-sig',newline='') as f:
        rows=[x for x in csv.DictReader(f) if all(x[k]=='True' for k in ('mesh_payloads_exact','transforms_exact','materials_exact','images_exact'))]
    with args.output.open('x',encoding='utf-8',newline='\n') as out:
        for i,row in enumerate(rows,1):
            record={'id':row['id'],'main_path':row['main_path'],'variant_path':row['variant_path'],'deletable_verified':False}
            try:
                a,ab=load(vault,row['main_path'],row['main_sha'])
                b,bb=load(vault,row['variant_path'],row['variant_sha'])
                paths=list(diffs(a,b))
                record.update(status='compared',binary_chunk_exact=ab==bb,json_exact=not paths,difference_count=len(paths),json_difference_paths=paths[:500],difference_paths_truncated=len(paths)>500)
            except (ValueError,OSError,KeyError,struct.error) as e:
                record.update(status='unverified',reason=str(e))
            out.write(json.dumps(record,ensure_ascii=False)+'\n');out.flush()
            print(f'{i}/{len(rows)} {record["status"]}',flush=True)
    if h(registry.read_bytes())!=args.registry_sha:
        raise ValueError('registry changed during audit')


if __name__=='__main__':
    main()
