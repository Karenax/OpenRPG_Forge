"""Capture four native gradients plus interpolation and final X on frozen inputs."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_noise_intermediates_v547.py')
source=parent.read_text('utf-8').replace('v547','v558').replace('V547','V558')
old="new=\"auditpath=W/'pirate-palm-wind-native-reference-v541/HELPER-OWNERSHIP.v1.json'\""
new="new=\"auditpath=W/'pirate-palm-wind-native-reference-v547/HELPER-OWNERSHIP.v1.json'\""
assert source.count(old)==1;source=source.replace(old,new)
start=source.index("kernel='''");end=source.index("'''",start+10)
kernel='''[numthreads(64,1,1)] void CSMain(uint3 id:SV_DispatchThreadID){
if(id.x>=_Count)return;InputCase v=_Input[id.x];
float u=v.world_position.x*3+v.time.x;float2 ip=floor(float2(u,u)*_WindSpeed);
float2 g00=Unity_GradientNoise_LegacyMod_Dir_float(ip);
float2 g01=Unity_GradientNoise_LegacyMod_Dir_float(ip+float2(0,1));
float2 g10=Unity_GradientNoise_LegacyMod_Dir_float(ip+float2(1,0));
float2 g11=Unity_GradientNoise_LegacyMod_Dir_float(ip+float2(1,1));
float noise;Unity_GradientNoise_LegacyMod_float(float2(u,u),_WindSpeed,noise);
VertexDescriptionInputs i=(VertexDescriptionInputs)0;i.ObjectSpacePosition=v.object_position.xyz;i.WorldSpacePosition=v.world_position.xyz;i.ObjectSpaceNormal=v.normal.xyz;i.ObjectSpaceTangent=v.tangent.xyz;i.uv0=v.uv;i.TimeParameters=v.time.xyz;
VertexDescription r=VertexDescriptionFunction(i);
_Output[id.x*3]=float4(g00,g01);
_Output[id.x*3+1]=float4(g10,g11);
_Output[id.x*3+2]=float4(noise-.5,(noise-.5)*_WindStrength*v.uv.y,r.Position.x,r.Position.x-v.object_position.x);
}'''
source=source[:start]+"kernel='''"+kernel+source[end:]
source=source.replace("diagnostic_layout=['scaled_uv_x','scaled_uv_y','floor_x','floor_y','fract_x','fract_y','hash_stage0','hash_stage1','gradient00_x','gradient00_y','noise','wind_before_position_add']", "diagnostic_layout=['g00x','g00y','g01x','g01y','g10x','g10y','g11x','g11y','centered_noise','wind','position_x','optimized_wind_delta']")
exec(compile(source,str(parent)+'#four-native-gradients','exec'),globals())
