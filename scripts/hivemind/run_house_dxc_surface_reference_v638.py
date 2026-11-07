"""Isolate Unity shader compilation with DXC; retain all previous source outcomes."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_dx12_surface_reference_v636.py')
_base_control=parent
exec(compile(parent.read_text('utf-8-sig').rsplit('\nexec(compile(code,',1)[0],str(parent)+'#prepare-recipe','exec'),globals())
code=code.replace('for p in [parent,recipe,S/','for p in [_base_control,parent,recipe,S/')
code=code.replace('house-dx12-surface-reference-v636','house-dxc-surface-reference-v638').replace('HouseDx12SurfaceV636','HouseDxcSurfaceV638')
code=code.replace("ap=W/'house-float-transport-reference-v632/HELPER-OWNERSHIP.v1.json'","ap=W/'house-dx12-surface-reference-v636/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==28","len(audit['helpers'])==29")
assert code.count('#pragma target 4.5')==1;code=code.replace('#pragma target 4.5','#pragma target 4.5\n#pragma use_dxc')
needle="base.write_text((S/'candidates/HouseImplicitSurfaceV621.cs').read_text('utf-8-sig').replace('HouseImplicitSurfaceV621','HouseDxcSurfaceV638'),'utf-8');helper="
replacement="""helper_text=(S/'candidates/HouseImplicitSurfaceV621.cs').read_text('utf-8-sig').replace('HouseImplicitSurfaceV621','HouseDxcSurfaceV638')
    helper_text=helper_text.replace('[Serializable]public class Report{','[Serializable]public class SamplerProfile{public string material_guid,property,format,graphics_format,filter,wrap_u,wrap_v;public float mip_bias;public int aniso,mips,width,height;public bool readable;}\\n [Serializable]public class Report{public SamplerProfile[] sampler_profiles;public int global_mipmap_limit;')
    helper_text=helper_text.replace('var frames=new List<Frame>();','var frames=new List<Frame>();var profiles=new List<SamplerProfile>();')
    marker='   for(int t=0;t<r.tiles.Length;t++)'
    capture='''   foreach(var property in new[]{"_L1_Height","_L1_Albedo","_L1_Normal"}){var tex=source.GetTexture(property) as Texture2D;if(tex==null)throw new Exception("Texture missing");profiles.Add(new SamplerProfile{material_guid=guid,property=property,format=tex.format.ToString(),graphics_format=tex.graphicsFormat.ToString(),filter=tex.filterMode.ToString(),wrap_u=tex.wrapModeU.ToString(),wrap_v=tex.wrapModeV.ToString(),mip_bias=tex.mipMapBias,aniso=tex.anisoLevel,mips=tex.mipmapCount,width=tex.width,height=tex.height,readable=tex.isReadable});}
'''+marker
    assert helper_text.count(marker)==1;helper_text=helper_text.replace(marker,capture).replace('new Report{unity_version=','new Report{sampler_profiles=profiles.ToArray(),global_mipmap_limit=QualitySettings.globalTextureMipmapLimit,unity_version=')
    base.write_text(helper_text,'utf-8');helper="""
assert code.count(needle)==1;code=code.replace(needle,replacement)
code=code.replace('Unchanged generated surface, native compressed resources, exact HDRP macro and zero bias on Direct3D12; API comparison diagnostic, no mesh/depth/Studio qualification','DXC compilation diagnostic: original surface function and resources, explicit use_dxc pragma on DX12, zero global bias; compiler control and sampler profiles, not original compiler or mesh/Studio qualification')
exec(compile(code,str(parent)+'#dxc-compiler-control','exec'),globals())
