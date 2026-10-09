"""Unwrap the official nested archive; preserve the first refused preparation."""
from pathlib import Path
BASE=Path(__file__).with_name('retain_unity_builtin_shader_sources_v1.py')
script=BASE.read_text('utf-8-sig')
old="ROOT=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v1')"
assert script.count(old)==1
script=script.replace(old,"ROOT=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v2')")
start=script.index('    with urllib.request.urlopen(')
end=script.index('    extracted=ROOT/',start)
script=script[:start]+"""    import shutil
    prior=Path('F:/OpenRPG_ImportWork/unity-builtin-shaders-6000.5.8f1-v1/builtin_shaders-6000.5.8f1.zip')
    assert prior.is_file()
    with zipfile.ZipFile(prior) as outer:
        assert outer.namelist()==['build/BuiltinShaders/builtin_shaders.zip']
    shutil.copyfile(prior,archive);assert sha(prior)==sha(archive)
"""+script[end:]
needle="    shaders=[p for p in extracted.rglob('*.shader')"
assert script.count(needle)==1
script=script.replace(needle,"""    nested=list(extracted.rglob('*.zip'));assert len(nested)==1
    with zipfile.ZipFile(nested[0]) as bundle:
        entries=bundle.infolist()
        assert len(entries)<2000 and sum(e.file_size for e in entries)<150*1024*1024
        for entry in entries:
            destination=(extracted/'builtin'/entry.filename).resolve()
            assert destination.is_relative_to((extracted/'builtin').resolve())
            assert not stat.S_ISLNK(entry.external_attr>>16)
            if entry.is_dir():destination.mkdir(parents=True,exist_ok=True);continue
            destination.parent.mkdir(parents=True,exist_ok=True)
            with destination.open('xb') as out:out.write(bundle.read(entry))
            pins[str(destination)]=sha(destination)
    pins[str(prior)]=sha(prior)
    pins[str(Path(__file__).with_name('retain_unity_builtin_shader_sources_v1.py'))]=sha(Path(__file__).with_name('retain_unity_builtin_shader_sources_v1.py'))
    shaders=[p for p in extracted.rglob('*.shader')""")
exec(compile(script,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
