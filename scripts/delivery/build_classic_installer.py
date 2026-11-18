"""Build the common checkout into one offline NSIS installer; archives stay explicit."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
from build_delivery import (STUDIO_RIGHTS_REVIEW, STUDIO_SCOPES, digest, studio_product,
                            verify_package_stream, verify_studio_redistribution_review, write)
import application_sources
from complete_rebuild import shared_source_inputs, source_inputs_equal


def logged(command, log, env, cwd=None):
    with log.open("w", encoding="utf-8") as stream:
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, stdout=stream, stderr=stream)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}); see {log}")


def verify_studio_delivery(root, receipt_sha256, source_manifest_sha256):
    """Re-read a Studio assembly without relying on an NSIS build."""
    import game_kit
    root = Path(root)
    application_sources.ordinary(root)
    if not root.is_absolute() or not root.is_dir():
        raise ValueError('Studio delivery must be an absolute physical directory')
    files = game_kit.inventory(root)
    if {item['path'] for item in files} != {'delivery.json', 'forge-delivery.exe', 'forge-studio.forgepack',
                                             'forge-studio.manifest.json', 'studio-assembly-receipt.json'}:
        raise ValueError('Studio delivery has missing or unexpected files')
    receipt_path = root/'studio-assembly-receipt.json'
    if not re.fullmatch(r'[0-9a-f]{64}', receipt_sha256) or digest(receipt_path) != receipt_sha256:
        raise ValueError('Studio assembly receipt pin differs')
    receipt = game_kit.read_document(receipt_path)
    entry = receipt.get('package')
    rights = entry.get('rights') if isinstance(entry, dict) else None
    if not isinstance(rights, str) or rights not in STUDIO_SCOPES:
        raise ValueError('Unknown Studio assembly rights')
    scope, status = STUDIO_SCOPES[rights]
    if (receipt.get('schema') != f'forge.delivery.{scope}-studio-assembly.v1' or
            receipt.get('status') != status or
            receipt.get('source_manifest_sha256') != source_manifest_sha256 or
            any(receipt.get(k) is not False for k in ('runtime_verified', 'installation_verified',
                                                       'distribution_qualified'))):
        raise ValueError('Studio assembly does not match the current source receipt')
    game_kit.require_windows_x64_executable(root/'forge-delivery.exe')
    if digest(root/'forge-delivery.exe') != receipt.get('delivery_cli_sha256'):
        raise ValueError('Studio delivery CLI differs from its receipt')
    index = game_kit.read_document(root/'delivery.json')
    descriptor = game_kit.read_document(root/'forge-studio.manifest.json')
    if (entry.get('file') != 'forge-studio.forgepack' or entry.get('id') != 'forge-studio' or
            index != {'schema':'forge.delivery.index.v1', 'packages':[entry]} or
            descriptor.get('schema') != 'forge.delivery.package.v1' or
            descriptor.get('id') != 'forge-studio' or descriptor.get('kind') != 'applications' or
            descriptor.get('platform') != 'windows-x64' or descriptor.get('rights') != rights or
            descriptor.get('version') != entry.get('version') or
            descriptor.get('source_revision') != source_manifest_sha256 or descriptor.get('product') != studio_product()):
        raise ValueError('Studio package index or product definition differs')
    package = root/entry['file']
    if package.stat().st_size != entry.get('bytes'):
        raise ValueError('Studio package size differs')
    review = None
    if rights == 'redistributable':
        review = receipt.get('redistribution_review')
        matches = [item for item in descriptor['files'] if item['path'] == STUDIO_RIGHTS_REVIEW]
        if (not isinstance(review, dict) or len(matches) != 1 or
                STUDIO_RIGHTS_REVIEW not in descriptor['license_paths'] or
                matches[0]['sha256'] != receipt.get('redistribution_review_sha256')):
            raise ValueError('Reviewed Studio package is missing its pinned review')
        verify_studio_redistribution_review(review, descriptor['files'], source_manifest_sha256,
                                            receipt['delivery_cli_sha256'], descriptor['version'])
    verify_package_stream(package, descriptor, entry.get('manifest_sha256'), entry.get('archive_sha256'),
                          expected_studio_review=review)
    return {'receipt':receipt, 'files':files}


def inspect_webview_installer(path):
    """Inspect the Windows PE installer stub; its payload architecture is separate."""
    import game_kit
    path = game_kit.physical_file(path)
    with path.open('rb') as stream:
        header = stream.read(64)
        if len(header) != 64 or header[:2] != b'MZ':
            raise ValueError('WebView2 installer must be a Windows PE executable')
        offset = int.from_bytes(header[60:64], 'little')
        if not 64 <= offset <= min(path.stat().st_size - 26, 16 * 1024 * 1024):
            raise ValueError('Invalid WebView2 PE header offset')
        stream.seek(offset)
        pe = stream.read(26)
    if pe[:4] != b'PE\0\0' or len(pe) != 26:
        raise ValueError('WebView2 installer must be a Windows PE executable')
    machine = (int.from_bytes(pe[4:6], 'little'), int.from_bytes(pe[24:26], 'little'))
    architectures = {(0x014c, 0x010b): 'x86', (0x8664, 0x020b): 'x64'}
    if machine not in architectures:
        raise ValueError('Unsupported WebView2 installer stub architecture')
    return architectures[machine]


def require_microsoft_signature(path):
    """Use the same Windows Authenticode policy as the installation bootstrap."""
    env = dict(os.environ, FORGE_WEBVIEW2_INSTALLER_VERIFY=str(path))
    system_root = os.environ.get('SystemRoot')
    powershell = 'powershell.exe'
    if system_root:
        shell_root = Path(system_root) / 'System32/WindowsPowerShell/v1.0'
        powershell = str(shell_root / 'powershell.exe')
        # A PowerShell 7 parent can expose incompatible modules to Windows PowerShell.
        env['PSMODULEPATH'] = str(shell_root / 'Modules')
    script = ("$s=Get-AuthenticodeSignature -LiteralPath $env:FORGE_WEBVIEW2_INSTALLER_VERIFY; "
              "if ($s.Status -ne 'Valid' -or "
              "$s.SignerCertificate.Subject -notmatch '(^|,\\s*)O=Microsoft Corporation(,|$)') { exit 1 }")
    try:
        result = subprocess.run([powershell, '-NoLogo', '-NoProfile', '-NonInteractive',
                                 '-Command', script], env=env, capture_output=True,
                                timeout=60, check=False)
    except (OSError, subprocess.TimeoutExpired) as error:
        raise ValueError('Cannot verify the WebView2 installer signature') from error
    if result.returncode:
        raise ValueError('WebView2 installer requires a valid Microsoft signature')


def build_studio_installer(args, *, repo=None, runner=logged):
    """Build an editor-only NSIS candidate; no application build or install is performed."""
    import game_kit
    repo = (repo or Path(__file__).resolve().parents[2]).resolve(strict=True)
    source_path = game_kit.physical_file(args.source_manifest)
    nsis = game_kit.physical_file(args.nsis)
    external_webview = getattr(args, 'external_webview2', False)
    crt_version = getattr(args, 'external_crt_min_version', None)
    if crt_version is not None and (
            not re.fullmatch(r'14\.[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}', crt_version)
            or any(int(part) > 65535 for part in crt_version.split('.'))):
        raise ValueError('Expected a four-part Visual C++ v14 minimum version')
    if external_webview:
        if args.webview2 is not None or args.webview2_sha256 is not None:
            raise ValueError('External WebView2 mode cannot embed a runtime installer')
    elif args.webview2 is None or args.webview2_sha256 is None:
        raise ValueError('Supply both WebView2 installer and SHA-256, or select external WebView2')
    webview = None if external_webview else game_kit.physical_file(args.webview2)
    pins = (args.studio_receipt_sha256, args.nsis_sha256)
    if webview is not None:
        pins += (args.webview2_sha256,)
    for pin in pins:
        if not isinstance(pin, str) or not re.fullmatch(r'[0-9a-f]{64}', pin):
            raise ValueError('Explicit lowercase SHA-256 inputs are required')
    if digest(nsis) != args.nsis_sha256 or (webview is not None and digest(webview) != args.webview2_sha256):
        raise ValueError('NSIS or WebView2 pin differs')
    webview_stub = None
    if webview is not None:
        webview_stub = inspect_webview_installer(webview)
        require_microsoft_signature(webview)
        if digest(webview) != args.webview2_sha256:
            raise ValueError('WebView2 installer changed during signature verification')
    output, evidence = Path(args.output), Path(args.evidence)
    for path in (output, evidence):
        if (not path.is_absolute() or '..' in path.parts or path.exists() or path.is_symlink() or
                not path.parent.is_dir() or path.parent.resolve().is_relative_to(repo)):
            raise ValueError('Choose fresh absolute installer and evidence paths outside the checkout')
        application_sources.ordinary(path.parent)
    if output.parent.resolve() == evidence.parent.resolve() and output.name == evidence.name:
        raise ValueError('Installer and evidence destinations must differ')
    if not isinstance(args.version, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9.+_-]{0,127}', args.version):
        raise ValueError('Installer version is invalid')
    source_hash = digest(source_path)
    source = game_kit.read_document(source_path)
    if (source.get('schema') != 'forge.delivery.shared-source-inputs.v1' or
            Path(source.get('source_root', '')).resolve() != repo or
            not source_inputs_equal(shared_source_inputs(repo), source)):
        raise ValueError('Current Studio source inventory required')
    verified = verify_studio_delivery(args.delivery_root, args.studio_receipt_sha256, source_hash)
    if verified['receipt']['package']['version'] != args.version:
        raise ValueError('Installer version differs from Studio package')
    rights = verified['receipt']['package']['rights']
    if rights == 'redistributable' and (not external_webview or crt_version is None):
        raise ValueError('Reviewed Studio requires external WebView2 and Visual C++ prerequisites')
    evidence.mkdir()
    root = Path(args.delivery_root)
    error = None
    try:
        # Keep the final rename on one volume even when evidence lives on another drive.
        with tempfile.TemporaryDirectory(prefix='.forge-studio-setup-', dir=output.parent) as staging:
            pending = Path(staging)/'studio-installer.pending.exe'
            command = [nsis, '/NOCD', '/INPUTCHARSET', 'UTF8', '/V3', '/DSTUDIO_ONLY',
                       f'/DSTUDIO_PACKAGE={root / "forge-studio.forgepack"}',
                       f'/DSTUDIO_DELIVERY_CLI={root / "forge-delivery.exe"}',
                       f'/DSTUDIO_DELIVERY_INDEX={root / "delivery.json"}',
                       '/DSTUDIO_EXTERNAL_WEBVIEW2' if external_webview else f'/DSTUDIO_WEBVIEW2={webview}',
                       f'/DPAYLOAD_DIR={root}', f'/DOUTPUT_FILE={pending}', f'/DVERSION={args.version}',
                       repo/'scripts/delivery/classic_installer.nsi']
            if crt_version is not None:
                command.insert(-1, f'/DSTUDIO_EXTERNAL_CRT_MIN_VERSION={crt_version}')
            runner(command, evidence/'nsis-build.log', dict(os.environ), repo)
            game_kit.physical_file(pending)
            if (digest(source_path) != source_hash or not source_inputs_equal(shared_source_inputs(repo), source) or
                    verify_studio_delivery(root, args.studio_receipt_sha256, source_hash) != verified or
                    digest(nsis) != args.nsis_sha256 or
                    (webview is not None and digest(webview) != args.webview2_sha256)):
                raise ValueError('Installer input changed during packaging')
            pending_hash = digest(pending)
            if output.exists() or output.is_symlink():
                raise ValueError('Installer output appeared during packaging')
            os.rename(pending, output)
    except Exception as failure:
        error = failure
    write(evidence/'studio-installer-result.json', {'schema':'forge.delivery.studio-installer-build.v1',
        'status':'FAIL' if error else 'BUILD_ONLY', 'error':str(error) if error else None,
        'source_manifest_sha256':source_hash, 'studio_assembly_receipt_sha256':args.studio_receipt_sha256,
        'nsis_sha256':args.nsis_sha256, 'webview2_sha256':args.webview2_sha256,
        'webview2_mode':'external' if external_webview else 'bundled',
        'external_crt_min_version':crt_version,
        'studio_package_rights':rights,
        'webview2_stub_architecture':webview_stub, 'webview2_microsoft_signature_verified':webview is not None,
        'webview2_payload_architecture_verified':False,
        'output_sha256':None if error else pending_hash, 'runtime_verified':False,
        'installation_verified':False, 'distribution_qualified':False})
    if error:
        raise error
    return {'output':str(output), 'sha256':pending_hash, 'distribution_qualified':False}


def main():
    parser = argparse.ArgumentParser(description='Build the offline Forge Studio installer')
    parser.add_argument('mode', choices=('studio-only',))
    for name in ('delivery-root', 'source-manifest', 'nsis', 'output', 'evidence'):
        parser.add_argument('--'+name, required=True, type=Path)
    for name in ('studio-receipt-sha256', 'nsis-sha256', 'version'):
        parser.add_argument('--'+name, required=True)
    webview = parser.add_mutually_exclusive_group(required=True)
    webview.add_argument('--webview2', type=Path)
    webview.add_argument('--external-webview2', action='store_true', help='Require a usable Microsoft WebView2 runtime already installed on Windows')
    parser.add_argument('--webview2-sha256')
    parser.add_argument('--external-crt-min-version',
                        help='Require the specified Microsoft Visual C++ v14 runtime or later')
    print(json.dumps(build_studio_installer(parser.parse_args()), ensure_ascii=False))


if __name__ == "__main__":
    main()
