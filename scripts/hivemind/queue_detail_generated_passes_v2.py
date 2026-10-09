"""Use the four-argument ShaderGraph API verified in the installed package."""
from pathlib import Path
import sys
BASE=Path(__file__).with_name('queue_detail_generated_passes_v1.py')
def main():
    text=BASE.read_text('utf-8')
    replacements=[('detail-native-generated-passes-v1','detail-native-generated-passes-v2'),('DetailGeneratedPassesV1','DetailGeneratedPassesV2'),
        ('m.GetParameters().Length==2','m.GetParameters().Length==4&&m.GetParameters()[3].IsOut'),
        ('object[] args={graphPath,null};','object[] args={graphPath,null,null,null};')]
    for old,new in replacements:
        assert old in text,old;text=text.replace(old,new)
    text=text.replace('files=[HELPER,Path(__file__),','files=[BASE,HELPER,Path(__file__),')
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE))
if __name__=='__main__':main()
