// Observe browser GPU allocations without retaining their objects or changing
// descriptors. Requested bytes differ from driver residency and heap alignment.
(() => {
  if (!globalThis.GPUDevice || !globalThis.WeakRef) return;
  const records = [];
  const owners = new WeakMap();
  const created = { buffer: 0, texture: 0, pipeline: 0, shader: 0 };
  const bytesCreated = { buffer: 0, texture: 0 };
  const sizes = { r8unorm: 1, r8snorm: 1, r8uint: 1, r8sint: 1,
    r16float: 2, r16uint: 2, r16sint: 2, rg8unorm: 2,
    r32float: 4, r32uint: 4, r32sint: 4, rg16float: 4,
    rgba8unorm: 4, "rgba8unorm-srgb": 4, bgra8unorm: 4,
    "bgra8unorm-srgb": 4, rgba8uint: 4, rgba8snorm: 4,
    rgb10a2unorm: 4, rg11b10ufloat: 4, depth32float: 4,
    depth24plus: 4, "depth24plus-stencil8": 4, depth16unorm: 2,
    rg32float: 8, rgba16float: 8, rgba16uint: 8,
    rgba32float: 16, rgba32uint: 16 };
  let unknownTextures = 0;
  function textureBytes(texture) {
    const block = sizes[texture.format];
    if (!block) { unknownTextures++; return 0; }
    let bytes = 0;
    for (let mip = 0; mip < texture.mipLevelCount; mip++) {
      bytes += Math.max(1, texture.width >> mip) * Math.max(1, texture.height >> mip)
        * (texture.dimension === "3d" ? Math.max(1, texture.depthOrArrayLayers >> mip) : texture.depthOrArrayLayers)
        * texture.sampleCount * block;
    }
    return bytes;
  }
  function track(object, kind, label, bytes) {
    const record = { weak: new WeakRef(object), kind, label: label || "sans libellé", bytes, destroyed: false };
    records.push(record);
    owners.set(object, record);
    created[kind]++;
    if (kind in bytesCreated) bytesCreated[kind] += bytes;
  }
  function observeMethod(name, kind, asynchronous = false) {
    const prototype = GPUDevice.prototype;
    const original = prototype[name];
    if (!original) return;
    prototype[name] = function (descriptor, ...rest) {
      const result = original.call(this, descriptor, ...rest);
      const adopt = object => {
        const bytes = kind === "buffer" ? object.size : kind === "texture" ? textureBytes(object) : 0;
        track(object, kind, descriptor?.label, bytes);
        return object;
      };
      return asynchronous ? result.then(adopt) : adopt(result);
    };
  }
  observeMethod("createBuffer", "buffer");
  observeMethod("createTexture", "texture");
  observeMethod("createShaderModule", "shader");
  observeMethod("createRenderPipeline", "pipeline");
  observeMethod("createComputePipeline", "pipeline");
  observeMethod("createRenderPipelineAsync", "pipeline", true);
  observeMethod("createComputePipelineAsync", "pipeline", true);
  for (const prototype of [globalThis.GPUBuffer?.prototype, globalThis.GPUTexture?.prototype]) {
    if (!prototype?.destroy) continue;
    const original = prototype.destroy;
    prototype.destroy = function (...args) {
      const result = original.apply(this, args);
      const record = owners.get(this);
      if (record) record.destroyed = true;
      return result;
    };
  }
  function read() {
    const live = { buffer: 0, texture: 0, pipeline: 0, shader: 0 };
    const bytes = { buffer: 0, texture: 0 };
    const groups = new Map();
    let retained = 0;
    for (const record of records) {
      if (record.destroyed || !record.weak.deref()) continue;
      records[retained++] = record;
      live[record.kind]++;
      if (record.kind in bytes) bytes[record.kind] += record.bytes;
      const key = `${record.kind}: ${record.label}`;
      const group = groups.get(key) || { label: key, count: 0, bytes: 0 };
      group.count++; group.bytes += record.bytes; groups.set(key, group);
    }
    records.length = retained;
    return { live, bytes, created: { ...created }, bytesCreated: { ...bytesCreated }, unknownTextures,
      largest: [...groups.values()].sort((a, b) => b.bytes - a.bytes).slice(0, 12) };
  }
  globalThis.__forgeGpuAllocations = { read };
  setInterval(() => {
    const metrics = document.querySelector(".forge-viewport-metrics");
    const diagnostics = document.querySelector(".gold-view-settings");
    if (!metrics || !metrics.textContent || !diagnostics) return;
    let output = document.getElementById("forge-gpu-allocations");
    if (!output) {
      const details = document.createElement("details");
      const summary = document.createElement("summary"); summary.textContent = "Allocations WebGPU";
      output = document.createElement("pre"); output.id = "forge-gpu-allocations";
      output.style.cssText = "white-space:pre-wrap;font-size:11px;max-height:240px;overflow:auto";
      details.append(summary, output); diagnostics.append(details);
    }
    const observation = read();
    output.textContent = `Descripteurs visibles ; résidence pilote différente.\nBuffers: ${Math.round(observation.bytes.buffer / 1048576)} Mio (${observation.live.buffer}). Textures: ${Math.round(observation.bytes.texture / 1048576)} Mio (${observation.live.texture}). Pipelines: ${observation.live.pipeline}.\nCréés depuis l’ouverture : ${JSON.stringify(observation.created)}. Octets créés : ${JSON.stringify(observation.bytesCreated)}. Formats non estimés : ${observation.unknownTextures}.\n` + observation.largest.map(group => `${group.label}: ${Math.round(group.bytes / 1048576)} Mio (${group.count})`).join("\n");
    output.setAttribute("data-forge-gpu-allocations", JSON.stringify(observation));
    output.title = observation.largest.map(group => `${group.label}: ${Math.round(group.bytes / 1048576)} Mio (${group.count})`).join("\n");
  }, 1000);
})();
