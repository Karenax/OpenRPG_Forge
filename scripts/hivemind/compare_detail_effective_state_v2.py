"""Retain exact serialized labels; pass-name alias resolution is unqualified."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_detail_effective_state_v1.py')
def main():
    text=BASE.read_text('utf-8')
    text=text.replace("ROOT=W/'detail-authored-effective-state-v1'", "ROOT=W/'detail-authored-effective-state-v2'")
    text=text.replace("authored_enabled=p['name'] not in disabled", "authored_enabled_by_exact_serialized_label=p['name'] not in disabled")
    text=text.replace("disabled_pass_differences=changes,", "disabled_pass_exact_label_comparison=changes,source_disabled_serialized_labels=sorted(disabled),source_pass_label_resolution_qualified=False,")
    text=text.replace("render_state_migration_implicitly_adopted=False,", "pass_behavior_change_inferred_from_label_comparison=False,render_state_migration_implicitly_adopted=False,")
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve())))
if __name__=='__main__':main()
