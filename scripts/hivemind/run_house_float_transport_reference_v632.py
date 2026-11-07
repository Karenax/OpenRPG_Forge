"""Isolate decoded RGBA-float transport in the same Unity fragment renderer."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_hdrp_bias_surface_reference_v623.py')
exec(compile(parent.read_text('utf-8-sig').split('exec(compile(code,')[0],str(parent)+'#prepare-recipe','exec'),globals())
code=code.replace('house-hdrp-bias-surface-reference-v623','house-float-transport-reference-v632').replace('HouseImplicitSurfaceV623','HouseFloatTransportV632')
code=code.replace("ap=W/'house-implicit-surface-reference-v621/HELPER-OWNERSHIP.v1.json'","ap=W/'house-active-sampling-diagnostic-v628/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==24","len(audit['helpers'])==27")
needle="base.write_text((S/'candidates/HouseImplicitSurfaceV621.cs').read_text('utf-8-sig').replace('HouseImplicitSurfaceV621','HouseFloatTransportV632'),'utf-8');helper="
replacement="""helper_text=(S/'candidates/HouseImplicitSurfaceV621.cs').read_text('utf-8-sig').replace('HouseImplicitSurfaceV621','HouseFloatTransportV632')
    helper_text=helper_text.replace('[Serializable]public class Request{','[Serializable]public class Decoded{public string material_guid,property;public string[] levels;public int width,height;}\\n [Serializable]public class Request{public Decoded[] decoded_textures;')
    marker='   for(int t=0;t<r.tiles.Length;t++)'
    setup='''   var decoded=new Dictionary<string,Texture2D>();
   try{
   foreach(var d in r.decoded_textures){if(d.material_guid!=guid)continue;var native=source.GetTexture(d.property) as Texture2D;if(native==null||native.width!=d.width||native.height!=d.height||native.mipmapCount!=d.levels.Length)throw new Exception("Native dimensions differ");
    var replacement=new Texture2D(d.width,d.height,TextureFormat.RGBAFloat,true,true);byte[] data;using(var stream=new MemoryStream()){foreach(var path in d.levels){var bytes=File.ReadAllBytes(path);stream.Write(bytes,0,bytes.Length);}data=stream.ToArray();}replacement.LoadRawTextureData(data);replacement.Apply(false,false);replacement.filterMode=native.filterMode;replacement.wrapModeU=native.wrapModeU;replacement.wrapModeV=native.wrapModeV;replacement.anisoLevel=native.anisoLevel;decoded.Add(d.property,replacement);
   }
   if(decoded.Count!=3)throw new Exception("Three decoded resources required");
'''+marker
    assert helper_text.count(marker)==1;helper_text=helper_text.replace(marker,setup)
    helper_text=helper_text.replace('probe.CopyPropertiesFromMaterial(source);','probe.CopyPropertiesFromMaterial(source);foreach(var d in decoded)probe.SetTexture(d.Key,d.Value);')
    close='   }\\n  }\\n  File.WriteAllText'
    assert helper_text.count(close)==1;helper_text=helper_text.replace(close,'   }\\n   }finally{foreach(var d in decoded)UnityEngine.Object.DestroyImmediate(d.Value);}\\n  }\\n  File.WriteAllText')
    base.write_text(helper_text,'utf-8');helper="""
assert code.count(needle)==1;code=code.replace(needle,replacement)
needle="    for p in [Path(__file__),checkpoint,ap,source,generated,controlled,dst,base,helper,ap2,request,"
addition="""    materials=read(W/'house-hdrp-bias-surface-gpu-comparison-v624/GPU-REQUEST.v1.json')['materials'];decoded=[]
    for m in materials:
        for property_name,texture in zip(['_L1_Height','_L1_Albedo','_L1_Normal'],m['textures']):
            levels=texture['levels'];assert len(levels)==12
            decoded.append(dict(material_guid=m['guid'],property=property_name,width=levels[0]['width'],height=levels[0]['height'],levels=[level['path'] for level in levels]))
    assert len(decoded)==18
    rq=read(request);rq['decoded_textures']=decoded;request.write_text(json.dumps(rq,indent=2),'utf-8')
    for p in [parent,W/'house-hdrp-bias-surface-gpu-comparison-v624/GPU-REQUEST.v1.json']:pins[str(p)]=sha(p)
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('Generated surface shader with exact HDRP macro and explicit zero-bias fixture on controlled plane; no prefab mesh, lighting/shadows, arbitrary scene bias or depth-host qualification','Transport diagnostic: same Unity shader with GPU-decoded RGBAFloat mip chains replacing BC resources on the probe material only; no asset qualification')
exec(compile(code,str(parent)+'#float-transport-reference','exec'),globals())
