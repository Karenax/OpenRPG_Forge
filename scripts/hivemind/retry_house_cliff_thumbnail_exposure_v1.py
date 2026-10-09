"""Retry only rejected cliff views with fixed native EV0, preserving source materials."""
from pathlib import Path
import importlib.util,json,shutil,subprocess,sys
BASE=Path(__file__).with_name('queue_house_thumbnail_batch_v1.py')
spec=importlib.util.spec_from_file_location('house_batch',BASE);batch=importlib.util.module_from_spec(spec);spec.loader.exec_module(batch)
OLD=batch.ROOT;ROOT=batch.W/'house-source-thumbnail-batch-0001-0003-exposure-v2'
def prepare():
    batch.no_unity();assert not ROOT.exists();ROOT.mkdir()
    assert batch.read(OLD/'RUN-RESULT.v1.json')['source_inputs_unchanged']
    source=batch.HELPER;helper=source.with_name('SourceLibraryThumbnailBatchV2.cs');assert not helper.exists()
    text=source.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailBatchV1','OpenRPGSourceLibraryThumbnailBatchV2')
    assert text.count('presentation_fixed_ev=8')==1 and text.count('fixedExposure.Override(8)')==1
    text=text.replace('presentation_fixed_ev=8','presentation_fixed_ev=0').replace('fixedExposure.Override(8)','fixedExposure.Override(0)')
    helper.write_text(text,'utf-8');target=batch.PROJECT/'Assets/Editor'/helper.name;assert not target.exists();shutil.copyfile(helper,target)
    prep=batch.read(OLD/'PREPARED.v1.json');prep['cases']=prep['cases'][:3];prep.update(expected_cards=3,expected_images=6,presentation_fixed_ev=0,rejected_previous_views=True)
    for i,c in enumerate(prep['cases']):
        folder=ROOT/f'case-{i:02d}';folder.mkdir();c['output']=str(folder/'NATIVE-RESULT.v1.json')
    prep['input_pins'].update({str(p):batch.sha(p) for p in [Path(__file__).resolve(),helper,target,OLD/'RUN-RESULT.v1.json',OLD/'NATIVE-RESULT.v1.json']})
    batch.write(ROOT/'REQUEST.v1.json',dict(cases=prep['cases'],output=str(ROOT/'NATIVE-RESULT.v1.json')));prep['input_pins'][str(ROOT/'REQUEST.v1.json')]=batch.sha(ROOT/'REQUEST.v1.json');batch.write(ROOT/'PREPARED.v1.json',prep)
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    batch.write(ROOT/'JOB.v1.json',dict(pid=proc.pid));print(json.dumps(dict(pid=proc.pid,cards=3,exposure_ev=0)))
def run():
    # Use the unchanged batch controller with only this private root and class name.
    text=BASE.read_text('utf-8');assert text.count("'OpenRPGSourceLibraryThumbnailBatchV1.Export'")==1
    exec(compile(text.replace("'OpenRPGSourceLibraryThumbnailBatchV1.Export'","'OpenRPGSourceLibraryThumbnailBatchV2.Export'"),str(BASE),'exec'),batch.__dict__)
    batch.ROOT=ROOT;batch.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
