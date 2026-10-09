"""Summarize measured CPU prop projection; never infer GPU draw or pixel parity."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def vector(value):
    return (isinstance(value, list) and len(value) == 3
            and all(isinstance(x, (int, float)) and not isinstance(x, bool)
                    and math.isfinite(x) for x in value))


def analyze(ledger):
    failures = []
    if (ledger.get('schema') != 'forge.tidewater.animation-clock-observation'
            or ledger.get('version') != 1 or ledger.get('status') != 'observed'
            or ledger.get('failure') is not None or ledger.get('listener_closed') is not True):
        failures.append('Successful owned capture required')
    projection = ((ledger.get('later') or {}).get('diagnostic') or {}).get('source_prop_projection')
    families = {}
    for name in ('village', 'beach'):
        family = projection.get(name) if isinstance(projection, dict) else None
        if not isinstance(family, dict):
            failures.append(f'{name}: actual projection diagnostic absent')
            continue
        roots = family.get('roots')
        if not isinstance(roots, list):
            failures.append(f'{name}: root measurements absent')
            continue
        issues = []
        parts = []
        ids = []
        for root in roots:
            identifier = root.get('id')
            ids.append(identifier)
            if not isinstance(identifier, str) or not identifier:
                failures.append(f'{name}: root identity absent')
            accepted, world = root.get('accepted_position'), root.get('world_position')
            if not vector(accepted) or not vector(world):
                failures.append(f'{name}.{identifier}: invalid position measurement')
            elif max(abs(a-b) for a, b in zip(accepted, world)) > 0.001:
                issues.append(dict(id=identifier, reason='Accepted/world position differs',
                                   accepted=accepted, world=world))
            children = root.get('parts')
            if not isinstance(children, list):
                failures.append(f'{name}.{identifier}: child measurements absent')
                continue
            if not children:
                issues.append(dict(id=identifier, reason='No observed direct render parts'))
            for part in children:
                parts.append(part)
                reasons = []
                for field in ('inherited_visible', 'view_visible', 'mesh_loaded'):
                    if not isinstance(part.get(field), bool):
                        failures.append(f'{name}.{identifier}: {field} measurement absent')
                    elif part[field] is False:
                        reasons.append(field + '=false')
                material = part.get('material')
                if not isinstance(material, dict):
                    reasons.append('Material absent')
                elif isinstance(material.get('opacity'), (int, float)) and material['opacity'] <= 0:
                    reasons.append('Opacity <= 0')
                if reasons:
                    issues.append(dict(id=identifier, name=root.get('name'), reasons=reasons,
                                       world_position=part.get('world_position'), material=material))
        if len(ids) != len(set(ids)):
            failures.append(f'{name}: duplicate root identity')
        if family.get('root_count') != len(roots) or family.get('part_count') != len(parts):
            failures.append(f'{name}: count inconsistent with observed direct children')
        families[name] = dict(root_count=len(roots), part_count=len(parts),
                              mesh_loaded=sum(p.get('mesh_loaded') is True for p in parts),
                              inherited_visible=sum(p.get('inherited_visible') is True for p in parts),
                              view_visible=sum(p.get('view_visible') is True for p in parts),
                              issues=issues)
    return dict(schema='forge.tidewater.prop-projection-analysis', version=1,
                status='measured' if not failures else 'failed_closed', failures=failures,
                families=families, gpu_draw_verified=False, visual_parity=False,
                limitations=['ViewVisibility is aggregate CPU visibility, not GPU draw completion.',
                             'Out-of-view objects can legitimately have view_visible=false.',
                             'Accepted/world position comparison assumes the existing identity scene root.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('ledger', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    raw = args.ledger.read_bytes()
    result = analyze(json.loads(raw))
    result['input'] = dict(path=str(args.ledger.resolve()), sha256=hashlib.sha256(raw).hexdigest())
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps(dict(status=result['status'], failures=result['failures'])))
    return 0 if not result['failures'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
