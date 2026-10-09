"""Parameter adapter for the unchanged, evidenced Townsmith converter wrapper."""
import json
from pathlib import Path
import sys

import convert_verified_townsmith_batch as converter
from assemble_native_villa_delivery import require, sha

def main(start):
    require(start >= 20 and start % 20 == 0 and start + 20 <= 893, 'Known complete 20-case subset required')
    suffix = f'{start:04d}-{start+19:04d}-v1'
    converter.RECEIPT = converter.W / ('townsmith-native-partial-reopen-' + suffix) / 'RESULT.v1.json'
    converter.ROOT = converter.W / ('townsmith-native-geometry-' + suffix)
    converter.main()
    result = converter.ROOT / 'RESULT.v1.json'
    data = json.loads(result.read_text('utf-8-sig'))
    data['parameter_adapter'] = {'path': str(Path(__file__).resolve()), 'sha256': sha(Path(__file__).resolve()),
        'start_ordinal': start, 'end_ordinal': start + 19, 'original_wrapper_body_unchanged': True}
    data['input_pins'][str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    temporary = result.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(data, indent=2), 'utf-8')
    temporary.replace(result)

if __name__ == '__main__':
    main(int(sys.argv[1]))
