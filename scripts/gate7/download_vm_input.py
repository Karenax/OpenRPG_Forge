"""Download OS/tool inputs from their publishers, with bounded streaming and digest checks."""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request

INPUTS = {
    "qemu": ("https://qemu.weilnetz.de/w64/", "qemu-w64-setup-20260811.exe", "qemu-w64-setup-20260811.sha512", "sha512"),
    "ubuntu": ("https://cloud-images.ubuntu.com/noble/current/", "noble-server-cloudimg-amd64.img", "SHA256SUMS", "sha256"),
    "windows": ("https://software-static.download.prss.microsoft.com/dbazure/", "26300.9457.260913-1737.26h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_fr-fr.iso", "https://support.microsoft.com/en-us/servicing/os/windows/docs/2026/09/verify-the-authenticity-of-a-windows-11-enterprise-evaluation-iso-file", "sha256"),
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("component", choices=INPUTS)
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    base, filename, sums_name, algorithm = INPUTS[args.component]
    manifest_url = sums_name if sums_name.startswith("https://") else base + sums_name
    with urllib.request.urlopen(manifest_url, timeout=30) as response:
        manifest = response.read(512 * 1024).decode("utf-8", errors="replace")
    if args.component == "windows":
        expected_fr = "4614fb8cdd960bc9ecf1b8ab9bc708335cf7b350b152c045ff6259ec5ff4fc01"
        matches = [expected_fr] if expected_fr in manifest.lower() else []
    else:
        matches = [line.split()[0] for line in manifest.splitlines() if line.split() and line.split()[-1].lstrip("*") == filename]
    if len(matches) != 1:
        raise ValueError("Publisher digest is absent or ambiguous")
    expected = matches[0].lower()
    args.directory.mkdir(parents=True, exist_ok=True)
    destination = args.directory / filename
    if destination.exists():
        with destination.open("rb") as stream:
            actual = hashlib.file_digest(stream, algorithm).hexdigest()
        if actual != expected:
            raise ValueError("Existing file differs from the publisher digest; it was preserved")
    else:
        partial = destination.with_suffix(destination.suffix + ".partial")
        digest = hashlib.new(algorithm)
        size = 0
        with urllib.request.urlopen(base + filename, timeout=60) as response, partial.open("xb") as output:
            final_url = response.url
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                if size > (10 if args.component == "windows" else 2) * 1024**3:
                    raise ValueError("Input exceeds its bounded download limit")
                output.write(chunk)
                digest.update(chunk)
        actual = digest.hexdigest()
        if actual != expected:
            raise ValueError("Downloaded input fails its publisher digest; partial file preserved")
        partial.rename(destination)
        with (args.directory / (filename + ".publisher.json")).open("x", encoding="utf-8") as stream:
            json.dump({"url": base + filename, "resolved_url": final_url, "manifest_url": manifest_url,
                       "algorithm": algorithm, "expected": expected, "actual": actual, "bytes": size}, stream, indent=2)
    print(json.dumps({"path": str(destination), "algorithm": algorithm, "digest": expected, "verified": True}))


if __name__ == "__main__":
    main()
