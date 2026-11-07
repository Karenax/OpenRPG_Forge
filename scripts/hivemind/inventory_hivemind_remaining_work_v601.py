"""Use numeric code points to avoid escaping-dependent audit predicates."""
from pathlib import Path
parent=Path(__file__).with_name('inventory_hivemind_remaining_work_v599.py')
code=parent.read_text('utf-8').replace('remaining-work-v599','remaining-work-v601')
replacement='''def strict_text_check(text,asset_id):
    skill.validate_text_encoding(text,asset_id)
    if any(ord(c)==65533 or 128<=ord(c)<=159 for c in text):
        raise ValueError('Replacement character or C1 control')
    for codec in ['cp1252','latin1']:
        try:
            repaired=text.encode(codec).decode('utf-8')
            assert repaired.encode('utf-8').decode(codec)==text
        except (UnicodeError,AssertionError):continue
        if repaired!=text and any(ord(c) in {194,195,226} for c in text):
            raise ValueError('Reversible '+codec+' decoded UTF-8 mojibake')

def inspect_text(card,path):'''
assert code.count('def inspect_text(card,path):')==1
code=code.replace('def inspect_text(card,path):',replacement)
code=code.replace("skill.validate_text_encoding(text,card['id']+'.'+field)","strict_text_check(text,card['id']+'.'+field)")
code=code.replace('Champs texte a corriger selon le validateur du skill','Champs texte a corriger selon le controle strict et reversible')
code=code.replace('O.mkdir();',"pins[str(parent)]=sha(parent)\nO.mkdir();")
exec(compile(code,str(parent)+'#numeric-code-point-text-audit','exec'),globals())
