from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
candidate=S/'candidates/SourceTownsmithDescriptorSlotsV2.cs'
runner=S/'queue_source_townsmith_descriptor_slots_v2.py'
assert not candidate.exists() and not runner.exists()
cs=(S/'candidates/SourceTownsmithDescriptorSlotsV1.cs').read_text(encoding='utf-8').replace('OpenRPGSourceTownsmithDescriptorSlotsV1','OpenRPGSourceTownsmithDescriptorSlotsV2')
old='part.subMeshCount=1;part.SetSubMesh(0,sourceDescriptor,MeshUpdateFlags.DontRecalculateBounds);'
assert cs.count(old)==1
cs=cs.replace(old,'''// Reducing subMeshCount truncates Unity's index buffer. Keep its original length.
     part.SetSubMesh(0,sourceDescriptor,MeshUpdateFlags.DontRecalculateBounds);
     for(int unused=1;unused<part.subMeshCount;unused++)part.SetSubMesh(unused,new SubMeshDescriptor(0,0,mesh.GetTopology(unused)),MeshUpdateFlags.DontRecalculateBounds);''')
candidate.write_text(cs,encoding='utf-8')
text=(S/'queue_source_townsmith_descriptor_slots_v1.py').read_text(encoding='utf-8').replace('townsmith-descriptor-slot-controls-v1','townsmith-descriptor-slot-controls-v2').replace('SourceTownsmithDescriptorSlotsV1','SourceTownsmithDescriptorSlotsV2')
compile(text,str(runner),'exec')
runner.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(runner),'prepare'],check=True)
