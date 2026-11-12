"""Prepare a testable importer with durable prefab catalog redirects."""
from pathlib import Path

source = Path('E:/CodexData/skills/openrpg-import-assets/scripts/asset_import.py')
target = Path('E:/OpenRPG_Forge/.build/asset-conversion/asset-import-prefab-guard-candidate-20261112.py')
raw = source.read_text(encoding='utf8')
start = raw.index('def redirected_archive_catalog(')
end = raw.index('\ndef prepare(', start)
guard = raw[start:end].replace('redirected_archive_catalog', 'redirected_prefab_catalog')
guard = guard.replace('source_catalog_archive_origin', 'source_catalog_prefab_origin')
guard = guard.replace('openrpg-archive-', 'openrpg-prefab-')
guard = guard.replace('archive catalog', 'prefab catalog').replace('archive catalogs', 'prefab catalogs')
guard = guard.replace('Archive catalog', 'Prefab catalog').replace('Derived archive', 'Derived prefab').replace('Stale derived archive', 'Stale derived prefab')
needle = '    catalog_path=redirected_archive_catalog(vault,catalog_path,registry_before)'
if raw.count(needle) != 1 or 'def redirected_prefab_catalog(' in raw:
    raise ValueError('importer structure changed')
result = raw[:end]+'\n'+guard+raw[end:]
result = result.replace(needle, '    catalog_path=redirected_prefab_catalog(vault,catalog_path,registry_before)\n'+needle)
with target.open('x', encoding='utf8', newline='') as stream:
    stream.write(result)
print(target)
