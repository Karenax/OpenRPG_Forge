import { strict as assert } from 'node:assert';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const script = readFileSync(new URL('./desktop.js', import.meta.url), 'utf8');
class Element {
  constructor(tag, doc) { this.tagName = tag.toUpperCase(); this.doc = doc; this.children = []; this.parent = null; this.listeners = {}; this.attrs = {}; this._text = ''; this._html = ''; this.disabled = false; this.selected = false; this.dataset = {}; }
  set textContent(value) { this._text = String(value); this.children = []; }
  get textContent() { return this._text + this.children.map(child => child.textContent).join(''); }
  set innerHTML(value) {
    this._html = String(value);
    if (this.tagName === 'DIALOG') {
      for (const name of ['launcher-exit-cancel','launcher-exit-confirm','launcher-exit-error']) { const child = new Element('button', this.doc); child.id = name; this.append(child); }
    }
  }
  get innerHTML() { return this._html; }
  setAttribute(name,value) { this.attrs[name] = String(value); }
  removeAttribute(name) { delete this.attrs[name]; }
  append(...children) { for (const child of children) { child.parent = this; this.children.push(child); } }
  replaceChildren(...children) { for (const child of this.children) child.parent = null; this.children = []; this._text = ''; this.append(...children); }
  addEventListener(name, fn) { (this.listeners[name] ||= []).push(fn); }
  fire(name, event = {}) { for (const fn of this.listeners[name] || []) fn({target:this, ...event}); }
  querySelectorAll(selector) { const found=[]; const visit=node=>{ for (const child of node.children) { if (selector === 'button' && child.tagName === 'BUTTON') found.push(child); visit(child); } }; visit(this); return found; }
  querySelector(selector) { const id=selector.startsWith('#') ? selector.slice(1) : ''; return this.find(node=>node.id===id); }
  find(predicate) { if (predicate(this)) return this; for (const child of this.children) { const match=child.find(predicate); if(match) return match; } return null; }
  closest(tag) { return this.tagName === tag.toUpperCase() ? this : this.parent?.closest(tag) || null; }
  get isConnected() { let node=this; while(node.parent) node=node.parent; return node===this.doc.body || node===this.doc.head; }
  get selectedIndex() { const chosen=this.children.findIndex(child=>child.selected); return chosen < 0 ? 0 : chosen; }
  set selectedIndex(value) { this.children.forEach((child,index)=>child.selected=index===value); }
  focus() { this.doc.activeElement=this; } showModal() { this.open=true; } close() { this.open=false; }
}
function environment(invoke, standalone=true, installer=false) {
  const doc={ documentElement:{dataset:{forgeLauncher:standalone?'standalone':undefined}}, listeners:{}, activeElement:null, title:'Jeu' };
  doc.createElement=tag=>new Element(tag,doc); doc.head=doc.createElement('head'); doc.body=doc.createElement('body');
  doc.addEventListener=(name,fn)=>(doc.listeners[name] ||= []).push(fn);
  doc.getElementById=id=>doc.body.find(node=>node.id===id);
  doc.querySelector=selector=>selector==='#main [role="status"]'?doc.getElementById('initial-status'):null;
  const nav=doc.createElement('nav'); nav.className='main-nav';
  for(const hash of ['#accueil','#aventures','#reglages']) { const link=doc.createElement('a'); link.hash=hash; nav.append(link); }
  doc.querySelectorAll=selector=>selector==='.main-nav a'?nav.children:[];
  const brand=doc.createElement('span'); brand.id='launcher-brand';
  const main=doc.createElement('main'); main.id='main';
  const footer=doc.createElement('span'); footer.id='launcher-footer';
  const announcer=doc.createElement('div'); announcer.id='announcer';
  doc.body.append(brand,nav,main,footer,announcer);
  const location={hash:''}; const events={}; const timers=[];
  const dispatch=(name,args)=> !installer && args?.command?.kind==='installer-status'
    ? Promise.resolve({kind:'not-installer'}) : invoke(name,args);
  const window={__TAURI__:{core:{invoke:dispatch}}, addEventListener:(name,fn)=>(events[name] ||= []).push(fn)};
  const context={window,document:doc,location,console,queueMicrotask,WeakMap,WeakSet,Object,String,Error,
    setTimeout:(fn,delay)=>{timers.push({fn,delay}); return timers.length;}};
  runInNewContext(script,context);
  return {doc,window,location,main,timers,fireHash(){for(const fn of events.hashchange||[])fn();},
    runTimer(){const timer=timers.shift(); assert.ok(timer,'expected a scheduled observation'); timer.fn();}};
}
const flush=()=>new Promise(resolve=>setImmediate(resolve));
const unsafeText='<img src=x onerror=alert(1)>';
const first={module_id:'module-1',area_id:'area-1'};
const second={module_id:'module-1',area_id:'area-2'};
const saveHash='a'.repeat(64);
const game={game_id:'project-1',title:unsafeText,summary:'Deux cartes',entry:first,modules:[{name:'Module',version:'1',zones:[{name:'Port',selection:first,resume:{status:'absent'},pending_resume:false},{name:'Crique',selection:second,resume:{status:'absent'},pending_resume:false}]}]};
const requests=[];
let launchId=0;
const env=environment(async (_name,{command})=>{
  requests.push(command);
  if (command.kind==='game-view') return {kind:'game-view',runtime_connected:true,game};
  if (command.kind==='game-status') return {kind:'game-status',launch_id:command.launch_id,status:{state:'exited',success:true,exit_code:0}};
  return {kind:'launch-started',process_id:7,launch_id:++launchId};
});
await flush();
assert.equal(env.doc.getElementById('launcher-brand').textContent,unsafeText);
assert.equal(env.main.find(node=>node.tagName==='H1').textContent,unsafeText);
assert.equal(env.main.find(node=>node.tagName==='H1').innerHTML,'');
let buttons=env.main.querySelectorAll('button'); assert.equal(buttons.length,3);
env.main.fire('click',{target:buttons[0]}); await flush();
assert.equal(requests.at(-1).kind,'request-entry-launch');
assert.equal(requests.at(-1).selection.area_id,'area-1');
env.runTimer(); await flush();
env.location.hash='#aventures'; env.fireHash();
const select=env.main.find(node=>node.tagName==='SELECT'); assert.equal(select.children[1].textContent,'Crique'); select.selectedIndex=1;
buttons=env.main.querySelectorAll('button'); env.main.fire('click',{target:buttons.find(button=>button.textContent==='Jouer cette carte')}); await flush();
assert.equal(requests.at(-1).kind,'request-module-launch'); assert.equal(requests.at(-1).selection.area_id,'area-2');
env.location.hash='#reglages'; env.fireHash(); assert.match(env.main.textContent,/ne sont pas encore appliqués/);
const failures=environment(async()=>({kind:'rejected',reason:'archive endommagée'})); await flush();
assert.match(failures.main.textContent,/indisponible/); assert.match(failures.main.textContent,/archive endommagée/);
assert.equal(failures.main.find(node=>node.tagName==='P' && node.attrs.role==='alert')!==null,true);
const legacy=environment(async()=>{throw new Error('unexpected');},false); assert.ok(legacy.window.ForgeDesktop);
const rollbackCommands=[];
let rollbackResult={kind:'rollback-prepared',available:true};
let rollbackApply={kind:'rejected',reason:'installation modifiée'};
const rollback=environment(async (_name,{command})=>{
  rollbackCommands.push(command.kind);
  if(command.kind==='game-view') return {kind:'game-view',runtime_connected:true,game:structuredClone(game)};
  if(command.kind==='prepare-rollback') return rollbackResult;
  if(command.kind==='apply-rollback') return rollbackApply;
  throw new Error('unexpected');
});
await flush(); rollback.location.hash='#reglages'; rollback.fireHash();
const clickRollback=label=>rollback.main.querySelectorAll('button').find(node=>node.textContent===label).fire('click');
clickRollback('Restaurer la version précédente'); await flush();
assert.match(rollback.main.textContent,/sauvegardes seront conservées/);
assert.equal(rollback.doc.activeElement.textContent,'Annuler');
assert.equal(rollbackCommands.includes('apply-rollback'),false);
clickRollback('Annuler');
assert.equal(rollback.doc.activeElement.id,'restore-previous');
assert.equal(rollbackCommands.includes('apply-rollback'),false);
clickRollback('Restaurer la version précédente'); await flush();
clickRollback('Restaurer'); await flush();
assert.match(rollback.main.textContent,/installation modifiée/);
assert.ok(rollback.main.find(node=>node.attrs.role==='alert'));
assert.equal(rollback.doc.activeElement.attrs.role,'alert');
rollbackResult={kind:'rollback-prepared',available:false};
clickRollback('Restaurer la version précédente'); await flush();
assert.match(rollback.main.textContent,/Aucune version précédente/);
rollbackResult={kind:'rollback-prepared',available:true}; rollbackApply={kind:'rollback-completed'};
clickRollback('Restaurer la version précédente'); await flush(); clickRollback('Restaurer'); await flush();
assert.match(rollback.main.textContent,/Fermez et rouvrez le lanceur/);
assert.equal(rollback.doc.activeElement.tagName,'H1');
assert.equal(rollback.main.querySelectorAll('button').length,0);
const rejectedClose=environment(async()=>({kind:'rejected',reason:'fermeture refusée'}),false);
const closeDialog=rejectedClose.doc.getElementById('launcher-exit-dialog');
const closeCancel=rejectedClose.doc.getElementById('launcher-exit-cancel');
const closeConfirm=rejectedClose.doc.getElementById('launcher-exit-confirm');
closeDialog.showModal(); closeConfirm.fire('click'); await flush();
assert.match(rejectedClose.doc.getElementById('launcher-exit-error').textContent,/fermeture refusée/);
assert.equal(closeCancel.disabled,false); assert.equal(closeConfirm.disabled,false);
closeCancel.fire('click'); assert.equal(closeDialog.open,false);
let completeLaunch;
const pending=environment(async (_name,{command})=> command.kind==='game-view'
  ? {kind:'game-view',runtime_connected:true,game}
  : new Promise(resolve=>{completeLaunch=resolve;}));
await flush();
pending.main.fire('click',{target:pending.main.querySelectorAll('button')[0]});
pending.location.hash='#aventures'; pending.fireHash();
assert.ok(pending.main.querySelectorAll('button').every(button=>button.disabled));
completeLaunch({kind:'launch-started',process_id:9,launch_id:1}); await flush();
assert.ok(pending.main.querySelectorAll('button').filter(button=>!button.hidden && button.id!=='refresh-game').every(button=>button.disabled));
assert.match(pending.main.textContent,/Lancement demandé/);
let rejectAfterNavigation;
const settingsPending=environment(async (_name,{command})=> command.kind==='game-view'
  ? {kind:'game-view',runtime_connected:true,game:structuredClone(game)}
  : new Promise(resolve=>{rejectAfterNavigation=resolve;}));
await flush();
settingsPending.main.fire('click',{target:settingsPending.main.querySelectorAll('button')[0]});
settingsPending.location.hash='#reglages'; settingsPending.fireHash();
assert.match(settingsPending.main.textContent,/Ouverture du jeu/);
rejectAfterNavigation({kind:'launch-blocked',reason:'sauvegarde modifiée'}); await flush();
assert.match(settingsPending.main.textContent,/sauvegarde modifiée/);
assert.equal(settingsPending.doc.getElementById('standalone-launch-status').attrs.role,'status');
const failedLaunch=environment(async (_name,{command})=>command.kind==='game-view'
  ? {kind:'game-view',runtime_connected:true,game:structuredClone(game)}
  : {kind:'launch-blocked',reason:'installation modifiée'});
await flush(); failedLaunch.main.fire('click',{target:failedLaunch.main.querySelectorAll('button')[0]}); await flush();
assert.match(failedLaunch.main.textContent,/installation modifiée/);
assert.match(failedLaunch.main.textContent,/Actualisez avant de réessayer/);
assert.equal(failedLaunch.main.querySelectorAll('button')[0].disabled,true);
failedLaunch.doc.getElementById('refresh-game').fire('click'); await flush();
assert.equal(failedLaunch.main.querySelectorAll('button')[0].disabled,false);
assert.doesNotMatch(failedLaunch.main.textContent,/Actualisez avant de réessayer/);
const savedGame=structuredClone(game);
savedGame.modules[0].zones[0].resume={status:'ready',saved_hash:saveHash};
const savedRequests=[];
const savedEnv=environment(async (_name,{command})=>{ savedRequests.push(command); return command.kind==='game-view'?{kind:'game-view',runtime_connected:true,game:savedGame}:{kind:'launch-started',process_id:7,launch_id:1}; });
await flush();
const savedButtons=savedEnv.main.querySelectorAll('button');
assert.equal(savedButtons[0].disabled,true); assert.equal(savedButtons[1].disabled,false);
assert.match(savedEnv.main.textContent,/Une sauvegarde existe/);
savedEnv.main.fire('click',{target:savedButtons[1]}); await flush();
assert.equal(savedRequests.at(-1).kind,'request-resume'); assert.equal(savedRequests.at(-1).resume.saved_hash,saveHash);
const pendingGame=structuredClone(savedGame);
pendingGame.modules[0].zones[0].pending_resume=true;
const pendingSave=environment(async()=>({kind:'game-view',runtime_connected:true,game:pendingGame})); await flush();
const pendingButtons=pendingSave.main.querySelectorAll('button');
assert.equal(pendingButtons[0].disabled,true); assert.equal(pendingButtons[1].disabled,false);
assert.match(pendingSave.main.textContent,/reprise est en attente/);

const inaccessibleGame=structuredClone(game);
inaccessibleGame.modules[0].zones[0].pending_resume=null;
inaccessibleGame.modules[0].zones[0].resume={status:'unavailable',reason:'Sauvegardes de cette carte inaccessibles.'};
const inaccessible=environment(async()=>({kind:'game-view',runtime_connected:true,game:inaccessibleGame}));
await flush();
assert.match(inaccessible.main.textContent,/Sauvegardes de cette carte inaccessibles/);
assert.equal(inaccessible.main.querySelectorAll('button')[0].disabled,true);
inaccessible.location.hash='#aventures'; inaccessible.fireHash();
const accessibleSelect=inaccessible.main.find(node=>node.tagName==='SELECT');
accessibleSelect.selectedIndex=1; accessibleSelect.fire('change');
assert.equal(inaccessible.main.querySelectorAll('button').find(button=>button.textContent==='Jouer cette carte').disabled,false);

const failedResume=environment(async (_name,{command})=>command.kind==='game-view'
  ? {kind:'game-view',runtime_connected:true,game:structuredClone(savedGame)}
  : {kind:'launch-blocked',reason:'sauvegarde modifiée'});
await flush(); failedResume.main.fire('click',{target:failedResume.main.querySelectorAll('button')[1]}); await flush();
assert.match(failedResume.main.textContent,/Actualisez avant de réessayer/);
assert.equal(failedResume.main.querySelectorAll('button')[1].disabled,true);
failedResume.doc.getElementById('refresh-game').fire('click'); await flush();
assert.equal(failedResume.main.querySelectorAll('button')[1].disabled,false);
assert.doesNotMatch(failedResume.main.textContent,/Actualisez avant de réessayer/);

let activeEntry=first; const entryRequests=[];
const entryRace=environment(async (_name,{command})=>{
  if(command.kind==='game-view') {
    const current=structuredClone(game); current.entry=structuredClone(activeEntry);
    return {kind:'game-view',runtime_connected:true,game:current};
  }
  entryRequests.push(command);
  return command.selection.area_id===activeEntry.area_id
    ? {kind:'launch-started',process_id:8,launch_id:6}
    : {kind:'launch-blocked',reason:'Le point de départ du jeu a changé. Actualisez avant de jouer.'};
});
await flush(); activeEntry=second;
entryRace.main.fire('click',{target:entryRace.main.querySelectorAll('button')[0]}); await flush();
assert.equal(entryRequests[0].kind,'request-entry-launch'); assert.equal(entryRequests[0].selection.area_id,'area-1');
assert.match(entryRace.main.textContent,/point de départ du jeu a changé/);
entryRace.doc.getElementById('refresh-game').fire('click'); await flush();
entryRace.main.fire('click',{target:entryRace.main.querySelectorAll('button')[0]}); await flush();
assert.equal(entryRequests[1].selection.area_id,'area-2');

let legacyImported=false; let legacyReviewAttempts=0;
const legacyFlow=environment(async (_name,{command})=>{
  if(command.kind==='game-view') return {kind:'game-view',runtime_connected:true,legacy_installation_found:true,
    game:structuredClone(legacyImported?savedGame:game)};
  if(command.kind==='review-legacy-save') {
    assert.equal(command.selection.area_id,first.area_id);
    return ++legacyReviewAttempts===1
      ? {kind:'rejected',reason:'Aucune ancienne sauvegarde pour cette carte.'}
      : {kind:'legacy-save-reviewed',saved_hash:saveHash};
  }
  assert.equal(command.kind,'import-legacy-save');
  assert.equal(command.resume.selection.area_id,first.area_id);
  assert.equal(command.resume.saved_hash,saveHash);
  legacyImported=true;
  return {kind:'legacy-save-imported'};
});
await flush();
legacyFlow.location.hash='#reglages'; legacyFlow.fireHash();
const legacyButton=text=>legacyFlow.main.find(node=>node.tagName==='BUTTON'&&node.textContent===text);
assert.equal(legacyButton('Importer la sauvegarde').hidden,true);
legacyButton('Rechercher une sauvegarde').fire('click'); await flush();
assert.match(legacyFlow.main.textContent,/Aucune ancienne sauvegarde/);
assert.equal(legacyButton('Importer la sauvegarde').hidden,true);
legacyButton('Rechercher une sauvegarde').fire('click'); await flush();
assert.equal(legacyButton('Importer la sauvegarde').hidden,false);
assert.equal(legacyFlow.doc.activeElement,legacyButton('Importer la sauvegarde'));
legacyButton('Importer la sauvegarde').fire('click'); await flush();
assert.match(legacyFlow.main.textContent,/Sauvegarde importée/);
legacyFlow.location.hash='#accueil'; legacyFlow.fireHash();
assert.equal(legacyButton('Reprendre').hidden,false);

let observedViews=0; let polls=0;
const observed=environment(async (_name,{command})=>{
  if(command.kind==='game-view') return {kind:'game-view',runtime_connected:true,game:++observedViews===1?game:savedGame};
  if(command.kind==='game-status') {
    assert.equal(command.launch_id,12);
    return {kind:'game-status',launch_id:12,status:++polls===1?{state:'running'}:{state:'exited',success:false,exit_code:9}};
  }
  return {kind:'launch-started',process_id:77,launch_id:12};
});
await flush(); observed.main.fire('click',{target:observed.main.querySelectorAll('button')[0]}); await flush();
observed.location.hash='#reglages'; observed.fireHash();
observed.runTimer(); await flush();
assert.match(observed.main.textContent,/Lancement demandé/); assert.equal(observedViews,1);
observed.runTimer(); await flush();
assert.match(observed.main.textContent,/arrêté avec une erreur/); assert.equal(observedViews,2);
observed.location.hash='#accueil'; observed.fireHash();
assert.equal(observed.main.querySelectorAll('button')[0].disabled,true);
assert.equal(observed.main.querySelectorAll('button')[1].disabled,false);

let normalViews=0;
const normal=environment(async (_name,{command})=>{
  if(command.kind==='game-view') { normalViews++; return {kind:'game-view',runtime_connected:true,game}; }
  if(command.kind==='game-status') return {kind:'game-status',launch_id:3,status:{state:'exited',success:true,exit_code:0}};
  return {kind:'launch-started',process_id:7,launch_id:3};
});
await flush(); normal.main.fire('click',{target:normal.main.querySelectorAll('button')[0]}); await flush();
normal.runTimer(); await flush(); assert.equal(normalViews,2); assert.match(normal.main.textContent,/programme du jeu s’est arrêté/);

let staleViews=0; let stalePolls=0;
const stale=environment(async (_name,{command})=>{
  if(command.kind==='game-view') { staleViews++; return {kind:'game-view',runtime_connected:true,game}; }
  if(command.kind==='game-status') return ++stalePolls===1
    ? {kind:'game-status',launch_id:999,status:{state:'exited',success:true,exit_code:0}}
    : {kind:'game-status',launch_id:4,status:{state:'exited',success:true,exit_code:0}};
  return {kind:'launch-started',process_id:7,launch_id:4};
});
await flush(); stale.main.fire('click',{target:stale.main.querySelectorAll('button')[0]}); await flush();
stale.runTimer(); await flush(); assert.match(stale.main.textContent,/Impossible de suivre/); assert.equal(staleViews,1);
assert.equal(stale.timers[0].delay,5000);
stale.runTimer(); await flush(); assert.equal(staleViews,2);

let rejectedPolls=0;
const rejectedObservation=environment(async (_name,{command})=>{
  if(command.kind==='game-view') return {kind:'game-view',runtime_connected:true,game};
  if(command.kind==='game-status') return ++rejectedPolls===1
    ? {kind:'rejected',reason:'suivi temporairement indisponible'}
    : {kind:'game-status',launch_id:5,status:{state:'running'}};
  return {kind:'launch-started',process_id:7,launch_id:5};
});
await flush(); rejectedObservation.main.fire('click',{target:rejectedObservation.main.querySelectorAll('button')[0]}); await flush();
rejectedObservation.runTimer(); await flush();
assert.match(rejectedObservation.main.textContent,/suivi temporairement indisponible/);
rejectedObservation.runTimer(); await flush();
assert.match(rejectedObservation.main.textContent,/Lancement demandé/);

const missingTracking=environment(async (_name,{command})=>command.kind==='game-view'
  ? {kind:'game-view',runtime_connected:true,game}
  : {kind:'launch-started',process_id:7});
await flush(); missingTracking.main.fire('click',{target:missingTracking.main.querySelectorAll('button')[0]}); await flush();
assert.match(missingTracking.main.textContent,/Lancement demandé, mais son suivi est indisponible/);
assert.doesNotMatch(missingTracking.main.textContent,/n’a pas pu démarrer/);

const installerCommands=[];
let finishInstall;
const installer=environment(async (_name,{command})=>{
  installerCommands.push(command);
  if(command.kind==='installer-status') return {kind:'installer-ready',title:unsafeText,destination:'C:/Jeux/test'};
  if(command.kind==='prepare-install') return {kind:'install-prepared',destination:command.destination,update:false};
  if(command.kind==='apply-install') return new Promise(resolve=>{finishInstall=resolve;});
  if(command.kind==='launch-installed') return {kind:'installed-launcher-started',process_id:9};
  throw new Error(`unexpected ${command.kind}`);
},true,true);
await flush();
assert.deepEqual(installerCommands.map(command=>command.kind),['installer-status']);
assert.equal(installer.main.find(node=>node.tagName==='H1').textContent,`Installer ${unsafeText}`);
assert.equal(installer.main.find(node=>node.tagName==='H1').innerHTML,'');
const installInput=installer.doc.getElementById('install-destination');
assert.equal(installer.main.find(node=>node.tagName==='LABEL').htmlFor,'install-destination');
installInput.value='C:/Jeux/choisi'; installInput.fire('input');
installer.main.find(node=>node.tagName==='FORM').fire('submit',{preventDefault(){}});
await flush();
assert.equal(installerCommands.at(-1).destination,'C:/Jeux/choisi');
assert.match(installer.main.textContent,/sera installé/);
assert.equal(installer.doc.activeElement.tagName,'H1');
installer.main.querySelectorAll('button').find(node=>node.textContent==='Installer').fire('click');
await flush();
assert.match(installer.main.textContent,/Installation en cours/);
assert.ok(installer.main.querySelectorAll('button').every(node=>node.disabled));
finishInstall({kind:'install-completed',entry:'C:/Jeux/choisi/Jouer.exe'}); await flush();
assert.match(installer.main.textContent,/Installation terminée/);
assert.match(installer.main.textContent,/Jouer\.exe/);
assert.equal(installerCommands.some(command=>command.kind==='launch-installed'),false);
installer.main.querySelectorAll('button').find(node=>node.textContent==='Ouvrir le jeu').fire('click');
await flush();
assert.match(installer.main.textContent,/Ouverture demandée/);
assert.equal(installer.main.querySelectorAll('button')[0].disabled,true);

const invalidInstaller=environment(async()=>({kind:'rejected',reason:'archive modifiée'}),true,true);
await flush();
assert.match(invalidInstaller.main.textContent,/archive modifiée/);
assert.ok(invalidInstaller.main.find(node=>node.attrs.role==='alert'));
assert.equal(invalidInstaller.doc.activeElement.tagName,'H1');

const failedInstall=environment(async (_name,{command})=>{
  if(command.kind==='installer-status') return {kind:'installer-ready',title:'Jeu',destination:'C:/Jeux'};
  if(command.kind==='prepare-install') return {kind:'install-prepared',destination:'C:/Jeux',update:false};
  return {kind:'rejected',reason:'fichiers modifiés'};
},true,true);
await flush();
failedInstall.main.find(node=>node.tagName==='FORM').fire('submit',{preventDefault(){}}); await flush();
failedInstall.main.querySelectorAll('button').find(node=>node.textContent==='Installer').fire('click'); await flush();
assert.match(failedInstall.main.textContent,/fichiers modifiés/);
assert.ok(failedInstall.main.find(node=>node.attrs.role==='alert'));
assert.ok(failedInstall.doc.getElementById('install-destination'));
assert.equal(failedInstall.main.textContent.includes('Installation terminée'),false);

const reopenCommands=[];
const reopen=environment(async (_name,{command})=>{
  reopenCommands.push(command);
  if(command.kind==='installer-status') return {kind:'installer-ready',title:'Jeu',destination:'C:/Jeux'};
  if(command.kind==='prepare-install') return {kind:'install-prepared',destination:'C:/Jeux',update:true,already_installed:true};
  return {kind:'installed-launcher-started',process_id:10};
},true,true);
await flush();
reopen.main.find(node=>node.tagName==='FORM').fire('submit',{preventDefault(){}}); await flush();
assert.match(reopen.main.textContent,/déjà installé/);
reopen.main.querySelectorAll('button').find(node=>node.textContent==='Ouvrir le jeu').fire('click'); await flush();
assert.equal(reopenCommands.some(command=>command.kind==='apply-install'),false);
assert.equal(reopenCommands.at(-1).kind,'launch-installed');

const staleOpenCommands=[];
const staleOpen=environment(async (_name,{command})=>{
  staleOpenCommands.push(command);
  if(command.kind==='installer-status') return {kind:'installer-ready',title:'Jeu',destination:'C:/Jeux'};
  if(command.kind==='prepare-install') return {kind:'install-prepared',destination:'C:/Jeux',update:true,already_installed:true};
  if(command.kind==='reverify-installed') return {kind:'install-revalidated',entry:'C:/Jeux/Jouer.exe'};
  return {kind:'rejected',reason:'installation modifiée ; vérifiez-la à nouveau'};
},true,true);
await flush();
staleOpen.main.find(node=>node.tagName==='FORM').fire('submit',{preventDefault(){}}); await flush();
staleOpen.main.querySelectorAll('button').find(node=>node.textContent==='Ouvrir le jeu').fire('click'); await flush();
assert.match(staleOpen.main.textContent,/installation modifiée/);
assert.ok(staleOpen.main.find(node=>node.attrs.role==='alert'));
assert.equal(staleOpen.main.querySelectorAll('button').find(node=>node.textContent==='Ouvrir le jeu').disabled,true);
assert.equal(staleOpenCommands.filter(command=>command.kind==='launch-installed').length,1);
staleOpen.main.querySelectorAll('button').find(node=>node.textContent==='Vérifier l’installation').fire('click'); await flush();
assert.equal(staleOpenCommands.at(-1).kind,'reverify-installed');
assert.equal(staleOpen.main.querySelectorAll('button').find(node=>node.textContent==='Ouvrir le jeu').disabled,false);
assert.equal(staleOpenCommands.filter(command=>command.kind==='launch-installed').length,1);

const repairCommands=[];
const repair=environment(async (_name,{command})=>{
  repairCommands.push(command);
  if(command.kind==='installer-status') return {kind:'installer-ready',title:'Jeu',destination:'C:/Jeux'};
  if(command.kind==='prepare-install') return {kind:'install-prepared',destination:'C:/Jeux',update:true,already_installed:true,entry:null,entry_error:'fichier étranger préservé'};
  if(command.kind==='ensure-player-entry') return {kind:'player-entry-ready',path:'C:/Jeux/Jouer.exe'};
  throw new Error('unexpected');
},true,true);
await flush();
repair.main.find(node=>node.tagName==='FORM').fire('submit',{preventDefault(){}}); await flush();
assert.match(repair.main.textContent,/fichier étranger préservé/);
assert.match(repair.main.textContent,/lanceur doit être créé ou réparé/);
repair.main.querySelectorAll('button').find(node=>node.textContent==='Créer ou réparer le lanceur').fire('click'); await flush();
assert.match(repair.main.textContent,/C:\/Jeux\/Jouer\.exe/);
assert.equal(repairCommands.some(command=>command.kind==='apply-install'),false);

const uninstallCommands=[];
const uninstall=environment(async (_name,{command})=>{
  uninstallCommands.push(command.kind);
  if(command.kind==='game-view') return {kind:'game-view',runtime_connected:true,game:structuredClone(game)};
  if(command.kind==='start-uninstall') return {kind:'uninstall-helper-started',path:'C:/Jeux/.forge-game-uninstall-1/Desinstaller.exe'};
  if(command.kind==='window') return {kind:'rejected',reason:'fermez manuellement'};
  throw new Error('unexpected');
});
await flush(); uninstall.location.hash='#reglages'; uninstall.fireHash();
const uninstallClick=label=>uninstall.main.querySelectorAll('button').find(node=>node.textContent===label).fire('click');
uninstallClick('Désinstaller');
await flush();
assert.equal(uninstall.doc.activeElement.textContent,'Annuler');
assert.match(uninstall.main.textContent,/sauvegardes seront conservées/);
uninstallClick('Annuler');
assert.equal(uninstall.doc.activeElement.id,'uninstall-game');
assert.equal(uninstallCommands.includes('start-uninstall'),false);
uninstallClick('Désinstaller'); uninstallClick('Ouvrir la désinstallation'); await flush();
assert.deepEqual(uninstallCommands.slice(-2),['start-uninstall','window']);
assert.match(uninstall.main.textContent,/Desinstaller\.exe/);
assert.match(uninstall.main.textContent,/fermez manuellement/);
assert.ok(uninstall.main.find(node=>node.attrs.role==='alert'));

const helperCommands=[];
let attemptUninstall=0;
const helper=environment(async (_name,{command})=>{
  helperCommands.push(command.kind);
  if(command.kind==='installer-status') return {kind:'uninstall-helper-ready',title:unsafeText,path:'C:/Jeux/.forge-game-uninstall-1/Desinstaller.exe'};
  if(command.kind==='apply-uninstall') return ++attemptUninstall===1
    ? {kind:'rejected',reason:'fichier verrouillé'}
    : {kind:'uninstall-completed',path:'C:/Jeux/.forge-game-uninstall-1/Desinstaller.exe',preserved_files:1,preserved_packages:0,counts_partial:true};
  if(command.kind==='cleanup-uninstall-helper') return {kind:'uninstall-cleanup-requested'};
  if(command.kind==='window') return {kind:'window-action-accepted'};
  throw new Error('unexpected');
},true,true);
await flush();
assert.equal(helper.main.find(node=>node.tagName==='H1').textContent,`Désinstaller ${unsafeText}`);
assert.equal(helper.main.find(node=>node.tagName==='H1').innerHTML,'');
assert.match(helper.main.textContent,/En cas d’interruption/);
helper.main.querySelectorAll('button')[0].fire('click'); await flush();
assert.match(helper.main.textContent,/fichier verrouillé/);
assert.equal(helper.doc.activeElement.attrs.role,'alert');
assert.equal(helperCommands.includes('cleanup-uninstall-helper'),false);
helper.main.querySelectorAll('button')[0].fire('click'); await flush();
assert.match(helper.main.textContent,/Jeu désinstallé/);
assert.match(helper.main.textContent,/Certains fichiers ont été conservés/);
assert.match(helper.main.textContent,/bilan des fichiers est partiel/);
assert.match(helper.main.textContent,/sera demandé à la fermeture/);
assert.equal(helperCommands.includes('cleanup-uninstall-helper'),false);
helper.main.querySelectorAll('button')[0].fire('click'); await flush();
assert.deepEqual(helperCommands.slice(-2),['cleanup-uninstall-helper','window']);
const partialHelper=environment(async()=>({kind:'uninstall-completed',path:'C:/Jeux/Desinstaller.exe',preserved_files:0,preserved_packages:0,counts_partial:true}),true,true);
await flush();
assert.match(partialHelper.main.textContent,/bilan des fichiers est partiel/);
const attentionCommands=[];
const attention=environment(async (_name,{command})=>{
  attentionCommands.push(command.kind);
  if(command.kind==='installer-status') return {kind:'uninstall-helper-ready',title:'Jeu',path:'C:/Jeux/Desinstaller.exe'};
  if(command.kind==='apply-uninstall') return {kind:'uninstall-needs-attention',path:'C:/Jeux/Desinstaller.exe',reason:'Le jeu a été retiré, mais son reçu final est indisponible.'};
  if(command.kind==='window') return {kind:'window-action-accepted'};
  throw new Error('unexpected');
},true,true);
await flush(); attention.main.querySelectorAll('button')[0].fire('click'); await flush();
assert.match(attention.main.textContent,/Vérification nécessaire/);
assert.match(attention.main.textContent,/reçu final est indisponible/);
attention.main.querySelectorAll('button')[0].fire('click'); await flush();
assert.equal(attentionCommands.includes('cleanup-uninstall-helper'),false);
console.log('PASS generic launcher contract fixture; no real UI observed');
