"""Reopen the new torch source and Pirate.06 using unchanged product reader logic."""
from pathlib import Path
import hashlib
import json

HERE = Path(__file__).resolve()
W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
ROOT = W / 'torch-and-pirate-current-native-reader-v146'

def digest(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    pointer = V / 'metadata/asset-source-support-registry.v1.json'
    ptr = json.loads(pointer.read_text('utf-8'))
    catalog_path = V / ptr['path']
    assert digest(catalog_path) == ptr['sha256']
    catalog = json.loads(catalog_path.read_text('utf-8'))
    torch = json.loads((W / 'medieval-torch-source-support-candidate-v142/CANDIDATES.v1.json').read_text('utf-8'))['assets'][0]
    matches = [a for a in catalog['assets'] if a['id'] == torch['id']]
    assert len(matches) == 1 and matches[0] == torch
    for dep in torch['source_dependencies']:
        p = V / dep['vault_path']
        assert p.stat().st_size == dep['bytes'] and digest(p) == dep['sha256']
    old = HERE.with_name('run_pirate_thumbnail_batch_0105_0112_native_v1.py')
    source = old.read_text('utf-8')
    source = source.replace("ROOT=W/'pirate-source-thumbnail-batch-0105-0112-native-reader-v1'", "ROOT=W/'torch-and-pirate-current-native-reader-v146'")
    source = source.replace('26735', '26736')
    needle = ' assert_eq!(rows.len(),input["cards"].as_array().unwrap().len());'
    extra = '''
 let torch_id="hivemind.medieval-kingdom.unity-guid-abc00000000004388197602292238737";
 let LibraryResponse::Page{page}=library.execute(LibraryRequest::Search{query:LibraryQuery{text:"torche".into(),pack:"hivemind.medieval-kingdom".into(),availability:"source_support".into(),limit:48,..Default::default()}}).unwrap()else{panic!("Torch search required")};
 assert_eq!(page.total_assets,26736);
 let found=page.assets.iter().find(|c|c.id==torch_id).expect("New torch searchable");
 assert!(!found.has_thumbnail); assert!(!found.runtime_qualified); assert_eq!(found.availability,"source_support");
 let LibraryResponse::Details{detail}=library.execute(LibraryRequest::Details{id:torch_id.into(),revision:page.revision.clone()}).unwrap()else{panic!("Torch details required")};
 assert_eq!(detail.license,"LicenseRef-Unity-Asset-Store-EULA"); assert!(detail.model_path.is_none()); assert!(detail.preview_path.is_none());
 let path=detail.source_path.as_ref().expect("Torch source preserved");
 assert_eq!(format!("{:x}",Sha256::digest(std::fs::read(path).unwrap())),"8e94c9b383f2aa2d720bd06ed71092fb02f45b4c6eb9fd103294a5fcc531cbb0");
 assert!(library.execute(LibraryRequest::Model{id:torch_id.into(),revision:page.revision.clone()}).is_err());
 assert!(library.execute(LibraryRequest::Thumbnail{id:torch_id.into(),revision:page.revision.clone()}).is_err());
'''
    assert source.count(needle) == 1
    source = source.replace(needle, extra + needle)
    source = source.replace('"indexed_cards":26736', '"torch_source_verified":true,"torch_preview_pending":true,"indexed_cards":26736')
    scope = {'__name__': 'v146_isolated_reader', '__file__': str(HERE)}
    exec(compile(source, str(HERE), 'exec'), scope)
    scope['main']()
    result = json.loads((ROOT / 'RESULT.v1.json').read_text('utf-8'))
    assert result['actual_exit_code'] == 0 and 'error' not in result
    assert digest(catalog_path) == ptr['sha256'] and json.loads(pointer.read_text('utf-8')) == ptr
    result.update(torch_source_verified=True, torch_dependency_files_rehashed=len(torch['source_dependencies']), source_catalog_sha256=ptr['sha256'], source_cards=len(catalog['assets']), wrapper_sha256=digest(HERE), reused_probe_sha256=digest(old))
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')

if __name__ == '__main__':
    main()
