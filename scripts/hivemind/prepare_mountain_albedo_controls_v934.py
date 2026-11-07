"""Diagnostic white albedo on material clones; no replacement asset admission."""
from pathlib import Path
recipe=Path(__file__).with_name('prepare_mountain_vertex_color_controls_v928.py')
code=recipe.read_text('utf-8').replace('mountain-vertex-color-controls-v928','mountain-albedo-controls-v934').replace('V928','V934').replace('diagnostic_white_vertex_colors','diagnostic_white_main_texture')
code=code.replace('var diagnosticClones=new System.Collections.Generic.List<Mesh>();var sourceMeshGuards=new System.Collections.Generic.Dictionary<Mesh,string>();','var diagnosticClones=new System.Collections.Generic.List<Material>();var sourceMeshGuards=new System.Collections.Generic.Dictionary<Material,string>();')
start=code.index("    injection='''");end=code.index("'''\n    source=source.replace(anchor,injection+anchor)",start)+3
injection=r'''   if(request.diagnostic_white_main_texture) {
    var mapped=new System.Collections.Generic.Dictionary<Material,Material>();
    foreach(var renderer in selectedLodRenderers) {
     var materials=renderer.sharedMaterials;
     for(int slot=0;slot<materials.Length;slot++) {
      var original=materials[slot];if(original.shader.name.IndexOf("SpeedTree",StringComparison.OrdinalIgnoreCase)<0)continue;
      if(!mapped.ContainsKey(original)) {
       int property=original.shader.FindPropertyIndex("_MainTex");if(property<0||original.shader.GetPropertyType(property)!=UnityEngine.Rendering.ShaderPropertyType.Texture)throw new Exception("Source main texture contract absent");
       sourceMeshGuards[original]=EditorJsonUtility.ToJson(original,true);
       var clone=new Material(original);clone.hideFlags=HideFlags.HideAndDontSave;clone.name="DIAGNOSTIC_ONLY_WHITE_MAIN_TEXTURE";clone.SetTexture("_MainTex",Texture2D.whiteTexture);
       if(!clone.shaderKeywords.SequenceEqual(original.shaderKeywords)||clone.shader!=original.shader)throw new Exception("Diagnostic shader/keywords changed");
       for(int i=0;i<original.shader.GetPropertyCount();i++) {
        string name=original.shader.GetPropertyName(i);if(name=="_MainTex")continue;var type=original.shader.GetPropertyType(i);
        if((type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range||type==UnityEngine.Rendering.ShaderPropertyType.Int)&&clone.GetFloat(name)!=original.GetFloat(name))throw new Exception("Diagnostic changed source scalar");
        if(type==UnityEngine.Rendering.ShaderPropertyType.Color&&clone.GetColor(name)!=original.GetColor(name))throw new Exception("Diagnostic changed source color");
        if(type==UnityEngine.Rendering.ShaderPropertyType.Vector&&clone.GetVector(name)!=original.GetVector(name))throw new Exception("Diagnostic changed source vector");
        if(type==UnityEngine.Rendering.ShaderPropertyType.Texture&&clone.GetTexture(name)!=original.GetTexture(name))throw new Exception("Diagnostic changed another texture");
       }
       if(clone.GetTexture("_MainTex")!=Texture2D.whiteTexture)throw new Exception("Diagnostic texture not applied");
       for(int pass=0;pass<clone.passCount;pass++) {
        string name=clone.GetPassName(pass);if(clone.GetShaderPassEnabled(name)!=original.GetShaderPassEnabled(name))throw new Exception("Diagnostic pass state changed");
        if(!clone.GetShaderPassEnabled(name)||name.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
        ShaderUtil.CompilePass(clone,pass,true);if(!ShaderUtil.IsPassCompiled(clone,pass)||ShaderUtil.ShaderHasError(clone.shader))throw new Exception("Diagnostic source pass compilation failed");
       }
       mapped.Add(original,clone);diagnosticClones.Add(clone);
      }
      materials[slot]=mapped[original];
     }
     renderer.sharedMaterials=materials;
    }
    if(diagnosticClones.Count==0)throw new Exception("No diagnostic material applied");
   }
'''
code=code[:start]+"    injection="+repr(injection)+code[end:]
code=code.replace('diagnostic-white-colors','diagnostic-white-main-texture')
code=code.replace('Diagnostic white vertex colors on explicitly identified mesh clones only. Altered renders are not admissible library thumbnails or conversion evidence.','Diagnostic white main albedo on material clones, only SpeedTree MainTex replaced, other properties/pass state/shader preserved. Altered renders excluded from library admission and conversion evidence.')
exec(compile(code,str(recipe)+'#diagnostic-only-white-main-texture','exec'),globals())
