"""One-shot independent reopen for the focused native source-material probe."""
from pathlib import Path

def main():
    base = Path(__file__).with_name('finish_null_mask_surface_v8.py')
    text = base.read_text('utf-8-sig')
    replacements = {
        'unity-source-null-mask-surface-v8': 'unity-source-null-mask-surface-v9',
        'detail-mask-surface-finish-v1': 'detail-mask-surface-finish-v2',
        'detail-mask-surface-independent-reopen-v1': 'detail-mask-surface-independent-reopen-v2',
        'reopen_null_mask_surface_v8.py': 'reopen_null_mask_surface_v9.py',
    }
    for old, new in replacements.items():
        assert old in text
        text = text.replace(old, new)
    old = "paths = [Path(__file__).resolve(),"
    assert text.count(old) == 1
    text = text.replace(old, old + " HERE/'finish_null_mask_surface_v8.py', HERE/'reopen_null_mask_surface_v8.py',")
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), dict(__file__=str(Path(__file__).resolve()), __name__='__main__'))

if __name__ == '__main__':
    main()
