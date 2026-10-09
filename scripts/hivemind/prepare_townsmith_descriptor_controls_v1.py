from pathlib import Path
import subprocess, sys
S=Path(__file__).parent
candidate=S/'candidates/SourceTownsmithDescriptorSlotsV1.cs'
runner=S/'queue_source_townsmith_descriptor_slots_v1.py'
assert not candidate.exists() and not runner.exists()
cs=(S/'candidates/SourceTownsmithExplicitSlotsV1.cs').read_text(encoding='utf-8')
cs=cs.replace('OpenRPGSourceTownsmithExplicitSlotsV1','OpenRPGSourceTownsmithDescriptorSlotsV1')
old='part.subMeshCount=1;part.SetIndices(mesh.GetIndices(submesh,true),mesh.GetTopology(submesh),0,false,0);part.bounds=mesh.bounds;ownedMeshes.Add(part);'
assert cs.count(old)==1
cs=cs.replace(old,'''var sourceDescriptor=mesh.GetSubMesh(submesh);
     part.subMeshCount=1;part.SetSubMesh(0,sourceDescriptor,MeshUpdateFlags.DontRecalculateBounds);part.bounds=mesh.bounds;ownedMeshes.Add(part);
     if(!part.GetIndices(0,true).SequenceEqual(mesh.GetIndices(submesh,true)))throw new Exception("Original indices changed");
     if(!part.vertices.SequenceEqual(mesh.vertices)||!part.normals.SequenceEqual(mesh.normals)||!part.tangents.SequenceEqual(mesh.tangents)||!part.uv.SequenceEqual(mesh.uv))throw new Exception("Original vertex attributes changed");
     if(!part.GetVertexAttributes().SequenceEqual(mesh.GetVertexAttributes()))throw new Exception("Vertex layout changed");
     File.WriteAllText(Path.Combine(root,item.id+".group-"+draw+".descriptor.txt"),"source indexStart="+sourceDescriptor.indexStart+"; indexCount="+sourceDescriptor.indexCount+"; baseVertex="+sourceDescriptor.baseVertex+"; indices and vertex attributes exact");''')
candidate.write_text(cs,encoding='utf-8')
text=(S/'queue_source_townsmith_explicit_slots_v1.py').read_text(encoding='utf-8')
text=text.replace('townsmith-explicit-slot-controls-v1','townsmith-descriptor-slot-controls-v1')
text=text.replace('SourceTownsmithExplicitSlotsV1','SourceTownsmithDescriptorSlotsV1')
text=text.replace("'isolated_triangle_grouping_controls':True","'isolated_triangle_grouping_controls':True,'original_index_descriptors_preserved':True")
compile(text,str(runner),'exec')
runner.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(runner),'prepare'],check=True)
