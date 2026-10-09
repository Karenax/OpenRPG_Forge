"""Independent 128x128 verification, preserving the 32-pixel minimum."""
from pathlib import Path

if __name__ == '__main__':
    text = Path(__file__).with_name('reopen_null_mask_surface_v8.py').read_text('utf-8-sig')
    text = text.replace('unity-source-null-mask-surface-v8', 'unity-source-null-mask-surface-v10')
    text = text.replace('detail-mask-surface-independent-reopen-v1', 'detail-mask-surface-independent-reopen-v3')
    text = text.replace('len(raw) == 8192', 'len(raw) == 131072').replace('pixels_reopened=36864', 'pixels_reopened=589824')
    text = text.replace("assert changed > 0, label + ' has no positive black-control coverage'", "assert changed >= 32, label + ' has fewer than 32 positive black-control pixels'")
    text = text.replace("assert len(report['rows']) == 36", "assert len(report['rows']) == 36\n        assert report['targeted_submesh_indices'] > 0\n        assert all(row['width'] == 128 and row['height'] == 128 for row in report['rows'])")
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), dict(__file__=str(Path(__file__).resolve()), __name__='__main__'))
