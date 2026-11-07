"""Temporarily declare one diagnostic SDK texture read; restore original bytes."""
from pathlib import Path
from datetime import datetime, timezone
import ast
import hashlib
import json
import subprocess
import sys
import time

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-declared-gbuffer3-instrumentation-v75'
SDK = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPass.cs'

def sha(p):
    with p.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def save(value):
    tmp = ROOT / 'RESULT.tmp'
    tmp.write_text(json.dumps(value, indent=2), encoding='utf-8')
    tmp.replace(ROOT / 'RESULT.v1.json')

if len(sys.argv) == 1:
    assert not ROOT.exists()
    ROOT.mkdir()
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT / 'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)), encoding='utf-8')
    print(json.dumps(dict(root=str(ROOT), controller_pid=child.pid)))
else:
    state = dict(status='source_sdk_read_declaration_preparing', full_import=False, installed_in_product=False)
    original = None
    patched_hash = None
    try:
        assert subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process Unity -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
        qpath = S / 'queue_townsmith_declared_gbuffer3_v75.py'
        assert not qpath.exists()
        q = (S / 'queue_townsmith_inpass_gbuffer3_v74.py').read_text(encoding='utf-8').replace('v74', 'v75').replace('V74', 'V75').replace('townsmith-door-source-inpass-gbuffer3-v75', 'townsmith-door-source-declared-gbuffer3-v75')
        tree = ast.parse((S / 'prepare_townsmith_inpass_gbuffer3_v74.py').read_text(encoding='utf-8'))
        buffer = next(ast.literal_eval(node.value) for node in tree.body if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'buffer' for t in node.targets))
        lookup = buffer[:buffer.index('  RTHandle actualBuffer=handles[3];')]
        method = '\n public TextureHandle retainedGbuffer3;\n public TextureHandle OpenRpgDeclareReadTexture(HDCamera camera){\n  if(camera.camera!=sourceCamera)return default(TextureHandle);\n  ' + lookup + '\n  retainedGbuffer3=handles[3];return retainedGbuffer3;\n }\n'
        execute = 'RTHandle actualBuffer=retainedGbuffer3;var texture=actualBuffer==null?null:actualBuffer.rt;\n  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Declared GBuffer3 format differs");'
        class_anchor = 'public sealed class SourceOwnedNormalCaptureV75 : CustomPass {'
        source_anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
        assert q.count(source_anchor) == 1
        insert = '    assert '+repr(buffer)+' in text\n    text=text.replace('+repr(buffer)+','+repr(execute)+')\n'
        insert += '    assert text.count('+repr(class_anchor)+')==1\n    text=text.replace('+repr(class_anchor)+','+repr(class_anchor+method)+')\n'
        q = q.replace(source_anchor, insert + source_anchor)
        compile(q, str(qpath), 'exec')
        qpath.write_text(q, encoding='utf-8')
        original = SDK.read_bytes()
        original_hash = hashlib.sha256(original).hexdigest()
        text = original.decode('utf-8')
        assert 'OpenRpgDeclareReadTexture' not in text
        newline = '\r\n' if '\r\n' in text else '\n'
        anchor = '                this.currentRenderTarget = ReadRenderTargets(builder, targets, hdCamera);'
        assert text.count(anchor) == 1
        declaration = '\n'.join([
            '                // Private read-only diagnostic. Preserve the requested resource lifetime.',
            '                var diagnosticRead = GetType().GetMethod("OpenRpgDeclareReadTexture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);',
            '                if (diagnosticRead != null)',
            '                {',
            '                    var diagnosticTexture = (TextureHandle)diagnosticRead.Invoke(this, new object[] { hdCamera });',
            '                    if (diagnosticTexture.IsValid()) builder.UseTexture(diagnosticTexture, AccessFlags.Read);',
            '                }',
        ]).replace('\n', newline)
        patched = text.replace(anchor, declaration + newline + anchor).encode('utf-8')
        (ROOT / 'CustomPass.original.cs').write_bytes(original)
        (ROOT / 'CustomPass.instrumented.cs').write_bytes(patched)
        patched_hash = hashlib.sha256(patched).hexdigest()
        state.update(status='source_sdk_read_declaration_running', sdk_path=str(SDK), sdk_original_sha256=original_hash, sdk_instrumented_sha256=patched_hash, sdk_restore_pending=True, input_pins={str(p):sha(p) for p in [Path(__file__), qpath, ROOT/'CustomPass.original.cs', ROOT/'CustomPass.instrumented.cs']})
        save(state)
        assert sha(SDK) == original_hash
        SDK.write_bytes(patched)
        assert sha(SDK) == patched_hash
        subprocess.run([sys.executable, '-B', str(qpath)], check=True)
        receipt = W / 'townsmith-door-source-declared-gbuffer3-v75/RUN-RESULT.v1.json'
        state['capture_receipt'] = str(receipt)
        save(state)
        while True:
            capture = json.loads(receipt.read_text(encoding='utf-8-sig'))
            if 'finished_utc' in capture:
                break
            time.sleep(2)
        state.update(capture_actual_exit_code=capture.get('actual_exit_code'), source_inputs_unchanged=capture.get('source_inputs_unchanged', False), neutral_reference_byte_equal=capture.get('neutral_reference_byte_equal', False))
        assert capture.get('actual_exit_code') == 0, capture
        state['status'] = 'declared_gbuffer3_source_capture_terminal'
    except Exception as exc:
        state.update(status='declared_gbuffer3_instrumentation_refusal_retained', error=repr(exc), traceback=__import__('traceback').format_exc())
    finally:
        if original is not None and patched_hash is not None:
            current = sha(SDK)
            assert current in [patched_hash, hashlib.sha256(original).hexdigest()], 'Unexpected concurrent SDK edit: original retained, automatic overwrite refused'
            SDK.write_bytes(original)
            state.update(sdk_restored_byte_equal=sha(SDK)==hashlib.sha256(original).hexdigest(), sdk_restore_pending=False)
        state['finished_utc'] = datetime.now(timezone.utc).isoformat()
        save(state)
