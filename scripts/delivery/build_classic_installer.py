"""Build the common checkout into one offline NSIS installer; archives stay explicit."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
from datetime import datetime, timezone

from build_delivery import assemble, copy, digest, package, regular, write
import application_sources
from complete_rebuild import (
    SOURCE_SCOPES, frontend_inputs, gate3_validator, regular_input,
    informative_checkout_changes, shared_build_environment, shared_source_inputs,
    source_file_hashes, source_inputs_equal, validated_gate3_inputs,
)


def inventory(root):
    result = []
    for path in sorted(root.rglob("*")):
        if "gen" in path.relative_to(root).parts:
            continue  # Tauri-generated ACL/schema output is not source input.
        regular(path)
        if path.is_file():
            result.append(dict(path=path.relative_to(root).as_posix(), bytes=path.stat().st_size,
                               sha256=digest(path)))
    return result


def logged(command, log, env, cwd=None):
    with log.open("w", encoding="utf-8") as stream:
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, stdout=stream, stderr=stream)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}); see {log}")


def output_precondition(output, replace, expected_hash):
    if output.exists():
        regular(output)
        if not replace or not expected_hash or digest(output) != expected_hash.lower():
            raise ValueError('Replacing the principal installer requires its exact previous SHA-256')
        return expected_hash.lower()
    if replace or expected_hash:
        raise ValueError('Expected principal installer is absent; do not silently change the replacement plan')
    return None


def shared_native_receipts(repo, evidence, cache):
    manifest_path = evidence / 'source-inputs.json'
    manifest = json.loads(manifest_path.read_bytes())
    if (manifest.get('schema') != 'forge.delivery.shared-source-inputs.v1' or
            manifest.get('source_root') != str(repo) or not source_inputs_equal(shared_source_inputs(repo), manifest)):
        raise ValueError('Fresh native receipts must cover the current common checkout')
    result = {}
    for stage, relative in [('studio', 'release/forge-studio.exe'),
                            ('game', 'release/examples/tidewater_plant_probe.exe')]:
        proof_path = evidence / f'{stage}-build-proof.json'
        proof = json.loads(proof_path.read_bytes())
        executable = cache / relative
        regular_input(cache, executable)
        if (proof.get('schema') != 'forge.delivery.native-build.v1' or
                proof.get('status') != 'BUILD_ONLY' or not proof.get('shared_working_tree') or
                proof.get('source_root') != str(repo) or
                not proof.get('source_stable_during_build') or
                proof.get('source_manifest_sha256') != digest(manifest_path) or
                Path(proof.get('executable', '')).resolve() != executable or
                proof.get('executable_sha256') != digest(executable)):
            raise ValueError(f'Historical or mismatched {stage} refused for the shared installer')
        adoption = proof.get('validated_gate3')
        if adoption is not None:
            if (stage not in ('studio', 'game') or
                    proof.get('adopted_verified_output') is not True or
                    proof.get('profile') != 'release'):
                raise ValueError('Only shared Studio or game Release outputs can use an adopted Gate 3 native receipt')
            adopted_executable, pins, links, _ = validated_gate3_inputs(
                gate3_validator(repo), Path(adoption['evidence']).resolve(), stage,
                source_file_hashes(manifest), cache, shared_build_environment(cache))
            if (adopted_executable != executable or links != adoption or
                    proof.get('frontend_pins') != pins or proof.get('profile') != 'release'):
                raise ValueError('Adopted Studio release chain differs from its authenticated receipt')
        elif proof.get('adopted_verified_output'):
            raise ValueError('Adopted native output is missing its original validated receipt chain')
        result[stage] = {'proof': proof, 'proof_path': proof_path, 'executable': executable}
    return manifest_path, manifest, result


def build_shared_installer(args):
    repo = Path(__file__).resolve().parents[2]
    if args.reuse_native_build or args.baseline:
        raise ValueError('Shared installers require current native builds and explicit content inputs')
    if not all((args.native_evidence, args.playtest, args.webview2, args.webview2_sha256)):
        raise ValueError('Shared installer requires --native-evidence, --playtest, --webview2 and --webview2-sha256')
    # Refuse missing/corrupt offline dependency sources before costly build work.
    # assemble preflights again and stages its own immutable checked snapshot.
    application_sources.prepare_for_assembly(args, repo)
    evidence, cache, output = args.evidence.resolve(), args.target.resolve(), args.output.absolute()
    for location in (evidence, cache, output):
        if location == repo or any(location.is_relative_to(repo / scope) for scope in SOURCE_SCOPES):
            raise ValueError('Build evidence, cache and installer must stay outside source input scopes')
    if evidence.exists():
        raise ValueError('Preserve existing packaging evidence; choose a fresh evidence directory')
    previous_hash = output_precondition(output, args.replace_output, args.expected_output_sha256)
    regular(args.webview2)
    if digest(args.webview2) != args.webview2_sha256.lower():
        raise ValueError('Offline WebView2 installer does not match its explicit SHA-256 pin')
    source_manifest, native_inputs, native = shared_native_receipts(repo, args.native_evidence.resolve(), cache)
    before = shared_source_inputs(repo)
    evidence.mkdir(parents=True)
    # A receipt copy is metadata, never a second product source tree.
    (evidence / 'source-inputs.json').write_bytes(source_manifest.read_bytes())
    write(evidence / 'installer-source-before.json', before)
    env = dict(os.environ, CARGO_TARGET_DIR=str(cache), CARGO_BUILD_JOBS='1',
               CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true', NO_COLOR='true',
               FORGE_AUBE_SOURCE=str(repo / 'apps/forge_launcher/canonical-ui'))
    env.pop('TAURI_CONFIG', None)
    env.pop('RUSTFLAGS', None)
    commands, error, build_proof = [], None, None

    def execute(command, cwd, label):
        commands.append({'arguments': list(map(str, command)), 'cwd': str(cwd)})
        write(evidence / 'installer-commands.json', commands)
        logged(command, evidence / f'{label}.log', env, cwd)

    pending = evidence / 'OpenRPG-Forge-Setup.pending.exe'
    try:
        launcher_root = repo / 'apps/forge_launcher'
        execute([args.node, launcher_root / 'scripts/build-aube.mjs'], launcher_root, 'aube-frontend-build')
        launcher_frontend = launcher_root / 'dist'
        frontend_pins = frontend_inputs(launcher_frontend)
        if not frontend_pins:
            raise ValueError('Aube production frontend is empty')
        for component, relative in [('delivery', 'tools/forge_delivery'),
                                    ('hub', 'apps/forge_hub'), ('launcher', 'apps/forge_launcher')]:
            manifest = repo / relative / 'Cargo.toml'
            features = ['--features', 'production'] if component == 'launcher' else []
            print(f'Testing and building {component} from {repo}', flush=True)
            execute([args.cargo, 'test', '--manifest-path', manifest, '--locked', '--offline', '-j', '1', *features],
                    repo / relative, f'{component}-tests')
            execute([args.cargo, 'build', '--manifest-path', manifest, '--release', '--locked', '--offline', '-j', '1', *features],
                    repo / relative, f'{component}-build')
        if frontend_inputs(launcher_frontend) != frontend_pins or not source_inputs_equal(shared_source_inputs(repo), before):
            raise ValueError('Shared inputs changed during launcher/Hub/CLI compilation')
        cli, hub = cache / 'release/forge_delivery.exe', cache / 'release/forge_hub.exe'
        launcher = cache / 'release/forge-launcher-prototype.exe'
        outputs = {'forge-delivery.exe': cli, 'forge-hub.exe': hub, 'forge-launcher.exe': launcher}
        for executable in outputs.values():
            regular_input(cache, executable)
        manifest_hash = digest(source_manifest)
        build_proof = dict(schema='forge.delivery.scoped-build.v1', status='BUILD_ONLY',
                           recorded_at_utc=datetime.now(timezone.utc).isoformat(),
                           shared_working_tree=True, source_root=str(repo),
                           source_manifest_sha256=manifest_hash, current_source_manifest_sha256=manifest_hash,
                           source_stable_during_build=True, frontend_pins=frontend_pins,
                           frontend=str(launcher_frontend), production=True, commands=commands.copy(),
                           outputs={name: digest(path) for name, path in outputs.items()},
                           output_paths={name: str(path) for name, path in outputs.items()},
                           reused_native_outputs=False, runtime_verified=False, gate_qualified=False)
        launcher_receipt = evidence / 'launcher-build-proof.json'
        write(launcher_receipt, build_proof)
        payload = evidence / 'payload'
        assemble(argparse.Namespace(shared_working_tree=True, frozen_archive=False,
                 delivery_cli=cli, hub=hub, launcher=launcher, aube_proof=launcher_receipt,
                 studio=native['studio']['executable'], studio_proof=native['studio']['proof_path'],
                 runtime=native['game']['executable'], runtime_proof=native['game']['proof_path'],
                 launcher_proof=launcher_receipt, source_manifest=source_manifest,
                  source_revision=native_inputs['head'], playtest=args.playtest.resolve(), area_id=args.area_id,
                 webview2=args.webview2.resolve(), output=payload, version=args.version,
                 cargo_cache=args.cargo_cache))
        # These are the normal NSIS bootstrap names, inside its private payload.
        copy(launcher, payload / 'Forge.exe')
        copy(hub, payload / 'Forge-ContentManager.exe')
        copy(args.webview2, payload / 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe')
        execute([args.nsis, '/NOCD', '/INPUTCHARSET', 'UTF8', '/V3', f'/DPAYLOAD_DIR={payload}',
                 f'/DOUTPUT_FILE={pending}', f'/DVERSION={args.version}',
                 repo / 'scripts/delivery/classic_installer.nsi'], repo, 'nsis-build')
        regular(pending)
    except Exception as failure:
        error = failure
    try:
        after = shared_source_inputs(repo)
    except Exception as failure:
        after = {'inventory_failed': str(failure)}
        if error is None:
            error = failure
    write(evidence / 'installer-source-after.json', after)
    stable = 'inventory_failed' not in after and source_inputs_equal(before, after)
    checkout_changes = informative_checkout_changes(before, after, gate3_validator(repo)) if 'inventory_failed' not in after else None
    write(evidence / 'installer-result.json', dict(status='BUILD_ONLY' if stable and not error else 'FAIL',
          source_stable_during_build=stable, error=str(error) if error else None,
          working_tree_changes=checkout_changes,
          gate3_closed=False, release_qualified=False))
    if error:
        raise error
    if not stable:
        raise ValueError('Shared inputs changed during packaging; the principal installer was preserved')
    # Compare again immediately before the atomic publication to the one requested path.
    output_precondition(output, args.replace_output, previous_hash)
    pending_hash = digest(pending)
    output.parent.mkdir(parents=True, exist_ok=True)
    os.replace(pending, output)
    payload_proof = json.loads((evidence / 'payload/input-provenance.json').read_bytes())
    write(evidence / 'installer-build-proof.json', dict(schema='forge.delivery.classic-installer.v1',
          status='BUILD_ONLY', shared_working_tree=True, version=args.version, output=str(output),
          bytes=output.stat().st_size, sha256=pending_hash, replaced_output_sha256=previous_hash,
          source_manifest_sha256=digest(source_manifest), source_stable_during_build=True,
          source_before_sha256=digest(evidence / 'installer-source-before.json'),
          source_after_sha256=digest(evidence / 'installer-source-after.json'),
          working_tree_changes=checkout_changes,
          orchestration_sha256={name:digest(repo / 'scripts/delivery' / name) for name in
                                ['build_classic_installer.py', 'build_delivery.py', 'classic_installer.nsi']},
          component_builds=dict(launcher=build_proof, studio=native['studio']['proof'], game=native['game']['proof']),
          assembled_inputs=payload_proof, offline_prerequisite_sha256=digest(args.webview2),
          commands=commands, gate3_closed=False, release_qualified=False))
    print(json.dumps(dict(output=str(output), bytes=output.stat().st_size, sha256=pending_hash)), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, help='Historical frozen delivery only')
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cargo", type=Path, required=True)
    parser.add_argument("--node", type=Path, required=True)
    parser.add_argument("--target", type=Path, required=True)
    parser.add_argument("--nsis", type=Path, required=True)
    parser.add_argument("--reuse-native-build",type=Path,help="Reuse a proven build only when scoped source inventories are byte-identical")
    parser.add_argument("--version", default="0.1.0-preview.20261010.3")
    parser.add_argument('--native-evidence', type=Path, help='Completed shared Studio/Game build receipts')
    parser.add_argument('--cargo-cache', type=Path,
                        help='Explicit Cargo registry root containing pinned offline dependency source archives')
    parser.add_argument('--playtest', type=Path, help='Explicit current content and authoring data')
    parser.add_argument('--area-id')
    parser.add_argument('--webview2', type=Path)
    parser.add_argument('--webview2-sha256')
    parser.add_argument('--replace-output', action='store_true')
    parser.add_argument('--expected-output-sha256')
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--shared-working-tree', action='store_true')
    mode.add_argument('--frozen-archive', action='store_true', help='Historical snapshot workflow only')
    args = parser.parse_args()
    if args.shared_working_tree:
        build_shared_installer(args)
        return
    if not args.baseline:
        raise ValueError('--baseline is required for the explicit frozen archive workflow')
    repo = Path(__file__).resolve().parents[2]
    evidence, baseline, output = args.evidence.absolute(), args.baseline.absolute(), args.output.absolute()
    if evidence.exists() or output.exists():
        raise ValueError("Choose fresh evidence and output paths; existing deliveries are preserved")
    index = json.loads((baseline / "delivery.json").read_bytes())
    old_proof = json.loads((baseline / "input-provenance.json").read_bytes())
    for entry in index["packages"]:
        if digest(baseline / entry["file"]) != entry["archive_sha256"]:
            raise ValueError(f"Baseline package does not match its pin: {entry['id']}")
    evidence.mkdir(parents=True)
    source = evidence / "source"
    for relative in ("apps/forge_hub", "apps/forge_launcher", "tools/forge_delivery"):
        copy(repo / relative, source / relative)
    logged([args.node, source / "apps/forge_launcher/scripts/build-aube.mjs"],
           evidence / "aube-frontend-build.log", dict(os.environ))
    # Freeze installer orchestration as well, including its code-native resources.
    copy(Path(__file__), evidence / "orchestration" / Path(__file__).name)
    copy(Path(__file__).with_name("build_delivery.py"), evidence / "orchestration/build_delivery.py")
    copy(repo / "scripts/delivery/classic_installer.nsi", evidence / "orchestration/classic_installer.nsi")
    resources = repo / "scripts/delivery/installer-resources"
    if resources.exists():
        copy(resources, evidence / "orchestration/installer-resources")
    source_manifest = dict(schema="forge.delivery.scoped-sources.v1", scope=["apps/forge_hub", "apps/forge_launcher", "tools/forge_delivery"],
                           files=inventory(source), working_tree_changes_included=True,
                           baseline_source_revision=old_proof["source_revision"])
    write(evidence / "source-inputs.json", source_manifest)
    env = dict(os.environ, CARGO_TARGET_DIR=str(args.target.absolute()))
    if args.reuse_native_build:
        previous = args.reuse_native_build.absolute()
        previous_proof = json.loads((previous/"launcher-build-proof.json").read_bytes())
        if (digest(previous/"source-inputs.json") != digest(evidence/"source-inputs.json") or
            previous_proof["source_manifest_sha256"] != digest(evidence/"source-inputs.json") or
            not previous_proof["source_stable_during_build"]):
            raise ValueError("Reuse refused: actual native inputs differ from this snapshot")
        for name in ("forge-delivery.exe","forge-hub.exe", "forge-launcher.exe"):
            if digest(previous/name) != previous_proof["outputs"][name]:
                raise ValueError("Native output does not match its original build proof")
            copy(previous/name,evidence/name)
        for component in ("delivery","hub", "launcher"):
            copy(previous/f"{component}-tests.log",evidence/f"{component}-tests.log")
            copy(previous/f"{component}-build.log",evidence/f"{component}-build.log")
        print("Native outputs reused after exact source/proof verification",flush=True)
    else:
        for component, relative in (("delivery", "tools/forge_delivery"), ("hub", "apps/forge_hub"), ("launcher", "apps/forge_launcher")):
            manifest = source / relative / "Cargo.toml"
            features = ["--features", "production"] if component == "launcher" else []
            print(f"Testing {component} from frozen inputs", flush=True)
            logged([args.cargo, "test", "--manifest-path", manifest, "--locked", "--offline", *features],
                   evidence / f"{component}-tests.log", env)
            print(f"Building {component}", flush=True)
            logged([args.cargo, "build", "--manifest-path", manifest, "--release", "--locked", "--offline", *features],
                   evidence / f"{component}-build.log", env)
    if inventory(source) != source_manifest["files"]:
        raise ValueError("Frozen sources changed during the build")
    if not args.reuse_native_build:
        copy(args.target / "release/forge_delivery.exe", evidence / "forge-delivery.exe")
        copy(args.target / "release/forge_hub.exe", evidence / "forge-hub.exe")
        copy(args.target / "release/forge-launcher-prototype.exe", evidence / "forge-launcher.exe")
    cli, hub = evidence / "forge-delivery.exe", evidence / "forge-hub.exe"
    launcher = evidence / "forge-launcher.exe"
    build_proof = dict(schema="forge.delivery.scoped-build.v1", status="BUILD_ONLY",
                       source_manifest_sha256=digest(evidence / "source-inputs.json"),
                       source_stable_during_build=True, outputs={"forge-delivery.exe":digest(cli), "forge-hub.exe":digest(hub), "forge-launcher.exe":digest(launcher)},
                       original_native_build=str(previous) if args.reuse_native_build else str(evidence),
                       reused_native_outputs=bool(args.reuse_native_build))
    write(evidence / "launcher-build-proof.json", build_proof)
    payload = evidence / "payload"
    payload.mkdir()
    core = evidence / "applications"
    copy(baseline / "build-inputs/applications", core)
    for component in ("game", "studio"):
        if digest(core / f"bin/forge-{component}.exe") != old_proof[f"{component}_sha256"]:
            raise ValueError(f"Baseline {component} differs from its build proof")
    copy(core / "provenance/launcher-build-proof.json", core / "provenance/launcher-build-proof-baseline.json")
    copy(cli, core / "bin/forge-delivery.exe")
    copy(hub, core / "bin/forge-hub.exe")
    copy(launcher, core / "bin/forge-launcher.exe")
    copy(evidence / "launcher-build-proof.json", core / "provenance/launcher-build-proof.json")
    copy(evidence / "source-inputs.json", core / "provenance/launcher-source-inputs.json")
    (core / "PROVENANCE.md").write_text(
        "# Applications de cette livraison locale\n\n"
        f"Version : {args.version}. Launcher et gestionnaire de livraison recompilés depuis les sources figées jointes.\n"
        "Jeu et Studio : binaires reconstruits le 09 octobre, conservés exactement avec leurs preuves et sources distinctes.\n"
        "Les changements simultanés du moteur ne font pas partie de cette révision de l'installateur.\n"
        "Le jeu reste la promenade native de référence. Gate 3 reste ouverte, release et aventure RPG non qualifiées.\n"
        "Droits des packs : utilisation privée locale; licences et provenance incluses.\n", encoding="utf-8")
    descriptor = json.loads((baseline / "forge-applications.manifest.json").read_bytes())
    descriptor.update(version=args.version, files=[])
    game = descriptor["product"]["apps"]["game"]
    if game["args"][1] != "{user}/captures/game.png":
        raise ValueError("Unexpected baseline capture recipe; review rather than rewriting another argument")
    game["args"][1] = "{user}/captures/game-{session}.png"
    descriptor["product"]["apps"]["launcher"] = dict(executable="bin/forge-launcher.exe", args=["--root", "{install}"], env={}, clear_env_prefixes=[])
    descriptor["product"]["apps"]["content-manager"] = dict(executable="bin/forge-hub.exe", args=["--root", "{install}"], env={}, clear_env_prefixes=[])
    core_entry = package(cli, core, payload, descriptor)
    entries = []
    for entry in index["packages"]:
        if entry["kind"] != "applications":
            copy(baseline / entry["file"], payload / entry["file"])
            copy(baseline / f"{entry['id']}.manifest.json", payload / f"{entry['id']}.manifest.json")
            entries.append(entry)
    entries.append(core_entry)
    copy(launcher, payload / "Forge.exe")
    copy(hub, payload / "Forge-ContentManager.exe")
    copy(cli, payload / "forge-delivery.exe")
    prereq = baseline / index["prerequisites"]["webview2"]["file"]
    if digest(prereq) != index["prerequisites"]["webview2"]["sha256"]:
        raise ValueError("Offline WebView2 does not match its baseline pin")
    copy(prereq, payload / prereq.name)
    write(payload / "delivery.json", dict(schema="forge.delivery.index.v1", version=args.version,
          platform="windows-x64", status="local_preview", packages=entries,
          prerequisites={"webview2":dict(file=prereq.name,sha256=digest(prereq),mode="offline_installer")}))
    # Evidence remains local; the only distributable entry point is the resulting EXE.
    print("Building the standalone offline installer", flush=True)
    logged([args.nsis, "/NOCD", "/INPUTCHARSET", "UTF8", "/V3", f"/DPAYLOAD_DIR={payload}", f"/DOUTPUT_FILE={output}",
            f"/DVERSION={args.version}", evidence / "orchestration/classic_installer.nsi"],
           evidence / "nsis-build.log", env)
    write(evidence / "installer-build-proof.json", dict(schema="forge.delivery.classic-installer.v1",
          status="BUILD_ONLY", version=args.version, output=str(output), bytes=output.stat().st_size,
          sha256=digest(output), source_manifest_sha256=digest(evidence / "source-inputs.json"),
          orchestration=inventory(evidence / "orchestration"), packages=entries,
          component_builds=dict(launcher=build_proof, baseline_game_studio=old_proof),
          offline_prerequisite_sha256=digest(prereq), gate3_closed=False, release_qualified=False))
    print(json.dumps(dict(output=str(output), bytes=output.stat().st_size, sha256=digest(output))), flush=True)


if __name__ == "__main__":
    main()
