/* Renderer regression on the embedded Aube input; IPC is an explicit fixture.
   Actual installed inventory, native admission and game spawning remain separate. */
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const assert = require('node:assert/strict');
const {createHash} = require('node:crypto');
const {chromium} = require(process.env.FORGE_PLAYWRIGHT_MODULE || 'playwright');

const cairns = {module_id:'b65b7a97-77d4-58a1-9a0f-3304d680ff2c',area_id:'30e53b52-88ff-50bf-8ded-18283e0a48d6'};
const secondCairnsArea = {...cairns,area_id:'87d92a90-6bac-5011-b8dc-66fb7c27ee2f'};
const relais = {module_id:'dfb318ac-e200-5ca4-9945-04b10906bad9',area_id:'4773745b-e7db-553b-aa9f-4bd7926e0ecf'};
const inventory = {
  kind:'modules',
  modules:[
    {name:'Les Trois Cairns',version:'1.0.0',zones:[cairns,secondCairnsArea]},
    {name:'Relais des lanternes <img id="module-injection" src=x onerror="window.injected=true">',version:'1.0.0',zones:[relais]},
  ],
  rejected:[{pack_id:'invalid-module',reason:'Archive refusée <img id="rejected-injection" src=x onerror="window.injected=true">'}],
};

(async () => {
  const [frontend, output] = process.argv.slice(2);
  if (!frontend || !output || fs.existsSync(output)) throw Error('Fresh explicit output required');
  fs.mkdirSync(output, {recursive:true});
  const browser = await chromium.launch({headless:true, channel:'msedge'});
  try {
    const page = await browser.newPage({viewport:{width:1600,height:960}, reducedMotion:'reduce'});
    const errors = [];
    page.on('pageerror', error => errors.push(String(error)));
    await page.addInitScript(modules => {
      window.fixtureCalls = [];
      window.fixtureInventoryMode = 'ready';
      window.fixtureHoldModuleLaunch = false;
      window.fixtureModuleReply = {kind:'launch-started',process_id:124};
      window.__TAURI__ = {core:{invoke:async (name, {command}) => {
        window.fixtureCalls.push({name,command:structuredClone(command)});
        if (command.kind === 'snapshot') return {kind:'snapshot',runtime_connected:true};
        if (command.kind === 'request-launch') return {kind:'launch-started',process_id:123};
        if (command.kind === 'list-modules') {
          if (window.fixtureInventoryMode === 'empty') return {kind:'modules',modules:[],rejected:[]};
          if (window.fixtureInventoryMode === 'refused') return {kind:'rejected',reason:'Inventaire refusé par la fixture.'};
          return structuredClone(modules);
        }
        if (command.kind === 'request-module-launch') {
          const reply = structuredClone(window.fixtureModuleReply);
          if (window.fixtureHoldModuleLaunch) return new Promise(resolve => {
            window.releaseModuleLaunch = () => resolve(reply);
          });
          return reply;
        }
        return {kind:'window-action-accepted',action:command.action};
      }}};
    }, inventory);
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
    assert(await page.evaluate(() => window.fixtureCalls.some(call => call.command.kind === 'request-launch')));

    await page.waitForFunction(() => document.querySelectorAll('#installed-modules .launch-module').length === 2);
    const items = page.locator('#installed-modules > ul > li');
    assert.equal(await items.count(), 2);
    assert.equal(await items.nth(0).locator('h3').innerText(), inventory.modules[0].name);
    assert.equal(await items.nth(1).locator('h3').innerText(), inventory.modules[1].name);
    await page.locator('#installed-modules details summary').click();
    assert.equal(await page.locator('#installed-modules details li').innerText(),
      `${inventory.rejected[0].pack_id} : ${inventory.rejected[0].reason}`);
    assert.equal(await page.locator('#installed-modules details button').count(), 0);
    assert.equal(await page.locator('#module-injection, #rejected-injection').count(), 0);
    assert.equal(await page.evaluate(() => window.injected), undefined);
    await page.screenshot({path:path.join(output,'aube-modules-installed.png')});

    const first = items.nth(0);
    assert.deepEqual(await first.locator('select option').allTextContents(), ['Carte 1','Carte 2']);
    await first.locator('select').selectOption('1');
    const blockedReason = 'Module indisponible <img id="launch-injection" src=x onerror="window.injected=true">';
    await page.evaluate(reason => {
      window.fixtureHoldModuleLaunch = true;
      window.fixtureModuleReply = {kind:'launch-blocked',code:'unavailable',reason};
    }, blockedReason);
    await first.locator('.launch-module').click();
    await page.waitForFunction(() => typeof window.releaseModuleLaunch === 'function');
    assert(await page.locator('#launch-tidewater').isDisabled());
    assert(await page.locator('.launch-module').evaluateAll(buttons => buttons.every(button => button.disabled)));
    await page.evaluate(() => {
      document.querySelectorAll('.launch-module')[1].click();
      document.querySelector('#launch-tidewater').click();
    });
    assert.equal(await page.evaluate(() => window.fixtureCalls.filter(call => call.command.kind === 'request-module-launch').length), 1);
    assert.equal(await page.evaluate(() => window.fixtureCalls.filter(call => call.command.kind === 'request-launch').length), 1);
    await page.evaluate(() => window.releaseModuleLaunch());
    await page.waitForFunction(reason => [...document.querySelectorAll('#installed-modules [role=status]')].some(status => status.textContent === reason), blockedReason);
    assert.equal(await page.locator('#launch-injection').count(), 0);
    await page.waitForFunction(() => [...document.querySelectorAll('.launch-module, #launch-tidewater')].every(button => !button.disabled));

    await page.evaluate(() => {
      window.fixtureHoldModuleLaunch = false;
      window.fixtureModuleReply = {kind:'launch-started',process_id:124};
    });
    await first.locator('.launch-module').click();
    await page.waitForFunction(() => document.querySelector('#installed-modules > ul > li [role=status]').textContent.includes('est ouvert'));
    await items.nth(1).locator('.launch-module').click();
    await page.waitForFunction(() => document.querySelectorAll('#installed-modules > ul > li [role=status]')[1].textContent.includes('est ouvert'));
    const moduleCalls = await page.evaluate(() => window.fixtureCalls.filter(call => call.command.kind === 'request-module-launch'));
    assert.deepEqual(moduleCalls, [secondCairnsArea,secondCairnsArea,relais].map(selection => ({
      name:'execute_launcher_command',command:{kind:'request-module-launch',selection},
    })));

    await page.evaluate(() => { window.fixtureInventoryMode = 'empty'; });
    await page.locator('#refresh-installed-modules').click();
    await page.waitForFunction(() => document.querySelector('#installed-modules').textContent.includes('Aucun module prêt à jouer dans cette installation.'));
    assert.equal(await page.locator('.launch-module').count(), 0);
    assert(!(await page.locator('#launch-tidewater').isDisabled()));
    await page.evaluate(() => { window.fixtureInventoryMode = 'refused'; });
    await page.locator('#refresh-installed-modules').click();
    await page.waitForFunction(() => document.querySelector('#installed-modules').textContent.includes('Impossible de lire les modules : Error: Inventaire refusé par la fixture.'));
    assert.equal(await page.locator('.launch-module').count(), 0);
    assert(!(await page.locator('#refresh-installed-modules').isDisabled()));
    await page.evaluate(() => { window.fixtureInventoryMode = 'ready'; });
    await page.locator('#refresh-installed-modules').click();
    await page.waitForFunction(() => document.querySelectorAll('.launch-module').length === 2);
    assert.equal(await page.evaluate(() => window.injected), undefined);
    assert.deepEqual(errors, []);
    const fixtureCalls = await page.evaluate(() => window.fixtureCalls);
    fs.writeFileSync(path.join(output,'fixture-calls.json'), JSON.stringify(fixtureCalls,null,2));
    const digest = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
    fs.writeFileSync(path.join(output,'verification.json'), JSON.stringify({
      status:'PASS_AUBE_RENDERER_REGRESSION_WITH_IPC_FIXTURE', homepage:true,
      guide:true,bestiary_search:true,game_launch_bridge:true,
      installed_module_inventory_rendered:true,rejected_module_text_safe:true,
      selected_area_ids_only:true,module_launch_refusal_visible:true,
      concurrent_launch_buttons_disabled:true,module_launch_retry:true,
      empty_inventory_refresh:true,inventory_refusal_visible:true,
      frontend_desktop_sha256:digest(path.join(frontend,'desktop.js')),
      verification_script_sha256:digest(__filename),
      native_game_spawn_tested:false,human_visual_acceptance:false,errors
    },null,2));
  } finally { await browser.close(); }
})().catch(error => { process.stderr.write(String(error)+'\n'); process.exitCode=1; });
