"""Retain the first independent refusal and locate its exact native comparison."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_firepit_behavior_v5.py')
script=BASE.read_text('utf-8-sig')
old="ROOT=W/'unity-delta-firepit-particle-behavior-v5'"
assert script.count(old)==1
script=script.replace(old,"ROOT=W/'unity-delta-firepit-particle-behavior-v6'")
old="except Exception as error:state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))"
assert script.count(old)==1
script=script.replace(old,"except Exception as error:\n        import traceback\n        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error), diagnostic_traceback=traceback.format_exc())")
exec(compile(script,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
