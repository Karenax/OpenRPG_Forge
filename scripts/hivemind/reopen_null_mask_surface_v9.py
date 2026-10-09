"""Require broader positive coverage on both focused native surface outputs."""
from pathlib import Path

def main():
    base = Path(__file__).with_name('reopen_null_mask_surface_v8.py')
    text = base.read_text('utf-8-sig')
    text = text.replace('unity-source-null-mask-surface-v8', 'unity-source-null-mask-surface-v9')
    text = text.replace('detail-mask-surface-independent-reopen-v1', 'detail-mask-surface-independent-reopen-v2')
    old = "assert changed > 0, label + ' has no positive black-control coverage'"
    assert text.count(old) == 1
    text = text.replace(old, "assert changed >= 32, label + ' has fewer than 32 positive black-control pixels'")
    old = "assert len(report['rows']) == 36"
    assert text.count(old) == 1
    text = text.replace(old, old + "\n        assert report['targeted_submesh_indices'] > 0 and report['targeted_submesh_world_radius'] > 0")
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), dict(__file__=str(Path(__file__).resolve()), __name__='__main__'))

if __name__ == '__main__':
    main()
