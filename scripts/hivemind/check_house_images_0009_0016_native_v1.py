"""Probe eight newly indexed source cards through the actual product library reader."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
text = (S / 'check_house_thumbnail_batch_native_reader_v1.py').read_text(encoding='utf-8')
replacements = {
    'house-source-thumbnail-batch-0001-0008-native-reader-v1': 'house-source-thumbnail-batch-0009-0016-native-reader-v1',
    'house-source-thumbnail-batch-0001-0008-index-v1': 'house-source-thumbnail-batch-0009-0016-index-v1',
    "index['cards_updated']==5": "index['cards_updated']==8", 'rows.len(),5': 'rows.len(),8',
    '"cards_verified":5': '"cards_verified":8', "native['cards_verified']==5": "native['cards_verified']==8",
    'cards_verified=5': 'cards_verified=8',
    'five_source_card_search_filter_details_and_thumbnail_verified_by_current_native_reader': 'eight_source_card_search_filter_details_and_thumbnail_verified_by_current_native_reader',
    'five_current_native_source_card_search_filter_details_images_passed': 'eight_current_native_source_card_search_filter_details_images_passed',
    'Five current native card probes passed': 'Eight current native card probes passed',
}
for old in sorted(replacements, key=len, reverse=True):
    assert old in text, old
    text = text.replace(old, replacements[old])
target = S / 'run_house_images_0009_0016_native_v1.py'
assert not target.exists()
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
