"""Record actual shader property bindings alongside the unchanged original scene."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
target=s/'queue_townsmith_effective_bindings_v35.py';assert not target.exists()
q=(s/'queue_townsmith_source_graph_probe_v34.py').read_text(encoding='utf-8')
q=q.replace('v34','v35').replace('V34','V35').replace('townsmith-door-source-graph-probe-v35','townsmith-door-source-effective-bindings-v35')
anchor='sourceUvPass.probeMaterial.CopyPropertiesFromMaterial(materials[0]);'
assert q.count(anchor)==1
capture='''
    var sourceProperties=new List<SourceEffectivePropertyV35>();var sourceShader=materials[0].shader;
    for(int property=0;property<sourceShader.GetPropertyCount();property++){
      var kind=sourceShader.GetPropertyType(property);string name=sourceShader.GetPropertyName(property);
      var record=new SourceEffectivePropertyV35{name=name,type=kind.ToString(),flags=sourceShader.GetPropertyFlags(property).ToString()};
      if(kind==ShaderPropertyType.Texture){
       var texture=materials[0].GetTexture(name);record.textureName=texture==null?null:texture.name;record.defaultTextureName=sourceShader.GetPropertyTextureDefaultName(property);
       record.scale=materials[0].GetTextureScale(name);record.offset=materials[0].GetTextureOffset(name);
       if(texture!=null){record.width=texture.width;record.height=texture.height;record.format=texture.graphicsFormat.ToString();record.path=AssetDatabase.GetAssetPath(texture);if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long localId)){record.guid=guid;record.fileId=localId.ToString();}}
      }else if(kind==ShaderPropertyType.Float||kind==ShaderPropertyType.Range)record.scalar=materials[0].GetFloat(name);
      else if(kind==ShaderPropertyType.Int)record.scalar=materials[0].GetInteger(name);
      else if(kind==ShaderPropertyType.Color)record.vector=materials[0].GetColor(name);
      else if(kind==ShaderPropertyType.Vector)record.vector=materials[0].GetVector(name);
      sourceProperties.Add(record);
    }
    File.WriteAllText(Path.Combine(root,item.id+".effective-shader-properties.json"),JsonUtility.ToJson(new SourceEffectivePropertiesV35{materialIdentity=Identity(sourceMaterials[0]),shaderIdentity=Identity(sourceShader),properties=sourceProperties.ToArray()},true));
'''
q=q.replace(anchor,anchor+capture.replace('\n','\\n'))
anchor="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert q.count(anchor)==1
classes='''
[Serializable] public class SourceEffectivePropertyV35 {public string name,type,flags,textureName,defaultTextureName,guid,fileId,path,format;public int width,height;public float scalar;public Vector4 vector;public Vector2 scale,offset;}
[Serializable] public class SourceEffectivePropertiesV35 {public string materialIdentity,shaderIdentity;public SourceEffectivePropertyV35[] properties;public bool actual_runtime_material_properties=true,full_import=false;}
'''
q=q.replace(anchor,'    text+='+repr(classes)+'\n'+anchor)
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
