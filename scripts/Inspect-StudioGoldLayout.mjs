// Read-only layout evidence for the local Gold candidate. No UI input or commands.
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { layoutVerdict, secondaryVerdict } from './studio-gold-layout-contract.mjs';
import { fullyVisible, presented } from './studio-gold-visibility.mjs';
import { captureDimensions } from './studio-gold-capture.mjs';
const [port = '9237', output, label = 'layout', surface = 'main'] = process.argv.slice(2);
if (process.argv.length > 6) throw new Error('Viewport emulation is not supported by this read-only inspector');
if (!/^\d{4,5}$/.test(port) || !output || !/^[a-z0-9-]+$/.test(label) || !['main','secondary'].includes(surface)) {
  throw new Error('Usage: node Inspect-StudioGoldLayout.mjs <loopback-port> <output-directory> <label> [main|secondary]');
}
const pages = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
const route = surface === 'secondary' ? 'atelier=1' : 'authoring=1';
const targets = pages.filter(p => p.type === 'page' && p.url === `http://tauri.localhost/index.html?${route}`);
if (targets.length !== 1) throw new Error('Expected one exact local Studio page for the requested surface');
const endpoint = new URL(targets[0].webSocketDebuggerUrl);
if (!['localhost', '127.0.0.1'].includes(endpoint.hostname) || endpoint.port !== port) throw new Error('Non-local CDP endpoint refused');
const socket = new WebSocket(endpoint);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let next = 0;
const pending = new Map();
socket.onmessage = ({ data }) => {
  const response = JSON.parse(data);
  const request = pending.get(response.id);
  if (!request) return;
  pending.delete(response.id);
  clearTimeout(request.timeout);
  response.error ? request.reject(new Error(JSON.stringify(response.error))) : request.resolve(response.result);
};
function call(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++next;
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`Timeout: ${method}`)); }, 15000);
    pending.set(id, { resolve, reject, timeout });
    socket.send(JSON.stringify({ id, method, params }));
  });
}
try {
  const themeReply = await call('Runtime.evaluate', {returnByValue:true,expression:`[...document.querySelectorAll('style')].find(e=>e.textContent.includes('.forge-workspaces button'))?.textContent ?? ''`});
  if(themeReply.exceptionDetails) throw new Error('Cannot read rendered theme');
  const theme = themeReply.result.value;
  const sourceTheme = await readFile(new URL('../apps/forge_studio/src/authoring_gold.css',import.meta.url),'utf8');
  const hash = value => createHash('sha256').update(value.replace(/\r\n/g,'\n')).digest('hex');
  const response = await call('Runtime.evaluate', { returnByValue: true, expression: `(() => {
    const box = selector => { const e=document.querySelector(selector); if(!e) return null; const r=e.getBoundingClientRect(); return {x:r.x,y:r.y,width:r.width,height:r.height,bottom:r.bottom,right:r.right,scrollHeight:e.scrollHeight,clientHeight:e.clientHeight}; };
    const tabs=[...document.querySelectorAll('[data-authoring-tab-button]')];
    const active=tabs.filter(t=>t.getAttribute('aria-current')==='page').map(t=>t.dataset.authoringTabButton);
    const panel=box('.authoring-panel-scroll');
    const canvas=box('#forge-authoring-canvas');
    const visible=e=>(${fullyVisible.toString()})(e,innerWidth,innerHeight,(x,y)=>document.elementFromPoint(x,y));
    const presented=${presented.toString()};
    const focused=document.activeElement;
    const focusStyle=focused&&getComputedStyle(focused);
    const focusEvidence=focused?{tag:focused.tagName,label:focused.getAttribute('aria-label')??focused.textContent.slice(0,100),focusVisible:focused.matches(':focus-visible'),outlineColor:focusStyle.outlineColor,outlineWidth:focusStyle.outlineWidth,outlineOffset:focusStyle.outlineOffset,fullyVisible:visible(focused)}:null;
    const inspectorFields=[...document.querySelectorAll('.atelier-transform-inspector input,.atelier-building-inspector input')].filter(presented).map(e=>({label:e.getAttribute('aria-label') ?? e.closest('label')?.textContent,value:e.value,disabled:e.disabled,fullyVisible:visible(e)}));
    const textContrastSamples=[...document.querySelectorAll('.atelier-eyebrow,.forge-project-card > span,.gold-asset-cards small,.atelier-detached-assets small,.atelier-topic-grid small,.forge-workspace-welcome > span,.forge-workspace-welcome p')].filter(presented).map(e=>{
      const style=getComputedStyle(e), backgrounds=[];
      for(let parent=e;parent&&backgrounds.length<4;parent=parent.parentElement){const s=getComputedStyle(parent);if(s.backgroundImage!=='none'||s.backgroundColor!=='rgba(0, 0, 0, 0)')backgrounds.push({tag:parent.tagName,classes:parent.className,background:s.backgroundImage,color:s.backgroundColor});}
      return {text:e.textContent.slice(0,100),color:style.color,fontSize:style.fontSize,opacity:style.opacity,hitTestVisible:visible(e),backgrounds};
    });
    const screenMetrics={width:screen.width,height:screen.height,availWidth:screen.availWidth,availHeight:screen.availHeight,colorDepth:screen.colorDepth};
    if(document.querySelector('.atelier-secondary')) {
      const inspector=box('.atelier-secondary-inspector');
      return {surface:'secondary',resolution:[innerWidth,innerHeight],devicePixelRatio,screenMetrics,inspectorFields,textContrastSamples,focusEvidence,
        mapModeAction:!!document.querySelector('.atelier-map-mode')&&visible(document.querySelector('.atelier-map-mode')),
        topicSelection:[...document.querySelectorAll('.atelier-topic-grid button')].map(e=>({label:e.textContent,pressed:e.getAttribute('aria-pressed')})),
        panes:['.atelier-secondary-scene','.atelier-secondary-tools','.atelier-secondary-inspector'].map(selector=>({...box(selector),visible:visible(document.querySelector(selector))})),
        inspectorFits:!!inspector&&inspector.scrollHeight<=inspector.clientHeight,
        horizontalOverflow:document.documentElement.scrollWidth>innerWidth,
        primaryActions:[...document.querySelectorAll('.atelier-secondary .topbar button,.atelier-topic-grid button,.atelier-transform-inspector fieldset > button')].filter(e=>e.getClientRects().length>0).map(e=>({label:e.textContent,fullyVisible:visible(e)})),
        status:document.querySelector('.atelier-secondary-status')?.textContent};
    }
    const topicWindow=box('.forge-topic-window');
    const dualScreen=document.querySelector('.authoring-shell')?.dataset.dualScreen==='true';
    const mapOnly=document.querySelector('.authoring-shell')?.dataset.mapOnly==='true';
    const docking=document.querySelector('.forge-topic-window')?.dataset.dock;
    const collapsed=document.querySelector('.forge-topic-window')?.dataset.collapsed==='true';
    const canvasClearOfPanel=!!canvas&&!!topicWindow&&(collapsed||docking==='floating'||canvas.bottom<=topicWindow.y+1||canvas.right<=topicWindow.x+1||canvas.x>=topicWindow.right-1);
    const axisHud=box('.forge-axis-hud'), library=box('.gold-library');
    const axisHudClearOfLibrary=!!axisHud&&(!library?.width||axisHud.bottom<=library.y+1||axisHud.y>=library.bottom-1||axisHud.right<=library.x+1||axisHud.x>=library.right-1);
    return {resolution:[innerWidth,innerHeight],devicePixelRatio,screenMetrics,active,tabCount:tabs.length,navigationVisible:tabs.every(visible),panel,canvas,canvasClearOfPanel,docking,dualScreen,mapOnly,
      mapModeAction:!!document.querySelector('.forge-map-mode')&&visible(document.querySelector('.forge-map-mode')),
      dockHidden:!document.querySelector('.authoring-main-tabs')?.getClientRects().length,
      panelsHidden:!document.querySelector('.authoring-scene-tree')?.getClientRects().length&&!document.querySelector('.forge-topic-window')?.getClientRects().length,
      viewportShare:canvas?.width/innerWidth,
      consolePresent:!!box('.forge-event-console')?.height,
      compassPresent:!!box('.forge-compass')?.height,
      axisHudPresent:!!axisHud?.height,axisHudClearOfLibrary,
      libraryGeometry:['.gold-library > summary','.gold-library-filters','.atelier-library-results','.gold-asset-cards','.gold-asset-cards button'].map(selector=>{const e=document.querySelector(selector);if(!e)return {selector};const s=getComputedStyle(e);return {selector,...box(selector),margin:s.margin,padding:s.padding,minHeight:s.minHeight,cssHeight:s.height};}),
      dock:box('.authoring-main-tabs'),topicWindow:box('.forge-topic-window'),
      dockAncestors:(()=>{let e=document.querySelector('.authoring-main-tabs');const a=[];while(e){const s=getComputedStyle(e);a.push({tag:e.tagName,cls:e.className,position:s.position,top:s.top,bottom:s.bottom,overflow:s.overflow,display:s.display});e=e.parentElement;}return a;})(),
      scene:box('.authoring-scene-tree'),library:box('.gold-library'),
      libraryOpen:!!document.querySelector('[data-authoring-tab="objects"]:not([hidden]) .gold-library[open]'),
      horizontalOverflow:document.documentElement.scrollWidth>innerWidth,
      panelFits:panel.scrollHeight<=panel.clientHeight,
      libraryCards:[...document.querySelectorAll('.gold-asset-cards button')].map(e=>({label:e.textContent,fullyVisible:visible(e)})),
      pointEditors:[...document.querySelectorAll('.gold-point-editor')].filter(visible).length,
      primaryActions:[...document.querySelectorAll('[data-primary-action],.authoring-command-bar button,.atelier-object-workspaces button,.atelier-transform-inspector fieldset > button,.atelier-transform-inspector > button,.atelier-building-controls button')].filter(presented).map(e=>({label:e.textContent,fullyVisible:visible(e)})),
      status:document.querySelector('.forge-event-console li')?.textContent,
      pathStatus:document.querySelector('.gold-path-inspector > output')?.textContent,
      inspectorFields,
      textContrastSamples,
      focusEvidence,
      inspectorStatus:[...document.querySelectorAll('.atelier-transform-inspector output,.atelier-transform-inspector [role="alert"]')].filter(e=>e.getClientRects().length>0).map(e=>e.textContent),
      controlStyles:[...document.querySelectorAll('.authoring-primary,.authoring-apply,.authoring-play,.forge-workspaces button[aria-pressed="true"],.authoring-main-tabs button[aria-current="page"],.atelier-object-workspaces button[aria-pressed="true"],.atelier-building-subjects button[aria-pressed="true"]')].filter(e=>!e.closest('[hidden]')&&e.getClientRects().length>0).map(e=>{const s=getComputedStyle(e);return {label:e.textContent,hovered:e.matches(':hover'),disabled:e.disabled,color:s.color,background:s.backgroundImage,opacity:s.opacity,fontSize:s.fontSize};}),
      sourceDiagnostic:document.getElementById('forge-r2-050-projection-diagnostic')?.textContent};
  })()` });
  if (response.exceptionDetails) throw new Error(JSON.stringify(response.exceptionDetails));
  const result = response.result.value;
  result.measurementMode = 'native_window';
  result.themeEvidence = {renderedSha256:hash(theme),sourceSha256:hash(sourceTheme),matchesCurrentSource:!!theme && hash(theme)===hash(sourceTheme)};
  Object.assign(result, surface === 'secondary' ? secondaryVerdict(result) : layoutVerdict(result));
  await mkdir(output, { recursive: true });
  result.captureEvidence = { captured: false, complete: false, mode: 'viewport', errors: [] };
  // Preserve geometry even if an oversized native compositor cannot be captured.
  // Missing pixels must stay explicit; a geometry PASS is not a visual PASS.
  await writeFile(path.join(output, `${label}.json`), JSON.stringify(result, null, 2));
  for (const options of [
    { mode: 'viewport', captureBeyondViewport: false },
    { mode: 'beyond_viewport', captureBeyondViewport: true },
    { mode: 'view', fromSurface: false },
  ]) {
    try {
      const { mode, ...params } = options;
      const screenshot = await call('Page.captureScreenshot', { format: 'png', ...params });
      const png = Buffer.from(screenshot.data, 'base64');
      const dimensions = captureDimensions(png, result.resolution, result.devicePixelRatio);
      await writeFile(path.join(output, `${label}.png`), png);
      result.captureEvidence.captured = true;
      result.captureEvidence.mode = mode;
      Object.assign(result.captureEvidence, dimensions);
      if (dimensions.complete) break;
      result.captureEvidence.errors.push(`Partial bitmap: ${dimensions.width}x${dimensions.height}`);
    } catch (error) {
      result.captureEvidence.errors.push(String(error));
    }
  }
  await writeFile(path.join(output, `${label}.json`), JSON.stringify(result, null, 2));
  console.log(JSON.stringify({ ...result, sourceDiagnostic: undefined }, null, 2));
  if (!result.layoutPassed || !result.captureEvidence.complete) process.exitCode = 1;
} finally { socket.close(); }
