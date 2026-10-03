// Bevy's browser shader path reserializes naga_oil WGSL and drops Tidewater's
// module-level derivative_uniformity directive. Restore it for the imported
// Tidewater materials just before WebGPU compiles the composed shader.
// Bevy 0.19's motion-blur shader also uses implicit-LOD textureSample inside
// a divergent loop. Its input textures have no mip chain, so explicit LOD 0
// preserves the intended sample while satisfying WebGPU validation.
(() => {
  const devicePrototype = globalThis.GPUDevice?.prototype;
  if (!devicePrototype || typeof devicePrototype.createShaderModule !== "function") return;

  const installed = Symbol.for("org.openrpg.forge.tidewater-webgpu-compat.v2");
  const original = devicePrototype.createShaderModule;
  if (original[installed]) return;

  const directive = "diagnostic(off, derivative_uniformity);\n";
  const existingDirective = /(?:^|\n)\s*diagnostic\s*\(\s*off\s*,\s*derivative_uniformity\s*\)\s*;/;
  const isTidewaterTerrain = (code) =>
    code.includes("fn terDetail(") &&
    code.includes("terrainDetailTex") &&
    code.includes("smpAniso4Repeat");
  const isTidewaterPlants = (code) =>
    code.includes("fn vegImpSample(") &&
    code.includes("vegImpA") &&
    code.includes("vegImpB");
  const isTidewaterWater = (code) =>
    code.includes("fn waterSurfaceVertex(") &&
    code.includes("fn shadeWater(") &&
    code.includes("shoreSimStateTex");
  const isBevyMotionBlur = (code) =>
    code.includes("motion_vectors") &&
    code.includes("screen_texture") &&
    code.includes("shutter_angle") &&
    code.includes("sample_uv");
  const implicitMotionSample = /textureSample\(\s*(screen_texture|motion_vectors)\s*,\s*texture_sampler\s*,\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/g;

  function createShaderModule(descriptor) {
    const code = descriptor?.code;
    if (typeof code === "string" && isBevyMotionBlur(code)) {
      const calls = [...code.matchAll(implicitMotionSample)];
      if (calls.length !== 4) {
        console.error("Bevy motion blur WGSL changed: expected four implicit samples, got", calls.length);
        return original.call(this, descriptor);
      }
      const explicit = code.replace(implicitMotionSample,
        (_match, texture, uv) => `textureSampleLevel(${texture}, texture_sampler, ${uv}, 0.0)`);
      const module = original.call(this, { ...descriptor, code: explicit });
      if (typeof module.getCompilationInfo === "function") {
        void module.getCompilationInfo().then((info) => {
          for (const message of info.messages) {
            if (message.type === "error") console.error("Bevy motion blur WGSL:", message.lineNum, message.linePos, message.message);
          }
        });
      }
      return module;
    }
    if (
      typeof code !== "string" ||
      existingDirective.test(code) ||
      (!isTidewaterTerrain(code) && !isTidewaterPlants(code) && !isTidewaterWater(code))
    ) {
      return original.call(this, descriptor);
    }
    const module = original.call(this, { ...descriptor, code: directive + code });
    if (isTidewaterWater(code) && typeof module.getCompilationInfo === "function") {
      void module.getCompilationInfo().then((info) => {
        for (const message of info.messages) {
          if (message.type === "error") console.error("Tidewater water WGSL:", message.message);
        }
      });
    }
    return module;
  }

  Object.defineProperty(createShaderModule, installed, { value: true });
  devicePrototype.createShaderModule = createShaderModule;
})();
