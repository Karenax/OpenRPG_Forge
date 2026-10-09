"""Stage RGB8 with GPU uploads reading the prepared RGBA bytes, never native RGB bytes."""
from pathlib import Path
import hashlib
import json

HERE = Path(__file__).resolve().parent
BASE = HERE / 'prepare_delta_rgb8_transport_candidate_v1.py'
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-rgb8-native-transport-candidate-v3'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    prior = W / 'delta-rgb8-native-transport-candidate-v2'
    preserved = {str(p): sha(p) for p in prior.rglob('*') if p.is_file()}
    source = BASE.read_text('utf-8-sig').replace('delta-rgb8-native-transport-candidate-v1',
                                               'delta-rgb8-native-transport-candidate-v3')
    source = source.replace('serde={version="=1.0.228"', 'serde={version="=1.0.229"')
    needle = "    (ROOT / 'src/main.rs').write_text(main, encoding='utf-8')"
    assert source.count(needle) == 1
    extra = r'''
    main = replace(main, '        let image = prepared.image();', """        let image = prepared.image();
        let gpu_data = image.data.as_ref().unwrap();
        assert_eq!(gpu_data.len(), data.len()/3*4);
        for (native_pixel, gpu_pixel) in data.chunks_exact(3).zip(gpu_data.chunks_exact(4)) {
            assert_eq!(&gpu_pixel[..3], native_pixel);
            assert_eq!(gpu_pixel[3], 255);
        }
        let mut altered = data.clone(); altered[0] ^= 1;
        assert!(descriptor.validate(&[&altered, &capture]).is_err());
        assert!(prepare_source_graph_imported_texture(&altered, &metadata).is_err());
        assert!(descriptor.validate(&[&data[..data.len()-1], &capture]).is_err());
        assert!(prepare_source_graph_imported_texture(&data[..data.len()-1], &metadata).is_err());
        let mut bad_mip = descriptor.clone(); bad_mip.mips[0].offset = 1;
        assert!(bad_mip.validate(&views).is_err());
        let mut false_capture: serde_json::Value = serde_json::from_slice(&capture).unwrap();
        false_capture["cases"][descriptor.capture_case_index as usize]["graphics_format"] = serde_json::json!("R8G8B8A8_UNorm");
        let changed_capture = serde_json::to_vec(&false_capture).unwrap();
        let mut false_descriptor = descriptor.clone(); false_descriptor.capture.sha256 = bytes_sha256(&changed_capture);
        assert!(false_descriptor.validate(&[&data, &changed_capture]).is_err());""")
    main = replace(main, '&data[mip.offset..mip.offset + mip.bytes],',
        '&gpu_data[mip.offset/3*4..(mip.offset + mip.bytes)/3*4],')
'''
    source = source.replace(needle, extra + '\n' + needle)
    namespace = {'__name__': 'candidate_v3', '__file__': str(Path(__file__).resolve())}
    exec(compile(source, str(BASE), 'exec'), namespace)
    namespace['main']()
    assert all(sha(p) == digest for p, digest in preserved.items())
    with (ROOT / 'PREPARATION-REVIEW.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(prior_candidate_preserved=preserved, prior_probe_executed=False,
                       issue='v2 upload sliced original RGB3 bytes using GPU RGBA4 row sizes',
                       fix='upload actual engine-prepared RGBA8 bytes; prove every RGB byte and alpha',
                       native_bytes_and_capture_preserved=True, corruption_controls_per_texture=6,
                       original_script_sha256=sha(BASE), renderer_installed=False), stream, indent=2)


if __name__ == '__main__':
    main()
