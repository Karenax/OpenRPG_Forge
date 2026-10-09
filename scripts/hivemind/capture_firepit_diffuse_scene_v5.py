"""Supply source normal/tangent/UV attributes for the reference receiver."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_diffuse_scene_v4.py')
source=BASE.read_text('utf-8').replace('firepit-diffuse-scene-v4','firepit-diffuse-scene-v5')
attributes='''let source_normals=match receiver_mesh.attribute(Mesh::ATTRIBUTE_NORMAL).unwrap(){bevy::mesh::VertexAttributeValues::Float32x3(v)=>v.iter().map(|n|reflection.transform_vector3(Vec3::from_array(*n)).to_array()).collect::<Vec<_>>(),_=>panic!("Receiver normals")}; let source_tangents=match receiver_mesh.attribute(Mesh::ATTRIBUTE_TANGENT).unwrap(){bevy::mesh::VertexAttributeValues::Float32x4(v)=>v.iter().map(|t|{let a=reflection.transform_vector3(Vec3::new(t[0],t[1],t[2]));[a.x,a.y,a.z,-t[3]]}).collect::<Vec<_>>(),_=>panic!("Receiver tangents")}; let source_uv=match receiver_mesh.attribute(Mesh::ATTRIBUTE_UV_0).unwrap(){bevy::mesh::VertexAttributeValues::Float32x2(v)=>v.iter().map(|uv|[uv[0],uv[1],0.,0.]).collect::<Vec<_>>(),_=>panic!("Receiver UV")}; receiver_mesh.insert_attribute(SOURCE_GRAPH_NORMAL,source_normals);receiver_mesh.insert_attribute(SOURCE_GRAPH_TANGENT,source_tangents);receiver_mesh.insert_attribute(SOURCE_GRAPH_UV0,source_uv);'''
needle="exec(compile(source,str(BASE),'exec'),globals())"
assert source.count(needle)==1
source=source.replace(needle,"source=source.replace('receiver_mesh.generate_tangents()?;','receiver_mesh.generate_tangents()?;'+"+repr(attributes)+")\n"+needle)
exec(compile(source,str(BASE),'exec'),globals())
