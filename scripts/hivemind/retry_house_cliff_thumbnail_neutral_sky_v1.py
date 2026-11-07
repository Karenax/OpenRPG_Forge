"""Retry only rejected cliff views with fixed native EV3, preserving source materials."""
from pathlib import Path
import importlib.util,json,shutil,subprocess,sys
BASE=Path(__file__).with_name('queue_house_thumbnail_batch_v1.py')
spec=importlib.util.spec_from_file_location('house_batch',BASE);batch=importlib.util.module_from_spec(spec);spec.loader.exec_module(batch)
OLD=batch.ROOT;ROOT=batch.W/'house-source-thumbnail-batch-0001-0003-neutral-sky-v1'
def prepare():
    batch.no_unity();assert not ROOT.exists();ROOT.mkdir()
    assert batch.read(OLD/'RUN-RESULT.v1.json')['source_inputs_unchanged']
    source=batch.HELPER;helper=source.with_name('SourceLibraryThumbnailBatchV6.cs');assert not helper.exists()
    text=source.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailBatchV1','OpenRPGSourceLibraryThumbnailBatchV6')
    assert text.count('presentation_fixed_ev=8')==1 and text.count('fixedExposure.Override(8)')==1
    text=text.replace('presentation_fixed_ev=8','presentation_fixed_ev=3').replace('fixedExposure.Override(8)','fixedExposure.Override(3)')
    needle='var tone=presentationProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);'
    assert text.count(needle)==1
    text=text.replace(needle,needle+' var environment=presentationProfile.Add<VisualEnvironment>(true);environment.skyType.Override(SkySettings.GetUniqueID<GradientSky>());environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);var neutralSky=presentationProfile.Add<GradientSky>(true);neutralSky.top.Override(new Color(.4f,.4f,.4f,1));neutralSky.middle.Override(new Color(.4f,.4f,.4f,1));neutralSky.bottom.Override(new Color(.4f,.4f,.4f,1));neutralSky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);neutralSky.multiplier.Override(1);neutralSky.updateMode.Override(EnvironmentUpdateMode.Realtime);')
    text=text.replace('public string presentation_tonemapping="Neutral";', 'public string presentation_tonemapping="Neutral";public string presentation_environment="uniform grey GradientSky 0.4; multiplier 1; dynamic ambient; source material unchanged";')
    helper.write_text(text,'utf-8');target=batch.PROJECT/'Assets/Editor'/helper.name;assert not target.exists();shutil.copyfile(helper,target)
    prep=batch.read(OLD/'PREPARED.v1.json');prep['cases']=prep['cases'][:3];prep.update(sub_batch="L08.IMAGE.HOUSE.08",expected_cards=3,expected_images=6,presentation_fixed_ev=3,rejected_previous_views=True)
    for i,c in enumerate(prep['cases']):
        folder=ROOT/f'case-{i:02d}';folder.mkdir();c['output']=str(folder/'NATIVE-RESULT.v1.json')
    prep['input_pins'].update({str(p):batch.sha(p) for p in [Path(__file__).resolve(),helper,target,OLD/'RUN-RESULT.v1.json',OLD/'NATIVE-RESULT.v1.json']})
    batch.write(ROOT/'REQUEST.v1.json',dict(cases=prep['cases'],output=str(ROOT/'NATIVE-RESULT.v1.json')));prep['input_pins'][str(ROOT/'REQUEST.v1.json')]=batch.sha(ROOT/'REQUEST.v1.json');batch.write(ROOT/'PREPARED.v1.json',prep)
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    batch.write(ROOT/'JOB.v1.json',dict(pid=proc.pid));print(json.dumps(dict(pid=proc.pid,cards=3,exposure_ev=3)))
def run():
    # Use the unchanged batch controller with only this private root and class name.
    text=BASE.read_text('utf-8');assert text.count("'OpenRPGSourceLibraryThumbnailBatchV1.Export'")==1
    exec(compile(text.replace("'OpenRPGSourceLibraryThumbnailBatchV1.Export'","'OpenRPGSourceLibraryThumbnailBatchV6.Export'"),str(BASE),'exec'),batch.__dict__)
    batch.ROOT=ROOT;batch.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
