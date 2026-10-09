from pathlib import Path
S=Path(__file__).parent
target=S/"reopen_townsmith_explicit_shadow_controls_v1.py"
assert not target.exists()
text=(S/"reopen_townsmith_door_pass_controls_v1.py").read_text(encoding="utf-8")
text=text.replace("townsmith-door-pass-controls-v1","townsmith-explicit-shadow-controls-v1").replace("townsmith-door-pass-controls-reopen-v1","townsmith-explicit-shadow-controls-reopen-v1")
text=text.replace("source_townsmith_door_pass_controls_captured_comparison_pending","source_townsmith_explicit_shadow_controls_captured_comparison_pending")
text=text.replace('len(report["rows"])==9','len(report["rows"])==6')
old='''        if case["control_mode"]=="visible_source_shadow":assert "original shadow renderer=True; child shadow=Off" in text
        else:assert "original shadow renderer=False; child shadow=On" in text'''
assert text.count(old)==1
text=text.replace(old,'''        assert "original shadow renderer=False; child shadow=Off" in text
        expected_groups=2 if case["control_mode"]=="explicit_full_shadow" else 1
        assert "explicit shadow groups="+str(expected_groups) in text''')
text=text.replace("nine_original_door_renderer_and_shadow_controls_reopened","six_explicit_door_shadow_geometry_controls_reopened")
text=text.replace("L03.TOWNSMITH.DESSINS.PASSES","L03.TOWNSMITH.DESSINS.OMBRES_EXPLICITES")
compile(text,str(target),"exec")
target.write_text(text,encoding="utf-8")
print(target)
