"""Compare authored times through Unity's declared float32 ABI, without tolerance."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_firepit_behavior_v5.py')
script=BASE.read_text('utf-8-sig')
old="ROOT=W/'unity-delta-firepit-particle-behavior-v5'"
assert script.count(old)==1
script=script.replace(old,"ROOT=W/'unity-delta-firepit-particle-behavior-v7'")
old="scoped=dict(request,particle_file_id=result['particle_file_id'],\n                renderer_file_id=case['renderer_file_id'],material_guid=case['material_guid'])"
assert script.count(old)==1
script=script.replace(old,"""scoped=dict(request,particle_file_id=result['particle_file_id'],
                renderer_file_id=case['renderer_file_id'],material_guid=case['material_guid'],
                times=[struct.unpack('<f',struct.pack('<f',t))[0] for t in request['times']])
            import copy
            bad=copy.deepcopy(result)
            row=next(r for r in bad['rows'] if r['requested_time']>0)
            bits=struct.unpack('<I',struct.pack('<f',row['requested_time']))[0]
            row['requested_time']=struct.unpack('<f',struct.pack('<I',bits+1))[0]
            try:verifier.check(bad,scoped,particle)
            except AssertionError:pass
            else:raise AssertionError('A one-ULP native time corruption was accepted')
            controls.setdefault('float32_time_controls',[]).append(dict(node=case['node_path'],
                wrong_native_time_one_ulp_refused=True, tolerance_used=False))
""")
old="except Exception as error:state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))"
assert script.count(old)==1
script=script.replace(old,"except Exception as error:\n        import traceback\n        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error), diagnostic_traceback=traceback.format_exc())")
exec(compile(script,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
