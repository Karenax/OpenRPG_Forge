"""Test shared production cube admission plus exact captured source storage."""
from pathlib import Path
BASE=Path(__file__).with_name('check_source_diffuse_probe_packet_v1.py')
source=BASE.read_text('utf-8').replace('source-diffuse-probe-packet-check-v1','source-sky-cube-storage-check-v1')
source=source.replace('source_graph_diffuse_probe.rs','source_graph_sky_cube.rs')
source=source.replace("ENGINE/'shaders/source_diffuse_probe.wgsl',",'')
source=source.replace('expected_tests=2','expected_tests=4').replace('tests_verified=2 if passed','tests_verified=4 if passed').replace('2 passed; 0 failed','4 passed; 0 failed')
source=source.replace('actual_diffuse_probe_packet_tests_running','actual_sky_cube_storage_tests_running').replace('actual_diffuse_probe_uniform_encoding_and_refusals_verified','actual_sky_cube_storage_admission_and_refusals_verified').replace('diffuse_probe_packet_test_refusal_retained','sky_cube_storage_test_refusal_retained').replace('diffuse_probe_packet_tests_refused','sky_cube_storage_tests_refused')
needle="    (ROOT/'Cargo.lock.before').write_bytes"
extra='''
    with (ROOT/'Cargo.toml').open('a',encoding='utf-8') as f:f.write('forge_core={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core"}\\n')
    packet_path=W/'source-sky-cube-packet-v1/PACKET.v1.json';packet=json.loads(packet_path.read_text('utf-8'))
    raw=Path(packet['path']);assert sha(raw)==packet['sha256']
    with (ROOT/'src/main.rs').open('a',encoding='utf-8') as f:
        f.write('\\n#[test] fn captured_source_cube_is_admitted_without_reencoding() {\\n')
        f.write('let bytes=include_bytes!("'+str(raw).replace('\\\\','/')+'");\\n')
        f.write('let digest=forge_core::ContentHash::parse("'+packet['sha256']+'").unwrap();\\n')
        f.write('let cube=probe::SourceGraphSkyCube::from_mip_major_rgba16f_le(bytes,digest,256,1,9).unwrap();\\n')
        f.write('let mut images=bevy::prelude::Assets::<bevy::prelude::Image>::default();let _binding=cube.install_unqualified(&mut images);assert_eq!(images.len(),1);\\n')
        f.write('let image=images.iter().next().unwrap().1;assert_eq!(image.data.as_deref(),Some(bytes.as_slice()));assert_eq!(image.texture_descriptor.mip_level_count,9);assert_eq!(image.texture_descriptor.size.depth_or_array_layers,6);assert_eq!(image.data_order,bevy::render::render_resource::TextureDataOrder::MipMajor); }\\n')
'''
assert source.count(needle)==1
source=source.replace(needle,extra+'\n'+needle)
source=source.replace("ROOT/'Cargo.lock.before']","ROOT/'Cargo.lock.before',raw,packet_path]")
exec(compile(source,str(BASE),'exec'),globals())
