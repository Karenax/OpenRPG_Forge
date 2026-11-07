"""Sample actual GeneralORM BaseMap and ORMMap using the measured source UV0."""
from pathlib import Path
recipe=Path(__file__).with_name('measure_mountain_uv_albedo_v933.py')
code=recipe.read_text('utf-8').replace('mountain-uv-albedo-measurements-v933','mountain-generalorm-channel-measurements-v942')
code=code.replace("if 'SpeedTree' not in slot['material']['shader']:continue", "if slot['material']['shader']!='Shader Graphs/S_GeneralORM':continue")
code=code.replace("p['name']=='_MainTex'", "p['name']=='_BaseMap'")
anchor="                rows.append(dict(id=card['id'],"
assert code.count(anchor)==1
addition="""                orm=next(p for p in slot['material']['properties'] if p['name']=='_ORMMap');orm_path=P/orm['texture_path'];assert str(orm_path) in pins and sha(orm_path)==pins[str(orm_path)]
                with Image.open(orm_path) as image:orm_pixels=np.array(image.convert('RGBA'),dtype=np.float64)/255
                orm_samples={mode:{kind:samples(orm_pixels,slot[key],mode) for kind,key in [('unique_vertices','vertex_uv0'),('triangle_centroids','triangle_centroid_uv0')]} for mode in ['repeat','clamp']}
"""
code=code.replace(anchor,addition+anchor.replace('dict(id=',"dict(orm_texture=orm['texture_path'],orm_rgba_samples=orm_samples,id="))
code=code.replace('assert len(rows)==3','assert len(rows)==6').replace('held_tree_uv0_source_albedo_samples_and_attribute_lengths_measured','generalorm_source_basemap_and_orm_channels_at_uv0_measured')
code=code.replace("id=r['id'],vertex_dark_repeat=", "id=r['id'],slot=r['submesh'],orm_mean_rgba=r['orm_rgba_samples']['repeat']['triangle_centroids']['mean_rgba'],vertex_dark_repeat=")
exec(compile(code,str(recipe)+'#generalorm-all-source-map-channel-samples','exec'),globals())
