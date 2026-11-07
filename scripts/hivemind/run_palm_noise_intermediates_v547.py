"""Retain exact Unity Forward helpers and capture noise intermediates on frozen inputs."""
from pathlib import Path
parent=Path(__file__).with_name('run_pirate_palm_wind_reference_v541.py')
code=parent.read_text('utf-8').replace('v541','v547').replace('V541','V547')
old="auditpath=W/'pirate-palm-native-contract-v535/HELPER-OWNERSHIP.v1.json'"
new="auditpath=W/'pirate-palm-wind-native-reference-v541/HELPER-OWNERSHIP.v1.json'"
assert code.count(old)==1;code=code.replace(old,new)
start=code.index('[numthreads(64,1,1)] void CSMain(')
end=code.index("\n'''",start)
kernel='''[numthreads(64,1,1)] void CSMain(uint3 id:SV_DispatchThreadID){
if(id.x>=_Count)return;InputCase v=_Input[id.x];
float u=v.world_position.x*3+v.time.x;
float2 p=float2(u,u)*_WindSpeed;
float2 ip=floor(p);float2 fp=frac(p);
float2 reduced=ip % 289;
float h0=float(34*reduced.x+1)*reduced.x % 289+reduced.y;
float h1=(34*h0+1)*h0 % 289;
float2 dir=Unity_GradientNoise_LegacyMod_Dir_float(ip);
float noise;Unity_GradientNoise_LegacyMod_float(float2(u,u),_WindSpeed,noise);
_Output[id.x*3]=float4(p,ip);
_Output[id.x*3+1]=float4(fp,h0,h1);
_Output[id.x*3+2]=float4(dir,noise,(noise-.5)*_WindStrength*v.uv.y);
}'''
code=code[:start]+kernel+code[end:]
code=code.replace("for p in [Path(__file__),proofpath", "for p in [parent,Path(__file__),proofpath")
code=code.replace("source_pass='Forward'", "diagnostic_layout=['scaled_uv_x','scaled_uv_y','floor_x','floor_y','fract_x','fract_y','hash_stage0','hash_stage1','gradient00_x','gradient00_y','noise','wind_before_position_add'],source_pass='Forward'")
code=code.replace('native_forward_wind_gpu_reference','native_noise_intermediate_gpu_reference')
exec(compile(code,str(parent)+'#source-noise-intermediates','exec'),globals())
