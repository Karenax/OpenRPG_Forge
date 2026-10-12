/* Pure presentation helpers, shared by the browser and offline regression tests. */
(function (root) {
  'use strict';
  const normalize = value => String(value ?? '').normalize('NFD').replace(/\p{M}/gu,'').toLowerCase().replace(/[’']/g,' ');
  const words = query => [...new Set(normalize(query).match(/[\p{L}\p{N}_]+/gu) || [])];
  const escape = value => String(value ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  function preferences(raw) {
    const value = raw && typeof raw==='object' && !Array.isArray(raw) ? raw : {};
    return {motion:value.motion !== false, quality:['Low','Medium','High','Ultra'].includes(value.quality)?value.quality:'High', resolution:['1920×1080','2560×1440','3840×2160'].includes(value.resolution)?value.resolution:'1920×1080', vsync:value.vsync!==false, font:[100,110,120].includes(value.font)?value.font:100};
  }
  // Canonical entries repeat some secrets in otherwise public sections. Keep their exact
  // text available for deliberate disclosure, using original (zero-based) source indexes.
  const secretPassages = {
    'creature.sangsue-d-orage': [
      {path:['encounter_use','telegraphs'], indexes:[2], label:'Signes de la capacité secrète'}
    ],
    'creature.brumaille': [
      {path:['encounter_use','telegraphs'], indexes:[3], label:'Interpréter un refus'}
    ],
    'creature.gardien-des-cendres': [
      {path:['rules','abilities'], indexes:[1], label:'Appel des esprits de cendre — données documentées'}
    ],
    'creature.prevote-gobeline-des-sept-vannes': [
      {path:['rules','environmental_interactions'], indexes:[2], label:'Le déversoir de secours'},
      {path:['encounter_use','noncombat_options'], indexes:[3], label:'Négociation autour de la purge'}
    ],
    'creature.cartographe-gnoll-du-tonnerre': [
      {path:['rules','environmental_interactions'], indexes:[2], label:'Interrompre la séquence majeure'},
      {path:['encounter_use','telegraphs'], indexes:[2], label:'Reconnaître la séquence majeure'}
    ],
    'creature.porte-schiste-orc': [
      {path:['encounter_use','telegraphs'], indexes:[2], label:'Le signal de sécurité'}
    ],
    'creature.greffepente-orque': [
      {path:['encounter_use','noncombat_options'], indexes:[2], label:'Le droit de refuge'}
    ],
    'creature.maitre-etai-orc-de-la-faille-claire': [
      {path:['rules','environmental_interactions'], indexes:[3], label:'La priorité de la cloche'},
      {path:['encounter_use','noncombat_options'], indexes:[2], label:'La preuve du sabotage'},
      {path:['encounter_use','noncombat_options'], indexes:[3], label:'Suspendre le conflit par l’alerte'}
    ]
  };
  const copy = value => Array.isArray(value) ? value.map(copy) : value && typeof value==='object' ? Object.fromEntries(Object.entries(value).map(([key,item])=>[key,copy(item)])) : value;
  const atPath = (value,path) => path.reduce((part,key)=>part?.[key],value);
  function secretSupplements(creature) {
    return (secretPassages[creature.creature_id] || []).flatMap(({path,indexes,label})=>indexes.flatMap(index=>{
      const value=atPath(creature,path)?.[index];
      return value===undefined ? [] : [{label,value:copy(value)}];
    }));
  }
  function publicCreature(creature) {
    // Both search and the public reader use this whitelist. Never mutate source content.
    const fields=['name','subtitle','classification','habitats','ecology','physique','senses','communication','signs','demeanor','encounter_use','salvage','rules','field_knowledge'];
    const result=Object.fromEntries(fields.filter(key=>key in creature).map(key=>[key,copy(creature[key])]));
    // Group exclusions before filtering so two indexes in one source array cannot shift.
    const removals=new Map();
    for(const passage of secretPassages[creature.creature_id] || []){
      const key=passage.path.join('.');
      if(!removals.has(key))removals.set(key,{path:passage.path,indexes:new Set()});
      passage.indexes.forEach(index=>removals.get(key).indexes.add(index));
    }
    for(const {path,indexes} of removals.values()){
      const parent=atPath(result,path.slice(0,-1)),key=path.at(-1);
      if(Array.isArray(parent?.[key]))parent[key]=parent[key].filter((_,index)=>!indexes.has(index));
    }
    result.field_knowledge=(result.field_knowledge || []).filter(item=>item.status!=='secret');
    if(result.rules){
      result.rules.attacks=(result.rules.attacks || []).filter(item=>item.visibility!=='secrete');
      result.rules.abilities=(result.rules.abilities || []).filter(item=>item.visibility!=='secrete');
      if(creature.creature_id==='creature.brumaille'){
        const portage=result.rules.abilities.find(item=>item.name==='Portage lié');
        if(portage)portage.effect=portage.effect.replace(/\bde Balance\b/g,'d’Équilibre').replace(/\bBalance\b/g,'Équilibre');
      }
    }
    return result;
  }
  const beginsWord = (text,index) => !/[\p{L}\p{N}_]$/u.test(text.slice(Math.max(0,index-2),index));
  function hasPrefix(text,token) {
    let at=-1;
    while((at=text.indexOf(token,at+1))!==-1)if(beginsWord(text,at))return true;
    return false;
  }
  function rank(items,query) {
    const tokens=words(query), phrase=normalize(query).trim();
    return items.filter(item=>tokens.every(word=>hasPrefix(normalize(item.search),word))).map((item,order)=>{
      const title=normalize(item.displayTitle||item.title||item.name);
      const score=!tokens.length?0:(title===phrase?100:0)+(title.startsWith(phrase)?30:0)+tokens.filter(word=>hasPrefix(title,word)).length*10;
      return {item,order,score};
    }).sort((a,b)=>b.score-a.score||a.order-b.order).map(result=>result.item);
  }
  function ranges(text,query) {
    // Keep offsets valid for combining accents and surrogate pairs before a match.
    let folded='',positions=[];
    for(let i=0;i<text.length;){
      const glyph=String.fromCodePoint(text.codePointAt(i)),part=normalize(glyph),end=i+glyph.length;
      folded+=part;
      // indexOf returns UTF-16 offsets, including both code units of an emoji.
      for(let unit=0;unit<part.length;unit++)positions.push({start:i,end});
      if(!part.length&&positions.length)positions.at(-1).end=end;
      i=end;
    }
    const found=[];
    for(const token of words(query)){
      let at=0;
      while((at=folded.indexOf(token,at))!==-1){if(beginsWord(folded,at))found.push([positions[at].start,positions[at+token.length-1].end]);at+=token.length;}
    }
    found.sort((a,b)=>a[0]-b[0]);const merged=[];
    for(const range of found){const last=merged.at(-1);if(last&&range[0]<=last[1])last[1]=Math.max(last[1],range[1]);else merged.push(range);}
    return merged;
  }
  function highlighted(text,query) {
    let html='',at=0;for(const [start,end] of ranges(text,query)){html+=escape(text.slice(at,start))+'<mark>'+escape(text.slice(start,end))+'</mark>';at=end;}return html+escape(text.slice(at));
  }
  function family(creature) {
    const name=normalize(creature.classification.family+' '+creature.name);
    const nature=normalize(creature.classification.nature);
    if (/gobelin/.test(name)) return 'Gobelins';
    if (/gnoll/.test(name)) return 'Gnolls';
    if (/\borc|\borque/.test(name)) return 'Orcs';
    if (/\bfaune\b|\bfaunes\b/.test(name)) return 'Faunes';
    if (/dragon|vouivre/.test(nature+' '+name)) return 'Dragons';
    if (/mort|revenant|spectr|squelette/.test(nature)) return 'Morts liés';
    if (/^geante?\b|^ogre|\bhumanoide geante?\b/.test(nature)) return 'Géants';
    if (/^elementaire/.test(nature)) return 'Élémentaires';
    if (/agregat cristallin/.test(nature)) return 'Présences minérales';
    if (/amorphe|gelatine|limon|gelee/.test(nature+' '+name)) return 'Organismes amorphes';
    if (/fongique/.test(nature)) return 'Colonies fongiques';
    if (/vegetal|racinaire/.test(nature)) return 'Êtres végétaux';
    if (/gardien|construct/.test(nature)) return 'Gardiens animés';
    if (/feerique/.test(nature)) return 'Êtres féeriques';
    if (/esprit/.test(nature)) return 'Esprits des lieux';
    if (/animal|arthropode|annelide|oiseau/.test(nature)) return 'Faune sauvage';
    return 'Autres créatures';
  }
  const api={normalize,words,escape,preferences,publicCreature,secretSupplements,rank,ranges,highlighted,family};
  if(typeof module==='object'&&module.exports)module.exports=api;else root.ForgeCodex=api;
})(typeof window==='undefined'?globalThis:window);
