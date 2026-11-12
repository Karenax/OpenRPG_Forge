"""Compare Unity prefab bytes except LOD values and known collider names."""
import re

HEADER=re.compile(r'^--- !u!(\d+) &-?\d+(?:\s.*)?$')
SCALAR=re.compile(r'^(\s*(?:-\s*)?(?:screenRelativeHeight|m_Size):\s*)[-+0-9.eE]+(\s*)$')
VECTOR=re.compile(r'^(\s*m_LocalReferencePoint:\s*)\{x:\s*[-+0-9.eE]+,\s*y:\s*[-+0-9.eE]+,\s*z:\s*[-+0-9.eE]+\}(\s*)$')
COLLIDER=re.compile(r'^(\s*m_Name:\s*)(?:Collider|BoxColliderObject(?: \(\d+\))?)(\s*)$')


def normalize(text):
    klass=None;out=[]
    for line in text.splitlines():
        h=HEADER.match(line)
        if h:klass=h.group(1)
        if klass=='205':
            line=SCALAR.sub(r'\g<1><LOD_VALUE>\g<2>',line)
            line=VECTOR.sub(r'\g<1><LOD_VECTOR>\g<2>',line)
        elif klass=='1':
            line=COLLIDER.sub(r'\g<1><COLLIDER_NAME>\g<2>',line)
        out.append(line)
    return out


def compare(a,b):
    x,y=a.splitlines(),b.splitlines()
    return x!=y and normalize(a)==normalize(b)
