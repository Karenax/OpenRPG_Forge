"""Extend the exact parameter test with clone-current and original-repeat controls."""
from pathlib import Path

recipe=Path(__file__).with_name('prepare_mountain_parameter_comparison_v901.py')
code=recipe.read_text('utf-8').replace('v901','v904').replace('V901','V904')
changes={
    'public bool restore_original_values; public MaterialComparison[] comparisons;':
        'public bool restore_original_values,clone_current_values; public MaterialComparison[] comparisons;',
    'if(request.restore_original_values){clone=new Material(material);clone.hideFlags=HideFlags.HideAndDontSave;comparisonClones.Add(clone);clone.shaderKeywords=entry.original_keywords;}':
        'if(request.restore_original_values||request.clone_current_values){clone=new Material(material);clone.hideFlags=HideFlags.HideAndDontSave;comparisonClones.Add(clone);if(request.restore_original_values)clone.shaderKeywords=entry.original_keywords;}',
    'if(clone!=null)clone.SetFloat(name,value);':
        'if(clone!=null&&request.restore_original_values)clone.SetFloat(name,value);',
    'if(clone!=null){clone.SetFloat(scalar.name,scalar.value);':
        'if(clone!=null&&request.restore_original_values){clone.SetFloat(scalar.name,scalar.value);',
    "for label, restore in [('warmup',False),('current',False),('original-settings',True),('repeat',False)]:":
        "for label, restore, clone in [('warmup',False,False),('current',False,False),('clone-current',False,True),('original-settings',True,False),('original-repeat',True,False),('repeat',False,False)]:",
    'restore_original_values=restore,comparisons=selected,':
        'restore_original_values=restore,clone_current_values=clone,comparisons=selected,',
    'capture_cases=188': 'capture_cases=282',
    'cases=188,materials=': 'cases=282,materials=',
}
for before,after in changes.items():
    assert before in code,before
    code=code.replace(before,after)
exec(compile(code,str(recipe)+'#clone-current-and-original-repeat', 'exec'),globals())
