"""Compare Unity prefabs excluding explicit pipeline light components.

Comparison never claims render equivalence: all original sources are retained.
"""
import re

HEADER=re.compile(r'^--- !u!(\d+) &(-?\d+)(?:\s.*)?$')
SCRIPT=re.compile(r'^\s*m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),',re.I|re.M)
COMPONENT=re.compile(r'^\s*-\s*component:\s*\{fileID:\s*(-?\d+)\}\s*$')
GAMEOBJECT=re.compile(r'^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}\s*$')
ENABLED=re.compile(r'^\s*m_Enabled:\s*(\d+)\s*$')
PIPELINE_LIGHT_SCRIPTS={'7a68c43fe1f2a47cfa234b5eeaa98012','474bcb49853aa07438625e644c072ee6'}


def normalized(text):
    lines=text.splitlines();chunks=[];current=[]
    for line in lines:
        if HEADER.match(line) and current:chunks.append(current);current=[]
        current.append(line)
    if current:chunks.append(current)
    removed=set();light_count=0
    for chunk in chunks:
        head=HEADER.match(chunk[0])
        if not head or head.group(1)!='114':continue
        script=SCRIPT.search('\n'.join(chunk))
        if script and script.group(1).lower() in PIPELINE_LIGHT_SCRIPTS:removed.add(head.group(2))
    if not removed:raise ValueError('no known pipeline light component')
    out=[];uses={key:0 for key in removed}
    for chunk in chunks:
        head=HEADER.match(chunk[0]);class_id=head.group(1) if head else None
        if head and head.group(2) in removed:continue
        if class_id=='108':
            objects=[m.group(1) for line in chunk if (m:=GAMEOBJECT.match(line))]
            enabled=[m.group(1) for line in chunk if (m:=ENABLED.match(line))]
            if len(objects)!=1 or len(enabled)!=1:raise ValueError('ambiguous light attachment')
            out.extend((chunk[0],'  m_GameObject: '+objects[0],'  m_Enabled: '+enabled[0]))
            light_count+=1;continue
        for line in chunk:
            m=COMPONENT.match(line)
            if m and m.group(1) in removed:
                uses[m.group(1)]+=1;continue
            if any(re.search(r'\bfileID:\s*'+re.escape(ident)+r'\b',line) for ident in removed):
                raise ValueError('pipeline light component has another reference')
            out.append(line)
    if not light_count or any(n!=1 for n in uses.values()):raise ValueError('invalid light component coverage')
    return out,light_count,len(removed)


def compare(first,second):
    a,la,ra=normalized(first);b,lb,rb=normalized(second)
    return {'equal_outside_pipeline_lights':a==b and la==lb and ra==rb,'light_count':(la,lb),'pipeline_components':(ra,rb)}
