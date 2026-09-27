import test from 'node:test';
import assert from 'node:assert/strict';
import { layoutVerdict, secondaryVerdict } from './studio-gold-layout-contract.mjs';
import { fullyVisible, presented } from './studio-gold-visibility.mjs';

test('collapsed details exclude content but retain summary and open content', () => {
  const summary = {tagName:'SUMMARY',contains:e=>e.inSummary};
  const details = {tagName:'DETAILS',open:false,children:[summary],parentElement:null};
  const e = {closest:()=>null,getClientRects:()=>[{}],parentElement:details};
  assert.equal(presented(e),false);
  e.inSummary=true;
  assert.equal(presented(e),true);
  e.inSummary=false;
  details.open=true;
  assert.equal(presented(e),true);
  details.parentElement={tagName:'DETAILS',open:false,children:[],parentElement:null};
  assert.equal(presented(e),false);
});

const good = { tabCount:8, active:['water'], navigationVisible:true, horizontalOverflow:false, canvasClearOfPanel:true, axisHudPresent:true, axisHudClearOfLibrary:true,
  canvas:{width:1358,height:846}, panelFits:true, libraryOpen:false, primaryActions:[{fullyVisible:true}], inspectorFields:[] };
test('an open library cannot clip the cards vertically', () => {
  assert.equal(layoutVerdict({...good,libraryOpen:true,library:{scrollHeight:285,clientHeight:255}}).layoutPassed,false);
  assert.equal(layoutVerdict({...good,libraryOpen:true,library:{scrollHeight:255,clientHeight:255}}).layoutPassed,true);
});
test('a usable baseline passes', () => assert.equal(layoutVerdict(good).layoutPassed, true));
test('a docked panel cannot cover a correctly sized map', () => {
  assert.equal(layoutVerdict({...good,canvasClearOfPanel:false}).layoutPassed,false);
  assert.equal(layoutVerdict({...good,canvasClearOfPanel:undefined}).layoutPassed,false);
});
test('overflow cannot be hidden behind a chrome pass', () => {
  assert.deepEqual(layoutVerdict({...good,panelFits:false}), {chromePassed:true,layoutPassed:false});
});
test('an offscreen main action fails even if panel metrics fit', () => {
  assert.equal(layoutVerdict({...good,primaryActions:[{fullyVisible:false}]}).layoutPassed, false);
});
test('missing observations fail closed', () => {
  assert.equal(layoutVerdict({...good,panelFits:undefined}).layoutPassed, false);
  assert.equal(layoutVerdict({...good,primaryActions:undefined}).layoutPassed, false);
});
test('old tab count, missing navigation and undersized canvas fail', () => {
  for (const delta of [{tabCount:7},{active:[]},{navigationVisible:false},
    {horizontalOverflow:true},{canvas:{width:600,height:846}},{canvas:null}]) {
    assert.equal(layoutVerdict({...good,...delta}).layoutPassed, false);
  }
});

const dual = {...good,dualScreen:true,mapOnly:true,mapModeAction:true,dockHidden:true,panelsHidden:true,viewportShare:0.98,
  navigationVisible:false,canvasClearOfPanel:false,consolePresent:true,compassPresent:true};
test('the XYZ reference cannot be hidden by the asset library', () => {
  assert.equal(layoutVerdict({...good,axisHudClearOfLibrary:false}).layoutPassed,false);
  assert.equal(layoutVerdict({...dual,axisHudClearOfLibrary:false}).layoutPassed,false);
});
test('dual-screen map-only mode retains feedback and recovery action', () => {
  assert.equal(layoutVerdict(dual).layoutPassed,true);
  for (const delta of [{dockHidden:false},{panelsHidden:false},{viewportShare:0.82},{mapModeAction:false},
    {consolePresent:false},{compassPresent:false},{axisHudPresent:false},{primaryActions:[{fullyVisible:false}]}]) {
    assert.equal(layoutVerdict({...dual,...delta}).layoutPassed,false);
  }
});
test('dual-screen mode shows both real side panels by default', () => {
  const withPanels = {...dual,mapOnly:false,panelsHidden:false,viewportShare:0.67,
    canvasClearOfPanel:true,scene:{width:200},topicWindow:{width:310}};
  assert.equal(layoutVerdict(withPanels).layoutPassed,true);
  for (const delta of [{panelsHidden:true},{scene:{width:100}},{topicWindow:{width:150}},
    {canvasClearOfPanel:false},{canvas:{width:500,height:846}}]) {
    assert.equal(layoutVerdict({...withPanels,...delta}).layoutPassed,false);
  }
});

const child = {};
const element = {
  getBoundingClientRect: () => ({x:10,y:10,left:10,top:10,right:110,bottom:50,width:100,height:40}),
  contains: node => node === child,
};
test('visible controls accept their own text child as a hit target', () => {
  assert.equal(fullyVisible(element,1920,1080,()=>child),true);
});
test('a clipped or covered control fails even with valid viewport bounds', () => {
  assert.equal(fullyVisible(element,1920,1080,()=>null),false);
  assert.equal(fullyVisible(element,1920,1080,(x,y)=>x<20&&y<20?{}:element),false);
});
test('offscreen controls fail without invoking hit testing', () => {
  assert.equal(fullyVisible(element,100,1080,()=>{throw new Error('unreachable');}),false);
});

const detached = {surface:'secondary',horizontalOverflow:false,inspectorFits:true,
  mapModeAction:true,
  topicSelection:Array.from({length:8},(_,i)=>({pressed:i===5?'true':'false'})),
  panes:[0,300,600].map(x=>({x,right:x+280,width:280,height:600,visible:true})),
  primaryActions:Array.from({length:9},()=>({fullyVisible:true})), inspectorFields:[]};
test('secondary requires three separated panes and all subject actions',()=>{
  assert.equal(secondaryVerdict(detached).layoutPassed,true);
  for(const delta of [{inspectorFits:false},{horizontalOverflow:true},{mapModeAction:false},{primaryActions:[]},{topicSelection:[]},
    {topicSelection:detached.topicSelection.map(t=>({pressed:t.pressed==='true'?'':null}))},
    {panes:[...detached.panes.slice(0,2),{...detached.panes[2],x:400}]}]) {
    assert.equal(secondaryVerdict({...detached,...delta}).layoutPassed,false);
  }
});

test('inspector fields must be measured and fully visible on both surfaces',()=>{
  for(const [baseline,verdict] of [[good,layoutVerdict],[detached,secondaryVerdict]]) {
    assert.equal(verdict({...baseline,inspectorFields:[{fullyVisible:true}]}).layoutPassed,true);
    for(const inspectorFields of [undefined,[{}],[{fullyVisible:false}]]) {
      assert.equal(verdict({...baseline,inspectorFields}).layoutPassed,false);
    }
  }
});
