/* Isolated local renderer: validates the presentation adapter with an explicit IPC
   fixture. This is not a native Tauri run or human visual acceptance. */
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const assert = require('node:assert/strict');
const {chromium} = require(process.env.FORGE_PLAYWRIGHT_MODULE || 'playwright');

(async () => {
  const [ui, output] = process.argv.slice(2);
  if (!ui || !output || fs.existsSync(output)) throw new Error('Use explicit source UI and fresh output directory');
  fs.mkdirSync(output, {recursive:true});
  const browser = await chromium.launch({headless:true, channel:'msedge'});
  const page = await browser.newPage({viewport:{width:1100,height:850}, reducedMotion:'reduce'});
  const errors = [];
  page.on('pageerror', error => errors.push(String(error)));
  await page.addInitScript(() => {
    window.fixtureCalls = [];
    let root = 'E:\\Jeux Forge\\Installation Élan';
    let installed = false;
    const packages = [
      {id:'forge-applications',label:window.fixtureTitle || 'Launcher, éditeur et jeu',kind:'applications',version:'preview',bytes:370645707,files:1100},
      {id:'reference-assets',label:'Assets de l’aventure de référence',kind:'assets',version:'preview',bytes:475808350,files:1000},
      {id:'reference-module',label:'Module de référence',kind:'module',version:'preview',bytes:54614767,files:27},
    ];
    const snapshot = {core:{id:'forge-applications',kind:'applications'},packs:{assets:{id:'reference-assets',kind:'assets'},module:{id:'reference-module',kind:'module'}}};
    window.__TAURI__ = {core:{invoke:async (_command,{request}) => {
      window.fixtureCalls.push(request);
      switch (request.action) {
        case 'status': return {root,installed,user_root:'E:\\Profil privé\\Données',packs:packages.map((p,n)=> n===0 ? {...p,label:window.fixtureTitle || p.label} : p),product:{title:window.fixtureTitle || 'OpenRPG Forge'},state:{revision:1,previous:null,active:snapshot}};
        case 'bundled': return {files:['local-fixture.forgepack']};
        case 'prepare': return {plan:{root,command_id:'fixture-command',operation:'install'},packages};
        case 'set_root': root = request.path; installed = false; return {root,plan_invalidated:true};
        case 'select_root': return {root,cancelled:true,plan_invalidated:false};
        case 'apply': installed = true; return {revision:1};
        case 'verify': return {revision:1};
        case 'launch': return {process_id:123};
        default: throw new Error('Unexpected fixture action: '+request.action);
      }
    }}};
  });
  try {
    await page.goto(pathToFileURL(path.join(ui,'index.html')).href);
    await page.waitForFunction(() => document.getElementById('root-path').value.includes('Élan'));
    assert.equal(await page.evaluate(async () => {
      const picture = new Image();
      picture.src = 'assets/aube.png';
      await picture.decode();
      return picture.naturalWidth > 1000;
    }),true,'Original launcher artwork must be included and load correctly');
    assert.equal(await page.locator('#play').isDisabled(), true);
    await page.screenshot({path:path.join(output,'hub-destination.png'),fullPage:true});
    await page.locator('#bundled').click();
    await page.locator('#preview').waitFor({state:'visible'});
    await page.locator('#root-path').fill('E:\\Autre dossier\\Éditeur');
    assert.equal(await page.locator('#preview').isVisible(), false, 'Editing the destination must hide its old plan');
    assert.equal(await page.locator('#apply').isDisabled(), true);
    assert.equal(await page.locator('#bundled').isDisabled(), true, 'A dirty destination must not prepare to the previous path');
    await page.locator('#set-root').click();
    await page.waitForFunction(() => !document.getElementById('bundled').disabled);
    await page.locator('#bundled').click();
    await page.locator('#preview').waitFor({state:'visible'});
    assert.ok((await page.locator('#summary').textContent()).includes('Autre dossier'), 'Review must display the actual selected destination');
    await page.screenshot({path:path.join(output,'hub-review.png'),fullPage:true});
    await page.locator('#apply').click();
    await page.waitForFunction(() => !document.getElementById('play').disabled);
    await page.locator('#play').click();
    await page.waitForFunction(() => window.fixtureCalls.some(call => call.action==='launch'));
    await page.screenshot({path:path.join(output,'hub-installed.png'),fullPage:true});
    await page.setViewportSize({width:900,height:650});
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth),false, 'Minimum native viewport must not scroll horizontally');
    await page.screenshot({path:path.join(output,'hub-minimum-window.png'),fullPage:true});
    await page.locator('#verify').click();
    await page.waitForFunction(() => document.getElementById('subtitle').textContent.includes('Vérification terminée'));
    await page.evaluate(() => { window.fixtureTitle = '<img src=x onerror="window.injected=true">'; });
    await page.locator('#choose-root').click();
    await page.waitForFunction(() => document.getElementById('packs').textContent.includes('<img'));
    assert.equal(await page.locator('#packs img').count(),0,'Package titles must be rendered as text');
    assert.equal(await page.evaluate(() => Boolean(window.injected)),false);
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify({
      schema:'forge.delivery.hub-ui-validation.v1',status:'PASS_LOCAL_MOCK_IPC_RENDERER',
      path_edit_invalidates_plan:true,unicode_path:true,dirty_path_blocks_prepare:true,
      original_artwork_loaded:true,review_displays_selected_destination:true,
      verification_feedback_preserved:true,package_title_rendered_as_text:true,
      apply_and_launch_fixture:true,minimum_viewport_no_horizontal_overflow:true,
      screenshots:['hub-destination.png','hub-review.png','hub-installed.png','hub-minimum-window.png'],
      native_folder_dialog_tested:false,native_tauri_visual_tested:false,human_visual_acceptance:false,
    },null,2)+'\n');
    process.stdout.write('PASS_LOCAL_MOCK_IPC_RENDERER\n');
  } finally {
    await browser.close();
  }
})().catch(error => { process.stderr.write(String(error.stack)+'\n'); process.exitCode=1; });
