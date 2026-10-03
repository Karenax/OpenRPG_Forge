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

test("page script is safe when WebGPU is unavailable", () => {
  assert.doesNotThrow(() => vm.runInNewContext(shim, {}));
});
