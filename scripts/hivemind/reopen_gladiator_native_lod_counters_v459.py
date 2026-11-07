"""Reconcile Unity visualization's vertex counter with captured submesh ranges."""
from pathlib import Path

parent=Path(__file__).resolve().with_name('reopen_gladiator_native_lod_metric_v458.py')
code=parent.read_text('utf-8').replace('gladiator-native-lod-metric-reopen-v458','gladiator-native-lod-metric-reopen-v459')
old="wanted_counts['vertexCount']+=m['vertex_count']"
new="wanted_counts['vertexCount']+=sum(p['source_submesh']['vertex_count'] for p in m['primitives'])"
assert code.count(old)==1
code=code.replace(old,new)
code=code.replace("assert seen==expected", "assert seen==expected\nassert not counter_differences, 'Native visualization counters must match captured source submesh ranges'\nprior_path=W/'gladiator-native-lod-metric-reopen-v458/RESULT.v1.json'\nprior=read(prior_path)\nassert len(prior['native_geometry_counter_differences'])==60 and all(r['counter']=='vertexCount' for r in prior['native_geometry_counter_differences'])")
code=code.replace("pins=dict(run['input_pins'])", "pins=dict(run['input_pins']);pins[str(parent)]=sha(parent);pins[str(prior_path)]=sha(prior_path)")
code=code.replace("native_geometry_counter_differences=counter_differences", "native_geometry_counter_differences=counter_differences, native_vertex_counter_semantics='sum_of_source_submesh_vertex_ranges', previous_unique_vertex_counter_differences_resolved=60")
code=code.replace("{len(counter_differences)} différences de compteurs géométriques natives sont conservées séparément.", "Tous les compteurs géométriques concordent. Les 60 écarts précédents du compteur vertexCount sont expliqués par la somme des plages de sommets de sous-mesh dans la visualisation Unity ; cette somme compte leurs recouvrements. Les buffers de géométrie n'ont pas changé.")
exec(compile(code,str(parent)+'#native-submesh-range-counter-v459','exec'),globals())
