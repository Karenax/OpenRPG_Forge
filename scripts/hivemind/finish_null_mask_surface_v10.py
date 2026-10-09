"""One-shot final verification of the higher-resolution owned native job."""
from pathlib import Path

if __name__ == '__main__':
    text = Path(__file__).with_name('finish_null_mask_surface_v8.py').read_text('utf-8-sig')
    for old,new in [('unity-source-null-mask-surface-v8', 'unity-source-null-mask-surface-v10'),
        ('detail-mask-surface-finish-v1', 'detail-mask-surface-finish-v3'),
        ('detail-mask-surface-independent-reopen-v1', 'detail-mask-surface-independent-reopen-v3'),
        ('reopen_null_mask_surface_v8.py', 'reopen_null_mask_surface_v10.py')]:
        assert old in text
        text = text.replace(old,new)
    text = text.replace('paths = [Path(__file__).resolve(),',
        "paths = [Path(__file__).resolve(), HERE/'finish_null_mask_surface_v8.py', HERE/'reopen_null_mask_surface_v8.py',")
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), dict(__file__=str(Path(__file__).resolve()), __name__='__main__'))
