"""Build the real workspace Web GLB example and run it against a local allowlist."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import http.server
import json
import os
import shutil
import subprocess
import sys
import threading
import time

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
O = W / 'browser-source-glb-v170'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
CHROME = Path('C:/Program Files/Google/Chrome/Application/chrome.exe')
NATIVE = W / 'townsmith-deferred-indirect-layer-0-v94'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(state):
    state['updated_utc'] = datetime.now(timezone.utc).isoformat()
    temporary = O / 'RUN-RESULT.tmp'
    temporary.write_text(json.dumps(state, indent=2), encoding='utf-8')
    temporary.replace(O / 'RUN-RESULT.v1.json')


if len(sys.argv) == 1:
    assert O.exists() and not (O / 'JOB.v1.json').exists()
    native = read(NATIVE / 'RUN-RESULT.v1.json')
    assert native['actual_exit_code'] == 0 and native['gpu_draw_executed']
    assert sha(NATIVE / 'NATIVE-RESULT.v1.json') == native['native_receipt_sha256']
    input_data = read(NATIVE / 'INPUT.v1.json')
    for path_key, sha_key in [('glb', 'glb_sha256'), ('brdf_path', 'brdf_sha256'),
                              ('sky_path', 'sky_sha256'), ('source_context', 'source_context_sha256')]:
        assert sha(Path(input_data[path_key])) == input_data[sha_key], input_data[path_key]
    input_data['context'] = read(Path(input_data['source_context']))
    (O / 'INPUT.v1.json').write_text(json.dumps(input_data, indent=2) + '\n', encoding='utf-8')
    site = O / 'site'
    site.mkdir()
    (O / 'temp').mkdir()
    (site / 'index.html').write_text('''<!doctype html><html lang="fr"><meta charset="utf-8">
<title>GLB source — contrôle WebGPU</title><h1>GLB source — contrôle WebGPU</h1>
<p id="state">Chargement du vrai chargeur Rust et des ressources locales…</p><pre id="result"></pre>
<script type="module">
import init,{run_glb_probe} from './pkg/verify_browser_source_glb_v141.js';
let frames=0,running=true;function heartbeat(){frames++;if(running)requestAnimationFrame(heartbeat);}requestAnimationFrame(heartbeat);
try{
 await init();
 const [input,glb,brdf,sky]=await Promise.all([fetch('/input.json').then(r=>r.text()),fetch('/case.glb').then(r=>r.arrayBuffer()),fetch('/brdf.bin').then(r=>r.arrayBuffer()),fetch('/sky.bin').then(r=>r.arrayBuffer())]);
 document.querySelector('#state').textContent='Chargeur, matériau et récupération GPU en cours';
 const result=JSON.parse(await run_glb_probe(input,new Uint8Array(glb),new Uint8Array(brdf),new Uint8Array(sky)));
 const packed=new Uint8Array(result.packed_image);delete result.packed_image;
 const digest=await crypto.subtle.digest('SHA-256',packed);result.packed_image_sha256=Array.from(new Uint8Array(digest),x=>x.toString(16).padStart(2,'0')).join('');
 result.packed_image_bytes=packed.length;result.page_animation_frames=frames;
 const saved=await fetch('/packed',{method:'POST',headers:{'Content-Type':'application/octet-stream'},body:packed});if(!saved.ok)throw new Error('GPU buffer delivery refused');
 document.querySelector('#state').textContent='Contrôle terminé';document.querySelector('#result').textContent=JSON.stringify({...result,material_curve:result.material_curve.length,recovered_material_curve:result.recovered_material_curve.length},null,2);
 await fetch('/receipt',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(result)});
}catch(error){await fetch('/receipt',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({status:'browser_glb_probe_refusal',error:String(error),page_animation_frames:frames,full_import:false})});}
finally{running=false;}
</script></html>''', encoding='utf-8')
    files = [Path(__file__), R / 'Cargo.toml', R / 'Cargo.lock', R / 'rust-toolchain.toml',
             O / 'INPUT.v1.json', site / 'index.html', NATIVE / 'INPUT.v1.json',
             NATIVE / 'RUN-RESULT.v1.json', NATIVE / 'NATIVE-RESULT.v1.json',
             *[Path(input_data[key]) for key in ['glb', 'source_context', 'brdf_path', 'sky_path']],
             W / 'townsmith-deferred-indirect-reopen-v94/RESULT.v1.json',
             W / 'srgb-browser-producer-reopen-v111/RESULT.v1.json',
             Path('F:/OpenRPG_AssetVault/metadata/hivemind/townsmith.intake.v1.json')]
    for folder in [R / 'crates', R / 'third_party']:
        files.extend(path for path in folder.rglob('*') if path.is_file()
                     and path.suffix in ['.rs', '.wgsl', '.toml']
                     and not any(part in ['target', '.git'] for part in path.relative_to(folder).parts))
    pins = {str(path): sha(path) for path in sorted(set(files))}
    epoch = O / 'source-epoch'
    for path_string, digest in list(pins.items()):
        path = Path(path_string)
        if path.is_relative_to(R):
            snapshot = epoch / path.relative_to(R)
            snapshot.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, snapshot)
            assert sha(snapshot) == digest
            pins[str(snapshot)] = digest
    (O / 'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,
        lot='L01.SRGB.WEB.BINDING.GLB', source_asset_bytes_embedded_in_repository=False,
        purchased_data_served_only_from_loopback_allowlist=True,
        source_context_and_images_unchanged=True, full_import=False), indent=2) + '\n', encoding='utf-8')
    save(dict(status='actual_common_browser_glb_probe_prepared', browser_gpu_executed=False, full_import=False))
    with (O / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=O, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
            creationflags=subprocess.CREATE_NO_WINDOW)
    (O / 'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid, one_shot=True)), encoding='utf-8')
    print(json.dumps(dict(controller_pid=child.pid, root=str(O), source_pins=len(pins))))
    sys.exit()

state = read(O / 'RUN-RESULT.v1.json')
server = browser = None
try:
    pins = read(O / 'PREPARED.v1.json')['input_pins']
    for path, expected in pins.items():
        assert sha(Path(path)) == expected, path
    env = os.environ.copy()
    env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'),
               RUSTDOC=str(BIN / 'rustdoc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', RUSTFLAGS='-Cdebuginfo=0',
               CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'),
               TEMP=str(O / 'temp'), TMP=str(O / 'temp'))
    command = [str(BIN / 'cargo.exe'), 'build', '--locked', '--offline', '-p', 'forge_engine',
               '--example', 'verify_browser_source_glb_v141', '--target', 'wasm32-unknown-unknown',
               '--no-default-features', '--features', 'web,private-source-fixtures']
    with (O / 'build.stdout.log').open('xb') as output, (O / 'build.stderr.log').open('xb') as error:
        child = subprocess.Popen(command, cwd=R, env=env, stdout=output, stderr=error,
                                 stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='building_actual_workspace_browser_glb_probe', cargo_pid=child.pid, build_command=command)
        save(state)
        state['build_exit_code'] = child.wait()
    assert state['build_exit_code'] == 0, 'Workspace Web example compilation refused'
    wasm = W / 'firepit-static-glb-product-check-v2/target/wasm32-unknown-unknown/debug/examples/verify_browser_source_glb_v141.wasm'
    command = ['C:/Users/karen/.cargo/bin/wasm-bindgen.exe', str(wasm), '--target', 'web', '--out-dir', str(O / 'site/pkg')]
    result = subprocess.run(command, cwd=O, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    (O / 'bindgen.stdout.log').write_bytes(result.stdout)
    (O / 'bindgen.stderr.log').write_bytes(result.stderr)
    state.update(bindgen_exit_code=result.returncode, wasm_sha256=sha(wasm))
    assert result.returncode == 0
    for path, expected in pins.items():
        assert sha(Path(path)) == expected, path
    # Build independently; serialize the actual GPU draw after owned Unity jobs.
    import ctypes
    ps = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
    predecessor_command = subprocess.check_output([ps, '-NoProfile', '-Command',
        '(Get-CimInstance Win32_Process -Filter "ProcessId=33132").CommandLine'], text=True).strip()
    if predecessor_command:
        assert 'queue_pirate_thumbnail_batch_v164.py run' in predecessor_command
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x100000, False, 33132)
        assert handle
        state.update(status='workspace_web_glb_built_waiting_for_exact_native_queue', waiting_pid=33132)
        save(state)
        try:
            assert kernel.WaitForSingleObject(handle, 0xffffffff) == 0
        finally:
            kernel.CloseHandle(handle)
    predecessor = read(W / 'pirate-source-thumbnail-batch-0113-0120-v163/QUEUE-RESULT.v1.json')
    assert predecessor.get('finished_utc') and predecessor['status'] in ['capture_terminal_review_required', 'pirate_capture_queue_refusal_retained']
    assert subprocess.check_output([ps, '-NoProfile', '-Command', '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
    for path, expected in pins.items():
        assert sha(Path(path)) == expected, path
    input_data = read(O / 'INPUT.v1.json')
    allowlist = {'/input.json': O / 'INPUT.v1.json', '/case.glb': Path(input_data['glb']),
                 '/brdf.bin': Path(input_data['brdf_path']), '/sky.bin': Path(input_data['sky_path'])}
    receipt = {}
    done = threading.Event()

    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(O / 'site'), **kwargs)

        def log_message(self, *args):
            pass

        def do_GET(self):
            if self.path in allowlist:
                path = allowlist[self.path]
                self.send_response(200)
                self.send_header('Content-Type', 'application/json' if self.path == '/input.json' else 'application/octet-stream')
                self.send_header('Content-Length', str(path.stat().st_size))
                self.end_headers()
                with path.open('rb') as stream:
                    shutil.copyfileobj(stream, self.wfile)
            else:
                super().do_GET()

        def do_POST(self):
            length = int(self.headers.get('Content-Length', '0'))
            if self.path == '/packed' and length == 512 * 512 * 4 and not (O / 'BROWSER-PACKED.rg11b10').exists():
                (O / 'BROWSER-PACKED.rg11b10').write_bytes(self.rfile.read(length))
            elif self.path == '/receipt' and 0 < length < 131072 and not done.is_set():
                value = json.loads(self.rfile.read(length))
                receipt.update(value)
                (O / 'BROWSER-RESULT.v1.json').write_text(json.dumps(value, indent=2), encoding='utf-8')
                done.set()
            else:
                self.send_error(400)
                return
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'OK')

    server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f'http://127.0.0.1:{server.server_port}/'
    profile = O / 'chrome-profile'
    profile.mkdir()
    command = [str(CHROME), '--headless=new', '--enable-unsafe-webgpu', '--disable-background-networking',
               '--disable-background-timer-throttling', '--disable-renderer-backgrounding',
               '--no-first-run', '--no-default-browser-check', f'--user-data-dir={profile}',
               '--remote-debugging-port=0', '--enable-logging=stderr', '--v=0', url]
    with (O / 'chrome.stdout.log').open('xb') as output, (O / 'chrome.stderr.log').open('xb') as error:
        browser = subprocess.Popen(command, cwd=O, stdout=output, stderr=error, stdin=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='actual_common_browser_glb_gpu_probe_running', browser_pid=browser.pid, url=url, browser_command=command)
        save(state)
        deadline = time.monotonic() + 360
        while not done.wait(.25):
            if browser.poll() is not None:
                raise RuntimeError('Browser exited before GLB receipt')
            if time.monotonic() > deadline:
                raise RuntimeError('GLB GPU receipt timed out; console retained')
        browser.terminate()
        state['own_browser_terminated_after_receipt'] = True
        state['browser_exit_code'] = browser.wait(timeout=50)
    assert receipt['status'] == 'actual_common_browser_glb_draw_binding_and_recovery_verified', receipt
    assert receipt['browser_gpu_executed'] and receipt['browser_material_draw_verified']
    assert receipt['resource_receipt']['backend'] == 'BrowserWebGpu' and receipt['gpu_errors'] == []
    assert receipt['draw_receipt']['consecutive_equal_readbacks'] >= 3
    assert receipt['recovery_consecutive_equal_readbacks'] >= 3 and receipt['recovery_pixels_byte_equal']
    assert receipt['packed_image_bytes'] == 512 * 512 * 4
    assert sha(O / 'BROWSER-PACKED.rg11b10') == receipt['packed_image_sha256']
    for path, expected in pins.items():
        assert sha(Path(path)) == expected, path
    (O / 'MATERIAL-CURVE.r32f').write_bytes(bytes(receipt['material_curve']))
    (O / 'RECOVERED-MATERIAL-CURVE.r32f').write_bytes(bytes(receipt['recovered_material_curve']))
    assert (O / 'MATERIAL-CURVE.r32f').read_bytes() == (O / 'RECOVERED-MATERIAL-CURVE.r32f').read_bytes()
    state.update(status='actual_common_browser_glb_binding_draw_and_recovery_passed',
                 browser_gpu_executed=True, browser_material_draw_verified=True,
                 source_inputs_unchanged=True, browser_receipt_sha256=sha(O / 'BROWSER-RESULT.v1.json'),
                 packed_image_sha256=sha(O / 'BROWSER-PACKED.rg11b10'), editor_visual_verified=False)
except Exception as error:
    state.update(status='browser_glb_probe_refusal_retained', error=repr(error),
                 traceback=__import__('traceback').format_exc())
finally:
    if browser is not None and browser.poll() is None:
        browser.terminate()
        try:
            browser.wait(timeout=50)
        except subprocess.TimeoutExpired:
            state['own_browser_cleanup_pending'] = True
    if server is not None:
        server.shutdown()
        server.server_close()
    state['finished_utc'] = datetime.now(timezone.utc).isoformat()
    save(state)
