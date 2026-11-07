"""Compare original authored inputs after official SDK-derived state rebuilding."""
from pathlib import Path

wrapper_path=Path(__file__).with_name('prepare_mountain_clone_control_v904.py')
wrapper=wrapper_path.read_text('utf-8')
anchor="exec(compile(code,str(recipe)+'#clone-current-and-original-repeat', 'exec'),globals())"
assert wrapper.count(anchor)==1
adapt=r'''
code=code.replace('v904','v908').replace('V904','V908')
code=code.replace('public bool restore_original_values,clone_current_values;', 'public bool restore_original_values,clone_current_values,apply_sdk_derived_state;')
code=code.replace('public Scalar[] changed_floats;', 'public Scalar[] changed_floats,original_author_floats;')
code=code.replace('public bool restored_original_values; public Scalar[] shader_defaults;', 'public bool restored_original_values,sdk_derived_state_applied,authored_inputs_preserved; public int active_author_float_count; public Scalar[] shader_defaults; public string sdk_material_state;')
before='                                 added_float_names=added)'
assert before in code
code=code.replace(before, "                                 added_float_names=added, original_author_floats=[dict(name=key,value=value) for key,value in before.items() if key not in {'_MaterialID','_MaterialTypeMask'} and not key.startswith('_Stencil')])")
before="for label, restore, clone in [('warmup',False,False),('current',False,False),('clone-current',False,True),('original-settings',True,False),('original-repeat',True,False),('repeat',False,False)]:"
assert before in code
code=code.replace(before, "for label, restore, clone, adapt_sdk in [('warmup',False,False,False),('current',False,False,False),('clone-current',False,True,False),('sdk-original-settings',True,False,True),('sdk-original-repeat',True,False,True),('repeat',False,False,False)]:")
code=code.replace('clone_current_values=clone,comparisons=selected,', 'clone_current_values=clone,apply_sdk_derived_state=adapt_sdk,comparisons=selected,')
before='     if(clone!=null) {\n      int replaced=0;'
assert before in code
after="""     int authorCount=0;bool sdkApplied=false;string sdkState=null;
     if(clone!=null&&request.apply_sdk_derived_state) {
      foreach(var input in entry.original_author_floats)if(clone.HasProperty(input.name)) {
       if(clone.GetFloat(input.name)!=input.value)throw new Exception(\"Authored value already differs before SDK derivation: \"+input.name);authorCount++;
      }
      if(!UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(clone))throw new Exception(\"Official SDK reset refused this source shader\");
      foreach(var input in entry.original_author_floats)if(clone.HasProperty(input.name)&&clone.GetFloat(input.name)!=input.value)throw new Exception(\"Official SDK changed an authored value: \"+input.name);
      sdkApplied=true;sdkState=EditorJsonUtility.ToJson(clone,true);
     }
     if(clone!=null) {
      int replaced=0;"""
code=code.replace(before,after)
code=code.replace('restored_original_values=request.restore_original_values,shader_defaults=observed.ToArray()', 'restored_original_values=request.restore_original_values,sdk_derived_state_applied=sdkApplied,authored_inputs_preserved=sdkApplied,active_author_float_count=authorCount,sdk_material_state=sdkState,shader_defaults=observed.ToArray()')
before='    O.mkdir(exist_ok=False)'
assert before in code
sdk="F:/OpenRPG_ImportWork/mountain-hdrp-capture-stage-v869/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2"
addition="    sdk=Path("+repr(sdk)+")\n    for relative in ['Editor/RenderPipeline/HDShaderUtils.cs','Runtime/Material/Lit/BaseLitAPI.cs','Runtime/Material/Lit/LitAPI.cs','Editor/AssetProcessors/MaterialPostProcessor.cs']:\n        path=sdk/relative; pins[str(path)]=sha(path)\n"
code=code.replace(before,addition+before)
code=code.replace('Original serialized scalar values and keywords versus migrated settings under installed SDK; new properties use measured current shader defaults. No original SDK parity claim.', 'Original serialized authored inputs with derived keywords/passes/stencil rebuilt by official installed HDRP API on clones; every active original author scalar guarded. No original SDK parity claim.')
'''
wrapper=wrapper.replace(anchor,adapt+'\n'+anchor)
exec(compile(wrapper,str(wrapper_path)+'#official-sdk-derivation', 'exec'),globals())
