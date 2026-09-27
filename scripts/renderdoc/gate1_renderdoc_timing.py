"""Optional GPU event durations from the RenderDoc replay API, never live FPS.

API reference: RenderDoc's official docs/python_api/examples/renderdoc/fetch_counters.rst.
No renderer code or third-party implementation is copied.
"""
import math


def fetch_durations(rd, controller, roots, structured):
    counter = rd.GPUCounter.EventGPUDuration
    if counter not in controller.EnumerateCounters():
        raise ValueError('EventGPUDuration unavailable')
    description = controller.DescribeCounter(counter)
    if (description.resultType != rd.CompType.Float or description.resultByteWidth != 8
            or description.unit != rd.CounterUnit.Seconds):
        raise ValueError('unexpected GPU duration representation')
    actions = {}
    required = set()

    def visit(action, ancestors):
        event = int(action.eventId)
        if event in actions:
            raise ValueError('duplicate action event')
        name = str(action.GetName(structured))
        actions[event] = (action, ancestors, name)
        if not action.children and action.flags & (rd.ActionFlags.Drawcall | rd.ActionFlags.Dispatch):
            required.add(event)
        for child in action.children:
            visit(child, ancestors + [name])

    for root in roots:
        visit(root, [])
    seen, rows, containers = set(), [], []
    for result in controller.FetchCounters([counter]):
        event = int(result.eventId)
        if result.counter != counter or event in seen or event not in actions:
            raise ValueError('unexpected or duplicate counter event')
        seen.add(event)
        duration = float(result.value.d)
        if not math.isfinite(duration) or duration < 0:
            raise ValueError('invalid GPU duration')
        action, ancestors, name = actions[event]
        record = {'event_id': event, 'name': name, 'ancestors': ancestors, 'duration_ms': duration * 1000}
        # Expanded MultiAction parent timings must not be added to their children.
        if action.children:
            containers.append(record)
        else:
            rows.append(record)
    missing = sorted(required - seen)
    if not rows or missing:
        raise ValueError('missing GPU durations for draw/dispatch leaves: {}'.format(missing))
    return {'schema': 'forge.renderdoc-event-gpu-duration/v1', 'status': 'PASS_REPLAY_ONLY',
            'counter': str(description.name), 'unit': 'milliseconds',
            'draw_dispatch_leaves': len(required), 'leaf_counter_rows': len(rows),
            'leaf_duration_sum_ms_not_frame_time': sum(row['duration_ms'] for row in rows),
            'rows': rows, 'container_rows_excluded_from_sum': containers,
            'limit': 'Instrumented replay event timings, not live frame time, FPS or current-candidate qualification.'}
