"""Read bounded native intervals and Windows ETW presentation intervals."""
import csv
import io
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


def load_presentmon(path, pid, start_qpc, end_qpc):
    """Read one app's PresentMon 2.6 capture with --v1_metrics --qpc_time.

    The caller supplies the QPC window witnessed by the game scenario. These
    intervals end at a Present call. Dropped presents remain in the result.
    """
    integer(pid, 1, 2**32 - 1)
    integer(start_qpc)
    integer(end_qpc, start_qpc + 1)
    path = Path(path)
    if not path.is_file():
        raise ValueError('PresentMon interval input is not a regular file')
    with path.open('rb') as stream:
        data = stream.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise ValueError('PresentMon intervals exceed 32 MiB')
    reader = csv.DictReader(io.StringIO(data.decode('utf-8-sig')))
    expected = {'ProcessID', 'QPCTime', 'SwapChainAddress',
                'msBetweenPresents', 'Dropped', 'PresentMode'}
    header = reader.fieldnames or []
    if not expected.issubset(header) or len(set(header)) != len(header):
        raise ValueError('Expected a PresentMon v1 CSV with raw QPC and display data')
    previous = None
    swap_chain = None
    samples = []
    for row in reader:
        if None in row or any(row[name] is None for name in expected):
            raise ValueError('Incomplete PresentMon row')
        if int(row['ProcessID']) != pid:
            continue
        qpc = int(row['QPCTime'])
        if not start_qpc <= qpc < end_qpc:
            continue
        address = row['SwapChainAddress']
        dropped = row['Dropped']
        mode = row['PresentMode']
        if not address or dropped not in ('0', '1') or not mode:
            raise ValueError('Incomplete PresentMon presentation state')
        if previous is None:
            # This interval may have started before the verified window.
            previous, swap_chain = qpc, address
            continue
        if address != swap_chain or qpc <= previous:
            raise ValueError('PresentMon window does not describe one ordered swap chain')
        interval = float(row['msBetweenPresents'])
        if not math.isfinite(interval) or interval <= 0 or len(samples) >= MAX_SAMPLES:
            raise ValueError('Invalid or excessive PresentMon intervals')
        samples.append({'qpc': qpc, 'present_interval_ms': interval,
                        'dropped': dropped == '1', 'present_mode': mode})
        previous = qpc
    if not samples:
        raise ValueError('No complete present interval for the process and QPC window')
    return samples
