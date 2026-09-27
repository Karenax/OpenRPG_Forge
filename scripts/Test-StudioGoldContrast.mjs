// Source palette regression checks, not a substitute for computed-style/visual QA.
import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const css = await readFile(new URL('../apps/forge_studio/src/authoring_gold.css', import.meta.url), 'utf8');
const rules = [...css.matchAll(/([^{}]+)\{([^{}]+)\}/g)];
function declarations(selector) {
  const matches = rules.filter(([,selectors]) => selectors.trim() === selector || selectors.split(',').some(s => s.trim() === selector));
  assert.ok(matches.length, `Missing explicit state: ${selector}`);
  // Later responsive rules may change only padding, not the background/color.
  const properties = new Map();
  for (const [, , body] of matches) for (const declaration of body.split(';')) {
    const colon = declaration.indexOf(':');
    if (colon > 0) properties.set(declaration.slice(0,colon).trim(),declaration.slice(colon+1).trim());
  }
  return [...properties].map(([name,value])=>`${name}:${value}`).join(';');
}

for (const [textSelector, surfaceSelector] of [
  ['.atelier-eyebrow', '.atelier-secondary-body > :is(aside,section)'],
  ['.forge-project-card > span', '.forge-project-card'],
  ['.gold-asset-cards small', '.authoring-tools .gold-asset-cards button'],
  ['.gold-asset-cards small', '.authoring-tools .gold-asset-cards button[aria-pressed="true"]'],
  ['.atelier-detached-assets small', '.authoring-shell button'],
  ['.atelier-topic-grid small', '.authoring-shell button'],
  ['.atelier-secondary-body button[aria-pressed="true"] small', '.atelier-secondary-body button[aria-pressed="true"]'],
]) {
  test(`secondary text >= 4.5:1: ${textSelector} on ${surfaceSelector}`, () => {
    const foreground=declarations(textSelector).match(/(?:^|;)\s*color\s*:\s*(#[a-f0-9]+)/i)?.[1];
    const gradient=declarations(surfaceSelector).match(/(?:^|;)\s*background\s*:\s*linear-gradient\(([^)]+)\)/)?.[1];
    const stops=gradient?.match(/#[a-f0-9]+/gi);
    assert.ok(foreground && stops?.length===2);
    for(const stop of stops) assert.ok(contrast(foreground,stop)>=4.5, `${foreground} on ${stop}: ${contrast(foreground,stop).toFixed(2)}`);
  });
}

for (const selector of ['.forge-workspace-welcome > span', '.forge-workspace-welcome p']) {
  test(`welcome text >= 4.5:1 on opaque canvas base: ${selector}`, () => {
    assert.match(declarations('.authoring-canvas-wrap'), /#d9d7ca/);
    const foreground=declarations(selector).match(/(?:^|;)\s*color\s*:\s*(#[a-f0-9]+)/i)?.[1];
    assert.ok(foreground);
    assert.ok(contrast(foreground,'#d9d7ca')>=4.5);
  });
}
function luminance(hex) {
  let value = hex.slice(1);
  if(value.length === 3) value = [...value].map(c=>c+c).join('');
  assert.equal(value.length, 6, 'Only opaque RGB colors are covered');
  const rgb = [0,2,4].map(i=>parseInt(value.slice(i,i+2),16)/255)
    .map(c=>c<=0.04045?c/12.92:((c+0.055)/1.055)**2.4);
  return rgb[0]*0.2126+rgb[1]*0.7152+rgb[2]*0.0722;
}
function contrast(a,b) {
  const x=luminance(a), y=luminance(b);
  return (Math.max(x,y)+0.05)/(Math.min(x,y)+0.05);
}

test('dock keyboard focus contrasts with both dark dock endpoints',()=>{
  const foreground=declarations('.authoring-tools .authoring-main-tabs button:focus-visible').match(/outline-color\s*:\s*(#[a-f0-9]+)/i)?.[1];
  assert.ok(foreground);
  // Include a lighter bound for the translucent dock over its ivory workspace.
  for(const backdrop of ['#49483f','#282e2b','#66645b']) {
    assert.ok(contrast(foreground,backdrop)>=3, `${foreground} on ${backdrop}`);
  }
});
for(const selector of [
  '.authoring-shell .authoring-primary',
  '.authoring-shell .authoring-primary:hover:not(:disabled)',
  '.authoring-shell .authoring-apply:hover:not(:disabled)',
  '.authoring-shell .forge-workspaces button[aria-pressed="true"]',
  '.authoring-shell .forge-workspaces button[aria-pressed="true"]:hover:not(:disabled)',
  '.authoring-play-actions .authoring-play:not(:disabled)',
  '.authoring-shell .authoring-play-actions .authoring-play:hover:not(:disabled)',
  '.authoring-tools .authoring-main-tabs button[aria-current="page"]',
  '.authoring-tools .authoring-main-tabs button[aria-current="page"]:hover:not(:disabled)',
  '.authoring-tools .atelier-object-workspaces button[aria-pressed="true"]',
  '.authoring-tools .atelier-building-subjects button[aria-pressed="true"]',
  '.authoring-tools .atelier-building-subjects button[aria-pressed="true"]:hover:not(:disabled)',
]) {
  test(`opaque gradient text >= 4.5:1: ${selector}`,()=>{
    const rule=declarations(selector);
    const foreground=rule.match(/(?:^|;)\s*color\s*:\s*(#[a-f0-9]+)/i)?.[1];
    const stops=rule.match(/(?:^|;)\s*background\s*:\s*linear-gradient\(([^)]+)\)/)?.[1].match(/#[a-f0-9]+/gi);
    assert.ok(foreground && stops?.length===2, 'Expected an explicit opaque two-stop gradient');
    for(const stop of stops) assert.ok(contrast(foreground,stop)>=4.5, `${foreground} on ${stop}: ${contrast(foreground,stop).toFixed(2)}`);
  });
}
