const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const shim = fs.readFileSync(
  path.join(__dirname, "..", "tidewater-webgpu-compat.js"),
  "utf8",
);
const directive = "diagnostic(off, derivative_uniformity);\n";
// Markers and the divergent derivative call observed in the composed WGSL.
const contactSurface = `
var<storage> renderContactFrame: RenderContactFrame;
fn stallSurface(in: StallInput, frame: StallFrame, mat: StallMaterialParameters) -> Surface {
  return surface;
}
fn hookContactShadow(P: vec3<f32>, N: vec3<f32>) -> f32 {
  let slope = max(abs(dpdx(P.z)), abs(dpdy(P.z)));
  return slope;
}
fn shadeSurface(s: Surface, P: vec3<f32>, N: vec3<f32>) -> f32 {
  if s.normal.y > 0.0 { return hookContactShadow(P, N); }
  return 0.0;
}
`;

function fixture() {
  const submitted = [];
  class GPUDevice {
    createShaderModule(descriptor) {
      submitted.push({ device: this, descriptor });
      return { descriptor };
    }
  }
  const context = vm.createContext({ GPUDevice });
  vm.runInContext(shim, context);
  return { GPUDevice, context, submitted };
}

test("Tidewater terrain and plant shaders receive the directive", () => {
  const { GPUDevice, submitted } = fixture();
  const device = new GPUDevice();
  const sources = [
    "fn terDetail(uv: vec2<f32>) { textureSample(terrainDetailTex, smpAniso4Repeat, uv); }",
    "fn vegImpSample(uv: vec2<f32>) { textureSample(vegImpA, smpLinearClamp, uv); textureSample(vegImpB, smpLinearClamp, uv); }",
  ];
  for (const code of sources) {
    const descriptor = { code, label: "imported Tidewater", compilationHints: [{ layout: "auto" }] };
    const module = device.createShaderModule(descriptor);
    const received = submitted.at(-1);
    assert.strictEqual(received.device, device);
    assert.notStrictEqual(received.descriptor, descriptor);
    assert.equal(received.descriptor.code, directive + code);
    assert.equal(received.descriptor.label, descriptor.label);
    assert.strictEqual(received.descriptor.compilationHints, descriptor.compilationHints);
    assert.strictEqual(descriptor.code, code);
    assert.strictEqual(module.descriptor, received.descriptor);
  }
});

test("unrelated shaders, existing directive, and repeat installation stay unchanged", () => {
  const { GPUDevice, context, submitted } = fixture();
  const device = new GPUDevice();
  const wrapped = GPUDevice.prototype.createShaderModule;
  vm.runInContext(shim, context);
  assert.strictEqual(GPUDevice.prototype.createShaderModule, wrapped);

  const unrelated = { code: "fn other() { textureSample(otherTex, smp, uv); }", label: "other" };
  device.createShaderModule(unrelated);
  assert.strictEqual(submitted.at(-1).descriptor, unrelated);

  const alreadyPatched = {
    code: "diagnostic( off, derivative_uniformity );\nfn terDetail(uv: vec2<f32>) { terrainDetailTex; smpAniso4Repeat; }",
  };
  device.createShaderModule(alreadyPatched);
  assert.strictEqual(submitted.at(-1).descriptor, alreadyPatched);

  const nonString = { code: 123, label: "invalid upstream descriptor" };
  device.createShaderModule(nonString);
  assert.strictEqual(submitted.at(-1).descriptor, nonString);
});

test("shared surface contact derivatives receive the source directive without changing WGSL", () => {
  const { GPUDevice, submitted } = fixture();
  const device = new GPUDevice();
  const descriptor = {
    code: contactSurface,
    label: "common full surface",
    compilationHints: [{ layout: "auto" }],
  };
  const module = device.createShaderModule(descriptor);
  const received = submitted.at(-1);
  assert.strictEqual(received.device, device);
  assert.equal(received.descriptor.code, directive + contactSurface);
  assert.equal(received.descriptor.label, descriptor.label);
  assert.strictEqual(received.descriptor.compilationHints, descriptor.compilationHints);
  assert.strictEqual(module.descriptor, received.descriptor);
  assert.equal(descriptor.code, contactSurface);
});

test("shared contact recognition requires the complete assembled material", () => {
  const { GPUDevice, submitted } = fixture();
  const device = new GPUDevice();
  for (const [marker, replacement] of [
    ["fn stallSurface(", "fn otherSurface("],
    ["fn hookContactShadow(", "fn otherShadow("],
    ["renderContactFrame", "otherContactFrame"],
  ]) {
    const descriptor = { code: contactSurface.replace(marker, replacement) };
    device.createShaderModule(descriptor);
    assert.strictEqual(submitted.at(-1).descriptor, descriptor, marker);
  }
});

test("shared contact directive and repeated shim installation are idempotent", () => {
  const { GPUDevice, context, submitted } = fixture();
  const device = new GPUDevice();
  const wrapped = GPUDevice.prototype.createShaderModule;
  vm.runInContext(shim, context);
  assert.strictEqual(GPUDevice.prototype.createShaderModule, wrapped);
  const descriptor = { code: "diagnostic( off, derivative_uniformity );\n" + contactSurface };
  device.createShaderModule(descriptor);
  assert.strictEqual(submitted.at(-1).descriptor, descriptor);
  assert.equal(submitted.length, 1);
});

test("page script is safe when WebGPU is unavailable", () => {
  assert.doesNotThrow(() => vm.runInNewContext(shim, {}));
});
