"""Reconcile native final report with every durable page and prior byte checks."""
from pathlib import Path
import json
import hashlib
import datetime

W=Path('F:/OpenRPG_ImportWork')
N=W/'library-checkpointed-native-audit-v2'
OUT=W/'library-all-records-independent-reconciliation-v4'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not OUT.exists();OUT.mkdir()
    result=dict(status='reconciling_final_native_library_record_audit',full_import=False,gpu_qualified=False)
    try:
        paths=[N/'RUN-RESULT.v2.json',N/'RESULT.v2.json',
               W/'library-available-images-independent-reopen-v2/RESULT.v1.json',
               W/'library-incremental-pages-reopen-v3/RESULT.v1.json',N/'LOCKED-SESSION.v1.json']
        run,final,first,increment,locked=[json.loads(p.read_bytes()) for p in paths]
        assert run['audit_exit_code']==0 and run['entries_checked']==26735
        assert sha(N/'RESULT.v2.json')==run['result_sha256']
        assert first['status']=='available_thumbnail_bytes_reopened_missing_images_retained'
        assert increment['status']=='new_library_pages_and_thumbnail_bytes_verified'
        assert sha(paths[2])==increment['prior_receipt_sha256']
        assert final['entries_checked']==26735 and not final['full_import']
        assert final['pack_and_availability_filters_checked'] and final['licenses_present']
        assert final['source_cards_non_placeable_enforced'] and final['all_detail_records_readable']
        assert final['all_declared_thumbnails_readable'] and not final['converted_model_bytes_read_in_this_audit']
        inherited=first['batches']+increment['batches']
        for batch in inherited:assert sha(batch['path'])==batch['sha256']
        offset=0;rows=[];new_images={};new_cards=0;new_refs=0;pages=[]
        for p in sorted((N/'batches').glob('*.json')):
            raw=p.read_bytes();page=json.loads(raw)
            assert page['offset']==offset and page['expected_total']==26735
            assert page['revision']==final['revision']==first['revision']==increment['revision']
            assert not page['full_import'] and not page['gpu_qualified']
            assert page['details_checked']==len(page['rows'])
            image_count=sum(r['preview_path'] is not None for r in page['rows'])
            assert image_count==page['images_read']
            if offset>=increment['cumulative_cards']:
                for row in page['rows']:
                    new_cards+=1
                    pimage,digest=row['preview_path'],row['thumbnail_sha256']
                    if pimage is None or digest is None:
                        assert pimage is None and digest is None
                    else:
                        if pimage not in new_images:new_images[pimage]=sha(pimage)
                        assert new_images[pimage]==digest
                        new_refs+=1
            rows.extend(page['rows']);offset+=len(page['rows'])
            pages.append(dict(path=str(p),sha256=hashlib.sha256(raw).hexdigest()))
        assert offset==26735 and rows==final['rows']
        assert len({r['id'] for r in rows})==26735
        assert new_cards==26735-increment['cumulative_cards']
        missing=[r for r in rows if r['preview_path'] is None]
        assert len(missing)==final['entries_without_thumbnail']
        images=sum(r['preview_path'] is not None for r in rows)
        assert images==final['thumbnail_bytes_read_and_hashed']==26735-len(missing)
        assert images==increment['cumulative_image_references']+new_refs
        for row in rows:
            assert row['license'].strip() and row['availability'] in ('converted_model','source_support')
        pins={str(p):sha(p) for p in paths}
        result.update(status='all_native_library_records_pages_filters_and_image_receipts_reconciled',
                      cards_checked=26735,image_references_checked=images,missing_images=len(missing),
                      new_cards_checked=new_cards,new_image_references_hashed=new_refs,
                      new_distinct_images_hashed=len(new_images),new_image_sha256=new_images,
                      inherited_image_checks_not_rehashed=True,all_pages_rehashed=True,
                      all_cards_observed=True,all_images_available=not missing,
                      source_cards_non_placeable_enforced=True,pack_and_availability_filters_checked=True,
                      licenses_present=True,actual_ui_visual_review=False,converted_model_bytes_verified=False,
                      source_geometry_verified=False,revision=final['revision'],input_pins=pins,pages=pages,
                      missing_image_cards=[dict(id=r['id'],pack=r['pack'],availability=r['availability']) for r in missing],
                      packs=final['packs'],availability=final['availability'],locked_session=locked)
    except Exception as error:result.update(status='refused_or_unverified',error=repr(error))
    result['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in {'new_image_sha256','pages','missing_image_cards','input_pins','packs','locked_session'}}))
if __name__=='__main__':main()
