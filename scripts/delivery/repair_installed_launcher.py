"""Upgrade only an installation matching an explicit previous build proof.
No uninstall, recursive cleanup or edits to personal workspaces are performed.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
from build_delivery import digest, write
from verify_classic_installer import reg_values, shell_paths

def inventory(root):
    return {p.relative_to(root).as_posix():digest(p) for p in root.rglob('*') if p.is_file()}

def call(cli,*args,env):
    result = subprocess.run([str(cli),*map(str,args)],env=env,capture_output=True,text=True,encoding='utf-8',check=True)
    return json.loads(result.stdout)

def main():
    p=argparse.ArgumentParser()
    for name in ('root','installer','payload','previous-proof','evidence'):
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--expected-version',default='0.1.0-preview.20261010.1')
    args=p.parse_args()
    root=args.root.absolute()
    if args.evidence.exists(): raise ValueError('Fresh evidence required')
    old_proof=json.loads(args.previous_proof.read_bytes())
    if (root/'.classic-installer-owned').read_text(encoding='utf-8').strip() != args.expected_version:
        raise ValueError('Installed candidate does not match the expected version')
    launcher_pin=old_proof['outputs'].get('forge-launcher.exe',old_proof['outputs'].get('forge-hub.exe'))
    if digest(root/'Forge.exe') != launcher_pin:
        raise ValueError('Installed launcher does not match the previous build proof; preserve it')
    if digest(root/'forge-delivery.exe') != old_proof['outputs']['forge-delivery.exe']:
        raise ValueError('Installed CLI ownership does not match')
    head=json.loads((root/'installation.json').read_bytes())
    user=Path(head['user_root'])
    env=dict(os.environ,FORGE_DELIVERY_USER_ROOT=str(user))
    menu,shortcuts=shell_paths()
    legacy={str(p):digest(p) for p in menu.glob('*.lnk') if p not in shortcuts}
    registration=reg_values(r'Software\OpenRPG Forge')
    if registration and registration.get('InstallRoot') != str(root):
        raise ValueError('Windows integration belongs to a different installation')
    uninstall_registration=reg_values(r'Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenRPGForge')
    if uninstall_registration and uninstall_registration.get('InstallLocation') != str(root):
        raise ValueError('Windows uninstall registration belongs to a different installation')
    shortcut_env=dict(env,FORGE_REPAIR_SHORTCUTS=json.dumps([str(p) for p in shortcuts if p.exists()]))
    ps_script="$links=ConvertFrom-Json $env:FORGE_REPAIR_SHORTCUTS; $shell=New-Object -ComObject WScript.Shell; @($links | ForEach-Object { $link=$shell.CreateShortcut($_); [pscustomobject]@{path=$_;target=$link.TargetPath;arguments=$link.Arguments} }) | ConvertTo-Json -Compress"
    shortcut_check=subprocess.run([str(Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'),
        '-NoLogo','-NoProfile','-NonInteractive','-Command',ps_script],env=shortcut_env,capture_output=True,text=True,encoding='utf-8',check=True)
    existing=json.loads(shortcut_check.stdout or '[]')
    if isinstance(existing,dict): existing=[existing]
    for entry in existing:
        if Path(entry['target']).parent != root:
            raise ValueError('An owned shortcut name points to a different installation; preserve it')
    before=inventory(user)
    # Ownership is proven by the marker, both exact binary hashes and shortcut
    # targets above. Do not rehash every unchanged asset twice during a repair.
    old_state=call(root/'forge-delivery.exe','status',root,env=env)
    args.evidence.mkdir(parents=True)
    write(args.evidence/'before-state.json',old_state)
    write(args.evidence/'before-personal-files.json',before)
    command=f'"{args.installer.absolute()}" /S /D={root}'
    print('Installing verified update; preserving personal data',flush=True)
    # Do not terminate NSIS on an arbitrary timeout while its apply child is
    # still updating the installation. The caller can observe this process.
    subprocess.run(command,env=env,check=True)
    print('Verifying installed packages and personal files',flush=True)
    state=call(root/'forge-delivery.exe','verify',root,env=env)
    plan=call(root/'forge-delivery.exe','launch-plan',root,'launcher',env=env)
    if Path(plan['executable']).name != 'forge-launcher.exe' or digest(root/'Forge.exe') != digest(args.payload/'Forge.exe'):
        raise ValueError('Launcher route was not corrected')
    if state['user_root'] != str(user) or inventory(user) != before:
        raise ValueError('Personal files changed during repair; inspect evidence')
    if legacy != {str(p):digest(p) for p in menu.glob('*.lnk') if p not in shortcuts}:
        raise ValueError('An unrelated legacy shortcut changed')
    if not all(p.is_file() for p in shortcuts):
        raise ValueError('Launcher, Studio or content-manager shortcut missing')
    manager=call(root/'forge-delivery.exe','launch-plan',root,'content-manager',env=env)
    if digest(root/'Forge-ContentManager.exe') != digest(Path(manager['executable'])):
        raise ValueError('The content manager is not separate from the launcher')
    write(args.evidence/'windows-integration.json',dict(
        registration=reg_values(r'Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenRPGForge'),
        shortcuts=[str(p) for p in shortcuts],legacy_hashes=legacy))
    probe_env=dict(env,FORGE_DELIVERY_INSTALL_ROOT=str(root))
    subprocess.run([str(root/'Forge.exe'),'--verify-webview',str(args.evidence/'aube-webview.json')],env=probe_env,check=True,timeout=80)
    probe=json.loads((args.evidence/'aube-webview.json').read_bytes())
    if not probe['pass'] or not probe['report']['runtime_connected']:
        raise ValueError('Native launcher cannot see installed game')
    write(args.evidence/'after-state.json',state)
    write(args.evidence/'launcher-plan.json',plan)
    result=dict(status='PASS_INSTALLED_LAUNCHER_REPAIRED',root=str(root),
                installer_sha256=digest(args.installer),user_root=str(user),
                personal_files_preserved=len(before),old_core_preserved=state['previous']==old_state['active'],
                windows_shortcuts_registered=5,legacy_shortcuts_preserved=len(legacy),content_manager_separate=True,
                aube_embedded_webview_ipc=True,game_gpu_probe=False,human_visual_acceptance=False)
    write(args.evidence/'verification.json',result)
    print(json.dumps(result,ensure_ascii=False),flush=True)

if __name__=='__main__': main()
