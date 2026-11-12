"""Find source variants with only explicit pipeline light differences."""
import argparse
import json
from pathlib import Path
import audit_lod_collider_variants as base
from compare_unity_light_prefabs import compare


def light_only(a,b):
    return compare(a,b)['equal_outside_pipeline_lights']


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();base.compare=light_only
    result=base.audit(a.vault,a.inventory)
    for row in result['results']:
        if row['state']=='lod_collider_only':row['state']='pipeline_light_only'
    result['limitations']='Comparison excludes known pipeline light components; visual, runtime and deletion equivalence unqualified.'
    with a.output.open('x',encoding='utf8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps({'pairs':len(result['results']),'eligible':sum(x['state']=='pipeline_light_only' for x in result['results']),
                      'review_required':sum(x['state']=='review_required' for x in result['results'])}))
