"""Check candidate integrity and preservation in the actual delivery staging shape."""
import argparse
import copy
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import linux_build_evidence
import runtime_notices as notices


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


class RuntimeNoticeCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bundle = self.root / "bundle"
        (self.bundle / "texts").mkdir(parents=True)
        self.text = b"An independently pinned candidate license text.\n"
        self.text_hash = notices.sha(self.text)
        self.text_path = self.bundle / "texts" / f"{self.text_hash}.txt"
        self.text_path.write_bytes(self.text)
        self.project = self.root / "THIRD_PARTY_NOTICES.md"
        self.project.write_bytes(b"Current project [attribution](third_party/example/LICENSE).\n")
        linked = self.root / "third_party/example/LICENSE"
        linked.parent.mkdir(parents=True)
        linked.write_bytes(b"Attribution from the build sources.\n")
        self.pins = {"Cargo.toml":"1" * 64, "Cargo.lock":"2" * 64,
                     "apps/forge_runtime/Cargo.toml":"3" * 64}
        self.source = {"files":{name:{"sha256": pin} for name, pin in self.pins.items()}, "deleted":[]}
        self.source["files"]["third_party/example/LICENSE"] = {"sha256":notices.sha(linked.read_bytes())}
        self.package_ids = ["local:forge_runtime", "registry:dependency"]
        base = {"schema":"forge.rust-dependency-notices.v2",
                "requests":[{"package":"forge_runtime", "target":"x86_64-pc-windows-msvc",
                             "features":["forge_runtime/module-game"], "no_default_features":True,
                             "package_ids":self.package_ids}],
                "packages":[{"package_id":"registry:dependency", "notices":[f"texts/{self.text_hash}.txt"]}]}
        write_json(self.bundle / "INDEX.json", base)
        base_hash = notices.sha((self.bundle / "INDEX.json").read_bytes())
        supplement = {"base_index_sha256":base_hash, "packages":[]}
        write_json(self.bundle / "SUPPLEMENT-NOTICES.json", supplement)
        supp_hash = notices.sha((self.bundle / "SUPPLEMENT-NOTICES.json").read_bytes())
        forge = {"base_index_sha256":base_hash, "supplement_index_sha256":supp_hash, "packages":[]}
        write_json(self.bundle / "FORGE-ORIGINAL-NOTICES.json", forge)
        asset = {"schema":"forge.asset-notice-supplements.v1", "base_index_sha256":base_hash,
                 "release_qualified":False, "assets":[{"package_id":"registry:dependency",
                 "notices":[{"path":f"texts/{self.text_hash}.txt", "sha256":self.text_hash, "bytes":len(self.text)}]}]}
        write_json(self.bundle / "ASSET-NOTICES.json", asset)
        self.graph = self.root / "graph.json"
        write_json(self.graph, {"schema":"forge.gate7.current-delivery-five-unresolved-graph.v1",
                               "failed":False, "paths_changed_during_run":[],
                               "manifests_and_locks_sha256":self.pins,
                               "scopes":[{"name":"module-game", "target":"x86_64-pc-windows-msvc",
                                          "graphs":{"all_nondev":{"exit_code":0}}}]})
        self.candidate = self.root / "candidate.json"
        candidate_files = [self.bundle / name for name in notices.INDEX_NAMES] + [self.text_path]
        write_json(self.candidate, {"schema":"forge.gate7.runtime-notice-asset-candidate.v2", "passed":True,
            "distribution_qualified":False, "graph_evidence_sha256":notices.sha(self.graph.read_bytes()),
            "manifest_snapshot_sha256":notices.sha(json.dumps(self.pins,sort_keys=True).encode()),
            "base_index_sha256":base_hash, "supplement_index_sha256":supp_hash,
            "forge_index_sha256":notices.sha((self.bundle / "FORGE-ORIGINAL-NOTICES.json").read_bytes()),
            "asset_index_sha256":notices.sha((self.bundle / "ASSET-NOTICES.json").read_bytes()),
            "bundle_files":len(candidate_files), "bundle_bytes":sum(path.stat().st_size for path in candidate_files)})

    def prepare(self):
        return notices.prepare(self.bundle, self.candidate, self.graph, self.source, self.project)

    def retarget_candidate(self, target):
        base_path = self.bundle / "INDEX.json"
        base = json.loads(base_path.read_bytes())
        base["requests"][0]["target"] = target
        write_json(base_path, base)
        base_hash = notices.sha(base_path.read_bytes())
        supplement_path = self.bundle / "SUPPLEMENT-NOTICES.json"
        supplement = json.loads(supplement_path.read_bytes())
        supplement["base_index_sha256"] = base_hash
        write_json(supplement_path, supplement)
        supplement_hash = notices.sha(supplement_path.read_bytes())
        forge_path = self.bundle / "FORGE-ORIGINAL-NOTICES.json"
        forge = json.loads(forge_path.read_bytes())
        forge.update(base_index_sha256=base_hash, supplement_index_sha256=supplement_hash)
        write_json(forge_path, forge)
        asset_path = self.bundle / "ASSET-NOTICES.json"
        asset = json.loads(asset_path.read_bytes())
        asset["base_index_sha256"] = base_hash
        write_json(asset_path, asset)
        graph = json.loads(self.graph.read_bytes())
        graph["scopes"][0]["target"] = target
        write_json(self.graph, graph)
        candidate = json.loads(self.candidate.read_bytes())
        candidate.update(graph_evidence_sha256=notices.sha(self.graph.read_bytes()),
                         base_index_sha256=base_hash, supplement_index_sha256=supplement_hash,
                         forge_index_sha256=notices.sha(forge_path.read_bytes()),
                         asset_index_sha256=notices.sha(asset_path.read_bytes()))
        candidate["bundle_bytes"] = sum((self.bundle / name).stat().st_size
                                        for name in notices.INDEX_NAMES) + self.text_path.stat().st_size
        write_json(self.candidate, candidate)

    def test_linux_notices_require_matching_target_graph_and_inventory(self):
        with self.assertRaisesRegex(ValueError, "does not cover.*linux"):
            notices.prepare(self.bundle, self.candidate, self.graph, self.source, self.project,
                            notices.LINUX_TARGET)
        graph = json.loads(self.graph.read_bytes())
        graph["scopes"][0]["target"] = notices.LINUX_TARGET
        write_json(self.graph, graph)
        candidate = json.loads(self.candidate.read_bytes())
        candidate["graph_evidence_sha256"] = notices.sha(self.graph.read_bytes())
        write_json(self.candidate, candidate)
        with self.assertRaisesRegex(ValueError, "inventory does not cover"):
            notices.prepare(self.bundle, self.candidate, self.graph, self.source, self.project,
                            notices.LINUX_TARGET)
        self.retarget_candidate(notices.LINUX_TARGET)
        prepared = notices.prepare(self.bundle, self.candidate, self.graph, self.source, self.project,
                                   notices.LINUX_TARGET)
        self.assertEqual(prepared.receipt["runtime_target"], notices.LINUX_TARGET)
        self.assertFalse(prepared.receipt["distribution_qualified"])
        with self.assertRaisesRegex(ValueError, "does not cover.*windows"):
            self.prepare()
        with self.assertRaisesRegex(ValueError, "Unsupported module-game notice target"):
            notices.prepare(self.bundle, self.candidate, self.graph, self.source, self.project, "any")

    def test_stages_current_notice_and_candidate_without_overwriting_playtest_notice(self):
        prepared = self.prepare()
        core = self.root / "applications"
        old = core / "licenses/THIRD_PARTY_NOTICES.md"
        old.parent.mkdir(parents=True)
        old.write_bytes(b"Historical playtest attributions.\n")
        receipt = notices.stage(prepared, core)
        self.assertEqual(old.read_bytes(), b"Historical playtest attributions.\n")
        self.assertEqual(receipt["project_notice_path"], "licenses/runtime-project/THIRD_PARTY_NOTICES.md")
        self.assertEqual((core / receipt["project_notice_path"]).read_bytes(), self.project.read_bytes())
        self.assertEqual((core / "licenses/runtime-project/third_party/example/LICENSE").read_bytes(),
                         b"Attribution from the build sources.\n")
        self.assertEqual((core / notices.PREFIX / "texts" / self.text_path.name).read_bytes(), self.text)
        self.assertEqual((core / receipt["candidate_index_path"]).read_bytes(), (self.bundle / "INDEX.json").read_bytes())
        self.assertEqual((core / receipt["candidate_asset_index_path"]).read_bytes(), (self.bundle / "ASSET-NOTICES.json").read_bytes())
        self.assertFalse(receipt["distribution_qualified"])

    def test_identical_playtest_notice_is_reused(self):
        prepared = self.prepare()
        core = self.root / "applications"
        old = core / "licenses/THIRD_PARTY_NOTICES.md"
        old.parent.mkdir(parents=True)
        old.write_bytes(self.project.read_bytes())
        receipt = notices.stage(prepared, core)
        self.assertEqual(receipt["project_notice_path"], "licenses/THIRD_PARTY_NOTICES.md")
        self.assertFalse((core / "licenses/runtime-project").exists())

    def test_refuses_changed_text_and_stale_build_before_staging(self):
        self.text_path.write_bytes(b"tampered")
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            self.prepare()
        self.text_path.write_bytes(self.text)
        self.source["files"]["Cargo.lock"]["sha256"] = "f" * 64
        with self.assertRaisesRegex(ValueError, "build Cargo manifests"):
            self.prepare()
        self.assertFalse((self.root / "applications").exists())

    def test_accepts_an_extra_pinned_tool_lock_without_changing_candidate_pins(self):
        name = "tools/security/glib_variant_str_iter/Cargo.lock"
        self.source["files"][name] = {"sha256":"4" * 64}
        prepared = self.prepare()
        self.assertEqual(prepared.receipt["additional_build_cargo_inputs"], {name:"4" * 64})

    def test_refuses_missing_inputs_and_unpinned_entries(self):
        with self.assertRaisesRegex(ValueError, "explicit Rust notice"):
            notices.prepare(None, self.candidate, self.graph, self.source, self.project)
        (self.bundle / "unrelated.txt").write_text("unexpected", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "bundle entries"):
            self.prepare()

    def test_refuses_missing_or_changed_project_reference(self):
        linked = self.root / "third_party/example/LICENSE"
        linked.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "differs from build inputs"):
            self.prepare()

    def test_refuses_asset_notice_with_a_wrong_attribution_text_pin(self):
        path = self.bundle / "ASSET-NOTICES.json"
        asset = json.loads(path.read_bytes())
        asset["assets"][0]["notices"][0]["sha256"] = "0" * 64
        write_json(path, asset)
        candidate = json.loads(self.candidate.read_bytes())
        candidate["asset_index_sha256"] = notices.sha(path.read_bytes())
        write_json(self.candidate, candidate)
        with self.assertRaisesRegex(ValueError, "asset notice text differs"):
            self.prepare()

    def test_assembly_checks_the_current_offline_runtime_closure(self):
        repo = Path(__file__).resolve().parents[2]
        metadata = {"packages":[{"id":"local:forge_runtime", "name":"forge_runtime"},
                                {"id":"registry:dependency", "name":"dependency"}],
                    "resolve":{"nodes":[{"id":"local:forge_runtime", "deps":[
                        {"pkg":"registry:dependency", "dep_kinds":[{"kind":None}]}]},
                        {"id":"registry:dependency", "deps":[]}]}}
        registry = self.root / "registry"
        registry.mkdir()
        args = argparse.Namespace(runtime_notice_bundle=self.bundle, runtime_notice_receipt=self.candidate,
                                  runtime_notice_graph=self.graph, cargo_cache=registry)
        self.source.update(schema="forge.delivery.shared-source-inputs.v1", source_root=str(repo))
        project_data = (repo / "THIRD_PARTY_NOTICES.md").read_text(encoding="utf-8")
        for name in set(re.findall(r"\]\((third_party/[^)]+)\)", project_data)):
            self.source["files"][name] = {"sha256":notices.sha((repo / name).read_bytes())}
        output = argparse.Namespace(returncode=0, stdout=json.dumps(metadata).encode())
        with patch.object(notices, "shared_source_inputs", return_value=self.source), \
             patch.object(notices.subprocess, "run", return_value=output) as cargo:
            prepared = notices.prepare_for_assembly(args, repo, self.source)
            self.assertEqual(prepared.receipt["current_runtime_package_count"], 2)
            self.assertNotIn("runtime_target", prepared.receipt)
            self.assertIn("--offline", cargo.call_args.args[0])
            self.assertEqual(Path(cargo.call_args.kwargs["env"]["CARGO_HOME"]), self.root)
            metadata["resolve"]["nodes"][0]["deps"] = []
            output.stdout = json.dumps(metadata).encode()
            with self.assertRaisesRegex(ValueError, "closure differs"):
                notices.prepare_for_assembly(args, repo, self.source)

    def test_assembly_refuses_a_missing_build_source_before_checking_cargo(self):
        repo = Path(__file__).resolve().parents[2]
        self.source.update(schema="forge.delivery.shared-source-inputs.v1", source_root=str(repo))
        complete = copy.deepcopy(self.source)
        complete["files"]["apps/forge_runtime/src/bin/forge-module.rs"] = {"sha256":"5" * 64}
        args = argparse.Namespace(runtime_notice_bundle=self.bundle, runtime_notice_receipt=self.candidate,
                                  runtime_notice_graph=self.graph, cargo_cache=self.root / "registry")
        with patch.object(notices, "shared_source_inputs", return_value=complete), \
             patch.object(notices.subprocess, "run") as cargo:
            with self.assertRaisesRegex(ValueError, "Complete current delivery build source inventory"):
                notices.prepare_for_assembly(args, repo, self.source)
            cargo.assert_not_called()

    def test_linux_assembly_checks_linux_cargo_closure(self):
        self.retarget_candidate(notices.LINUX_TARGET)
        repo = Path(__file__).resolve().parents[2]
        metadata = {"packages":[{"id":"local:forge_runtime", "name":"forge_runtime"},
                                {"id":"registry:dependency", "name":"dependency"}],
                    "resolve":{"nodes":[{"id":"local:forge_runtime", "deps":[
                        {"pkg":"registry:dependency", "dep_kinds":[{"kind":None}]}]},
                        {"id":"registry:dependency", "deps":[]}]}}
        registry = self.root / "registry"
        registry.mkdir()
        args = argparse.Namespace(runtime_notice_bundle=self.bundle, runtime_notice_receipt=self.candidate,
                                  runtime_notice_graph=self.graph, cargo_cache=registry,
                                  runtime_notice_target=notices.LINUX_TARGET)
        self.source.update(schema="forge.delivery.shared-source-inputs.v1", source_root=str(repo))
        project_data = (repo / "THIRD_PARTY_NOTICES.md").read_text(encoding="utf-8")
        for name in set(re.findall(r"\]\((third_party/[^)]+)\)", project_data)):
            self.source["files"][name] = {"sha256":notices.sha((repo / name).read_bytes())}
        output = argparse.Namespace(returncode=0, stdout=json.dumps(metadata).encode())
        with patch.object(notices, "shared_source_inputs", return_value=self.source), \
             patch.object(notices.subprocess, "run", return_value=output) as cargo:
            prepared = notices.prepare_for_assembly(args, repo, self.source)
        command = cargo.call_args.args[0]
        self.assertEqual(command[command.index("--filter-platform") + 1], notices.LINUX_TARGET)
        self.assertEqual(prepared.receipt["runtime_target"], notices.LINUX_TARGET)
        self.assertEqual(prepared.receipt["current_runtime_package_count"], 2)


class WindowsDeliveryNoticeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repo = self.root / "repo"
        self.registry = self.root / "cargo-home/registry"
        self.registry.mkdir(parents=True)
        self.names = (".cargo/config.toml", "Cargo.toml", "Cargo.lock", "tools/forge_delivery/Cargo.toml",
                      "tools/forge_delivery/Cargo.lock", "scripts/tidewater/collect_rust_notices.py")
        files = {}
        for name in self.names:
            path = self.repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            data = ((Path(__file__).resolve().parents[2] / name).read_bytes()
                    if name.endswith("collect_rust_notices.py") else ("fixture " + name).encode())
            path.write_bytes(data)
            files[name] = {"bytes":len(data), "sha256":notices.sha(data)}
        self.source = {"schema":"forge.delivery.shared-source-inputs.v1", "source_root":str(self.repo),
                       "files":files}
        self.index = self.root / "INDEX.json"
        local_id = "path+" + (self.repo / "tools/forge_delivery").as_uri() + "#forge_delivery@0.1.0"
        registry_id = "registry+https://example.invalid/index#required@1.0.0"
        self.package_ids = [local_id, registry_id]
        local = {"name":"forge_delivery", "version":"0.1.0", "source":None,
                 "license":"MIT OR Apache-2.0", "authors":[], "repository":None}
        external = {"name":"required", "version":"1.0.0", "source":"registry+https://example.invalid/index",
                    "license":"MIT", "authors":["Example author"], "repository":"https://example.invalid/repo"}
        write_json(self.index, {"schema":"forge.rust-dependency-notices.v2",
                               "packages":[{"package_id":local_id, **local}, {"package_id":registry_id, **external}]})
        self.metadata = {"packages":[{"id":local_id, "manifest_path":str(self.repo/'tools/forge_delivery/Cargo.toml'),
                                      **local}, {"id":registry_id, **external},
                                     {"id":"registry:dev-only", "name":"dev-only"}],
                         "resolve":{"nodes":[{"id":local_id, "deps":[
                             {"pkg":registry_id, "dep_kinds":[{"kind":None}]},
                             {"pkg":"registry:dev-only", "dep_kinds":[{"kind":"dev"}]}]},
                             {"id":registry_id, "deps":[]}, {"id":"registry:dev-only", "deps":[]}]}}

    def result(self):
        return argparse.Namespace(returncode=0, stdout=json.dumps(self.metadata).encode())

    def verify(self):
        return notices.windows_delivery_cli_scope(self.repo, self.source, self.registry, self.index)

    def test_replays_windows_cli_with_its_registry_and_checks_nondev_coverage(self):
        with patch.object(notices.subprocess, "run", return_value=self.result()) as cargo:
            result = self.verify()
        command = cargo.call_args.args[0]
        self.assertEqual(command, ["cargo", "metadata", "--manifest-path",
            str(self.repo / "tools/forge_delivery/Cargo.toml"), "--format-version", "1",
            "--locked", "--offline", "--filter-platform", notices.WINDOWS_TARGET])
        self.assertEqual(Path(cargo.call_args.kwargs["env"]["CARGO_HOME"]), self.registry.parent)
        self.assertEqual(result["package_count"], 2)
        self.assertEqual(result["package_ids_sha256"], notices.sha(json.dumps(sorted(self.package_ids)).encode()))
        self.assertEqual(result["index_sha256"], notices.sha(self.index.read_bytes()))
        self.assertRegex(result["normalized_package_ids_sha256"], r"^[0-9a-f]{64}$")

    def test_refuses_cli_dependency_absent_from_the_pinned_index(self):
        write_json(self.index, {"schema":"forge.rust-dependency-notices.v2",
                                "packages":[{"package_id":self.package_ids[0]}]})
        with patch.object(notices.subprocess, "run", return_value=self.result()):
            with self.assertRaisesRegex(ValueError, "not covered"):
                self.verify()

    def test_refuses_duplicate_package_identity_before_running_cargo(self):
        write_json(self.index, {"schema":"forge.rust-dependency-notices.v2",
                                "packages":[{"package_id":"duplicate"}, {"package_id":"duplicate"}]})
        with patch.object(notices.subprocess, "run") as cargo:
            with self.assertRaisesRegex(ValueError, "identities are invalid"):
                self.verify()
            cargo.assert_not_called()

    def test_refuses_changed_package_metadata_and_unowned_local_manifest(self):
        package = self.metadata["packages"][1]
        for field, changed in (("name", "renamed"), ("version", "1.0.1"), ("license", "GPL-3.0-only"),
                               ("authors", ["Different author"]), ("repository", "https://other.invalid"),
                               ("source", "registry+https://other.invalid/index")):
            with self.subTest(field=field):
                previous = package[field]
                package[field] = changed
                with patch.object(notices.subprocess, "run", return_value=self.result()):
                    with self.assertRaisesRegex(ValueError, "metadata differs"):
                        self.verify()
                package[field] = previous
        self.metadata["packages"][0]["manifest_path"] = str(self.root/'outside/Cargo.toml')
        with patch.object(notices.subprocess, "run", return_value=self.result()):
            with self.assertRaisesRegex(ValueError, "manifest is outside or unsafe"):
                self.verify()

    def test_refuses_missing_or_changed_cargo_inputs(self):
        (self.repo / "tools/forge_delivery/Cargo.lock").write_bytes(b"changed")
        with patch.object(notices.subprocess, "run") as cargo:
            with self.assertRaisesRegex(ValueError, "Windows delivery input differs"):
                self.verify()
            cargo.assert_not_called()

    def test_refuses_changed_index_during_replay(self):
        def change_index(*args, **kwargs):
            self.index.write_bytes(self.index.read_bytes() + b" ")
            return self.result()
        with patch.object(notices.subprocess, "run", side_effect=change_index):
            with self.assertRaisesRegex(ValueError, "inputs changed"):
                self.verify()

    def test_refuses_failed_offline_metadata(self):
        with patch.object(notices.subprocess, "run", return_value=argparse.Namespace(returncode=1)):
            with self.assertRaisesRegex(ValueError, "closure offline"):
                self.verify()


class LinuxSnapshotNoticeTests(unittest.TestCase):
    """An extracted source ZIP and synthetic Cargo graph; no Cargo is executed."""

    def setUp(self):
        base=RuntimeNoticeCandidateTests('test_identical_playtest_notice_is_reused')
        base.setUp(); self.addCleanup(base.doCleanups)
        self.base=base
        self.repo=base.root/'snapshot'; self.repo.mkdir()
        self.registry=base.root/'cargo/registry'; self.registry.mkdir(parents=True)
        self.target=base.root/'cargo-target'; self.target.mkdir()
        source=Path(__file__).resolve().parents[2]
        contents={root+'/fixture.txt':root.encode() for root in linux_build_evidence.SOURCE_ROOTS}
        contents.update({'Cargo.toml':b'[workspace]\n', 'Cargo.lock':b'# lock fixture\n',
            'rust-toolchain.toml':b'[toolchain]\nchannel="1.98.0"\n',
            'apps/forge_runtime/Cargo.toml':b'[package]\nname="forge_runtime"\nversion="0.1.0"\n',
            'apps/forge_launcher/Cargo.toml':b'[package]\nname="forge_launcher_prototype"\nversion="0.4.0"\n',
            'apps/forge_launcher/Cargo.lock':b'# launcher lock fixture\n',
            '.cargo/config.toml':b'[net]\noffline=true\n',
            'THIRD_PARTY_NOTICES.md':b'Current project [attribution](third_party/example/LICENSE).\n',
            'third_party/example/LICENSE':b'Attribution from the build sources.\n'})
        for name in ('scripts/gate6/capture_inputs.py','scripts/tidewater/collect_rust_notices.py',
                     'scripts/delivery/runtime_notices.py','scripts/delivery/linux_build_evidence.py',
                     'scripts/delivery/application_sources.py','scripts/delivery/complete_rebuild.py',
                     'scripts/delivery/game_kit.py'):
            contents[name]=(source/name).read_bytes()
        self.contents=contents
        for name,data in contents.items():
            path=self.repo/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
        self.original=r'E:\OpenRPG_Forge\OpenRPG_Forge-Rust'
        self.manifest={'schema':'forge.gate7.vm-source-snapshot.v1','roots':list(linux_build_evidence.SOURCE_ROOTS),
            'repository':self.original,'development_checkout':False,
            'files':{name:{'sha256':notices.sha(data),'bytes':len(data)} for name,data in contents.items()}}
        raw=json.dumps(self.manifest).encode()
        self.manifest_path=base.root/'source-manifest.json'; self.manifest_path.write_bytes(raw)
        self.archive=base.root/'snapshot.zip'
        with zipfile.ZipFile(self.archive,'x') as bundle:
            for name,data in contents.items(): bundle.writestr(name,data)
            bundle.writestr('__forge_vm_source_manifest__.json',raw)
        self.source_sha=linux_build_evidence.sha(self.archive)
        self.manifest_sha=notices.sha(raw)
        self.capture=base.root/'snapshot.receipt.json'
        write_json(self.capture,{'full_read_verified':True,'sha256':self.source_sha,
            'archive_bytes':self.archive.stat().st_size,'files':len(contents)})
        self.original_id='path+file:///E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_runtime#0.1.0'
        self.current_id='path+'+(self.repo/'apps/forge_runtime').as_uri()+'#0.1.0'
        self.registry_id='registry+https://github.com/rust-lang/crates.io-index#dependency@1.2.3'
        self.original_launcher_id='path+file:///E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_launcher#forge_launcher_prototype@0.4.0'
        self.current_launcher_id='path+'+(self.repo/'apps/forge_launcher').as_uri()+'#forge_launcher_prototype@0.4.0'
        base.pins=notices.cargo_inputs(self.manifest)
        graph=json.loads(base.graph.read_bytes()); graph['manifests_and_locks_sha256']=base.pins
        graph['scopes'][0].update(package='forge_runtime',features=['forge_runtime/module-game'],
                                  no_default_features=True,include_root_dev=False,
                                  graphs={'all_nondev':{'exit_code':0,'package_count':2,
                                    'package_ids_sha256':notices.sha(json.dumps(sorted([self.original_id,self.registry_id])).encode())}})
        write_json(base.graph,graph)
        index_path=base.bundle/'INDEX.json'; index=json.loads(index_path.read_bytes())
        index['requests'][0].update(manifest=self.original+'\\Cargo.toml',include_root_dev=False,
                                    package_ids=[self.original_id,self.registry_id])
        index['packages']=[{'package_id':self.original_id,'name':'forge_runtime','version':'0.1.0',
                            'source':None,'license':'MIT OR Apache-2.0','repository':None,'authors':[], 'notices':[]},
                           {'package_id':self.registry_id,'name':'dependency','version':'1.2.3',
                            'source':'registry+https://github.com/rust-lang/crates.io-index',
                            'license':'MIT','repository':'https://example.test/dependency','authors':['A. Developer'],
                            'notices':[f'texts/{base.text_hash}.txt']}]
        write_json(index_path,index)
        asset_path=base.bundle/'ASSET-NOTICES.json'; asset=json.loads(asset_path.read_bytes())
        asset['assets'][0]['package_id']=self.registry_id; write_json(asset_path,asset)
        candidate=json.loads(base.candidate.read_bytes())
        candidate['manifest_snapshot_sha256']=notices.sha(json.dumps(base.pins,sort_keys=True).encode())
        write_json(base.candidate,candidate)
        base.retarget_candidate(notices.LINUX_TARGET)
        self.metadata={'packages':[{'id':self.current_id,'name':'forge_runtime','version':'0.1.0','source':None,
                                    'license':'MIT OR Apache-2.0','repository':None,'authors':[],
                                    'manifest_path':str(self.repo/'apps/forge_runtime/Cargo.toml')},
                                   {'id':self.registry_id,'name':'dependency','version':'1.2.3',
                                    'source':'registry+https://github.com/rust-lang/crates.io-index',
                                    'license':'MIT','repository':'https://example.test/dependency','authors':['A. Developer']}],
                       'resolve':{'nodes':[{'id':self.current_id,'deps':[{'pkg':self.registry_id,
                                            'dep_kinds':[{'kind':None}]}]}, {'id':self.registry_id,'deps':[]}]}}
        self.args=argparse.Namespace(runtime_notice_bundle=base.bundle,runtime_notice_receipt=base.candidate,
            runtime_notice_graph=base.graph,cargo_cache=self.registry,cargo_target=self.target)

    def enable_launcher_scope(self):
        base=self.base
        index_path=base.bundle/'INDEX.json'; index=json.loads(index_path.read_bytes())
        ids=[self.original_launcher_id,self.registry_id]
        index['requests'].append({'manifest':self.original+'\\apps\\forge_launcher\\Cargo.toml',
            'package':'forge_launcher_prototype','target':notices.LINUX_TARGET,'features':['standalone'],
            'no_default_features':False,'include_root_dev':False,'package_ids':ids})
        index['packages'].append({'package_id':self.original_launcher_id,'name':'forge_launcher_prototype',
            'version':'0.4.0','source':None,'license':'MIT OR Apache-2.0','repository':None,'authors':[], 'notices':[]})
        write_json(index_path,index)
        graph=json.loads(base.graph.read_bytes())
        graph['scopes'].append({'name':'launcher-standalone','package':'forge_launcher_prototype',
            'target':notices.LINUX_TARGET,'features':['standalone'],'no_default_features':False,'include_root_dev':False,
            'graphs':{'all_nondev':{'exit_code':0,'package_count':2,
                      'package_ids_sha256':notices.sha(json.dumps(sorted(ids)).encode())}}})
        write_json(base.graph,graph)
        base.retarget_candidate(notices.LINUX_TARGET)
        self.launcher_metadata={'packages':[{'id':self.current_launcher_id,'name':'forge_launcher_prototype',
            'version':'0.4.0','source':None,'license':'MIT OR Apache-2.0','repository':None,'authors':[],
            'manifest_path':str(self.repo/'apps/forge_launcher/Cargo.toml')},self.metadata['packages'][1]],
            'resolve':{'nodes':[{'id':self.current_launcher_id,'deps':[{'pkg':self.registry_id,
                          'dep_kinds':[{'kind':None}]}]}, {'id':self.registry_id,'deps':[]}]}}

    def prepare(self, *, mutate=None, include_launcher=False):
        def cargo(*args,**kwargs):
            if mutate: mutate()
            command=args[0]
            launcher='forge_launcher' in str(command[command.index('--manifest-path')+1])
            metadata=self.launcher_metadata if launcher else self.metadata
            return argparse.Namespace(returncode=0,stdout=json.dumps(metadata).encode())
        with patch.object(notices.sys,'platform','linux'), patch.object(notices,'shared_source_inputs',
                side_effect=AssertionError('Git checkout is not part of the Linux snapshot protocol')), \
                patch.object(notices.subprocess,'run',side_effect=cargo) as called:
            result=notices.prepare_for_linux_snapshot(self.args,self.repo,self.archive,self.manifest_path,
                          self.source_sha,self.manifest_sha,self.capture,include_launcher=include_launcher)
        return result,called

    def test_admits_exact_linux_graph_from_snapshot_without_git(self):
        prepared,cargo=self.prepare()
        command=cargo.call_args.args[0]
        self.assertIn('--locked',command); self.assertIn('--offline',command)
        self.assertEqual(command[command.index('--filter-platform')+1],notices.LINUX_TARGET)
        self.assertEqual(cargo.call_args.kwargs['cwd'],self.repo)
        self.assertEqual(cargo.call_args.kwargs['env']['CARGO_NET_OFFLINE'],'true')
        self.assertEqual(prepared.receipt['linux_source_zip_sha256'],self.source_sha)
        self.assertEqual(prepared.receipt['linux_source_manifest_sha256'],self.manifest_sha)
        self.assertEqual(prepared.receipt['current_runtime_package_count'],2)
        self.assertNotEqual(prepared.receipt['current_runtime_package_ids_sha256'],
                            prepared.receipt['captured_runtime_package_ids_sha256'])
        self.assertTrue(prepared.receipt['path_package_root_normalized'])
        self.assertFalse(prepared.receipt['distribution_qualified'])

    def test_refuses_changed_snapshot_before_running_cargo(self):
        (self.repo/'Cargo.lock').write_bytes(b'changed lock')
        with patch.object(notices.sys,'platform','linux'),patch.object(notices.subprocess,'run') as cargo:
            with self.assertRaisesRegex(ValueError,'snapshot.*(changed|differs)'):
                notices.prepare_for_linux_snapshot(self.args,self.repo,self.archive,self.manifest_path,
                                                   self.source_sha,self.manifest_sha,self.capture)
            cargo.assert_not_called()

    def test_refuses_metadata_closure_or_local_origin_divergence(self):
        self.metadata['resolve']['nodes'][0]['deps']=[]
        with self.assertRaisesRegex(ValueError,'closure differs'): self.prepare()
        self.metadata['resolve']['nodes'][0]['deps']=[{'pkg':self.registry_id,'dep_kinds':[{'kind':None}]}]
        self.metadata['packages'][0]['manifest_path']=str(self.base.root/'elsewhere/Cargo.toml')
        with self.assertRaisesRegex(ValueError,'manifest|outside'): self.prepare()

    def test_refuses_colliding_or_outside_local_cargo_identities(self):
        alias=self.original_id.replace('#0.1.0','#forge_runtime@0.1.0')
        package={'name':'forge_runtime','version':'0.1.0','source':None,
                 'license':'MIT OR Apache-2.0','repository':None,'authors':[]}
        with self.assertRaisesRegex(ValueError,'collide'):
            notices._canonical_packages({self.original_id:package,alias:package},self.original,
                                         self.manifest,windows_origin=True)
        outside='path+file:///E:/OpenRPG_Forge/another-checkout/apps/forge_runtime#0.1.0'
        with self.assertRaisesRegex(ValueError,'outside'):
            notices._local_cargo_id(outside,self.original,self.manifest['files'],windows_origin=True)
        ambiguous=self.original_id.replace('/apps/','%2Fapps/')
        with self.assertRaisesRegex(ValueError,'unsafe'):
            notices._local_cargo_id(ambiguous,self.original,self.manifest['files'],windows_origin=True)

    def test_refuses_snapshot_link_before_running_cargo(self):
        outside=self.base.root/'outside'; outside.mkdir()
        marker=outside/'keep'; marker.write_bytes(b'unchanged')
        link=self.repo/'third_party/untrusted-link'
        if os.name == 'nt':
            subprocess.run([os.environ.get('COMSPEC','cmd.exe'),'/d','/c','mklink','/J',str(link),str(outside)],
                           capture_output=True,check=True)
        else:
            link.symlink_to(outside,target_is_directory=True)
        try:
            with self.assertRaisesRegex(ValueError,'link|junction'):
                self.prepare()
            self.assertEqual(marker.read_bytes(),b'unchanged')
        finally:
            if os.name == 'nt': os.rmdir(link)
            else: link.unlink()
        self.assertEqual(marker.read_bytes(),b'unchanged')

    def test_refuses_git_directory_and_external_identity_drift(self):
        git=self.repo/'.git'; git.mkdir()
        with self.assertRaisesRegex(ValueError,'snapshot root inventory differs'): self.prepare()
        git.rmdir()
        self.metadata['packages'][1]['source']='registry+https://unexpected.invalid/index'
        with self.assertRaisesRegex(ValueError,'closure differs'): self.prepare()

    def test_refuses_missing_local_graph_pin_and_changed_license_metadata(self):
        old_graph=self.base.graph.read_bytes(); old_candidate=self.base.candidate.read_bytes()
        graph=json.loads(self.base.graph.read_bytes())
        graph['manifests_and_locks_sha256'].pop('apps/forge_runtime/Cargo.toml')
        write_json(self.base.graph,graph)
        candidate=json.loads(self.base.candidate.read_bytes())
        candidate['graph_evidence_sha256']=notices.sha(self.base.graph.read_bytes())
        candidate['manifest_snapshot_sha256']=notices.sha(json.dumps(graph['manifests_and_locks_sha256'],sort_keys=True).encode())
        write_json(self.base.candidate,candidate)
        with self.assertRaisesRegex(ValueError,'local Cargo manifests'): self.prepare()
        self.base.graph.write_bytes(old_graph); self.base.candidate.write_bytes(old_candidate)
        self.metadata['packages'][1]['license']='GPL-3.0-only'
        with self.assertRaisesRegex(ValueError,'closure differs'): self.prepare()

    def test_launcher_scope_is_explicit_and_uses_same_snapshot_and_registry(self):
        with self.assertRaisesRegex(ValueError,'launcher-standalone.*scope'):
            self.prepare(include_launcher=True)
        self.enable_launcher_scope()
        prepared,cargo=self.prepare(include_launcher=True)
        self.assertEqual(cargo.call_count,2)
        launcher_command=cargo.call_args.args[0]
        self.assertIn('--offline',launcher_command)
        self.assertEqual(launcher_command[launcher_command.index('--features')+1],'standalone')
        self.assertNotIn('--no-default-features',launcher_command)
        self.assertEqual(prepared.receipt['launcher_standalone_scope']['package_count'],2)
        self.assertFalse(prepared.receipt['distribution_qualified'])
        self.launcher_metadata['packages'][0]['version']='0.4.1'
        with self.assertRaisesRegex(ValueError,'closure differs'): self.prepare(include_launcher=True)

    def test_linux_scope_with_default_features_omits_empty_feature_flag(self):
        graph=json.loads(self.base.graph.read_bytes()); graph['scopes'][0]['features']=[]
        graph['scopes'][0]['no_default_features']=False
        index=json.loads((self.base.bundle/'INDEX.json').read_bytes())
        index['requests'][0]['features']=[]; index['requests'][0]['no_default_features']=False
        completed=argparse.Namespace(returncode=0,stdout=json.dumps(self.metadata).encode())
        with patch.object(notices.subprocess,'run',return_value=completed) as cargo:
            scope=notices._linux_notice_scope(self.repo,self.manifest,graph,index,self.original,True,{},
                name='module-game',package='forge_runtime',cargo_manifest='Cargo.toml',features=[],no_default=False)
        self.assertEqual(scope['package_count'],2)
        self.assertNotIn('--features',cargo.call_args.args[0])
        self.assertNotIn('--no-default-features',cargo.call_args.args[0])

    def test_refuses_mutation_during_metadata_and_changed_project_notice(self):
        lock=self.repo/'Cargo.lock'
        with self.assertRaisesRegex(ValueError,'snapshot.*(changed|differs)'):
            self.prepare(mutate=lambda:lock.write_bytes(b'changed during metadata'))
        lock.write_bytes(self.contents['Cargo.lock'])
        collector=self.repo/'scripts/tidewater/collect_rust_notices.py'
        with self.assertRaisesRegex(ValueError,'snapshot input differs'):
            self.prepare(mutate=lambda:collector.write_bytes(b'raise AssertionError("must not execute")'))
        collector.write_bytes(self.contents['scripts/tidewater/collect_rust_notices.py'])
        (self.repo/'THIRD_PARTY_NOTICES.md').write_bytes(b'different notice')
        with self.assertRaisesRegex(ValueError,'snapshot.*(changed|differs)'): self.prepare()

    def test_refuses_changed_zip_and_incomplete_capture_receipt(self):
        receipt=json.loads(self.capture.read_bytes()); receipt['full_read_verified']=False; write_json(self.capture,receipt)
        with self.assertRaisesRegex(ValueError,'capture receipt'): self.prepare()
        receipt['full_read_verified']=True; write_json(self.capture,receipt)
        with self.archive.open('ab') as stream: stream.write(b'changed')
        with self.assertRaisesRegex(ValueError,'ZIP differs'): self.prepare()


if __name__ == "__main__":
    unittest.main()
