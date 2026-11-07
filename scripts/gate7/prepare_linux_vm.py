"""Prepare, never overwrite, a local Linux qualification VM with no host filesystem share."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--qemu", type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    vm = root / "linux"
    if vm.exists():
        raise ValueError("Existing VM preserved; use its saved launch descriptor")
    base = root / "inputs/noble-server-cloudimg-amd64.img"
    with base.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != "04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94":
            raise ValueError("Ubuntu input differs from the qualified publisher digest")
    # Pure Python ISO authoring helper, loaded directly from its verified wheel.
    with urllib.request.urlopen("https://pypi.org/pypi/pycdlib/1.22.0/json", timeout=30) as response:
        package = json.load(response)
    wheel_info = next(item for item in package["urls"] if item["filename"].endswith(".whl"))
    wheel = root / "inputs" / wheel_info["filename"]
    if not wheel.exists():
        with urllib.request.urlopen(wheel_info["url"], timeout=30) as response:
            data = response.read(2 * 1024**2)
        if hashlib.sha256(data).hexdigest() != wheel_info["digests"]["sha256"]:
            raise ValueError("ISO helper fails publisher digest")
        with wheel.open("xb") as stream:
            stream.write(data)
    with wheel.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != wheel_info["digests"]["sha256"]:
            raise ValueError("Existing ISO helper fails publisher digest")
    sys.path.insert(0, str(wheel))
    import pycdlib
    vm.mkdir()
    key = vm / "ssh_ed25519"
    subprocess.run(["C:/Windows/System32/OpenSSH/ssh-keygen.exe", "-q", "-t", "ed25519", "-N", "", "-C", "forge-gate7-local-linux", "-f", str(key)], check=True)
    public = key.with_suffix(".pub").read_text("utf-8").strip()
    user_data = ("#cloud-config\nusers:\n  - name: forge\n    sudo: ALL=(ALL) NOPASSWD:ALL\n"
                 "    shell: /bin/bash\n    lock_passwd: true\n    ssh_authorized_keys:\n"
                 f"      - {public}\nssh_pwauth: false\ndisable_root: true\npackage_update: false\n").encode()
    metadata = json.dumps({"instance-id": "forge-gate7-linux-20261023", "local-hostname": "forge-gate7-linux"}).encode()
    iso = pycdlib.PyCdlib()
    iso.new(interchange_level=3, joliet=3, rock_ridge="1.09", vol_ident="cidata")
    for name, iso_name, data in [("user-data", "USERDATA.;1", user_data), ("meta-data", "METADATA.;1", metadata)]:
        iso.add_fp(io.BytesIO(data), len(data), iso_path="/" + iso_name, rr_name=name, joliet_path="/" + name)
    seed = vm / "seed.iso"
    iso.write(str(seed))
    iso.close()
    disk = vm / "system.qcow2"
    subprocess.run([str(args.qemu / "qemu-img.exe"), "create", "-f", "qcow2", "-F", "qcow2", "-b", str(base), str(disk), "160G"], check=True)
    arguments = ["-name", "forge-gate7-linux", "-machine", "q35", "-accel", "whpx", "-smp", "4", "-m", "8192",
                 "-drive", f"file={disk},if=virtio,format=qcow2", "-drive", f"file={seed},media=cdrom,readonly=on",
                 "-netdev", "user,id=n0,hostfwd=tcp:127.0.0.1:2227-:22", "-device", "virtio-net-pci,netdev=n0",
                 "-qmp", "tcp:127.0.0.1:4427,server=on,wait=off", "-display", "none", "-serial", f"file:{vm / 'serial.log'}", "-boot", "c"]
    launch = {"schema": "forge.gate7.local-vm.v1", "executable": str(args.qemu / "qemu-system-x86_64.exe"), "arguments": arguments,
              "memory_mib": 8192, "vcpus": 4, "disk_max_gib": 160, "ssh_address": "127.0.0.1", "ssh_port": 2227,
              "ssh_key": str(key), "user": "forge", "host_filesystem_shared": False,
              "source_sha256": "04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94",
              "iso_helper_version": "1.22.0", "iso_helper_sha256": wheel_info["digests"]["sha256"]}
    with (vm / "launch.json").open("x", encoding="utf-8") as stream:
        json.dump(launch, stream, indent=2)
    print(json.dumps({"launch": str(vm / "launch.json"), "ssh": "forge@127.0.0.1:2227", "started": False}))


if __name__ == "__main__":
    main()
