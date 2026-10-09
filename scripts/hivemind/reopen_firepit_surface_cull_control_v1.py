from pathlib import Path
BASE_CONTROL=Path(__file__).with_name('reopen_firepit_surface_native_v1.py')
source=BASE_CONTROL.read_text('utf-8').replace("CAP=W/'firepit-surface-native-v1'","CAP=W/'firepit-surface-cull-control-v1'").replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-surface-cull-control-case-v1'").replace('firepit-surface-native-reopen-v1','firepit-surface-cull-control-reopen-v1')
source=source.replace("result=dict(status=", "result=dict(private_no_cull_control=True,authored_cull_state_qualified=False,private_control_not_delivered=True,status=")
exec(compile(source,str(BASE_CONTROL),'exec'),globals())
