"""Exercise the exact offline EXE in a fresh root, retaining bounded test evidence.

Default tests use /NOINTEGRATION. The explicit --shell-integration option checks
that every exact owned entry is absent before a temporary per-user installation,
then verifies cleanup and preservation of all preexisting legacy shortcuts.
Clean-machine prerequisite setup remains a separate qualification.
"""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import winreg

from build_delivery import digest, write


def call(cli, *args, env):
    result = subprocess.run([str(cli), *map(str,args)], env=env, capture_output=True,
                            text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(result.stderr[-5000:])
    return json.loads(result.stdout)


def classic(executable, root, env, log, uninstall=False, integration=False):
    # NSIS documents /D as the final, unquoted remainder of the command line.
    # Popen gets a Windows command line directly; no shell or interpolation occurs.
    command = f'"{executable}" /S' + ('' if integration else ' /NOINTEGRATION') + ('' if uninstall else f' /D={root}')
    with log.open('w',encoding='utf-8') as stream:
        result = subprocess.run(command, env=env, stdout=stream, stderr=stream, timeout=600)
    if result.returncode:
        raise RuntimeError(f"Classic installer returned {result.returncode}: {log}")
    if uninstall:
        # A classic NSIS uninstaller relocates itself to TEMP before deleting its
        # installed executable; wait for that child to finish its owned-file work.
        deadline = time.monotonic() + 120
        while (root/'forge-delivery.exe').exists() and time.monotonic() < deadline:
            time.sleep(.2)


def user_inventory(root):
    return {p.relative_to(root).as_posix():digest(p) for p in root.rglob('*') if p.is_file()}


def reg_values(key):
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,key,0,winreg.KEY_READ | winreg.KEY_WOW64_32KEY) as handle:
            return {winreg.EnumValue(handle,n)[0]:winreg.EnumValue(handle,n)[1]
                    for n in range(winreg.QueryInfoKey(handle)[1])}
    except FileNotFoundError:
        return None


def shell_paths():
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders') as handle:
        desktop = Path(winreg.QueryValueEx(handle,'Desktop')[0])
        programs = Path(winreg.QueryValueEx(handle,'Programs')[0])
    menu = programs/'OpenRPG Forge'
    return menu,[menu/'OpenRPG Forge.lnk',menu/'Désinstaller OpenRPG Forge.lnk',desktop/'OpenRPG Forge.lnk',
                 menu/'Gérer les contenus Forge.lnk', menu/'OpenRPG Forge Studio.lnk']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--installer', type=Path, required=True)
    parser.add_argument('--payload', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--shell-integration',action='store_true',help='Test actual per-user integration only if none of its exact entries exist')
    parser.add_argument('--entrypoint-probe',type=Path,help='Existing native startup probe (does not approve visuals or run a GPU game test)')
    args = parser.parse_args()
    evidence = args.evidence.absolute()
    evidence.mkdir(parents=True, exist_ok=False)
    root, user = evidence / 'Dossier installé Élan', evidence / 'Données personnelles Éditeur'
    env = dict(os.environ, FORGE_DELIVERY_USER_ROOT=str(user))
    product_key,uninstall_key = r'Software\OpenRPG Forge',r'Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenRPGForge'
    menu,shortcuts = shell_paths()
    existing_shortcuts = {str(p):digest(p) for p in menu.glob('*.lnk')}
    if args.shell_integration and (reg_values(product_key) is not None or reg_values(uninstall_key) is not None or any(p.exists() for p in shortcuts)):
        raise ValueError('Shell integration would replace existing entries; use /NOINTEGRATION instead')
    classic(args.installer.absolute(),root,env,evidence/'fresh-install.log',integration=args.shell_integration)
    cli = root/'forge-delivery.exe'
    state = call(cli,'verify',root,env=env)
    if state['user_root'] != str(user):
        raise ValueError('Installer did not preserve the requested isolated user profile')
    if not (root/'Forge.exe').is_file() or not (root/'Uninstall.exe').is_file():
        raise ValueError('Native launcher/uninstaller missing')
    if args.shell_integration:
        registered = reg_values(uninstall_key)
        if not registered or registered.get('InstallLocation') != str(root) or registered.get('UninstallString') != f'"{root / "Uninstall.exe"}"':
            raise ValueError('Windows uninstall registration does not point to the actual chosen directory')
        if reg_values(product_key).get('InstallRoot') != str(root) or not all(p.is_file() for p in shortcuts):
            raise ValueError('Classic Windows integration missing')
        write(evidence/'windows-integration.json',dict(registration=registered,shortcuts=[str(p) for p in shortcuts]))
    workspace = user/'workspaces/openrpg-forge'
    edited = next(workspace.rglob('authoring.json'))
    edited.write_bytes(edited.read_bytes()+b'\n  \n')
    (workspace/'retouche personnelle.txt').write_text('Mon projet doit survivre.\n',encoding='utf-8')
    before = user_inventory(user)
    write(evidence/'fresh-state.json',state)
    for app in ('launcher','editor','game'):
        plan = call(cli,'launch-plan',root,app,env=env)
        if not Path(plan['executable']).is_file():
            raise ValueError(f'Broken native entry point: {app}')
        write(evidence/f'{app}-launch-plan.json',plan)
        if app == 'launcher':
            if Path(plan['executable']).name != 'forge-launcher.exe' or digest(root/'Forge.exe') != digest(Path(plan['executable'])):
                raise ValueError('The installer launcher points to the content manager')
            diagnostic = subprocess.run([str(root/'Forge.exe'),'--verify-distributable'],env=env,capture_output=True,text=True,check=True)
            if 'FORGE_LAUNCHER_AUBE_FRONTEND_READY' not in diagnostic.stdout:
                raise ValueError('The real Aube frontend is not embedded')
            probe_env = dict(env,FORGE_DELIVERY_INSTALL_ROOT=str(root))
            subprocess.run([str(root/'Forge.exe'),'--verify-webview',str(evidence/'aube-webview.json')],env=probe_env,check=True,timeout=80)
            report = json.loads((evidence/'aube-webview.json').read_bytes())
            if not report['pass'] or not report['report']['runtime_connected']:
                raise ValueError('Native Aube renderer/IPC does not see the installed game')
    if digest(root/'Forge-ContentManager.exe') == digest(root/'Forge.exe'):
        raise ValueError('The content manager replaced the launcher')
    if args.entrypoint_probe:
        subprocess.run([os.sys.executable,str(args.entrypoint_probe),'--delivery',str(args.payload),
                        '--installation',str(root),'--output',str(evidence/'entrypoints')],
                       env=env,check=True)
    # Same EXE can be rerun in place without resetting the workspace.
    classic(args.installer.absolute(),root,env,evidence/'reinstall.log',integration=args.shell_integration)
    call(cli,'verify',root,env=env)
    if user_inventory(user) != before:
        raise ValueError('Reinstallation changed personal files')
    (root/'fichier personnel.txt').write_text('Conserver ce fichier étranger.\n',encoding='utf-8')
    package = root/'packages'/state['active']['core']['id']/state['active']['core']['revision']
    foreign = package/'annotation personnelle.txt'
    foreign.write_text('Conserver même dans un dossier de package.\n',encoding='utf-8')
    classic(root/'Uninstall.exe',root,env,evidence/'uninstall.log',uninstall=True,integration=args.shell_integration)
    if (root/'installation.json').exists() or (root/'Forge.exe').exists() or cli.exists():
        raise ValueError('Uninstallation left an active owned installation')
    if not foreign.is_file() or not (root/'fichier personnel.txt').is_file():
        raise ValueError('Uninstallation removed a foreign file')
    if user_inventory(user) != before:
        raise ValueError('Uninstallation changed personal work')
    if (package/'bin/forge-game.exe').exists() or (package/'bin/forge-studio.exe').exists():
        raise ValueError('Uninstallation left owned application binaries')
    if args.shell_integration:
        if reg_values(product_key) is not None or reg_values(uninstall_key) is not None or any(p.exists() for p in shortcuts):
            raise ValueError('Uninstallation left owned Windows integration entries')
        if {str(p):digest(p) for p in menu.glob('*.lnk')} != existing_shortcuts:
            raise ValueError('Uninstallation changed legacy shortcuts')
    report = dict(schema='forge.delivery.classic-validation.v1',status='PASS_LOCAL_CLASSIC_INSTALLER',
        installer_sha256=digest(args.installer),validation_script_sha256=digest(Path(__file__)),
        root=str(root),user_root=str(user),custom_unicode_path=True,full_file_verification=True,
        offline_embedded_payload=True,reinstallation_preserves_user_files=True,
        aube_launcher_embedded_webview_ipc_verified=True,content_manager_separate=True,
        native_launch_recipes_checked=3,uninstall_removes_owned_applications=True,
        native_launcher_editor_startup_probed=bool(args.entrypoint_probe),
        uninstall_preserves_user_files=len(before),uninstall_preserves_foreign_files=True,
        windows_shortcuts_registry_tested=args.shell_integration,legacy_shortcuts_preserved=args.shell_integration,
        clean_machine_webview2_install_tested=False,
        human_visual_acceptance=False,gate3_closed=False,release_qualified=False)
    write(evidence/'verification.json',report)
    print(json.dumps(report,ensure_ascii=False),flush=True)


if __name__ == '__main__':
    main()
