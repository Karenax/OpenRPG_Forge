"""Run production BRDF storage refusals and actual captured-table admission."""
from pathlib import Path
BASE = Path(__file__).with_name('check_source_diffuse_probe_packet_v1.py')
source = BASE.read_text('utf-8').replace('source-diffuse-probe-packet-check-v1', 'source-brdf-storage-check-v1')
source = source.replace('source_graph_diffuse_probe.rs', 'source_graph_preintegrated_brdf.rs')
source = source.replace('shaders/source_diffuse_probe.wgsl', 'shaders/source_preintegrated_brdf.wgsl')
source = source.replace('expected_tests=2', 'expected_tests=4').replace('tests_verified=2 if passed', 'tests_verified=4 if passed')
source = source.replace('2 passed; 0 failed', '4 passed; 0 failed')
source = source.replace('actual_diffuse_probe_packet_tests_running', 'actual_brdf_storage_tests_running')
source = source.replace('actual_diffuse_probe_uniform_encoding_and_refusals_verified', 'actual_brdf_storage_admission_and_refusals_verified')
source = source.replace('diffuse_probe_packet_test_refusal_retained', 'brdf_storage_test_refusal_retained')
source = source.replace('diffuse_probe_packet_tests_refused', 'brdf_storage_tests_refused')
needle = "(ROOT/'Cargo.lock.before').write_bytes"
extra = '''
    cargo=ROOT/'Cargo.toml'
    with cargo.open('a',encoding='utf-8') as f:
        f.write('forge_core={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core"}\\n')
    raw=W/'firepit-source-fgd-reference-v1/source-fgd.rgba32f'
    protocol=json.loads((W/'source-brdf-lookup-case-v1/CASE.v1.json').read_text('utf-8'))
    assert sha(raw)==protocol['inputs']['sha256']
    main=ROOT/'src/main.rs'
    with main.open('a',encoding='utf-8') as f:
        f.write('\\n#[test] fn captured_source_table_is_admitted_without_reencoding() {\\n')
        f.write('let bytes=include_bytes!("'+str(raw).replace('\\\\','/')+'");\\n')
        f.write('let digest=forge_core::ContentHash::parse("'+sha(raw)+'").unwrap();\\n')
        f.write('let table=probe::SourceGraphPreintegratedBrdf::from_rgba32f_le(bytes,digest,64,64).unwrap();\\n')
        f.write('let mut images=bevy::prelude::Assets::<bevy::prelude::Image>::default();\\n')
        f.write('let _binding=table.install_unqualified(&mut images);assert_eq!(images.len(),1);\\n')
        f.write('let image=images.iter().next().unwrap().1;assert_eq!(image.data.as_deref(),Some(bytes.as_slice()));}\\n')
    '''
assert source.count(needle) == 1
source = source.replace(needle, extra + needle)
source = source.replace("ROOT/'Cargo.lock.before']", "ROOT/'Cargo.lock.before',raw,W/'source-brdf-lookup-case-v1/CASE.v1.json']")
exec(compile(source, str(BASE), 'exec'), globals())
