"""Bounded, deterministic layout shared by atlas bake and future card export."""
import math


def atlas_layout(groups, tile_size=64, gutter=4, max_size=2048):
    if (type(groups) is not int or groups < 1 or
        any(type(v) is not int for v in (tile_size, gutter, max_size)) or
        tile_size < 16 or tile_size & (tile_size - 1) or
        max_size < tile_size or max_size & (max_size - 1) or
        gutter < 2 or 2 * gutter >= tile_size):
        raise ValueError('positive group count, power-of-two sizes and usable gutter required')
    count = groups * 3
    if count > (max_size // tile_size) ** 2:
        raise ValueError('atlas pixel budget exceeded; do not silently reduce quality')
    side = tile_size
    while (side // tile_size) ** 2 < count:
        side *= 2
    columns = side // tile_size
    tiles = []
    for index in range(count):
        x, y = index % columns * tile_size, index // columns * tile_size
        tiles.append({'group': index // 3, 'axis': 'xyz'[index % 3],
                      'tile_xy': [x, y], 'content_xy': [x + gutter, y + gutter],
                      'content_size': tile_size - 2 * gutter})
    return {'size': side, 'tile_size': tile_size, 'gutter': gutter, 'tiles': tiles,
            'rgba8_bytes_per_map': side * side * 4,
            'rgba8_full_mips_bytes_per_map': sum(4 * (side >> i) ** 2
                                               for i in range(int(math.log2(side)) + 1))}
