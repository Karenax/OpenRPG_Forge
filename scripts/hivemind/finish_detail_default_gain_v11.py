"""One-shot RG measurement verification after the owned native job terminates."""
from pathlib import Path

if __name__=='__main__':
    base=Path(__file__).with_name('finish_null_mask_surface_v8.py')
    text=base.read_text('utf-8-sig')
    for old,new in [
        ('unity-source-null-mask-surface-v8','detail-default-native-gain-v11'),
        ('detail-mask-surface-finish-v1','detail-default-native-gain-finish-v1'),
        ('detail-mask-surface-independent-reopen-v1','detail-default-native-rg-binding-v1'),
        ('reopen_null_mask_surface_v8.py','reopen_detail_default_gain_v11.py'),
        ('native_surface_default_qualified','native_default_rg_binding_qualified')]:
        assert old in text
        text=text.replace(old,new)
    text=text.replace('paths = [Path(__file__).resolve(),','paths = [Path(__file__).resolve(), base,')
    exec(compile(text,str(Path(__file__).resolve()),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__',base=base))
