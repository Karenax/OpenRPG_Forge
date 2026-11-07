"""Collect dependency notices for the legacy Windows playtest or explicit shipping scopes."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
from pathlib import Path


NOTICE_FILE_RE = re.compile(r"(?:^|[._-])(license|licence|copying|notice|copyright)(?:[._-].*)?$", re.I)


def metadata(
    manifest: Path,
    *,
    target: str = "x86_64-pc-windows-msvc",
    features: tuple[str, ...] = (),
    no_default_features: bool = False,
) -> dict:
    command = [
        "cargo", "metadata", "--manifest-path", str(manifest),
        "--format-version", "1", "--locked", "--offline", "--filter-platform", target,
    ]
    if features:
        command.extend(["--features", ",".join(features)])
    if no_default_features:
        command.append("--no-default-features")
    completed = subprocess.run(
        command,
        check=True,
        capture_output=True,
    )
    return json.loads(completed.stdout)


def dependencies(document: dict, root_name: str, *, include_root_dev: bool = False) -> dict[str, dict]:
    packages = {package["id"]: package for package in document["packages"]}
    nodes = {node["id"]: node for node in document["resolve"]["nodes"]}
    roots = [package["id"] for package in packages.values() if package["name"] == root_name]
    if len(roots) != 1:
        raise ValueError(f"Expected one Cargo package named {root_name}, found {len(roots)}")
    pending = roots[:]
    seen: set[str] = set()
    while pending:
        package_id = pending.pop()
        if package_id in seen:
            continue
        seen.add(package_id)
        for dependency in nodes[package_id]["deps"]:
            if any(
                kind.get("kind") != "dev" or (include_root_dev and package_id == roots[0])
                for kind in dependency["dep_kinds"]
            ):
                pending.append(dependency["pkg"])
    return {package_id: packages[package_id] for package_id in seen}


def notice_relative_path(root: Path, source: Path) -> Path:
    try:
        return source.relative_to(root)
    except ValueError:
        # Keep the historical basename behavior for an absolute license_file.
        return Path(source.name)


def notice_files(package: dict) -> list[Path]:
    root = Path(package["manifest_path"]).parent
    # Registry/git archives can carry component notices below their package root;
    # path packages may contain unrelated frontend/assets trees.
    candidates = root.rglob("*") if package.get("source") else root.iterdir()
    names = {
        path
        for path in candidates
        if path.is_file() and NOTICE_FILE_RE.search(path.name)
    }
    if package.get("license_file"):
        path = root / package["license_file"]
        if path.is_file():
            names.add(path)
    return sorted(names, key=lambda path: notice_relative_path(root, path).as_posix().casefold())


def collect(forge_manifest: Path, launcher_manifest: Path, output: Path) -> dict:
    forge = dependencies(metadata(forge_manifest), "forge_engine")
    launcher = dependencies(metadata(launcher_manifest), "forge_launcher_prototype")
    return write_notices({**forge, **launcher}, output, {
        "schema": "forge.windows-playtest.rust-dependency-notices.v1",
        "target": "x86_64-pc-windows-msvc",
        "root_packages": ["forge_engine", "forge_launcher_prototype"],
    })


def collect_scopes(
    scopes: list[dict], output: Path, *, supplements: Path | None = None, supplement_lock: Path | None = None,
    forge_root: Path | None = None, forge_receipt: Path | None = None, forge_spec: Path | None = None,
) -> dict:
    """Conservative Cargo closures for explicit shipping roots, not a linked SBOM.

    A shipped example may opt into its root's dev-dependencies; dependencies'
    own tests/examples never become shipping roots implicitly. Metadata still
    includes build dependencies and resolver feature unification.
    """
    if (supplements is None) != (supplement_lock is None):
        raise ValueError("Supplement root and verification lock must be supplied together")
    forge_inputs = (forge_root, forge_receipt, forge_spec)
    if any(value is not None for value in forge_inputs) and (any(value is None for value in forge_inputs) or supplements is None):
        raise ValueError("Forge licences require a root, receipt, specification, and verified supplements")
    if not scopes:
        raise ValueError("At least one explicit shipping scope is required")
    packages = {}
    requests = []
    documents = {}
    for scope in scopes:
        manifest = Path(scope["manifest"]).resolve(strict=True)
        target = scope["target"]
        supplied_features = scope.get("features", ())
        if not isinstance(supplied_features, (list, tuple)):
            raise ValueError("Cargo features must be a sequence of individual names")
        features = tuple(supplied_features)
        no_default = scope.get("no_default_features", False)
        include_dev = scope.get("include_root_dev", False)
        if not target or type(no_default) is not bool or type(include_dev) is not bool:
            raise ValueError("Invalid explicit target or feature/dev policy")
        if not all(isinstance(feature, str) and feature and "," not in feature for feature in features):
            raise ValueError("Cargo features must be nonempty individual names")
        key = (manifest, target, features, no_default)
        if key not in documents:
            documents[key] = metadata(manifest, target=target, features=features, no_default_features=no_default)
        selected = dependencies(documents[key], scope["package"], include_root_dev=include_dev)
        packages.update(selected)
        requests.append({
            "manifest": str(manifest), "package": scope["package"], "target": target,
            "features": list(features), "no_default_features": no_default,
            "include_root_dev": include_dev, "package_ids": sorted(selected),
        })
    index = write_notices(packages, output, {
        "schema": "forge.rust-dependency-notices.v2",
        "targets": sorted({scope["target"] for scope in requests}),
        "root_packages": sorted({scope["package"] for scope in requests}),
        "scope": "Conservative Cargo normal/build closures, with explicit root dev-dependencies for shipped examples; not an exact linked SBOM or all asset/frontend/system notices",
        "requests": requests,
        "notice_storage": "One byte-identical text per SHA-256, with per-package source filenames",
    }, deduplicate=True)
    if supplements is not None:
        assert supplement_lock is not None
        index["supplement_summary"] = include_supplements(packages, output, supplements, supplement_lock)
    if forge_root is not None and forge_receipt is not None and forge_spec is not None:
        index["forge_license_summary"] = include_forge_licenses(packages, output, forge_root, forge_receipt, forge_spec)
    return index


def _physical_file(path: Path) -> bytes:
    """Reject links/reparse points and oversized notice input before reading."""
    info = path.lstat()
    if path.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400 or not path.is_file():
        raise ValueError(f"Notice input is not a physical file: {path}")
    if info.st_size > 16 * 1024 * 1024:
        raise ValueError(f"Notice input is oversized: {path}")
    data = path.read_bytes()
    after = path.stat(follow_symlinks=False)
    if (info.st_size, info.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
        raise ValueError(f"Notice input changed while reading: {path}")
    return data


def _digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _physical_directory_chain(path: Path) -> None:
    """Reject a linked ancestor before entering a notice source directory."""
    if path.drive and not path.is_absolute():
        raise ValueError("Notice directory has an ambiguous drive-relative path")
    absolute = path if path.is_absolute() else Path.cwd() / path
    directory = Path(absolute.anchor)
    for part in (None, *absolute.parts[1:]):
        if part == "..":
            directory = directory.parent
        elif part is not None:
            directory = directory / part
        info = directory.lstat()
        if (stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400
            or not stat.S_ISDIR(info.st_mode)):
            raise ValueError(f"Notice directory is not physical: {directory}")


def _supplement_text(root: Path, relative: str, digest: str, length: int | None) -> bytes:
    if not re.fullmatch(r"(?:texts|external-texts)/[0-9a-f]{64}\.txt", relative):
        raise ValueError("Unsafe supplement text path")
    if relative.rsplit("/", 1)[1] != digest + ".txt":
        raise ValueError("Supplement text path differs from its digest")
    directory = root / relative.split("/", 1)[0]
    info = directory.lstat()
    if directory.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400 or not directory.is_dir():
        raise ValueError("Supplement text directory is not physical")
    data = _physical_file(directory / (digest + ".txt"))
    if _digest(data) != digest or (length is not None and len(data) != length):
        raise ValueError("Supplement text differs from its receipt")
    return data


def _unresolved_transitive_references(packages: dict[str, dict], base_packages: dict[str, dict],
                                      upstream: dict, stage: Path, vcs_digests: set[str],
                                      prepared: dict[str, bytes]) -> list[dict]:
    """Preserve source-pinned references without resolving a package notice.

    The stage lock and hashes protect byte identity. This function does not fetch
    sources, establish an ancestral relationship or decide legal applicability.
    """
    items = upstream.get("unresolved_transitive_references", [])
    if not isinstance(items, list) or len(items) > 128:
        raise ValueError("Invalid transitive reference list")
    results = []
    seen: set[str] = set()
    transitive_texts: set[str] = set()
    transitive_bytes = 0

    def pinned_text(value: dict) -> dict:
        nonlocal transitive_bytes
        if (not isinstance(value, dict) or set(value) != {"path", "sha256", "bytes"}
            or not isinstance(value["path"], str)
            or not isinstance(value["sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", value["sha256"])
            or type(value["bytes"]) is not int or not 0 <= value["bytes"] <= 1024 * 1024):
            raise ValueError("Invalid transitive reference text pin")
        if value["sha256"] not in transitive_texts and transitive_bytes + value["bytes"] > 16 * 1024 * 1024:
            raise ValueError("Transitive reference text budget exceeded")
        data = _supplement_text(stage, value["path"], value["sha256"], value["bytes"])
        if value["sha256"] not in transitive_texts:
            transitive_texts.add(value["sha256"])
            transitive_bytes += len(data)
        previous = prepared.setdefault(value["sha256"], data)
        if previous != data:
            raise ValueError("Different notice bytes for the same digest")
        return {"path": f"texts/{value['sha256']}.txt", "sha256": value["sha256"], "bytes": len(data)}

    expected_keys = {"package_id", "package_revision", "package_manifest_sha256", "package_source_path",
                     "package_source_sha256", "evidence", "documents", "exact_source_confirmed"}
    document_keys = {"path", "sha256", "bytes", "source_repository", "source_revision", "source_path"}
    for item in items:
        if (not isinstance(item, dict) or set(item) != expected_keys
            or item["exact_source_confirmed"] is not False):
            raise ValueError("Invalid or promoted transitive reference")
        identity = item["package_id"]
        if (not isinstance(identity, str) or identity in seen or identity not in packages
            or identity not in base_packages):
            raise ValueError("Duplicate or absent transitive package")
        seen.add(identity)
        current, previous = packages[identity], base_packages[identity]
        if (not current.get("source") or any(key in previous and previous[key] != current.get(key)
            for key in ("name", "version", "license", "repository", "source"))):
            raise ValueError("Transitive package metadata differs")
        revision = item["package_revision"]
        if not isinstance(revision, str) or not re.fullmatch(r"[0-9a-f]{40}", revision):
            raise ValueError("Invalid transitive package revision")
        for key in ("package_manifest_sha256", "package_source_sha256"):
            if not isinstance(item[key], str) or not re.fullmatch(r"[0-9a-f]{64}", item[key]):
                raise ValueError("Invalid transitive package source pin")
        source_name = item["package_source_path"]
        if (not isinstance(source_name, str)
            or not re.fullmatch(r"src/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+", source_name)
            or any(part in {".", ".."} for part in source_name.split("/"))):
            raise ValueError("Unsafe transitive package source path")
        root = Path(current["manifest_path"]).parent
        _physical_directory_chain(root / Path(source_name).parent)
        vcs = _physical_file(root / ".cargo_vcs_info.json")
        vcs_info = json.loads(vcs)
        if (_digest(vcs) not in vcs_digests or vcs_info.get("git", {}).get("sha1") != revision
            or _digest(_physical_file(root / "Cargo.toml.orig")) != item["package_manifest_sha256"]
            or _digest(_physical_file(root / source_name)) != item["package_source_sha256"]):
            raise ValueError("Transitive package source differs from its pins")
        evidence = pinned_text(item["evidence"])
        documents = item["documents"]
        if not isinstance(documents, list) or not 1 <= len(documents) <= 32:
            raise ValueError("Invalid transitive reference documents")
        origins: set[tuple[str, str, str]] = set()
        preserved = []
        for document in documents:
            if not isinstance(document, dict) or set(document) != document_keys:
                raise ValueError("Invalid transitive reference document")
            repository, ref, source_path = (document["source_repository"], document["source_revision"],
                                            document["source_path"])
            if (not isinstance(repository, str)
                or not re.fullmatch(r"https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository)
                or any(part in {".", ".."} for part in repository.split("/")[-2:])
                or not isinstance(ref, str) or not re.fullmatch(r"[0-9a-f]{40}", ref)
                or not isinstance(source_path, str)
                or not re.fullmatch(r"(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+", source_path)
                or any(part in {".", ".."} for part in source_path.split("/"))):
                raise ValueError("Invalid transitive document origin")
            origin = (repository, ref, source_path)
            if origin in origins:
                raise ValueError("Duplicate transitive document origin")
            origins.add(origin)
            text = pinned_text({key: document[key] for key in ("path", "sha256", "bytes")})
            preserved.append({**text, "source_repository": repository, "source_revision": ref,
                              "source_path": source_path, "source_url": f"{repository}/blob/{ref}/{source_path}"})
        results.append({"package_id": identity, "name": current["name"], "version": current["version"],
                        "declared_license": current.get("license"), "package_repository": current.get("repository"),
                        "package_revision": revision, "package_manifest_sha256": item["package_manifest_sha256"],
                        "package_source_path": source_name, "package_source_sha256": item["package_source_sha256"],
                        "reference_type": "transitive_source_reference", "evidence": evidence, "documents": preserved,
                        "exact_source_confirmed": False, "comparison_verified_by_collector": False,
                        "document_origin_verified_by_collector": False, "package_notice_supplied": False,
                        "applicability_review_required": True, "distribution_qualified": False})
    return results


def include_supplements(packages: dict[str, dict], output: Path, stage: Path, lock_path: Path) -> dict:
    """Add pinned supplements only to the exact Cargo inventory they were prepared for.

    The result is a portable index and deduplicated texts. It is not a licence
    choice, an exact linked SBOM, or a distribution-compliance decision.
    """
    _physical_directory_chain(stage)
    raw = {name: _physical_file(stage / name) for name in (
        "INDEX.json", "UPSTREAM-NOTICES.json", "STANDARD-NOTICES.json", "GITLAB-NOTICES.json")}
    lock_bytes = _physical_file(lock_path)
    lock = json.loads(lock_bytes)
    if (lock.get("schema") != "forge.gate7.notice-supplement-cross-check.v1"
        or lock.get("supplement_texts_verified") is not True
        or lock.get("index_sha256") != {name: _digest(data) for name, data in raw.items()}):
        raise ValueError("Supplement indices differ from the verification lock")
    if _physical_file(output / "INDEX.json") != raw["INDEX.json"]:
        raise ValueError("Supplements belong to a different Cargo inventory")
    base, upstream, standard, gitlab = (json.loads(raw[name]) for name in (
        "INDEX.json", "UPSTREAM-NOTICES.json", "STANDARD-NOTICES.json", "GITLAB-NOTICES.json"))
    if (upstream.get("schema") != "forge.upstream-notice-supplements.v1"
        or standard.get("schema") != "forge.standard-notice-supplements.v1"
        or gitlab.get("schema") != "forge.gitlab-notice-supplements.v1"
        or upstream.get("base_index_sha256") != _digest(raw["INDEX.json"])
        or standard.get("base_index_sha256") != _digest(raw["INDEX.json"])
        or standard.get("upstream_index_sha256") != _digest(raw["UPSTREAM-NOTICES.json"])
        or gitlab.get("base_index_sha256") != _digest(raw["INDEX.json"])
        or gitlab.get("upstream_index_sha256") != _digest(raw["UPSTREAM-NOTICES.json"])
        or gitlab.get("standard_index_sha256") != _digest(raw["STANDARD-NOTICES.json"])):
        raise ValueError("Supplement index chain differs")
    base_packages = {item["package_id"]: item for item in base["packages"]}
    if len(base_packages) != len(base["packages"]) or set(base_packages) != set(packages):
        raise ValueError("Current Cargo packages differ from the pinned inventory")
    vcs_digests = set(upstream["source_vcs_hashes"].values())
    seen: set[str] = set()
    prepared: dict[str, bytes] = {}
    entries = []
    for kind, group in (("upstream", upstream), ("standard", standard), ("gitlab", gitlab)):
        for entry in group["packages"]:
            package_id = entry["package_id"]
            if package_id in seen or package_id not in packages:
                raise ValueError("Duplicate or absent supplemented package")
            seen.add(package_id)
            current = packages[package_id]
            original = base_packages[package_id]
            declared = entry.get("declared_license", entry.get("declared_license_raw"))
            if (not current.get("source") or original["notices"]
                or (current["name"], current["version"], current.get("license"), current.get("repository"))
                != (entry["name"], entry["version"], declared, entry["repository"])):
                raise ValueError(f"Supplement package metadata differs: {package_id}")
            root = Path(current["manifest_path"]).parent
            vcs = _physical_file(root / ".cargo_vcs_info.json")
            vcs_info = json.loads(vcs)
            if vcs_info.get("git", {}).get("sha1") != entry["commit"]:
                raise ValueError(f"Supplement source revision differs: {package_id}")
            reference_commit = entry.get("reference_commit")
            if kind == "upstream":
                if _digest(vcs) not in vcs_digests or vcs_info.get("path_in_vcs") != entry.get("path_in_vcs"):
                    raise ValueError(f"Supplement source pin differs: {package_id}")
                source_checks = entry.get("source_checks")
                if source_checks is not None:
                    required = {"Cargo.toml.orig", "src/lib.rs"}
                    if (not isinstance(source_checks, dict) or not required.issubset(source_checks)
                        or len(source_checks) > 257
                        or (reference_commit is None and set(source_checks) != required)):
                        raise ValueError(f"Supplement source checks differ: {package_id}")
                    source_directory = root / "src"
                    info = source_directory.lstat()
                    if (source_directory.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400
                        or not source_directory.is_dir()):
                        raise ValueError(f"Supplement source directory is not physical: {package_id}")
                    if reference_commit is not None:
                        found = set()
                        for directory, dirs, files in os.walk(source_directory, followlinks=False):
                            for name in dirs:
                                subdirectory = Path(directory) / name
                                info = subdirectory.lstat()
                                if (subdirectory.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400
                                    or not subdirectory.is_dir()):
                                    raise ValueError("Supplement source directory is not physical")
                            for name in files:
                                found.add((Path(directory) / name).relative_to(root).as_posix())
                                if len(found) > 256:
                                    raise ValueError(f"Supplement source checks differ: {package_id}")
                        if set(source_checks) != {"Cargo.toml.orig", *found}:
                            raise ValueError(f"Supplement source checks differ: {package_id}")
                    for relative, expected_hash in source_checks.items():
                        if (relative != "Cargo.toml.orig"
                            and (not re.fullmatch(r"src/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+", relative)
                                 or any(part in {".", ".."} for part in relative.split("/")))):
                            raise ValueError(f"Supplement source checks differ: {package_id}")
                        if _digest(_physical_file(root / relative)) != expected_hash:
                            raise ValueError(f"Supplement source file differs: {package_id}")
            else:
                if _digest(vcs) != entry["cargo_vcs_sha256"]:
                    raise ValueError(f"Supplement source pin differs: {package_id}")
                if vcs_info.get("path_in_vcs") != entry.get("path_in_vcs") and not entry.get("path_in_vcs_basis"):
                    raise ValueError(f"Supplement source path differs: {package_id}")
                if _digest(_physical_file(root / "Cargo.toml.orig")) != entry["source_manifest_sha256"]:
                    raise ValueError(f"Supplement source manifest differs: {package_id}")
                source = root / ("lib.rs" if "source_notice_sha256" in entry else "src/lib.rs")
                expected = entry.get("source_notice_sha256", entry.get("source_file_sha256"))
                if _digest(_physical_file(source)) != expected:
                    raise ValueError(f"Supplement source file differs: {package_id}")
            notices = entry.get("notices") if kind != "standard" else [{
                "path": entry["staged_text_path"], "sha256": entry["staged_text_sha256"],
                "source_url": entry["standard_url"], "upstream_path": None,
            }]
            if not notices:
                raise ValueError(f"Supplement contains no texts: {package_id}")
            if reference_commit is not None:
                if (kind != "upstream" or not isinstance(reference_commit, str)
                    or not re.fullmatch(r"[0-9a-f]{40}", reference_commit)
                    or reference_commit == entry["commit"]
                    or entry.get("reference_basis") != "identical_published_manifest_and_source"
                    or entry.get("source_checks") is None
                    or any(f"/blob/{reference_commit}/" not in (notice.get("source_url") or "")
                           for notice in notices)):
                    raise ValueError(f"Supplement reference revision differs: {package_id}")
            out_notices = []
            for notice in notices:
                digest = notice["sha256"]
                text = _supplement_text(stage, notice["path"], digest, notice.get("bytes"))
                if digest in prepared and prepared[digest] != text:
                    raise ValueError("Different notice bytes for the same digest")
                prepared[digest] = text
                out_notices.append({"path": f"texts/{digest}.txt", "sha256": digest,
                                    "bytes": len(text), "source_url": notice.get("source_url"),
                                    "upstream_path": notice.get("upstream_path")})
            result_entry = {"package_id": package_id, "name": entry["name"], "version": entry["version"],
                            "declared_license": declared, "repository": entry["repository"],
                            "commit": entry["commit"], "path_in_vcs": entry.get("path_in_vcs"),
                            "source": kind, "notices": out_notices, "applicability_review_required": True}
            if reference_commit is not None:
                result_entry["reference_commit"] = reference_commit
                result_entry["reference_basis"] = entry["reference_basis"]
            entries.append(result_entry)
    transitive = _unresolved_transitive_references(packages, base_packages, upstream, stage, vcs_digests, prepared)
    # All inputs are validated before copying or writing an index.
    for digest, text in prepared.items():
        path = output / "texts" / (digest + ".txt")
        if path.exists():
            if _physical_file(path) != text:
                raise ValueError("Existing output text differs")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("xb") as stream:
                stream.write(text)
    remaining = sorted(package_id for package_id, item in base_packages.items()
                       if not item["notices"] and package_id not in seen)
    result = {"schema": "forge.rust-notice-supplements.v1", "base_index_sha256": _digest(raw["INDEX.json"]),
              "supplement_lock_sha256": _digest(lock_bytes),
              "input_index_sha256": {name: _digest(data) for name, data in raw.items()},
              "package_count": len(entries), "packages_without_available_notice": remaining,
              "packages": entries, "applicability_review_required": True, "distribution_qualified": False}
    if transitive:
        result["unresolved_transitive_references"] = transitive
    with (output / "SUPPLEMENT-NOTICES.json").open("x", encoding="utf8") as stream:
        json.dump(result, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
    return {"supplemented_packages": len(entries), "packages_without_available_notice": len(remaining)}


def include_forge_licenses(packages: dict[str, dict], output: Path, root: Path,
                           receipt_path: Path, spec_path: Path) -> dict:
    """Attach the owner's licence only to explicitly pinned original Forge packages.

    Third-party components, vendored sources and assets retain their own terms.
    This index is not a distribution or linked-SBOM qualification.
    """
    root = root.resolve(strict=True)
    base_bytes = _physical_file(output / "INDEX.json")
    supplement_bytes = _physical_file(output / "SUPPLEMENT-NOTICES.json")
    receipt_bytes = _physical_file(receipt_path)
    spec_bytes = _physical_file(spec_path)
    base = json.loads(base_bytes)
    supplement = json.loads(supplement_bytes)
    receipt = json.loads(receipt_bytes)
    spec = json.loads(spec_bytes)
    if (receipt.get("schema") != "forge.gate7.owner-code-license.v1"
        or receipt.get("owner_confirmation") != "MIT OR Apache-2.0"
        or spec.get("schema") != "forge.gate7.original-code-license-spec.v1"
        or spec.get("base_index_sha256") != _digest(base_bytes)
        or spec.get("supplement_index_sha256") != _digest(supplement_bytes)
        or spec.get("owner_receipt_sha256") != _digest(receipt_bytes)):
        raise ValueError("Forge licence specification or owner receipt differs")
    original = {item["package_id"]: item for item in base["packages"]}
    remaining = supplement["packages_without_available_notice"]
    if (len(original) != len(base["packages"]) or set(original) != set(packages)
        or len(remaining) != len(set(remaining)) or not set(remaining) <= set(original)):
        raise ValueError("Forge licence inventory differs")
    notices = []
    data_by_digest = {}
    for name in ("LICENSE-MIT", "LICENSE-APACHE", "LICENSE.md"):
        data = _physical_file(root / name)
        actual = {"sha256": _digest(data), "bytes": len(data)}
        if receipt.get("files", {}).get(name) != actual:
            raise ValueError(f"Forge licence text differs: {name}")
        digest = actual["sha256"]
        data_by_digest[digest] = data
        notices.append({"source": name, "path": f"texts/{digest}.txt", **actual})
    entries = []
    seen = set()
    for item in spec["packages"]:
        package_id = item["package_id"]
        if package_id in seen or package_id not in remaining or package_id not in packages:
            raise ValueError("Duplicate, supplemented, or unknown Forge package")
        seen.add(package_id)
        current, previous = packages[package_id], original[package_id]
        relative = Path(item["manifest"])
        if relative.is_absolute() or not relative.parts or any(part in (".", "..") for part in relative.parts):
            raise ValueError("Unsafe Forge package manifest path")
        path = root
        for part in relative.parts[:-1]:
            path = path / part
            info = path.lstat()
            if path.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400 or not path.is_dir():
                raise ValueError("Forge package path is not physical")
        path = path / relative.name
        data = _physical_file(path)
        if (path.resolve(strict=True) != Path(current["manifest_path"]).resolve(strict=True)
            or _digest(data) != item["manifest_sha256"] or current.get("source") is not None
            or previous["notices"] or current.get("license") != "MIT OR Apache-2.0"
            or (current["name"], current["version"]) != (item["name"], item["version"])
            or (previous["name"], previous["version"], previous["license"], previous["source"])
               != (item["name"], item["version"], "MIT OR Apache-2.0", None)):
            raise ValueError(f"Forge package metadata or source differs: {package_id}")
        entries.append({"package_id": package_id, "name": item["name"], "version": item["version"],
                        "manifest": relative.as_posix(), "manifest_sha256": item["manifest_sha256"],
                        "declared_license": "MIT OR Apache-2.0", "scope": "original Forge contributions only",
                        "notices": notices})
    if not entries:
        raise ValueError("No explicitly pinned original Forge packages")
    result_path = output / "FORGE-ORIGINAL-NOTICES.json"
    if result_path.exists():
        raise ValueError("Forge licence index already exists")
    for digest, data in data_by_digest.items():
        path = output / "texts" / (digest + ".txt")
        if path.exists() and _physical_file(path) != data:
            raise ValueError("Existing output text differs")
    for digest, data in data_by_digest.items():
        path = output / "texts" / (digest + ".txt")
        if not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("xb") as stream:
                stream.write(data)
    still_missing = sorted(set(remaining) - seen)
    result = {"schema": "forge.original-code-notices.v1", "base_index_sha256": _digest(base_bytes),
              "supplement_index_sha256": _digest(supplement_bytes), "spec_sha256": _digest(spec_bytes),
              "owner_receipt_sha256": _digest(receipt_bytes), "packages": entries,
              "packages_without_available_notice": still_missing, "third_party_review_required": True,
              "distribution_qualified": False}
    with result_path.open("x", encoding="utf8") as stream:
        json.dump(result, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
    return {"original_packages": len(entries), "packages_without_available_notice": len(still_missing)}


def write_notices(packages: dict[str, dict], output: Path, header: dict, *, deduplicate: bool = False) -> dict:
    output.mkdir(parents=True, exist_ok=False)
    entries = []
    identities = {}
    for package in sorted(packages.values(), key=lambda item: (item["name"], item["version"], item["id"])):
        folder = re.sub(r"[^A-Za-z0-9._-]", "_", f"{package['name']}-{package['version']}")
        if folder in identities and identities[folder] != package['id']:
            folder += "-" + hashlib.sha256(package['id'].encode()).hexdigest()[:16]
        identities[folder] = package['id']
        sources = notice_files(package)
        copied = []
        origins = []
        if sources:
            destination = output / folder
            if not deduplicate:
                destination.mkdir()
            for source in sources:
                relative = notice_relative_path(Path(package["manifest_path"]).parent, source)
                if deduplicate:
                    raw = source.read_bytes()
                    digest = hashlib.sha256(raw).hexdigest()
                    stored = Path("texts") / (digest + ".txt")
                    target = output / stored
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if target.exists():
                        if target.read_bytes() != raw:
                            raise ValueError("Different notice bytes for the same SHA-256")
                    else:
                        with target.open("xb") as stream:
                            stream.write(raw)
                    copied.append(stored.as_posix())
                    origins.append({"source_relative_path": relative.as_posix(), "path": stored.as_posix(), "sha256": digest, "bytes": len(raw)})
                else:
                    target = destination / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(source, target)
                    copied.append((Path(folder) / relative).as_posix())
        entries.append(
            {
                "package_id": package["id"],
                "name": package["name"],
                "version": package["version"],
                "license": package.get("license"),
                "authors": package.get("authors", []),
                "repository": package.get("repository"),
                "source": package.get("source"),
                "notices": copied,
                **({"notice_sources": origins} if deduplicate else {}),
            }
        )
    index = {
        **header,
        "package_count": len(entries),
        "packages_without_bundled_notice": sum(not entry["notices"] for entry in entries),
        "packages": entries,
    }
    (output / "INDEX.json").write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return index


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("forge_manifest", type=Path, nargs="?")
    parser.add_argument("launcher_manifest", type=Path, nargs="?")
    parser.add_argument("output", type=Path, nargs="?")
    parser.add_argument("--scope-spec", type=Path, help="forge.rust-notice-scopes.v1 JSON with explicit Cargo roots/features/targets")
    parser.add_argument("--scope-output", type=Path)
    parser.add_argument("--supplements-root", type=Path, help="Pinned supplemental notices for the exact scope inventory")
    parser.add_argument("--supplements-lock", type=Path, help="Verified supplement-index hashes for this inventory")
    parser.add_argument("--forge-license-root", type=Path, help="Project root containing the confirmed original-code licences")
    parser.add_argument("--forge-license-receipt", type=Path, help="Owner confirmation and licence text hashes")
    parser.add_argument("--forge-license-spec", type=Path, help="Explicit package identities and manifest hashes")
    arguments = parser.parse_args()
    legacy = (arguments.forge_manifest, arguments.launcher_manifest, arguments.output)
    if arguments.scope_spec is not None or arguments.scope_output is not None:
        if arguments.scope_spec is None or arguments.scope_output is None or any(path is not None for path in legacy):
            parser.error("Use --scope-spec and --scope-output together, without legacy positional paths")
        specification = json.loads(arguments.scope_spec.read_text(encoding="utf-8-sig"))
        if specification.get("schema") != "forge.rust-notice-scopes.v1":
            parser.error("Unsupported shipping scope specification")
        result = collect_scopes(specification["scopes"], arguments.scope_output, supplements=arguments.supplements_root,
                                supplement_lock=arguments.supplements_lock, forge_root=arguments.forge_license_root,
                                forge_receipt=arguments.forge_license_receipt, forge_spec=arguments.forge_license_spec)
    else:
        if any(value is not None for value in (arguments.supplements_root, arguments.supplements_lock,
                                               arguments.forge_license_root, arguments.forge_license_receipt,
                                               arguments.forge_license_spec)):
            parser.error("Supplemental notices require explicit shipping scopes")
        if any(path is None for path in legacy):
            parser.error("Expected forge_manifest launcher_manifest output, or an explicit scope specification")
        result = collect(*legacy)
    print(
        "RUST_NOTICES_COLLECTED"
        f" packages={result['package_count']}"
        f" missing_text={result['packages_without_bundled_notice']}"
    )
    if "supplement_summary" in result:
        print("RUST_NOTICE_SUPPLEMENTS", json.dumps(result["supplement_summary"]))
    if "forge_license_summary" in result:
        print("RUST_FORGE_ORIGINAL_NOTICES", json.dumps(result["forge_license_summary"]))
