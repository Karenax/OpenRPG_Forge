"""Read bounded v1-v3 native intervals. Snapshot fences belong to snapshot.frame only."""
import json
import math
from pathlib import Path

MAX_BYTES = 32 * 1024 * 1024
MAX_SAMPLES = 20_000
MAX_METADATA = 4096


def integer(value, minimum=0, maximum=2**64 - 1):
    if type(value) is not int or not minimum <= value <= maximum:
        raise ValueError('Invalid native interval integer')
    return value


def decode(document):
    if not isinstance(document, dict):
        raise ValueError('Invalid native interval document')
    schema = document.get('schema')
    if schema not in ('forge.native-main-frame-intervals.v1', 'forge.native-main-frame-intervals.v2',
                      'forge.native-main-frame-intervals.v3'):
        raise ValueError('Unsupported native interval schema')
    if document.get('truncated') is not False:
        raise ValueError('Truncated native interval evidence is incomplete')
    maximum = integer(document.get('maximum_samples'), 1, MAX_SAMPLES)
    samples = document.get('samples')
    if not isinstance(samples, list) or len(samples) > maximum:
        raise ValueError('Native interval sample budget exceeded')
    snapshots = {}
    if schema.endswith(('.v2', '.v3')):
        maximum_metadata = integer(document.get('maximum_metadata_snapshots_per_domain'), 1, MAX_METADATA)
        for name in ('graphics', 'imported'):
            values = document.get(name + '_observations')
            if not isinstance(values, list) or len(values) > maximum_metadata:
                raise ValueError('Native metadata budget exceeded')
            for value in values:
                if not isinstance(value, dict):
                    raise ValueError('Invalid native metadata snapshot')
                integer(value.get('frame'), 0, 2**32 - 1)
                if not isinstance(value.get('observation'), dict):
                    raise ValueError('Invalid native metadata snapshot')
            snapshots[name] = values
    result = []
    for row in samples:
        if not isinstance(row, dict):
            raise ValueError('Invalid native interval row')
        frame = integer(row.get('frame'), 0, 2**32 - 1)
        integer(row.get('tick'))
        milliseconds = row.get('frame_ms')
        if type(milliseconds) not in (int, float) or not math.isfinite(milliseconds) or milliseconds <= 0:
            raise ValueError('Invalid native interval duration')
        if type(row.get('projection_ready')) is not bool:
            raise ValueError('Native readiness observation absent')
        if schema.endswith('.v3'):
            fields = ('selected_triangles', 'rendered_batches', 'generation', 'pending')
            present = [name in row for name in fields]
            if any(present) and not all(present):
                raise ValueError('Incomplete static HLOD observation')
            if all(present):
                for name in fields[:3]:
                    integer(row[name])
                if type(row['pending']) is not bool:
                    raise ValueError('Invalid static HLOD readiness')
        value = dict(row)
        for name in ('graphics', 'imported'):
            reference = row.get(name)
            if schema.endswith('.v1'):
                if reference is not None and not isinstance(reference, dict):
                    raise ValueError('Invalid legacy native metadata')
                value[name + '_observed_frame'] = frame if reference is not None else None
            elif reference is not None:
                index = integer(reference, 0, len(snapshots[name]) - 1)
                snapshot = snapshots[name][index]
                if snapshot['frame'] > frame:
                    raise ValueError('Native metadata reference points into the future')
                value[name] = snapshot['observation']
                value[name + '_observed_frame'] = snapshot['frame']
            else:
                value[name] = None
                value[name + '_observed_frame'] = None
        result.append(value)
    return result


def load(path):
    path = Path(path)
    if not path.is_file():
        raise ValueError('Native interval input is not a regular file')
    with path.open('rb') as stream:
        data = stream.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise ValueError('Native intervals exceed 32 MiB')
    return decode(json.loads(data))
