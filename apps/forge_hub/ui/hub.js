/* Presentation adapter only; all plans, validation and mutations live in Rust. */
const invoke = window.__TAURI__.core.invoke;
const $ = id => document.getElementById(id);
let pending = null;
const kind = {applications:'Applications',assets:'Assets',module:'Module'};
const sizes = n => `${(n / 1024 / 1024).toFixed(1)} Mio`;
function item(list, label, detail){const li=document.createElement('li');const name=document.createElement('strong');name.textContent=label;const meta=document.createElement('span');meta.textContent=detail;li.append(name,meta);list.append(li);}
async function call(request){return invoke('delivery',{request});}
async function task(action,message){$('error').hidden=true;$('status').textContent=message;document.querySelectorAll('button').forEach(b=>b.disabled=true);try{await action();}catch(error){$('error').textContent=String(error);$('error').hidden=false;}finally{await refresh();}}
async function refresh(){try{const s=await call({action:'status'});$('location').textContent=s.root;$('packs').replaceChildren();$('play').disabled=$('edit').disabled=$('verify').disabled=!s.installed;$('rollback').disabled=!s.installed||!s.state.previous;['select','bundled','cancel'].forEach(id=>$(id).disabled=false);$('apply').disabled=!pending;if(s.installed){$('title').textContent=s.product.title;$('subtitle').textContent='Jouez à votre aventure ou poursuivez sa création dans le Studio.';$('version').textContent=`Installation ${s.state.revision}`;$('status').textContent='Installation prête · packs de contenu séparés';item($('packs'),'Applications',s.state.active.core.id);Object.values(s.state.active.packs).forEach(p=>item($('packs'),p.id,kind[p.kind]||p.kind));}else{$('status').textContent='Préparez la livraison fournie ou sélectionnez vos packages Forge.';}}catch(error){$('error').textContent=String(error);$('error').hidden=false;['select','bundled'].forEach(id=>$(id).disabled=false);}}
function preview(response){pending=response.plan;$('preview').hidden=false;$('changes').replaceChildren();$('summary').textContent=pending.operation==='rollback'?'Retour à la version précédente. Vos projets et sauvegardes seront conservés.':`${response.packages.length} package(s) seront vérifiés et installés. Les versions précédentes seront conservées.`;response.packages.forEach(p=>item($('changes'),p.label,`${kind[p.kind]} · ${sizes(p.bytes)} · ${p.files} fichiers`));$('plan').textContent=JSON.stringify(pending,null,2);$('apply').disabled=false;}
$('play').onclick=()=>task(()=>call({action:'launch',app:'game'}),'Démarrage du jeu…');
$('edit').onclick=()=>task(()=>call({action:'launch',app:'editor'}),'Ouverture de l’éditeur…');
$('verify').onclick=()=>task(async()=>{await call({action:'verify'});$('subtitle').textContent='Vérification terminée : tous les fichiers correspondent à leur manifeste.';},'Vérification des fichiers…');
$('select').onclick=()=>task(async()=>{const files=await call({action:'select'});if(files.length)preview(await call({action:'prepare',files}));},'Sélection des packages…');
$('bundled').onclick=()=>task(async()=>{const {files}=await call({action:'bundled'});if(!files.length)throw 'Aucune livraison jointe. Choisissez « Ajouter des packages ».';preview(await call({action:'prepare',files}));},'Vérification de la livraison…');
$('rollback').onclick=()=>task(async()=>preview(await call({action:'rollback'})),'Préparation du retour…');
$('apply').onclick=()=>task(async()=>{await call({action:'apply',command_id:pending.command_id});pending=null;$('preview').hidden=true;},'Installation en cours…');
$('cancel').onclick=()=>{pending=null;$('preview').hidden=true;};
refresh();
