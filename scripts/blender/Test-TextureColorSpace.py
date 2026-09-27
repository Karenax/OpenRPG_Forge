"""Real Blender byte-image round-trip of Forge's linear procedural pixel contract.

Run headless with --disable-autoexec --python-exit-code 1 --python this-file
-- --output-dir <fresh directory>. No production assets are rewritten.
"""
import argparse
import importlib.util
from pathlib import Path
import sys

import bpy
import numpy as np


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    (args.output_dir / 'textures').mkdir(parents=True, exist_ok=False)
    spec = importlib.util.spec_from_file_location(
        'forge_color_test', Path(__file__).with_name('Build-VisualValleyAssetKit.py'))
    kit = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(kit)
    for name in ('base_color', 'roughness', 'normal'):
        source = np.array([[[0.18, 0.003, 0.8, 0.25], [0.0, 0.5, 1.0, 1.0]]], dtype=np.float32)
        original = source.copy()
        _, receipt = kit.save_texture_image(args.output_dir, 'fixture', 'fixture', name, source)
        assert np.array_equal(source, original), 'writer mutated procedural source'
        loaded = bpy.data.images.load(str(args.output_dir / receipt['path']), check_existing=False)
        values = np.array(loaded.pixels[:], dtype=np.float32).reshape(source.shape)
        if name == 'base_color':
            rgb = values[..., :3]
            values[..., :3] = np.where(rgb <= 0.04045, rgb / 12.92,
                                       ((rgb + 0.055) / 1.055) ** 2.4)
        assert np.allclose(values, source, atol=0.005, rtol=0), (name, values, source)
        assert receipt['colorspace'] == ('sRGB' if name == 'base_color' else 'Non-Color')
    print('TEXTURE_COLOR_SPACE_ROUNDTRIP_PASS: 3 maps; alpha and source preserved', flush=True)


if __name__ == '__main__':
    main()
