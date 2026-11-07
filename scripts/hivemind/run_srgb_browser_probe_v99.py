"""One-shot browser GPU test after the specific native producer job completes."""
from pathlib import Path
from datetime import datetime,timezone
import ctypes,hashlib,http.server,json,os,subprocess,sys,threading,time
W=Path('F:/OpenRPG_ImportWork');O=W/'srgb-browser-producer-v99';R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
NATIVE=W/'async-srgb-producer-v98';CHROME=Path('C:/Program Files/Google/Chrome/Application/chrome.exe')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
 v['updated_utc']=datetime.now(timezone.utc).isoformat();p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),encoding='utf-8');p.replace(O/'RUN-RESULT.v1.json')
if len(sys.argv)==1:
 assert O.exists() and not (O/'JOB.v1.json').exists()
 with (O/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps({'controller_pid':child.pid,'one_shot':True,'not_a_scheduled_automation':True}),encoding='utf-8');print(json.dumps({'root':str(O),'controller_pid':child.pid}));sys.exit()
state={'status':'waiting_for_specific_native_gpu_job','browser_gpu_executed':False,'full_import':False};save(state)
server=None;browser=None
try:
 native=read(NATIVE/'RUN-RESULT.v1.json')
 if 'finished_utc' not in native:
  pid=native['cargo_pid'];kernel=ctypes.WinDLL('kernel32',use_last_error=True)
  kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p
  kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong];kernel.WaitForSingleObject.restype=ctypes.c_ulong
  kernel.CloseHandle.argtypes=[ctypes.c_void_p]
  handle=kernel.OpenProcess(0x100000,False,pid)
  if handle:
   try:
    state['wait_cargo_pid']=pid;save(state)
    while kernel.WaitForSingleObject(handle,1000)==258:pass
   finally:kernel.CloseHandle(handle)
  deadline=time.monotonic()+30
  while 'finished_utc' not in read(NATIVE/'RUN-RESULT.v1.json') and time.monotonic()<deadline:time.sleep(.25)
 native=read(NATIVE/'RUN-RESULT.v1.json');assert native.get('actual_exit_code')==0 and native.get('gpu_probe_executed'),native
 pins=read(O/'PREPARED.v1.json')['input_pins']
 for p,h in pins.items():assert sha(Path(p))==h,p
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
 command=[str(BIN/'cargo.exe'),'build','--offline','--manifest-path',str(O/'Cargo.toml'),'--target','wasm32-unknown-unknown']
 with (O/'build.stdout.log').open('xb') as out,(O/'build.stderr.log').open('xb') as err:
  child=subprocess.Popen(command,cwd=O,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
  state.update(status='building_actual_common_rust_browser_probe',cargo_pid=child.pid,build_command=command);save(state);state['build_exit_code']=child.wait();save(state);assert child.returncode==0
 wasm=W/'firepit-static-glb-product-check-v2/target/wasm32-unknown-unknown/debug/openrpg_srgb_browser_probe.wasm'
 command=[str(Path('C:/Users/karen/.cargo/bin/wasm-bindgen.exe')),str(wasm),'--target','web','--out-dir',str(O/'site/pkg')]
 result=subprocess.run(command,cwd=O,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE);(O/'bindgen.stdout.log').write_bytes(result.stdout);(O/'bindgen.stderr.log').write_bytes(result.stderr);state['bindgen_exit_code']=result.returncode;save(state);assert result.returncode==0
 for p,h in pins.items():assert sha(Path(p))==h,p
 state['wasm_sha256']=sha(wasm);state['linked_wasm_sha256']=sha(O/'site/pkg/openrpg_srgb_browser_probe_bg.wasm')
 done=threading.Event();receipt={}
 class Handler(http.server.SimpleHTTPRequestHandler):
  def __init__(self,*args,**kwargs):super().__init__(*args,directory=str(O/'site'),**kwargs)
  def log_message(self,*args):pass
  def do_POST(self):
   if self.path!='/receipt':self.send_error(404);return
   length=int(self.headers.get('Content-Length','0'))
   if not 0<length<65536:self.send_error(413);return
   try:value=json.loads(self.rfile.read(length))
   except Exception:self.send_error(400);return
   if done.is_set():self.send_error(409);return
   receipt.update(value);(O/'BROWSER-RESULT.v1.json').write_text(json.dumps(value,indent=2),encoding='utf-8');self.send_response(200);self.end_headers();self.wfile.write(b'OK');done.set()
 server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start();url=f'http://127.0.0.1:{server.server_port}/'
 profile=O/'chrome-profile';profile.mkdir();command=[str(CHROME),'--headless=new','--enable-unsafe-webgpu','--disable-background-networking','--disable-background-timer-throttling','--no-first-run','--no-default-browser-check',f'--user-data-dir={profile}','--timeout=120000','--dump-dom',url]
 with (O/'chrome.stdout.log').open('xb') as out,(O/'chrome.stderr.log').open('xb') as err:
  browser=subprocess.Popen(command,cwd=O,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
  state.update(status='actual_browser_gpu_probe_running',browser_pid=browser.pid,url=url,browser_command=command);save(state)
  deadline=time.monotonic()+130
  while not done.wait(.25):
   if browser.poll() is not None:raise RuntimeError('Browser exited before a GPU receipt')
   if time.monotonic()>deadline:raise RuntimeError('Browser GPU receipt timed out')
  state['browser_exit_code']=browser.wait(timeout=20)
 assert receipt.get('status')=='actual_common_rust_producer_calibrated_on_browser_webgpu',receipt
 assert receipt['browser_gpu_executed'] and receipt['backend']=='BrowserWebGpu' and len(receipt['curve_bytes'])==1024 and receipt['gpu_errors']==[] and receipt['pending_frame_polls']>0 and receipt['page_animation_frames']>0
 assert receipt['cloned_device_identity_equal'] and receipt['second_device_identity_differs']
 for p,h in pins.items():assert sha(Path(p))==h,p
 (O/'BROWSER-CURVE.r32f').write_bytes(bytes(receipt['curve_bytes']))
 state.update(status='actual_common_rust_producer_browser_gpu_probe_passed',browser_gpu_executed=True,source_inputs_unchanged=True,browser_receipt_sha256=sha(O/'BROWSER-RESULT.v1.json'),browser_curve_sha256=sha(O/'BROWSER-CURVE.r32f'),browser_material_draw_verified=False)
except Exception as e:state.update(status='browser_probe_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
finally:
 if browser is not None and browser.poll() is None:browser.terminate();browser.wait(timeout=15)
 if server is not None:server.shutdown();server.server_close()
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)
