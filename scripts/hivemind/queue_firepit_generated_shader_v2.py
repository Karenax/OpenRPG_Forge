"""Use the current SDK's four-argument ShaderGraphImporter test entrypoint."""
from pathlib import Path
BASE_GENERATED = Path(__file__).with_name('queue_firepit_generated_shader_v1.py')
source = BASE_GENERATED.read_text('utf-8').replace('firepit-generated-shader-v1','firepit-generated-shader-v2').replace('SourceGeneratedShaderV1','SourceGeneratedShaderV2')
source = source.replace('m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string)', 'm.GetParameters().Length==4&&m.GetParameters()[0].ParameterType==typeof(string)&&m.GetParameters()[3].IsOut')
source = source.replace('S_MasterProps.shadergraph",null};','S_MasterProps.shadergraph",null,null,null};')
exec(compile(source,str(BASE_GENERATED),'exec'),globals())
