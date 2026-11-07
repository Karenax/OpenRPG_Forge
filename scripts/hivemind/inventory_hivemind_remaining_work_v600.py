"""Detect reversible UTF-8 mojibake beyond the skill's current regex coverage."""
from pathlib import Path
parent=Path(__file__).with_name('inventory_hivemind_remaining_work_v599.py')
code=parent.read_text('utf-8').replace('remaining-work-v599','remaining-work-v600')
needle='def inspect_text(card,path):'
replacement='''def strict_text_check(text,asset_id):
    skill.validate_text_encoding(text,asset_id)
    if '\\ufffd' in text or any('\\u0080'<=c<='\\u009f' for c in text):
        raise ValueError('Replacement character or C1 control in text')
    for encoding in ['cp1252','latin1']:
        try:
            repaired=text.encode(encoding).decode('utf-8')
            assert repaired.encode('utf-8').decode(encoding)==text
        except (UnicodeError,AssertionError):continue
        if repaired!=text and any(c in text for c in ['\\u00c2','\\u00c3','\\u00e2']):
            raise ValueError('Reversible '+encoding+' decoded UTF-8 mojibake')

def inspect_text(card,path):'''
assert code.count(needle)==1;code=code.replace(needle,replacement)
code=code.replace("skill.validate_text_encoding(text,card['id']+'.'+field)","strict_text_check(text,card['id']+'.'+field)")
code=code.replace('Champs texte a corriger selon le validateur du skill','Champs texte a corriger selon le controle strict et reversible')
code=code.replace('O.mkdir();',"pins[str(parent)]=sha(parent)\nO.mkdir();")
exec(compile(code,str(parent)+'#strict-reversible-text-audit','exec'),globals())
