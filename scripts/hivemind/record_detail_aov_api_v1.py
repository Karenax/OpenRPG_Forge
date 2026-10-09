"""Preserve installed primary SDK evidence; this is not a rendered AOV proof."""
from pathlib import Path
import hashlib
import json
import datetime

W = Path('F:/OpenRPG_ImportWork')
SDK = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2'
OUT = W / 'detail-native-aov-api-review-v1'

def main():
    assert not OUT.exists()
    pipeline = (SDK / 'Runtime/RenderPipeline/HDRenderPipeline.cs').read_text('utf-8-sig')
    docs = (SDK / 'Documentation~/AOVs.md').read_text('utf-8-sig')
    request = (SDK / 'Runtime/RenderPipeline/RenderPass/AOV/AOVRequest.cs').read_text('utf-8-sig')
    assert 'hdCam.SetAOVRequests(s_EmptyAOVRequests)' in pipeline
    assert 'StandardRequest standardRequest = renderRequest as StandardRequest' in pipeline
    assert 'SetOverrideRenderFormat' in request and 'SetOverrideRenderFormat' in docs
    assert 'enter Play Mode' in docs
    OUT.mkdir()
    sources = []
    for relative in ['Runtime/RenderPipeline/HDRenderPipeline.cs', 'Documentation~/AOVs.md',
                     'Runtime/RenderPipeline/RenderPass/AOV/AOVRequest.cs']:
        original = SDK / relative
        raw = original.read_bytes()
        copy = OUT / relative
        copy.parent.mkdir(parents=True, exist_ok=True)
        copy.write_bytes(raw)
        assert copy.read_bytes() == raw
        sources.append(dict(path=str(original), preserved=str(copy), sha256=hashlib.sha256(raw).hexdigest()))
    result = dict(status='installed_hdrp_aov_api_reviewed_render_not_started',
                  standard_request_disables_aovs=True, precision_override_api_exists=True,
                  documented_path='Camera AOV requests in the native normal render loop / Play Mode',
                  next_action='Prepare an isolated normal-loop AOV capture with explicit float output, clock and restoration guards; do not reuse StandardRequest.',
                  native_default_rg_qualified=False, gpu_capture_completed=False, full_import=False,
                  sources=sources, finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'sources'}))

if __name__ == '__main__':
    main()
