"""Assemble a local preview from explicit, existing native inputs, never rebuild/qualify them.

The client needs only the native executables and detached .forgepack files.
Development Python is not shipped or needed on the client.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

from complete_rebuild import frontend_inputs, shared_source_inputs, source_inputs_equal


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def regular(path):
    stat = path.lstat()
    if path.is_symlink() or getattr(stat, "st_file_attributes", 0) & 0x400:
        raise ValueError(f"Link/reparse point refused: {path}")


def copy(source, destination):
    source, destination = Path(source), Path(destination)
    regular(source)
    if source.is_file():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        if digest(source) != digest(destination):
            raise ValueError(f"Source changed while copying: {source}")
        return
    destination.mkdir(parents=True, exist_ok=True)
    for entry in sorted(source.iterdir()):
        # User saves and derived caches are not an authoring seed.
        if entry.name in {".game-saves", ".git", "target", "__pycache__"}:
            continue
        copy(entry, destination / entry.name)


def run(executable, *args):
    result = subprocess.run([str(executable), *map(str, args)], capture_output=True, text=True,
                            encoding="utf-8", check=False)
    if result.returncode:
        raise RuntimeError(result.stderr[-5000:])
    return json.loads(result.stdout)


def descriptor(id, label, kind, version, source_revision, product=None):
    return dict(schema="forge.delivery.package.v1", id=id, label=label, version=version,
                kind=kind, platform="windows-x64" if kind == "applications" else "any",
                source_revision=source_revision, rights="private_local_only",
                license_paths=["PROVENANCE.md"], files=[], product=product)


def package(cli, staging, delivery, d):
    description = staging.parent / f"{d['id']}.descriptor.json"
    write(description, d)
    output = delivery / f"{d['id']}.forgepack"
    result = run(cli, "pack", staging, description, output)
    write(delivery / f"{d['id']}.manifest.json", result["descriptor"])
    return dict(file=output.name, id=d["id"], kind=d["kind"], version=d["version"],
                manifest_sha256=result["manifest_sha256"], archive_sha256=result["archive_sha256"],
                bytes=output.stat().st_size, rights=d["rights"], status="local_preview")


def covers_source_scope(manifest, scope):
    scopes = [value.rstrip('/') for value in manifest.get('scope', [])]
    declared = any(scope == value or scope.startswith(value + '/') for value in scopes)
    files = manifest.get('files', {})
    if isinstance(files, list):
        files = {entry['path']: entry for entry in files}
    return declared and f'{scope}/Cargo.toml' in files


def validate_shared_applications(args, manifest, applications, launcher_proof):
    repo = Path(__file__).resolve().parents[2]
    if (manifest.get('schema') != 'forge.delivery.shared-source-inputs.v1' or
            Path(manifest.get('source_root', '')).resolve() != repo or
            not source_inputs_equal(shared_source_inputs(repo), manifest)):
        raise ValueError('Application sources differ from the shared input receipt')
    manifest_hash = digest(args.source_manifest)
    for executable, proof in applications:
        if (proof.get('schema') != 'forge.delivery.native-build.v1' or
                proof.get('status') != 'BUILD_ONLY' or not proof.get('shared_working_tree') or
                proof.get('source_root') != str(repo) or
                not proof.get('source_stable_during_build') or
                proof.get('source_manifest_sha256') != manifest_hash or
                Path(proof.get('executable', '')).resolve() != executable.resolve() or
                proof.get('executable_sha256') != digest(executable)):
            raise ValueError('Current Studio/Game receipt required; historical application refused')
    if (not launcher_proof.get('shared_working_tree') or
            launcher_proof.get('source_root') != str(repo) or
            launcher_proof.get('status') != 'BUILD_ONLY' or not launcher_proof.get('production') or
            not launcher_proof.get('source_stable_during_build') or
            launcher_proof.get('source_manifest_sha256') != manifest_hash or
            launcher_proof.get('current_source_manifest_sha256') != manifest_hash):
        raise ValueError('Launcher/Hub/CLI must have a current shared build receipt')
    for name, executable in [('forge-launcher.exe', args.launcher), ('forge-hub.exe', args.hub),
                             ('forge-delivery.exe', args.delivery_cli)]:
        if (launcher_proof.get('outputs', {}).get(name) != digest(executable) or
                Path(launcher_proof.get('output_paths', {}).get(name, '')).resolve() != executable.resolve()):
            raise ValueError(f'Current shared native output changed: {name}')
    for scope in ('apps/forge_launcher', 'apps/forge_hub', 'tools/forge_delivery'):
        if not covers_source_scope(manifest, scope):
            raise ValueError(f'Shared source manifest does not cover {scope}')
    launcher_frontend = repo / 'apps/forge_launcher/dist'
    if (launcher_proof.get('frontend') != str(launcher_frontend) or
            launcher_proof.get('frontend_pins') != frontend_inputs(launcher_frontend)):
        raise ValueError('Aube production frontend differs from the embedded build inputs')
    studio_proof = applications[1][1]
    frontend = repo / 'apps/forge_studio/dist'
    if (studio_proof.get('frontend') != str(frontend) or
            studio_proof.get('frontend_pins') != frontend_inputs(frontend)):
        raise ValueError('Canonical Studio frontend differs from the embedded build inputs')
    static_receipt = frontend / 'forge-studio-local-assets-manifest.json'
    receipt = json.loads(static_receipt.read_text(encoding='utf-8-sig'))
    staged = {entry['path']: entry['sha256'] for entry in frontend_inputs(frontend)
              if entry['path'].startswith(('forge-imports/', 'forge-kit/'))}
    imports = sum(name.startswith('forge-imports/') for name in staged)
    kit = sum(name.startswith('forge-kit/') for name in staged)
    if (receipt.get('schema') != 'forge.studio.local-assets.v1' or
            receipt.get('status') != 'STAGED' or not receipt.get('assets_present') or
            not receipt.get('source_unchanged_during_staging') or
            not receipt.get('destination_verified') or receipt.get('files') != staged or
            receipt.get('import_files') != imports or receipt.get('kit_files') != kit or
            not imports or not kit):
        raise ValueError('Complete authenticated Studio static assets are required for installation')
    static_manifest = Path(receipt['asset_root']) / 'frontend-static-inputs.json'
    if digest(static_manifest) != receipt.get('source_manifest_sha256'):
        raise ValueError('Studio static source manifest differs from its staging receipt')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delivery-cli", required=True, type=Path)
    parser.add_argument("--hub", required=True, type=Path)
    parser.add_argument("--launcher", required=True, type=Path, help="Actual Aube production launcher, distinct from the content manager")
    parser.add_argument("--aube-proof", required=True, type=Path, help="Production installed-launcher build proof; portable launchers are not installation adapters")
    parser.add_argument("--studio", required=True, type=Path)
    parser.add_argument("--studio-proof", required=True, type=Path)
    parser.add_argument("--playtest", required=True, type=Path)
    parser.add_argument("--runtime", type=Path, help="Explicit freshly rebuilt game; defaults to the playtest runtime")
    parser.add_argument("--source-manifest", type=Path, help="Hash-pinned build input inventory")
    parser.add_argument("--runtime-proof", type=Path)
    parser.add_argument("--launcher-proof", type=Path)
    parser.add_argument("--area-id")
    parser.add_argument("--webview2", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--version", default="0.1.0-preview.20261008")
    parser.add_argument("--source-revision", required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--shared-working-tree', action='store_true')
    mode.add_argument('--frozen-archive', action='store_true', help='Historical assembly inputs only')
    args = parser.parse_args()
    assemble(args)


def assemble(args):
    if args.shared_working_tree and not all((args.runtime, args.runtime_proof,
                                           args.source_manifest, args.launcher_proof)):
        raise ValueError('Shared assembly requires current Game, Studio and launcher build receipts')
    aube_proof = json.loads(args.aube_proof.read_bytes())
    aube_sources = args.aube_proof.parent / "source-inputs.json"
    if (aube_proof.get("outputs", {}).get("forge-launcher.exe") != digest(args.launcher) or
        not aube_proof.get("source_stable_during_build") or
        aube_proof.get("source_manifest_sha256") != digest(aube_sources) or
        not covers_source_scope(json.loads(aube_sources.read_bytes()), "apps/forge_launcher")):
        raise ValueError("Installed Aube launcher does not match its source build proof")
    runtime = args.runtime or args.playtest / "runtime/tidewater.exe"
    if args.runtime and (not args.runtime_proof or not args.source_manifest):
        raise ValueError("A rebuilt runtime requires its proof and source manifest")
    if args.runtime_proof:
        runtime_proof = json.loads(args.runtime_proof.read_bytes())
        if digest(runtime) != runtime_proof["executable_sha256"].lower():
            raise ValueError("Game does not match its build proof")
    args.output = args.output.absolute()
    if args.output.exists():
        raise ValueError("Output exists; choose a new delivery revision")
    proof = json.loads(args.studio_proof.read_bytes())
    if digest(args.studio) != proof["executable_sha256"].lower():
        raise ValueError("Studio does not match its build proof")
    if args.runtime:
        source_manifest_hash = digest(args.source_manifest)
        source_manifest = json.loads(args.source_manifest.read_bytes())
        if source_manifest["head"] != args.source_revision:
            raise ValueError("Source revision differs from the frozen manifest")
        for build in (runtime_proof, proof):
            if (build.get("schema") != "forge.delivery.native-build.v1" or
                build.get("status") != "BUILD_ONLY" or not build.get("source_stable_during_build") or
                build.get("source_manifest_sha256") != source_manifest_hash):
                raise ValueError("Rebuilt application proof does not match the frozen sources")
    if args.launcher_proof:
        launcher_proof = json.loads(args.launcher_proof.read_bytes())
        if (launcher_proof.get("current_source_manifest_sha256") != digest(args.source_manifest) or
            launcher_proof["outputs"]["forge-hub.exe"] != digest(args.hub) or
            launcher_proof["outputs"]["forge-delivery.exe"] != digest(args.delivery_cli)):
            raise ValueError("Launcher/installer build proof mismatch")
    if args.shared_working_tree:
        if aube_proof != launcher_proof or digest(aube_sources) != digest(args.source_manifest):
            raise ValueError('All installed applications must use the same shared source manifest')
        validate_shared_applications(args, source_manifest,
                                     [(runtime, runtime_proof), (args.studio, proof)], launcher_proof)
    launcher_check = subprocess.run([str(args.launcher), "--verify-distributable"], capture_output=True, text=True)
    if launcher_check.returncode or "FORGE_LAUNCHER_AUBE_FRONTEND_READY" not in launcher_check.stdout or digest(args.launcher) == digest(args.hub):
        raise ValueError("An embedded Aube launcher distinct from the Hub is required")
    markers = ["data/scene/fixture.json", "data/scene/LICENSE-Tidewater.txt",
               "data/scene/CREDITS-Tidewater.md", "data/workspace", "licenses"]
    if not args.runtime:
        markers.append('runtime/tidewater.exe')
    for marker in markers:
        if not (args.playtest / marker).exists():
            raise ValueError(f"Missing runtime input: {marker}")
    module_catalog = args.playtest / "data/scene/static-assets.json"
    if not module_catalog.is_file():
        raise ValueError(f"Missing regular Studio module asset catalog: {module_catalog}")
    regular(module_catalog)
    args.output.mkdir(parents=True)
    work = args.output / "build-inputs"
    work.mkdir()
    assets = work / "assets"
    assets.mkdir()
    for path in sorted((args.playtest / "data").iterdir()):
        if path.name != "workspace":
            copy(path, assets / "data" / path.name)
    copy(args.playtest / "content", assets / "content")
    copy(args.playtest / "licenses", assets / "licenses")
    (assets / "PROVENANCE.md").write_text(
        "# Assets de la scène de référence\n\nSnapshot local des données et crédits du playtest existant.\n"
        f"Source : {args.playtest.name}\nRévision de livraison : {args.source_revision}\n"
        "Les licences et métadonnées originales sont conservées.\n"
        "Droits de redistribution globale : non qualifiés ; utilisation privée locale.\n", encoding="utf-8")
    asset_entry = package(args.delivery_cli, assets, args.output,
                          descriptor("reference-assets", "Assets de l’aventure de référence", "assets", args.version, args.source_revision))
    module = work / "module"
    module.mkdir()
    copy(args.playtest / "data/workspace", module / "workspace")
    (module / "PROVENANCE.md").write_text(
        "# Sources authorées du module\n\nCopie immuable de l’autorité workspace existante.\n"
        "Création d’une copie utilisateur une seule fois lors de l’installation ; mises à jour sans écrasement des retouches.\n"
        "Les sauvegardes personnelles sont exclues. Le module conserve IDs, journaux et archives d’auteur.\n", encoding="utf-8")
    module_entry = package(args.delivery_cli, module, args.output,
                           descriptor("reference-module", "Module de référence", "module", args.version, args.source_revision))
    core = work / "applications"
    core.mkdir()
    copy(args.delivery_cli, core / "bin/forge-delivery.exe")
    copy(args.hub, core / "bin/forge-hub.exe")
    copy(args.launcher, core / "bin/forge-launcher.exe")
    copy(args.aube_proof, core / "provenance/aube-build-proof.json")
    copy(args.studio, core / "bin/forge-studio.exe")
    copy(runtime, core / "bin/forge-game.exe")
    copy(args.studio_proof, core / "provenance/studio-build-proof.json")
    if args.runtime_proof:
        copy(args.runtime_proof, core / "provenance/game-build-proof.json")
    if args.source_manifest:
        copy(args.source_manifest, core / "provenance/source-inputs.json")
    if args.launcher_proof:
        copy(args.launcher_proof, core / "provenance/launcher-build-proof.json")
    if args.shared_working_tree:
        receipt = Path(proof['frontend']) / 'forge-studio-local-assets-manifest.json'
        copy(receipt, core / 'provenance/studio-local-assets-manifest.json')
        static_manifest = Path(json.loads(receipt.read_text(encoding='utf-8-sig'))['asset_root']) / 'frontend-static-inputs.json'
        copy(static_manifest, core / 'provenance/studio-static-inputs.json')
    copy(args.playtest / "licenses", core / "licenses")
    (core / "PROVENANCE.md").write_text(
        "# Applications natives de la livraison locale\n\n"
        "Launcher : L’Aube des royaumes. Hub : gestionnaire de contenu séparé. Studio et jeu sont identifiés par leurs preuves de build.\n"
        "Jeu natif : promenade Tidewater existante, pas la qualification de l’aventure RPG Gate 3.\n"
        "La compilation et le packaging ne constituent pas une qualification de release.\n"
        "Windows x64 et WebView2 sont requis. Aucun Cargo, Node ou Python requis côté client.\n", encoding="utf-8")
    data = "{pack:reference-assets}/data"
    content = "{pack:reference-assets}/content"
    scene = json.loads((assets / "data/scene/fixture.json").read_bytes())
    areas = []
    for terrain in sorted((module / "workspace").glob("*/authoring.json")):
        document = json.loads(terrain.read_bytes())
        area_id = document.get("head", {}).get("base", {}).get("terrain", {}).get("area_id")
        if area_id:
            if area_id != terrain.parent.name:
                raise ValueError("Authoring area identity differs from its directory")
            areas.append(area_id)
    if args.area_id:
        if args.area_id not in areas:
            raise ValueError("Requested authoring area missing from the module")
        area = args.area_id
    elif len(areas) == 1:
        area = areas[0]
    else:
        raise ValueError("Select one authoring area explicitly with --area-id")
    # The current native entry-point ABI lives in this recipe, never in installer code.
    building_pin = digest(assets / 'data/buildings/package.json') if args.shared_working_tree else "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6"
    launch = dict(executable="bin/forge-game.exe", args=[f"{data}/scene", "{user}/captures/game-{session}.png",
                  "--water-spray-clouds", f"{data}/ocean", f"{data}/shore", f"{data}/spray", f"{data}/clouds",
                  "--workspace-vegetation", "{workspace}", area, "--building-package", f"{data}/buildings", building_pin, "--walk"],
                  env={"FORGE_TIDEWATER_DEBRIS_ROOT":f"{content}/debris/procedural",
                       "FORGE_TIDEWATER_SCANNED_ROOT":f"{content}/debris/scanned",
                       "FORGE_TIDEWATER_GAME_FISH_ROOT":f"{content}/fish/game",
                       "FORGE_TIDEWATER_FISH_PROP_ROOT":f"{content}/fish/surface",
                       "FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT":f"{content}/debris/pebbles",
                       "FORGE_TIDEWATER_SCANNED_PROGRAM":f"{content}/debris/scanned.wgsl"},
                  clear_env_prefixes=["FORGE_TIDEWATER_"])
    product = dict(schema="forge.delivery.product.v1", id="openrpg-forge", title="OpenRPG Forge",
                   apps={"launcher":dict(executable="bin/forge-launcher.exe", args=["--root", "{install}"], env={}, clear_env_prefixes=[]),
                         "content-manager":dict(executable="bin/forge-hub.exe", args=["--root", "{install}"], env={}, clear_env_prefixes=[]),
                         "game":launch, "editor":dict(executable="bin/forge-studio.exe", args=[],
                         env={"FORGE_AUTHORING_ROOT":"{workspace}", "OPENRPG_ASSET_VAULT":"{pack:asset-library}",
                              "FORGE_NATIVE_GAME_EXECUTABLE":"{app}/bin/forge-game.exe",
                              "FORGE_NATIVE_GAME_CONTENT_ROOT":"{pack:reference-assets}",
                              "FORGE_NATIVE_GAME_SESSION_ROOT":"{user}/game-sessions",
                              "FORGE_MODULE_ASSET_ROOT":f"{data}/scene",
                              "FORGE_MODULE_ASSET_CATALOG":"static-assets.json",
                              "FORGE_MODULE_ASSET_CATALOG_SHA256":digest(assets / "data/scene/static-assets.json")},
                         clear_env_prefixes=["FORGE_AUTHORING_","FORGE_TIDEWATER_","FORGE_NATIVE_GAME_","FORGE_MODULE_ASSET_","WEBVIEW2_"])},
                   required_packs=[dict(id=p["id"],manifest_sha256=p["manifest_sha256"],kind=p["kind"])
                                   for p in (asset_entry,module_entry)], optional_packs=["asset-library"],
                   authoring_seed=dict(pack_id="reference-module",path="workspace"))
    application_entry = package(args.delivery_cli, core, args.output,
                                descriptor("forge-applications", "Launcher, éditeur et jeu", "applications", args.version, args.source_revision, product))
    copy(args.hub, args.output / "ForgeSetup.exe")
    copy(args.delivery_cli, args.output / "forge-delivery.exe")
    copy(args.webview2, args.output / "prerequisites/MicrosoftEdgeWebView2RuntimeInstallerX64.exe")
    copy(Path(__file__).parent / "Start-Forge.ps1", args.output / "Start-Forge.ps1")
    copy(Path(__file__).parent / "Start-Forge.cmd", args.output / "Start-Forge.cmd")
    write(args.output / "delivery.json", dict(schema="forge.delivery.index.v1", version=args.version,
          platform="windows-x64", status="local_preview", packages=[asset_entry,module_entry,application_entry],
          setup_sha256=digest(args.output/"ForgeSetup.exe"), library_index="asset-library-transfer.json",
          prerequisites={"webview2":{"file":"prerequisites/MicrosoftEdgeWebView2RuntimeInstallerX64.exe", "sha256":digest(args.webview2), "mode":"offline_installer"}}))
    (args.output / "LISEZ-MOI.txt").write_text(
        "OpenRPG Forge — livraison locale préparatoire\n\n"
        "1. Conserver ForgeSetup.exe et les trois .forgepack dans le même dossier.\n"
        "2. Ouvrir Start-Forge.cmd (vérifie WebView2), puis Préparer cette livraison et Installer ces packages.\n"
        "3. Jouer ouvre le jeu natif actuel ; Ouvrir l’éditeur lance le Studio.\n"
        "Les fichiers sont vérifiés. Les versions précédentes, projets et sauvegardes sont conservés.\n"
        "Les assets sont séparés des applications. La bibliothèque privée complète se prépare à part.\n"
        "Cette livraison locale n’est pas une release qualifiée ni une clôture de Gate 3.\n", encoding="utf-8")
    write(args.output / "input-provenance.json", dict(schema="forge.delivery.inputs.v1",
          assembly_script_sha256=digest(Path(__file__)),
          source_revision=args.source_revision, studio_sha256=digest(args.studio),
          studio_build_proof_sha256=digest(args.studio_proof), game_sha256=digest(runtime),
          source_manifest_sha256=digest(args.source_manifest) if args.source_manifest else None,
          game_build_proof_sha256=digest(args.runtime_proof) if args.runtime_proof else None,
          launcher_sha256=digest(args.launcher), content_manager_sha256=digest(args.hub), cli_sha256=digest(args.delivery_cli),
          shared_working_tree=args.shared_working_tree,
          game_capability="native_walk_preview", gate3_closed=False, release_qualified=False))
    if args.shared_working_tree:
        validate_shared_applications(args, source_manifest,
                                     [(runtime, runtime_proof), (args.studio, proof)], launcher_proof)
    print(json.dumps(dict(output=str(args.output), packages=3, status="local_preview")))


if __name__ == "__main__":
    main()
