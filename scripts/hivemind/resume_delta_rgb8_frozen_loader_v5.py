"""Complete all remaining literal include dependencies after the prior job terminates."""
from pathlib import Path

RECOVERY_BASE=Path(__file__).with_name('resume_delta_rgb8_frozen_loader_v4.py')
SCRIPT=RECOVERY_BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("OLD = W / 'delta-rgb8-frozen-canonical-loader-v3'", "OLD = W / 'delta-rgb8-frozen-canonical-loader-v4'")
replace("ROOT = W / 'delta-rgb8-frozen-canonical-loader-v4'", "ROOT = W / 'delta-rgb8-frozen-canonical-loader-v5'")
replace("assert 'due to 8 previous errors' in (OLD / 'native.stderr.log').read_text('utf-8-sig')",
        "assert \"couldn't read\" in (OLD / 'native.stderr.log').read_text('utf-8-sig')")
replace("    names = ['environment', 'player_controller', 'terrain', 'vegetation', 'water', 'weather', 'world-sketch']\n    relatives = [Path('examples/visual_valley') / (name+'.v1.json') for name in names]\n    relatives.append(Path('examples/gate0/proof.scene.json'))", """    import re
    relatives=set()
    for source_file in SNAPSHOT.rglob('*.rs'):
        for relative in re.findall(r'include_(?:str|bytes)!\\s*\\(\\s*"([^"]+)"', source_file.read_text('utf-8-sig')):
            copied=(source_file.parent/relative).resolve()
            assert copied.is_relative_to(SNAPSHOT.resolve())
            if not copied.is_file(): relatives.add(copied.relative_to(SNAPSHOT.resolve()))
    assert len(relatives)==4, 'Review changed inclusion closure before copying'
    relatives=sorted(relatives)
    pins[str(RECOVERY_BASE)]=native.sha(RECOVERY_BASE)
""")
replace('original_include_files_added=8', 'original_include_files_added=len(copies)')
exec(compile(SCRIPT,str(RECOVERY_BASE),'exec'),{'__name__':'__main__',
    '__file__':str(Path(__file__).resolve()),'RECOVERY_BASE':RECOVERY_BASE})
