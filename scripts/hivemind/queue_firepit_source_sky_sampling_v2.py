"""Retain v1 refusal; avoid the original light-direction variable name."""
from pathlib import Path
BASE=Path(__file__).with_name('queue_firepit_source_sky_sampling_v1.py')
source=BASE.read_text('utf-8').replace('firepit-source-sky-sampling-v1','firepit-source-sky-sampling-v2')
for name in ['Reference','Readback','MipMetadata','Metadata']:
    source=source.replace('SourceSkySampling'+name+'V1','SourceSkySampling'+name+'V2')
source=source.replace('var directions=new Vector3','var sourceSkySampleDirections=new Vector3').replace('directions.Length','sourceSkySampleDirections.Length').replace('foreach(var d in directions)','foreach(var d in sourceSkySampleDirections)')
exec(compile(source,str(BASE),'exec'),globals())
