// Package the verified Tidewater boat shader capture for a generic Rust loader.
// The acquisition remains the source of truth; this helper only validates and
// reshapes its recorded bytes. It never edits the source checkout or the input
// acquisition directory.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';

const COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const CAPTURE_SCHEMA = 'forge.tidewater.boat-shader-capture';
const PACKAGE_SCHEMA = 'forge.source-mesh-shaders';
const SOURCE_INPUT_COUNT = 344;
const TOPOLOGIES = new Set(['point-list', 'line-list', 'triangle-list']);
const CULL_MODES = new Set(['none', 'front', 'back']);
const DEPTH_COMPARES = new Set(['greater-equal', 'less-equal', 'always']);
const PROGRAM_FIELDS = [
  'material', 'pass', 'defines', 'has_fragment', 'modules', 'group0',
  'group1', 'draw_binding', 'code',
];

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const jsonBytes = value => Buffer.from(JSON.stringify(value, null, 2) + '\n', 'utf8');
const ascii = (left, right) => left < right ? -1 : left > right ? 1 : 0;
const sorted = values => [...values].sort(ascii);

async function readJson(file) {
  return JSON.parse((await fs.readFile(file)).toString('utf8'));
}

async function mustRead(file) {
  try {
    return await fs.readFile(file);
  } catch (error) {
    throw new Error(`Missing required file: ${file}: ${error.message}`);
  }
}

async function mustBeAbsent(file) {
  try {
    await fs.lstat(file);
    throw new Error(`Refusing to overwrite existing path: ${file}`);
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
}

function git(upstream, ...args) {
  return execFileSync('git', ['-C', upstream, ...args], {
    encoding: 'utf8', windowsHide: true,
  }).trim();
}

function relativePath(root, target) {
  const relative = path.relative(root, target);
  assert(relative && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative),
    `Expected a path inside ${root}: ${target}`);
  return relative;
}

function outsidePath(root, target) {
  const relative = path.relative(root, target);
  return relative && (relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative));
}

function safeRelative(relative) {
  const normalized = relative.replaceAll('\\', '/');
  assert(normalized && !normalized.startsWith('/') && !normalized.split('/').includes('..'),
    `Unsafe relative path: ${relative}`);
  return normalized;
}

function isI32(value) {
  return Number.isInteger(value) && value >= -2147483648 && value <= 2147483647;
}

function validateV2RasterContracts(capture) {
  if (capture.version !== 2) return;
  for (const material of capture.materials) {
    const raster = material.raster;
    assert(raster && typeof raster === 'object', `Raster contract missing for ${material.name}`);
    assert(TOPOLOGIES.has(raster.topology), `Invalid raster topology for ${material.name}`);
    assert(typeof raster.colorWrite === 'boolean', `Invalid raster colorWrite for ${material.name}`);
    assert(raster.depthCompare === null || DEPTH_COMPARES.has(raster.depthCompare), `Invalid raster depthCompare for ${material.name}`);
    assert(isI32(raster.depthBias), `Invalid raster depthBias for ${material.name}`);
    assert(typeof raster.depthBiasSlopeScale === 'number' && Number.isFinite(raster.depthBiasSlopeScale), `Invalid raster depthBiasSlopeScale for ${material.name}`);
  }
  for (const shader of capture.shaders) {
    const overrides = shader.pass?.rasterOverrides;
    const variant = `${shader.material}/${shader.pass?.kind}/${shader.pass?.late ? 'late' : 'early'}`;
    assert(overrides && typeof overrides === 'object', `Raster overrides missing for ${variant}`);
    assert(overrides.cullMode === null || CULL_MODES.has(overrides.cullMode), `Invalid raster cullMode for ${variant}`);
    assert(isI32(overrides.depthBias), `Invalid raster override depthBias for ${variant}`);
    assert(typeof overrides.depthBiasSlopeScale === 'number' && Number.isFinite(overrides.depthBiasSlopeScale), `Invalid raster override depthBiasSlopeScale for ${variant}`);
  }
}

function reference(files, file) {
  const bytes = files.get(file);
  assert(bytes, `Missing package file ${file}`);
  return { file, sha256: sha256(bytes), bytes: bytes.length };
}

async function validateSource(acquisition, upstream) {
  const capturePath = path.join(acquisition, 'capture.json');
  const captureReceiptPath = path.join(acquisition, 'capture.json.receipt.json');
  const sourceInputsPath = path.join(acquisition, 'source-inputs.json');
  const captureBytes = await mustRead(capturePath);
  const captureReceiptBytes = await mustRead(captureReceiptPath);
  const sourceInputsBytes = await mustRead(sourceInputsPath);
  const capture = JSON.parse(captureBytes.toString('utf8'));
  const captureReceipt = JSON.parse(captureReceiptBytes.toString('utf8'));
  const sourceInputs = JSON.parse(sourceInputsBytes.toString('utf8'));

  assert.equal(git(upstream, 'rev-parse', 'HEAD'), COMMIT, 'Unexpected source revision');
  assert.equal(git(upstream, 'status', '--porcelain=v1', '--untracked-files=normal'), '',
    'Source checkout must be clean');

  assert.equal(capture.schema, CAPTURE_SCHEMA, 'Unexpected capture schema');
  assert([1, 2].includes(capture.version), 'Unexpected capture version');
  assert.equal(capture.status, 'complete', 'Capture is not complete');
  assert.equal(capture.source_app_started, false, 'Capture started the source app loop');
  assert.equal(capture.gpu_apis_replaced, false, 'Capture replaced a GPU API');
  assert(Array.isArray(capture.errors) && capture.errors.length === 0, 'Capture has errors');
  assert(Array.isArray(capture.materials) && capture.materials.length === 7,
    'Expected seven captured materials');
  assert(Array.isArray(capture.shaders) && capture.shaders.length === 20,
    'Expected twenty captured shader programs');
  assert(Array.isArray(capture.hooks) && capture.hooks.length === 8,
    'Expected eight captured hooks');

  assert.equal(captureReceipt.commit, COMMIT, 'Capture receipt source pin differs');
  assert.equal(captureReceipt.bytes, captureBytes.length, 'Capture receipt byte count differs');
  assert.equal(captureReceipt.sha256, sha256(captureBytes), 'Capture receipt hash differs');
  assert.equal(captureReceipt.source_inputs_sha256, sha256(sourceInputsBytes),
    'Capture receipt input hash differs');
  assert.equal(captureReceipt.original_source_modified, false,
    'Acquisition reports source modification');
  assert.equal(captureReceipt.gpu_apis_replaced, false,
    'Acquisition reports a replaced GPU API');

  assert.equal(sourceInputs.commit, COMMIT, 'Source-input pin differs');
  assert(Array.isArray(sourceInputs.files), 'Source-input list is missing');
  assert.equal(sourceInputs.files.length, SOURCE_INPUT_COUNT,
    `Expected ${SOURCE_INPUT_COUNT} source inputs`);
  const seen = new Set();
  for (const row of sourceInputs.files) {
    const relative = safeRelative(row.path);
    assert(!seen.has(relative), `Duplicate source input: ${relative}`);
    seen.add(relative);
    assert.match(row.sha256, /^[0-9a-f]{64}$/, `Invalid source hash: ${relative}`);
    assert(Number.isInteger(row.bytes) && row.bytes >= 0, `Invalid source size: ${relative}`);
    const sourcePath = path.join(upstream, relative);
    const stat = await fs.lstat(sourcePath);
    assert(stat.isFile() && !stat.isSymbolicLink(), `Source input is not a regular file: ${relative}`);
    const bytes = await fs.readFile(sourcePath);
    assert.equal(bytes.length, row.bytes, `Source size drift: ${relative}`);
    assert.equal(sha256(bytes), row.sha256, `Source hash drift: ${relative}`);
  }

  const shaderHashes = new Set();
  for (const shader of capture.shaders) {
    assert.equal(typeof shader.material, 'string');
    assert.equal(typeof shader.pass, 'object');
    assert.equal(typeof shader.wgsl, 'string');
    assert.match(shader.sha256, /^[0-9a-f]{64}$/, 'Invalid shader hash');
    assert(!shaderHashes.has(shader.sha256), `Duplicate shader hash: ${shader.sha256}`);
    shaderHashes.add(shader.sha256);
    assert.equal(sha256(Buffer.from(shader.wgsl, 'utf8')), shader.sha256,
      `WGSL hash drift for ${shader.material}/${shader.pass.kind}`);
    assert(Array.isArray(shader.compilation_messages),
      `Compilation messages missing for ${shader.material}/${shader.pass.kind}`);
    assert(!shader.compilation_messages.some(message => message.type === 'error'),
      `WGSL compilation error for ${shader.material}/${shader.pass.kind}`);
    assert.equal(shader.validation_error, null,
      `Pipeline validation error for ${shader.material}/${shader.pass.kind}`);
    if (capture.version === 2) {
      const variant = `${shader.material}/${shader.pass.kind}/${shader.pass.late ? 'late' : 'early'}`;
      assert.equal(shader.pipeline_ready, true, `Pipeline readiness missing for ${variant}`);
      assert.equal(shader.pipeline_failed, false, `Pipeline async failure recorded for ${variant}`);
      assert.equal(shader.error ?? null, null, `Variant error recorded for ${variant}`);
      assert.equal(shader.scope_pop_error ?? null, null, `Error-scope pop failure recorded for ${variant}`);
      assert(shader.pipeline_state && typeof shader.pipeline_state === 'object', `Pipeline state missing for ${variant}`);
    }
  }
  validateV2RasterContracts(capture);

  const license = await mustRead(path.join(upstream, 'LICENSE'));
  assert(license.includes('MIT License') && license.includes('Permission is hereby granted'),
    'Upstream license is not the expected MIT notice');
  const credits = await mustRead(path.join(upstream, 'CREDITS.md'));

  return {
    capture,
    captureBytes,
    captureReceiptBytes,
    sourceInputsBytes,
    sourceInputs,
    license,
    credits,
    captureSha256: sha256(captureBytes),
  };
}

function parseArguments(argv) {
  const check = argv.includes('--check');
  const positional = argv.filter(argument => argument !== '--check');
  assert.equal(positional.length, 3,
    'Usage: node Package-BoatShaders.mjs [--check] ACQUISITION_DIR OUTPUT_DIR UPSTREAM_DIR');
  return {
    check,
    acquisition: path.resolve(positional[0]),
    output: path.resolve(positional[1]),
    upstream: path.resolve(positional[2]),
  };
}

async function preflight(paths) {
  const acquisition = await fs.realpath(paths.acquisition);
  const upstream = await fs.realpath(paths.upstream);
  assert(outsidePath(upstream, paths.output), 'Output must be outside the source checkout');
  assert(outsidePath(acquisition, paths.output), 'Output must be outside the acquisition directory');
  const outputParent = path.dirname(paths.output);
  const parentStat = await fs.lstat(outputParent);
  assert(parentStat.isDirectory(), `Output parent is not a directory: ${outputParent}`);
  await mustBeAbsent(paths.output);
  await mustBeAbsent(`${paths.output}.partial`);
  const validated = await validateSource(acquisition, upstream);
  return { ...paths, acquisition, upstream, validated };
}

async function writePackage(paths) {
  const { capture, captureBytes, captureReceiptBytes, sourceInputsBytes, sourceInputs,
    license, credits, captureSha256 } = paths.validated;
  const product = new Map();
  const evidence = new Map([
    ['proofs/capture.json', captureBytes],
    ['proofs/capture-receipt.json', captureReceiptBytes],
    ['proofs/source-inputs.json', sourceInputsBytes],
    ['LICENSE-Tidewater.txt', license],
    ['CREDITS-Tidewater.md', credits],
  ]);

  for (const shader of capture.shaders) {
    product.set(`shaders/${shader.sha256}.wgsl`, Buffer.from(shader.wgsl, 'utf8'));
  }

  const programs = capture.shaders.map(shader => ({
    material: shader.material,
    pass: shader.pass,
    defines: shader.defines,
    has_fragment: shader.has_fragment,
    modules: shader.modules,
    group0: shader.group0,
    group1: shader.group1,
    draw_binding: shader.draw_binding,
    code: reference(product, `shaders/${shader.sha256}.wgsl`),
    ...(capture.version === 2 ? { pipeline_state: shader.pipeline_state } : {}),
  }));
  for (const program of programs) {
    assert.deepEqual(Object.keys(program), capture.version === 2 ? [...PROGRAM_FIELDS, 'pipeline_state'] : PROGRAM_FIELDS,
      'Program contains a field outside the source-shader contract');
  }

  const manifest = {
    schema: PACKAGE_SCHEMA,
    version: capture.version,
    source_commit: COMMIT,
    provenance: {
      capture: reference(evidence, 'proofs/capture.json'),
      receipt: reference(evidence, 'proofs/capture-receipt.json'),
      inputs: reference(evidence, 'proofs/source-inputs.json'),
      license: reference(evidence, 'LICENSE-Tidewater.txt'),
      credits: reference(evidence, 'CREDITS-Tidewater.md'),
    },
    frame: capture.frame,
    materials: capture.materials,
    hooks: capture.hooks,
    files: Object.fromEntries(sorted([...evidence.keys(), ...product.keys()]).map(file => {
      const bytes = evidence.get(file) ?? product.get(file);
      assert(bytes, `Missing payload for ${file}`);
      return [file, { sha256: sha256(bytes), bytes: bytes.length }];
    })),
    programs,
  };
  const manifestBytes = jsonBytes(manifest);
  const helperBytes = await fs.readFile(new URL(import.meta.url));
  const receipt = {
    schema: `${PACKAGE_SCHEMA}.receipt`,
    version: 1,
    source_commit: COMMIT,
    manifest_sha256: sha256(manifestBytes),
    helper_sha256: sha256(helperBytes),
    capture_sha256: captureSha256,
    source_input_count: sourceInputs.files.length,
    source_modified: false,
    qualifier: 'source-only-no-Rust-draw',
  };
  const receiptBytes = jsonBytes(receipt);
  const summary = {
    schema: `${PACKAGE_SCHEMA}.summary`,
    version: 1,
    manifest_schema: PACKAGE_SCHEMA,
    manifest_version: capture.version,
    source_commit: COMMIT,
    source_input_count: sourceInputs.files.length,
    counts: { materials: capture.materials.length, shaders: capture.shaders.length, hooks: capture.hooks.length },
    runtime_files: sorted(product.keys()),
    evidence_files: sorted(evidence.keys()),
    inline_fields: ['frame', 'materials', 'hooks'],
    program_fields: PROGRAM_FIELDS,
    provenance_fields: ['capture', 'receipt', 'inputs', 'license', 'credits'],
    qualifier: 'source-only-no-Rust-draw',
    manifest_sha256: sha256(manifestBytes),
  };
  const summaryBytes = jsonBytes(summary);

  const files = new Map([
    ...evidence,
    ...product,
    ['manifest.json', manifestBytes],
    ['package-receipt.json', receiptBytes],
    ['schema-summary.json', summaryBytes],
  ]);
  const staging = `${paths.output}.partial`;
  await fs.mkdir(staging, { recursive: false });
  for (const file of sorted(files.keys())) {
    const destination = path.join(staging, file);
    await fs.mkdir(path.dirname(destination), { recursive: true });
    await fs.writeFile(destination, files.get(file), { flag: 'wx' });
  }
  await mustBeAbsent(paths.output);
  await fs.rename(staging, paths.output);
  console.log(JSON.stringify({
    status: 'source-mesh-shaders-packaged',
    output: paths.output,
    source_commit: COMMIT,
    source_input_count: sourceInputs.files.length,
    materials: capture.materials.length,
    shaders: capture.shaders.length,
    hooks: capture.hooks.length,
    runtime_files: product.size,
    manifest_sha256: receipt.manifest_sha256,
    helper_sha256: receipt.helper_sha256,
    capture_sha256: receipt.capture_sha256,
    source_modified: false,
    qualifier: receipt.qualifier,
  }, null, 2));
}

try {
  const paths = parseArguments(process.argv.slice(2));
  const prepared = await preflight(paths);
  const { capture, sourceInputs } = prepared.validated;
  if (paths.check) {
    console.log(JSON.stringify({
      status: 'preflight-ok',
      source_commit: COMMIT,
      source_input_count: sourceInputs.files.length,
      materials: capture.materials.length,
      shaders: capture.shaders.length,
      hooks: capture.hooks.length,
      output_absent: true,
      source_modified: false,
    }, null, 2));
  } else {
    await writePackage(prepared);
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
