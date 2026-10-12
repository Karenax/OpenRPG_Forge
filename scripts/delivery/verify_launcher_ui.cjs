/* Renderer regression on the embedded Aube input; IPC is an explicit fixture.
   Actual native loading and Snapshot IPC are checked separately by --verify-webview. */
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const assert = require('node:assert/strict');
const {chromium} = require(process.env.FORGE_PLAYWRIGHT_MODULE || 'playwright');

(async () => {
  const [frontend, output] = process.argv.slice(2);
  if (!frontend || !output || fs.existsSync(output)) throw Error('Fresh explicit output required');
  fs.mkdirSync(output, {recursive:true});
  const browser = await chromium.launch({headless:true, channel:'msedge'});
  try {
    const page = await browser.newPage({viewport:{width:1600,height:960}, reducedMotion:'reduce'});
    const errors = [];
    page.on('pageerror', error => errors.push(String(error)));
    await page.addInitScript(() => {
      window.fixtureCalls = [];
      window.__TAURI__ = {core:{invoke:async (name, {command}) => {
        window.fixtureCalls.push({name,command});
        if (command.kind === 'snapshot') return {kind:'snapshot',runtime_connected:true};
        if (command.kind === 'request-launch') return {kind:'launch-started',process_id:123};
        return {kind:'window-action-accepted',action:command.action};
      }}};
    });
    const url = pathToFileURL(path.join(frontend,'index.html')).href;
    await page.goto(url);
    await page.locator('.home-library').waitFor();
    assert.match(await page.locator('h1').innerText(), /Chaque monde/);
    await page.screenshot({path:path.join(output,'aube-accueil.png')});
    await page.goto(url+'#guide');
    await page.locator('.codex').waitFor();
    assert.match(await page.locator('#main').innerText(), /Guide du joueur/);
    await page.goto(url+'#bestiaire?q=carillon');
    await page.locator('.codex').waitFor();
    assert.match(await page.locator('#main').innerText(), /Carillon de bronze/);
    await page.screenshot({path:path.join(output,'aube-bestiaire.png')});
    await page.goto(url+'#aventures');
    await page.locator('#launch-tidewater:not([disabled])').waitFor();
    await page.locator('#launch-tidewater').click();
    await page.waitForFunction(() => document.querySelector('#launch-tidewater-status').textContent.includes('ouverte'));
    assert(page && await page.evaluate(() => window.fixtureCalls.some(call => call.command.kind === 'request-launch')));
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(output,'verification.json'), JSON.stringify({
      status:'PASS_AUBE_RENDERER_REGRESSION_WITH_IPC_FIXTURE', homepage:true,
      guide:true,bestiary_search:true,game_launch_bridge:true,
      native_game_spawn_tested:false,human_visual_acceptance:false,errors
    },null,2));
  } finally { await browser.close(); }
})().catch(error => { process.stderr.write(String(error)+'\n'); process.exitCode=1; });
