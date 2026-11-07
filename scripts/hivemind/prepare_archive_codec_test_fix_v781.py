"""Prepare a test correction without changing sources pinned by active checks."""
from pathlib import Path
import datetime, hashlib, json

R = Path(__file__).resolve().parents[2]
O = Path('F:/OpenRPG_ImportWork/placement-physics-codec-test-diagnosis-v781')
source = R / 'crates/forge_project/tests/placement_physics.rs'
candidate = R / 'scripts/hivemind/candidates/PlacementPhysicsArchiveTestsV781.rs'
assert not O.exists() and not candidate.exists()
raw = source.read_bytes()
text = raw.decode('utf-8').replace('\r\n', '\n')
needle = 'serde_json::from_slice::<serde_json::Value>(&empty_archive).unwrap()["version"],\n            43'
assert text.count(needle) == 1
text = text.replace(needle, needle.removesuffix('43') + 'version')
needle = '        undo(&mut reopened, "undo-remove-copy");'
assert text.count(needle) == 1
text = text.replace(needle, '''        let mut empty_reopened = ZoneEditSession::from_archive_bytes(&empty_archive).unwrap();
        assert_eq!(empty_reopened.head(), reopened.head());
        undo(&mut empty_reopened, "undo-remove-copy-after-reopen");
        assert_eq!(
            empty_reopened.head().corrections.placements.as_ref().unwrap()[0].physics,
            original.physics
        );
''' + needle)
candidate.write_text(text, 'utf-8')
assert source.read_bytes() == raw
O.mkdir()
(O / 'placement_physics.before.rs').write_bytes(raw)
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()
evidence = Path('F:/OpenRPG_ImportWork/placement-physics-project-regressions-v771')
result = dict(status='codec_preservation_test_correction_prepared_not_applied',
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    source=str(source), source_sha256=sha(source), candidate=str(candidate),
    candidate_sha256=sha(candidate), prior_result_sha256=sha(evidence/'RESULT.v1.json'),
    prior_log_sha256=sha(evidence/'cargo.log'),
    diagnosis='archive_bytes preserves reopened codec; expected header is 43/44/45 according to codec',
    strengthened_check='reopen the empty archive and undo removal to recover the physical package pins',
    applied=False, tests_verified=False, full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(result))
