"""Replay recorded CDLOD selections with different bounded cache capacities.

This estimates CPU mesh cache misses only. It does not simulate GPU fences,
rendering, frame times or allocations outside ResidentTiles.
"""
import argparse
import hashlib
import json
from pathlib import Path

MAX_LOG_BYTES = 64 * 1024 * 1024
MAX_EVENTS = 20_000
MAX_NODES = 16_384
CAPACITIES = (256, 384, 512)


def cache_hash(cache):
    keys = [list(key) for key in sorted(cache)]
    return hashlib.sha256(json.dumps(keys, separators=(',', ':')).encode()).hexdigest()


def node_key(value):
    if (not isinstance(value, list) or len(value) != 4
            or any(type(n) is not int or n < 0 or n >= 2**64 for n in value)):
        raise ValueError('Invalid tile identity')
    return tuple(value)


class Cache:
    def __init__(self, capacity, initial, generation, root_key):
        if not isinstance(initial, list) or len(initial) > MAX_NODES:
            raise ValueError('Initial cache absent or oversized')
        self.capacity = capacity
        self.generation = generation
        self.root_key = None if root_key is None else node_key(root_key)
        self.entries = {}
        for entry in initial:
            if not isinstance(entry, list) or len(entry) != 3:
                raise ValueError('Invalid initial tile')
            key, used, entity = entry
            key = node_key(key)
            if key in self.entries or type(used) is not int or used < 0 or type(entity) is not bool:
                raise ValueError('Invalid initial cache state')
            self.entries[key] = (used, entity)
        self.misses = 0
        self.evictions = 0
        self.maximum_cached = len(self.entries)
        self.changed_selections_with_misses = 0

    def select(self, nodes, root_changed):
        if root_changed:
            self.entries.clear()
            self.generation = 0
            self.root_key = None
        active = set(nodes)
        previous = {key for key, (used, entity) in self.entries.items()
                    if used == self.generation and entity}
        if root_changed or previous != active:
            self.generation += 1
        representative = self.root_key if self.root_key in active else (nodes[0] if nodes else None)
        if representative is not None and representative != self.root_key:
            if self.root_key in self.entries:
                used, _ = self.entries[self.root_key]
                self.entries[self.root_key] = (used, False)
            if representative in self.entries:
                used, _ = self.entries[representative]
                self.entries[representative] = (used, False)
            self.root_key = representative
        misses = sum(key not in self.entries for key in nodes)
        self.misses += misses
        self.changed_selections_with_misses += int(misses > 0)
        for key in nodes:
            self.entries[key] = (self.generation, True)
        excess = max(0, len(self.entries) - max(len(active), self.capacity))
        oldest = sorted((used, key) for key, (used, _) in self.entries.items()
                        if used != self.generation and key != self.root_key)
        for _, key in oldest[:excess]:
            del self.entries[key]
            self.evictions += 1
        self.maximum_cached = max(self.maximum_cached, len(self.entries))


def replay(events):
    groups = {}
    for event in events:
        if not isinstance(event, dict) or not isinstance(event.get('material'), str) or not isinstance(event.get('root'), str):
            raise ValueError('Invalid selection record')
        name = (event['material'], event['root'])
        generation = event.get('prior_generation')
        if type(generation) is not int or generation < 0 or type(event.get('root_changed')) is not bool:
            raise ValueError('Invalid tile generation')
        nodes = event.get('nodes')
        if not isinstance(nodes, list) or len(nodes) > MAX_NODES:
            raise ValueError('Invalid selected nodes')
        nodes = [node_key(key) for key in nodes]
        if len(nodes) != len(set(nodes)):
            raise ValueError('Duplicate selected node')
        if name not in groups:
            groups[name] = {
                'events': 0,
                'caches': {capacity: Cache(capacity, event.get('initial_cache'), generation,
                                           event.get('prior_root_key')) for capacity in CAPACITIES},
            }
        group = groups[name]
        actual = group['caches'][256]
        root = None if event.get('prior_root_key') is None else node_key(event['prior_root_key'])
        if (actual.generation != generation or actual.root_key != root
                or cache_hash(actual.entries) != event.get('prior_cache_sha256')):
            raise ValueError(f'Actual 256-tile cache does not match replay at frame {event.get("frame")}')
        for cache in group['caches'].values():
            cache.select(nodes, event['root_changed'])
        group['events'] += 1
    return [
        {'material': material, 'root': root, 'selection_changes': group['events'],
         'capacities': {str(cap): {'new_meshes': cache.misses,
                                   'selection_changes_with_new_meshes': cache.changed_selections_with_misses,
                                   'evictions': cache.evictions,
                                   'maximum_cached': cache.maximum_cached}
                        for cap, cache in group['caches'].items()}}
        for (material, root), group in sorted(groups.items())
    ]


def read_events(path):
    path = Path(path)
    if not path.is_file() or path.stat().st_size > MAX_LOG_BYTES:
        raise ValueError('Tile history is absent or exceeds the input bound')
    events = []
    with path.open(encoding='utf8', errors='strict') as stream:
        for line in stream:
            if line.startswith('QUALIFY_TILE_SELECTION '):
                events.append(json.loads(line[len('QUALIFY_TILE_SELECTION '):]))
                if len(events) > MAX_EVENTS:
                    raise ValueError('Tile history event bound exceeded')
    if not events:
        raise ValueError('No tile selections found')
    return events


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    events = read_events(args.log)
    document = {'schema': 'forge.gate7.tile-cache-replay.v1',
                'input_sha256': hashlib.sha256(args.log.read_bytes()).hexdigest(),
                'selection_events': len(events), 'groups': replay(events),
                'limitations': ['CPU geometry cache simulation from one instrumented route.',
                    'Does not establish GPU admission, frame time, memory usage, visual parity, or production benefit.',
                    'The 256 capacity replay must match each recorded cache checksum before comparison.']}
    with args.output.open('x', encoding='utf8') as stream:
        json.dump(document, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps(document, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
