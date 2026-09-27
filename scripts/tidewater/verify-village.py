"""Validate village draw data and every original baked texture mip."""
import json
import math
import struct


def validate_village(root, paths):
    scene = json.loads((root / 'village/render.json').read_text())
    textures = json.loads((root / 'village/textures.json').read_text())['textures']
    assert scene['schema'] == 'forge.tidewater.village-render' and scene['version'] == 1
    assert len(scene['nodes']) == 31 and len(scene['materials']) == 7
    assert len({n['source_id'] for n in scene['nodes']}) == len(scene['nodes'])
    materials = {m['id']:m for m in scene['materials']}
    for node in scene['nodes']:
        assert node['geometry'] in scene['geometries'] and node['material'] in materials
        assert len(node['matrix_world']) == 16 and all(math.isfinite(n) for n in node['matrix_world'])
        assert node['source_id'].startswith('tidewater:1438b1ab:village:')
    for geometry in scene['geometries'].values():
        attributes = geometry['attributes']
        assert set(attributes) == {'position','normal','uv','tint','vdata'}
        count = attributes['position']['count']
        assert 0 < count < 2_000_000
        for name, arity in [('position',3),('normal',3),('uv',2),('tint',3),('vdata',4)]:
            entry = attributes[name]
            assert entry['path'] in paths and entry['item_size'] == arity and entry['count'] == count
            data = (root / entry['path']).read_bytes()
            assert len(data) == count * arity * 4
            assert all(math.isfinite(v) for (v,) in struct.iter_unpack('<f',data))
        entry = geometry['indices']
        assert entry['path'] in paths
        data = (root / entry['path']).read_bytes()
        assert len(data) == geometry['triangles'] * 12 == geometry['source_draw_range']['count'] * 4
        assert all(i < count for (i,) in struct.iter_unpack('<I',data))
    for material in materials.values():
        assert material['side'] in ('front','back','double')
        for binding in material['bindings'].values():
            assert binding['texture'] in textures
        assert material['modules'] and all(m in scene['modules'] for m in material['modules'])
    for module in scene['modules'].values():
        assert module['code'] in paths
    assert textures
    for texture in textures.values():
        assert texture['format'] == 'rgba8unorm'
        mips = texture['mips']
        width, height = mips[0]['width'], mips[0]['height']
        assert len(mips) == max(width,height).bit_length()
        assert 1 <= width <= 2048 and 1 <= height <= 2048
        for level, mip in enumerate(mips):
            assert mip['path'] in paths and mip['level'] == level
            assert mip['width'] == max(1,width >> level) and mip['height'] == max(1,height >> level)
            assert (root / mip['path']).stat().st_size == mip['width'] * mip['height'] * 4
        data = (root / mips[0]['path']).read_bytes()
        assert min(data) != max(data), 'blank village texture'
    assert scene['omitted'] and all(n['name'] == 'village_fish' for n in scene['omitted'])
