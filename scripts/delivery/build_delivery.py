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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delivery-cli", required=True, type=Path)
    parser.add_argument("--hub", required=True, type=Path)
    parser.add_argument("--studio", required=True, type=Path)
    parser.add_argument("--studio-proof", required=True, type=Path)
    parser.add_argument("--playtest", required=True, type=Path)
    parser.add_argument("--runtime", type=Path, help="Explicit freshly rebuilt game; defaults to the playtest runtime")
    parser.add_argument("--source-manifest", type=Path, help="Hash-pinned build input inventory")
    parser.add_argument("--runtime-proof", type=Path)
    parser.add_argument("--webview2", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--version", default="0.1.0-preview.20261008")
    parser.add_argument("--source-revision", required=True)
    args = parser.parse_args()
    runtime = args.runtime or args.playtest / "runtime/tidewater.exe"
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
    for marker in ["runtime/tidewater.exe", "data/scene/fixture.json", "data/scene/LICENSE-Tidewater.txt",
                   "data/scene/CREDITS-Tidewater.md", "data/workspace", "licenses"]:
        if not (args.playtest / marker).exists():
            raise ValueError(f"Missing runtime input: {marker}")
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
    copy(args.studio, core / "bin/forge-studio.exe")
    copy(runtime, core / "bin/forge-game.exe")
    copy(args.studio_proof, core / "provenance/studio-build-proof.json")
    if args.runtime_proof:
        copy(args.runtime_proof, core / "provenance/game-build-proof.json")
    if args.source_manifest:
        copy(args.source_manifest, core / "provenance/source-inputs.json")
    copy(args.playtest / "licenses", core / "licenses")
    (core / "PROVENANCE.md").write_text(
        "# Applications natives de la livraison locale\n\n"
        "Launcher : nouveau hub Tauri/Rust. Éditeur : candidat Studio identifié par sa preuve de build.\n"
        "Jeu natif : promenade Tidewater existante, pas la qualification de l’aventure RPG Gate 3.\n"
        "La compilation et le packaging ne constituent pas une qualification de release.\n"
        "Windows x64 et WebView2 sont requis. Aucun Cargo, Node ou Python requis côté client.\n", encoding="utf-8")
    data = "{pack:reference-assets}/data"
    content = "{pack:reference-assets}/content"
    scene = json.loads((assets / "data/scene/fixture.json").read_bytes())
    terrain = next((module / "workspace").glob("*/authoring.json"), None)
    area = terrain.parent.name if terrain else "e620dd19-ead9-5913-a6d6-0f93109148c0"
    # The current native entry-point ABI lives in this recipe, never in installer code.
    building_pin = "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6"
    launch = dict(executable="bin/forge-game.exe", args=[f"{data}/scene", "{user}/captures/game.png",
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
                   apps={"launcher":dict(executable="bin/forge-hub.exe", args=["--root", "{install}"], env={}, clear_env_prefixes=[]),
                         "game":launch, "editor":dict(executable="bin/forge-studio.exe", args=[],
                         env={"FORGE_AUTHORING_ROOT":"{workspace}", "OPENRPG_ASSET_VAULT":"{pack:asset-library}"},
                         clear_env_prefixes=["FORGE_AUTHORING_","FORGE_TIDEWATER_","WEBVIEW2_"])},
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
          launcher_sha256=digest(args.hub), cli_sha256=digest(args.delivery_cli),
          game_capability="native_walk_preview", gate3_closed=False, release_qualified=False))
    print(json.dumps(dict(output=str(args.output), packages=3, status="local_preview")))


if __name__ == "__main__":
    main()
