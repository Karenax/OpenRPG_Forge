"""Check every vendored byte against the pinned crate and the two-line upstream fix."""
import argparse
import hashlib
import os
from pathlib import Path, PurePosixPath
import stat
import tarfile

CRATE_SHA256 = "233daaf6e83ae6a12a52055f568f9d7cf4671dabb78ff9560ab6da230ce00ee5"
PATCH_SHA256 = "a742bf59f1b00af0f434623b34e2fd969dc8697d37c796efdf909dd9eb19b854"


def expected_files(archive):
    if hashlib.sha256(archive.read_bytes()).hexdigest() != CRATE_SHA256:
        raise ValueError("Original registry archive differs")
    files = {}
    with tarfile.open(archive, "r:gz") as bundle:
        for member in bundle:
            path = PurePosixPath(member.name)
            if (path.is_absolute() or ".." in path.parts or path.parts[0] != "glib-0.18.5"
                    or not member.isfile() or member.size > 4 * 1024**2):
                raise ValueError("Unexpected registry archive member")
            relative = path.relative_to("glib-0.18.5").as_posix()
            if relative in files:
                raise ValueError("Duplicate registry member")
            files[relative] = bundle.extractfile(member).read()
    if len(files) != 121 or sum(map(len, files.values())) != 1717584:
        raise ValueError("Registry archive inventory differs")
    original = files["src/variant_iter.rs"]
    old_declaration = b"let p: *mut libc::c_char = std::ptr::null_mut();"
    old_argument = b"                &p,"
    if original.count(old_declaration) != 1 or original.count(old_argument) != 1:
        raise ValueError("Upstream fix context differs")
    files["src/variant_iter.rs"] = original.replace(old_declaration, old_declaration.replace(b"let p", b"let mut p")).replace(old_argument, b"                &mut p,")
    return files


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--upstream-patch", type=Path, required=True)
    parser.add_argument("--vendor", type=Path, required=True)
    args = parser.parse_args()
    if hashlib.sha256(args.upstream_patch.read_bytes()).hexdigest() != PATCH_SHA256:
        raise ValueError("Pinned upstream patch differs")
    files = expected_files(args.archive)
    actual = {}
    def physical(path):
        flags = path.lstat()
        if stat.S_ISLNK(flags.st_mode) or getattr(flags, "st_file_attributes", 0) & 0x400:
            raise ValueError("Physical vendor files required")
    absolute = args.vendor.absolute()
    for parent in (absolute, *absolute.parents):
        physical(parent)
    for directory, names, filenames in os.walk(args.vendor, followlinks=False):
        for name in names + filenames:
            physical(Path(directory) / name)
        for name in filenames:
            path = Path(directory) / name
            actual[path.relative_to(args.vendor).as_posix()] = path.read_bytes()
    if actual != files:
        raise ValueError("Vendor inventory or bytes differ from exact upstream backport")
    print(f"PASS: {len(files)} files, only the two upstream VariantStrIter lines changed")


if __name__ == "__main__":
    main()
