/* Original presentation prototype. No Forge commands, filesystem, backend or canonical data. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const NS = 'http://www.w3.org/2000/svg';
  const tools = [
    ['select','Sélection','#84acfa,#547be4','M5 3l14 10-7 1-3 7z','SÉLECTIONNER','Sélection','Choisis un outil pour explorer la maquette.'],
    ['terrain','Terrain','#deb66a,#b68d4c','M2 20L10 4l5 9 3-5 5 12z','SCULPTER','Terrain','Les réglages changent la brosse de démonstration.'],
    ['nature','Nature','#95ce85,#489879','M12 22v-8M5 13C0 3 12 1 12 1s13 6 5 13c-4 3-9 2-12-1','FAIRE POUSSER','Nature','Clique sur la carte pour préparer un arbre.'],
    ['paths','Chemins','#f2c879,#dd9659','M5 22c18-5-9-9 7-13s4-7 4-7','DESSINER','Chemins','Clique pour poser les points. Valide pour garder le tracé.'],
    ['water','Eau','#7dd9e5,#3b9fc5','M12 2C9 8 4 12 4 16a8 8 0 0016 0c0-4-5-9-8-14z','MODELER','Eau','Clique pour dessiner une rivière illustrative.'],
    ['build','Construire','#efa78c,#d27f73','M3 11l9-9 9 9M5 10v12h14V10M10 22v-8h4v8','BÂTIR','Construire','Clique sur la carte pour préparer une maison.'],
    ['objects','Objets','#baa4f2,#8670cd','M3 7l9-5 9 5v12l-9 5-9-5zM3 7l9 6 9-6M12 13v11','DÉCORER','Objets','Choisis un décor, puis clique sur la carte.'],
    ['library','Décors','#8ccbd3,#5795b8','M3 4h7l2 3h9v14H3z','', '', '']
  ];
  const assets = [['Chêne','tree'],['Pin','tree'],['Buisson','tree'],['Rocher','rock'],['Maison','house'],['Bosquet','tree']];
  let tool='objects', asset=0, zoom=1, points=[], ghost=null, history=[], brush=12;
  const say = message => { $('status').textContent=message; };
  function svgNode(name, attrs) { const el=document.createElementNS(NS,name); for(const [k,v] of Object.entries(attrs)) el.setAttribute(k,v); return el; }
  function thumb(index) { return `<svg class="thumb" viewBox="0 0 100 100" aria-hidden="true"><use href="#${assets[index][1]}" x="12" y="5" width="76" height="88" ${index===1?'transform="translate(20 0) scale(.65 1)"':index===2?'transform="translate(0 45) scale(1 .5)"':''}/></svg>`; }
  tools.forEach(([id,label,colors,path]) => {
    const b=document.createElement('button'); b.dataset.tool=id;b.setAttribute('aria-pressed',String(id===tool));b.innerHTML=`<span class="dock-icon" style="background:linear-gradient(145deg,${colors})"><svg viewBox="0 0 26 26" aria-hidden="true"><path d="${path}"/></svg></span><span class="dock-label">${label}</span>`;
    b.onclick=()=>{if(id==='library'){$('library').hidden=!$('library').hidden;b.setAttribute('aria-pressed',String(!$('library').hidden));}else setTool(id);};
    b.onpointerenter=()=>{b.previousElementSibling?.classList.add('near');b.nextElementSibling?.classList.add('near');};b.onpointerleave=()=>document.querySelectorAll('.near').forEach(x=>x.classList.remove('near'));
    $('dock').append(b);
  });
  assets.forEach(([name],i)=>{const b=document.createElement('button');b.className='asset';b.setAttribute('aria-label',`Choisir ${name}`);b.innerHTML=thumb(i)+`<span>${name}</span>`;b.onclick=()=>{asset=i;setTool('objects');renderAssets();};$('assets').append(b);});
  function renderAssets(){[...$('assets').children].forEach((b,i)=>{b.classList.toggle('selected',i===asset);b.setAttribute('aria-pressed',String(i===asset));});}
  function range(label,value,min,max,unit){return `<label class="field"><span class="field-head"><span>${label}</span><output>${value} ${unit}</output></span><input type="range" aria-label="${label}" value="${value}" min="${min}" max="${max}" data-unit="${unit}"></label>`;}
  function clearDraft(){points=[];ghost=null;$('draft').replaceChildren();$('apply').disabled=true;if($('pointCount'))$('pointCount').textContent='Aucun point posé';}
  function setTool(id){
    clearDraft(); tool=id;const data=tools.find(t=>t[0]===id);$('toolKicker').textContent=data[4];$('toolTitle').textContent=data[5];$('toolHint').textContent=data[6];$('toolBadge').textContent={select:'↖',terrain:'⌁',nature:'♧',paths:'〰',water:'≈',build:'⌂',objects:'◇'}[id];
    document.querySelectorAll('[data-tool]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.tool===id||(b.dataset.tool==='library'&&!$('library').hidden))));
    document.querySelectorAll('[data-select]').forEach(b=>b.classList.toggle('active',b.dataset.select===id));
    let content='';
    if(['objects','nature','build'].includes(id)){let a=id==='build'?4:id==='nature'?0:asset;content=`<div class="choice">${thumb(a)}<div><strong>${assets[a][0]}</strong><small>Décor illustratif</small></div></div>`+range('Rotation',0,0,360,'°')+range('Échelle',100,40,180,'%');}
    else if(id==='paths'||id==='water'){content=`<label class="field">Style <select id="material"><option>${id==='paths'?'Sentier naturel':'Ruisseau'}</option><option>${id==='paths'?'Chemin pavé':'Rivière'}</option></select></label>`+range('Largeur',id==='paths'?8:18,3,40,'m')+'<p class="hint" id="pointCount">Aucun point posé</p>';}
    else if(id==='terrain'){content='<div class="swatches"><button class="swatch" aria-label="Herbe" aria-pressed="true" style="background:#9bc58a"></button><button class="swatch" aria-label="Sable" aria-pressed="false" style="background:#e4d2a4"></button><button class="swatch" aria-label="Roche" aria-pressed="false" style="background:#aabbb5"></button></div>'+range('Rayon',12,3,40,'m')+range('Intensité',35,1,100,'%');}
    else content='<p class="hint">Le dock regroupe les outils. La scène et la bibliothèque peuvent se replier pour libérer la carte.</p>';
    $('controls').innerHTML=content;$('apply').textContent=['paths','water'].includes(id)?'Créer le tracé':id==='terrain'?'Appliquer':'Placer';
    $('controls').querySelectorAll('input').forEach(input=>input.oninput=()=>{input.previousElementSibling.querySelector('output').textContent=`${input.value} ${input.dataset.unit}`;if(input.getAttribute('aria-label')==='Rayon')brush=Number(input.value);updateGhost();});
    $('controls').querySelectorAll('.swatch').forEach(b=>b.onclick=()=>{$('controls').querySelectorAll('.swatch').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));say('Matériau de démonstration : '+b.getAttribute('aria-label'));});
    if($('material'))$('material').onchange=updateGhost;
    $('toolTitle').classList.remove('pulse');void $('toolTitle').offsetWidth;$('toolTitle').classList.add('pulse');say(data[6]);
  }
  function updateGhost(){
    const inputs=$('controls').querySelectorAll('input');
    const line=$('draft').querySelector('polyline');
    if(line&&inputs.length){line.setAttribute('stroke-width',inputs[0].value);line.setAttribute('stroke',tool==='paths'?($('material').selectedIndex?'#babec0':'#f9dd98'):($('material').selectedIndex?'#409dc8':'#67cce0'));}
    if(!ghost)return;
    if(tool==='terrain'){ghost.querySelector('circle').setAttribute('r',brush*2);ghost.querySelector('circle').setAttribute('opacity',Number(inputs[1].value)/100);}
    if(inputs.length===2&&['objects','nature','build'].includes(tool)){const rotation=Number(inputs[0].value),scale=Number(inputs[1].value)/100;ghost.setAttribute('transform',`translate(${ghost.dataset.x} ${ghost.dataset.y}) rotate(${rotation}) scale(${scale})`);}
  }
  $('map').addEventListener('click',e=>{
    if(tool==='select'){say('Sélection : choisis un outil dans le dock pour essayer un geste.');return;}
    const p=new DOMPoint(e.clientX,e.clientY).matrixTransform($('map').getScreenCTM().inverse());
    if(['paths','water'].includes(tool)){points.push([p.x,p.y]);$('draft').replaceChildren();const line=svgNode('polyline',{points:points.map(p=>p.join(',')).join(' '),fill:'none',stroke:tool==='paths'?'#f9dd98':'#67cce0','stroke-width':$('controls').querySelector('input').value,'stroke-linecap':'round','stroke-linejoin':'round'});$('draft').append(line);points.forEach(([x,y])=>$('draft').append(svgNode('circle',{cx:x,cy:y,r:5,fill:'white',stroke:'#2985d2','stroke-width':2})));$('pointCount').textContent=`${points.length} point${points.length>1?'s':''} posé${points.length>1?'s':''}`;$('apply').disabled=points.length<2;say(points.length<2?'Ajoute un deuxième point.':'Tracé prêt · Créer le tracé pour confirmer.');return;}
    $('draft').replaceChildren();ghost=svgNode('g',{'data-x':p.x,'data-y':p.y,transform:`translate(${p.x} ${p.y})`});
    if(tool==='terrain'){ghost.append(svgNode('circle',{r:brush*2,fill:'#dcedb6',opacity:.6,stroke:'white','stroke-width':2}));say('Emprise illustrative · aucune sculpture du moteur.');}
    else{const a=tool==='build'?4:tool==='nature'?0:asset;ghost.append(svgNode('ellipse',{cx:0,cy:0,rx:38,ry:17,fill:'none',stroke:'#fff','stroke-width':3,'stroke-dasharray':'5 4'}));ghost.append(svgNode('use',{href:'#'+assets[a][1],x:-38,y:-82,width:76,height:92}));say('Aperçu posé · ajuste puis clique sur Placer.');}
    $('draft').append(ghost);$('apply').disabled=false;updateGhost();
  });
  $('apply').onclick=()=>{if($('apply').disabled)return;updateGhost();const group=svgNode('g',{});[...$('draft').children].forEach(n=>group.append(n));$('placed').append(group);history.push(group);clearDraft();say('Modification appliquée à la démo uniquement.');};
  $('cancel').onclick=()=>{clearDraft();say('Aperçu annulé.');};
  $('undo').onclick=()=>{const last=history.pop();last?.remove();say(last?'Dernière action de démonstration annulée.':'Aucune action à annuler.');};
  $('save').onclick=()=>say('Simulation seulement : rien n’est enregistré dans Forge.');
  document.querySelectorAll('[data-select]').forEach(b=>b.onclick=()=>setTool(b.dataset.select));
  $('libraryClose').onclick=()=>{$('library').hidden=true;document.querySelector('[data-tool="library"]').setAttribute('aria-pressed','false');};
  $('sceneClose').onclick=()=>{$('scenePanel').hidden=true;$('sceneOpen').hidden=false;};$('sceneOpen').onclick=()=>{$('scenePanel').hidden=false;$('sceneOpen').hidden=true;};
  function setZoom(v){zoom=Math.max(.8,Math.min(1.7,v));$('map').style.setProperty('--zoom',zoom);$('zoomValue').textContent=Math.round(zoom*100)+' %';}
  $('zoomIn').onclick=()=>setZoom(zoom+.1);$('zoomOut').onclick=()=>setZoom(zoom-.1);
  $('grid').onclick=()=>{const on=$('grid').getAttribute('aria-pressed')!=='true';$('grid').setAttribute('aria-pressed',String(on));let grid=$('gridLines');if(!grid){grid=svgNode('path',{id:'gridLines',stroke:'#fff',opacity:.28,'stroke-width':1,fill:'none','pointer-events':'none',d:Array.from({length:29},(_,i)=>`M${i*50} 0V900`).join(' ')+Array.from({length:19},(_,i)=>`M0 ${i*50}H1400`).join(' ')});$('map').append(grid);}grid.style.display=on?'':'none';};
  $('settings').onclick=()=>$('preferences').showModal();
  $('motion').checked=!matchMedia('(prefers-reduced-motion: reduce)').matches;
  $('motion').onchange=()=>document.body.classList.toggle('no-motion',!$('motion').checked);
  $('glass').onchange=()=>document.body.classList.toggle('no-glass',!$('glass').checked);
  $('accent').onchange=()=>{const colors={blue:['#257cdc','#e7f1ff'],purple:['#8660cf','#f0eaff'],green:['#248675','#e0f4ec']}[$('accent').value];document.documentElement.style.setProperty('--accent',colors[0]);document.documentElement.style.setProperty('--accent-soft',colors[1]);};
  document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!$('preferences').open){clearDraft();say('Aperçu annulé.');}});
  let seed=27;function rand(){seed=(seed*1664525+1013904223)>>>0;return seed/4294967296;}
  for(let i=0;i<105;i++){const x=rand()*1400,y=rand()*900;if((x>400&&x<1010&&y>190&&y<650)||Math.abs(x-820)<100)continue;const size=35+rand()*38;$('grove').append(svgNode('use',{href:'#tree',x,y,width:size,height:size*1.25,opacity:.7+rand()*.3}));}
  renderAssets();setTool('objects');
})();
