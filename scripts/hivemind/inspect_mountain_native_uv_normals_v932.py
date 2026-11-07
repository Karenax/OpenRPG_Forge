"""Reuse the read-only native probe with UV/normal/tangent measurements."""
from pathlib import Path
recipe=Path(__file__).with_name('inspect_mountain_native_vertices_v924.py')
code=recipe.read_text('utf-8').replace('mountain-native-vertex-diagnostic-v924','mountain-native-uv-normal-diagnostic-v932').replace('V924','V932')
code=code.replace("'MountainVertexDiagnosticV932.cs'}", "'MountainVertexDiagnosticV924.cs','MountainVertexColorControlsV928.cs','MountainVertexDiagnosticV932.cs'}")
anchor='public Vector4 color_min,color_max;public MaterialRow material;'
assert code.count(anchor)==1
code=code.replace(anchor,'public Vector4 color_min,color_max;public int normal_count,tangent_count,zero_normals,zero_tangents;public float normal_length_min,normal_length_max,tangent_length_min,tangent_length_max;public Vector2[] vertex_uv0,triangle_centroid_uv0;public int[] uv_channel_counts;public MaterialRow material;')
anchor='    var slots=new List<Slot>();'
assert code.count(anchor)==1
code=code.replace(anchor,'''    var uv0=new List<Vector2>();mesh.GetUVs(0,uv0);var normals=new List<Vector3>();mesh.GetNormals(normals);var tangents=new List<Vector4>();mesh.GetTangents(tangents);
    if(uv0.Count!=mesh.vertexCount||normals.Count!=mesh.vertexCount||tangents.Count!=mesh.vertexCount)throw new Exception("Required source UV/normal/tangent stream missing");
    var uvCounts=new int[8];for(int channel=0;channel<8;channel++){var values=new List<Vector4>();mesh.GetUVs(channel,values);uvCounts[channel]=values.Count;}
'''+anchor)
anchor='     var lo=new Vector4(float.PositiveInfinity'
assert code.count(anchor)==1
code=code.replace(anchor,'''     var indices=mesh.GetIndices(submesh);if(indices.Length%3!=0||mesh.GetTopology(submesh)!=UnityEngine.MeshTopology.Triangles)throw new Exception("Triangle source required");
     var centroids=new List<Vector2>();for(int i=0;i<indices.Length;i+=3)centroids.Add((uv0[indices[i]]+uv0[indices[i+1]]+uv0[indices[i+2]])/3f);
     var normalLengths=vertices.Select(v=>normals[v].magnitude).ToArray();var tangentLengths=vertices.Select(v=>new Vector3(tangents[v].x,tangents[v].y,tangents[v].z).magnitude).ToArray();
     if(normalLengths.Any(v=>!float.IsFinite(v))||tangentLengths.Any(v=>!float.IsFinite(v))||vertices.Any(v=>!float.IsFinite(uv0[v].x)||!float.IsFinite(uv0[v].y)))throw new Exception("Nonfinite source attributes");
'''+anchor)
anchor='new Slot{submesh=submesh,'
assert code.count(anchor)==1
code=code.replace(anchor,'new Slot{normal_count=vertices.Length,tangent_count=vertices.Length,zero_normals=normalLengths.Count(v=>v==0),zero_tangents=tangentLengths.Count(v=>v==0),normal_length_min=normalLengths.Min(),normal_length_max=normalLengths.Max(),tangent_length_min=tangentLengths.Min(),tangent_length_max=tangentLengths.Max(),vertex_uv0=vertices.Select(v=>uv0[v]).ToArray(),triangle_centroid_uv0=centroids.ToArray(),uv_channel_counts=uvCounts,submesh=submesh,')
code=code.replace('native_speedtree_vertices_and_material_properties_measured','native_speedtree_uv_normal_tangent_streams_measured')
exec(compile(code,str(recipe)+'#source-uv-normal-tangent-measurements','exec'),globals())
