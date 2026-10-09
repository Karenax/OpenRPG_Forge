"""Pin the actual source and target selector difference; no pixel qualification."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'firepit-receiver-cascade-selection-audit-v1'
SOURCE = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/Shadow/HDShadowAlgorithms.hlsl'
TARGET = Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src/render/shadows.wgsl')
RECEIVER = W / 'firepit-source-tent7-receiver-bias-case-v2/private-receiver-forward.wgsl'

def main():
    texts = {p: p.read_text(encoding='utf-8') for p in (SOURCE, TARGET, RECEIVER)}
    required = {
        SOURCE: ['int EvalShadow_GetSplitIndex(', 'dsd.sphereCascades[i]', 'distSq / sphere.w', 'relDistance <= 1.0', 'dsd.cascadeBorders[shadowSplitIndex]'],
        TARGET: ['fn get_cascade_index(', '-view_z < (*light).cascades[i].far_bound'],
        RECEIVER: ['let index=shadows::get_cascade_index(light_id,view_z)', 'light.cascades_overlap_proportion'],
    }
    for path, markers in required.items():
        for marker in markers:
            if marker not in texts[path]:
                raise RuntimeError(f'Refused: selector evidence changed: {path}: {marker}')
    receipts = [W / 'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json', W / 'firepit-source-shadow-oriented-image-comparison-v1/RESULT.v1.json']
    source, image = [json.loads(p.read_text(encoding='utf-8')) for p in receipts]
    if len(source['directional_cascade_spheres']) != 4 or image['full_shadow_fidelity_qualified']:
        raise RuntimeError('Refused: unexpected reference state')
    pins = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in [*texts, *receipts, Path(__file__)]}
    result = {
        'status': 'source_spherical_and_current_view_depth_selectors_differ_verified',
        'finished_utc': datetime.now(timezone.utc).isoformat(),
        'sub_lot': 'L01.OMBRES.RECEPTEUR.SELECTION.AUDIT',
        'source_selection': 'First cascade whose squared sphere distance ratio is >0 and <=1; spherical directional border fade',
        'current_receiver_selection': 'Bevy first cascade with negative view_z below far_bound; view-depth overlap fade',
        'source_directional_spheres_from_existing_gpu_receipt': source['directional_cascade_spheres'],
        'gpu_receipt_replayed_this_run': False,
        'current_receiver_calls_native_view_depth_selector': True,
        'cause_of_pixel_mismatch_proven': False,
        'pixel_fidelity_qualified': False,
        'prior_shadow_iou': image['shadow_mask_iou'],
        'full_import': False,
        'complete_assets_delivered_increment': 0,
        'next_action': 'Use measured source spheres and borders in a private receiver replay; define thresholds before capture; compare original with/without-shadow views and retain all refusals.',
        'input_pins': pins,
    }
    OUT.mkdir(exist_ok=True)
    path = OUT / 'RESULT.v1.json'
    if path.exists():
        old = json.loads(path.read_text(encoding='utf-8'))
        if old['input_pins'] != pins:
            raise RuntimeError('Refused: immutable audit inputs changed; use a new revision')
        return path
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return path

if __name__ == '__main__':
    print(main())
