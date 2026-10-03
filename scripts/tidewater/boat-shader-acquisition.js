import { App } from '/src/App.js';
import { GPU } from '/src/engine/gpu/GPU.js';
import { FrameUniforms } from '/src/engine/render/Frame.js';
import { MeshRenderer } from '/src/engine/render/MeshRenderer.js';
import { buildMeshShader } from '/src/engine/render/MeshShader.js';
import { composeShader } from '/src/engine/gpu/Shader.js';
import { SceneLighting } from '/src/engine/render/wgsl/lighting.js';
import { DEPTH_FORMAT, LAYERS } from '/src/engine/render/SceneRenderer.js';

const status = document.querySelector('#status');
const button = document.querySelector('#capture');
const hash = async text => [...new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text)))].map(x => x.toString(16).padStart(2, '0')).join('');
function value(v) {
  if (typeof v === 'number' || typeof v === 'boolean' || v === null) return v;
  if (v?.toArray) return v.toArray();
  if (v?.elements) return [...v.elements];
  if (Array.isArray(v)) return v.map(value);
  if (ArrayBuffer.isView(v)) return [...v];
  throw Error('Unsupported source uniform value');
}
function block(u) {
  return { struct: u.structName, bytes: u.byteLength, fields: u.order.map(name => ({ name, type: u.layout[name].typeStr, offset: u.layout[name].offset, size: u.layout[name].size, stride: u.layout[name].stride, value: value(u.fields[name].value) })) };
}
function binding(set) {
  return set.names.map((name, index) => ({ name, binding: index, declaration: set.described[index].decl, layout: set.described[index].layout, uniform: set.specs[name].uniform ? block(set.specs[name].uniform) : null }));
}
function descriptorBlend(blend) {
  if (blend === undefined || blend === null) return null;
  if (!blend.color || !blend.alpha) throw Error('Pipeline descriptor blend is incomplete');
  return {
    color: { srcFactor: blend.color.srcFactor, dstFactor: blend.color.dstFactor, operation: blend.color.operation },
    alpha: { srcFactor: blend.alpha.srcFactor, dstFactor: blend.alpha.dstFactor, operation: blend.alpha.operation },
  };
}
function pipelineStateFromDescriptor(desc) {
  const primitive = desc?.primitive;
  const depthStencil = desc?.depthStencil;
  if (!primitive || !depthStencil) throw Error('Pipeline descriptor has no primitive/depthStencil state');
  if (primitive.topology === undefined || primitive.cullMode === undefined || primitive.frontFace === undefined) throw Error('Pipeline descriptor primitive state is incomplete');
  if (depthStencil.format === undefined || depthStencil.depthWriteEnabled === undefined || depthStencil.depthCompare === undefined || depthStencil.depthBias === undefined || depthStencil.depthBiasSlopeScale === undefined) throw Error('Pipeline descriptor depth state is incomplete');
  if (desc.fragment && !Array.isArray(desc.fragment.targets)) throw Error('Pipeline descriptor fragment targets are incomplete');
  const targets = desc.fragment?.targets ?? [];
  const multisample = desc.multisample ?? {};
  return {
    primitive: {
      topology: primitive.topology,
      cullMode: primitive.cullMode ?? 'none',
      frontFace: primitive.frontFace ?? 'ccw',
    },
    targets: targets.map((target, index) => {
      if (target?.format === undefined || target?.writeMask === undefined) throw Error(`Pipeline descriptor target ${index} is incomplete`);
      return { format: target.format, blend: descriptorBlend(target.blend), writeMask: target.writeMask };
    }),
    depthStencil: {
      format: depthStencil.format,
      depthWriteEnabled: depthStencil.depthWriteEnabled,
      depthCompare: depthStencil.depthCompare,
      depthBias: depthStencil.depthBias,
      depthBiasSlopeScale: depthStencil.depthBiasSlopeScale,
    },
    // MeshRenderer omits this field; these are WebGPU descriptor defaults,
    // recorded explicitly rather than reconstructed from material policy.
    multisample: {
      count: multisample.count ?? 1,
      mask: multisample.mask ?? 0xffffffff,
      alphaToCoverageEnabled: multisample.alphaToCoverageEnabled ?? false,
    },
  };
}
async function submit(result) {
  const response = await fetch('/forge-boat-capture-result', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(result, null, 2) });
  if (!response.ok) throw Error(await response.text());
  return response.json();
}
button.addEventListener('click', async () => {
  button.disabled = true;
  const result = { schema: 'forge.tidewater.boat-shader-capture', version: 2, status: 'running', started_utc: new Date().toISOString(), source_app_started: false, gpu_apis_replaced: false, materials: [], shaders: [], errors: [] };
  try {
    const app = new App();
    await app.init((progress, stage) => { status.textContent = `${Math.round(progress * 100)} % — ${stage}`; });
    // App.start() is deliberately not called: capture the genuine post-init
    // island state with its installed hooks, before the animation loop.
    const hooks = Object.entries(SceneLighting.hooks);
    for (const required of ['directModulation', 'ambientModulation', 'contactShadow', 'shadowPosition', 'bounce', 'localLights', 'envSpecular', 'envDiffuse']) {
      if (!SceneLighting.hooks[required]) throw Error(`Missing island hook ${required}`);
    }
    result.hooks = await Promise.all(hooks.map(async ([name, module]) => ({ name, module: module.name, code_sha256: await hash(module.code) })));
    result.adapter = { vendor: GPU.adapter.info.vendor, architecture: GPU.adapter.info.architecture, device: GPU.adapter.info.device, description: GPU.adapter.info.description };
    result.limits = Object.fromEntries(['maxUniformBuffersPerShaderStage','maxStorageBuffersPerShaderStage','maxSampledTexturesPerShaderStage','maxSamplersPerShaderStage'].map(k => [k, GPU.device.limits[k]]));
    result.features = [...GPU.features].sort();
    result.frame = block(FrameUniforms);
    const renderer = new MeshRenderer();
    const objects = Object.values(app.boat.meshes);
    result.parts = objects.map(o => ({ name: o.name, material: o.material.name, cast_shadow: o.castShadow, receive_shadow: o.receiveShadow, render_order: o.renderOrder, layer_mask: o.layers.mask }));
    const unique = [...new Map(objects.map(o => [o.material, o])).entries()];
    if (objects.length !== 12 || unique.length !== 7) throw Error('Boat source inventory differs');
    for (const [material, object] of unique) {
      const vl = renderer._layout(object, object.geometry, material);
      result.materials.push({ name: material.name, side: material.side, transparent: material.transparent, blending: material.blending, depth_write: material.depthWrite, depth_test: material.depthTest, velocity_weight: material.velocityWeight, vertex_colors: material.vertexColors, lit: material.lit, defines: material.allDefines(), uniforms: block(material.uniformBlock), vertex_layout: vl.layout, vertex_buffers: vl.buffers.map(b => b.layout), raster: { topology: material.topology, colorWrite: material.colorWrite, depthCompare: material.depthCompare, depthBias: material.depthBias, depthBiasSlopeScale: material.depthBiasSlopeScale } });
      // SceneRenderer assigns late by layer, independently of material opacity.
      const lateValues = [...new Set(objects.filter(o => o.material === material).map(o => {
        if (o.layers.mask === (1 << LAYERS.OPAQUE)) return false;
        if (o.layers.mask === (1 << LAYERS.TRANSPARENT)) return true;
        throw Error(`Boat part layer is not a single source main layer: ${o.name}`);
      }))];
      const passes = lateValues.map(late => ({ kind: 'main', late, colorFormats: ['rgba16float','rgba16float','rgba8unorm'], depthFormat: DEPTH_FORMAT, depthCompare: 'greater-equal' }));
      passes.push({ kind: 'color', late: false, colorFormats: ['rgba16float'], depthFormat: DEPTH_FORMAT, depthCompare: 'greater-equal' });
      // Exact policy of Shadows.render(), src/engine/render/Shadows.js:155-166.
      if (objects.some(o => o.material === material && o.castShadow)) passes.push({ kind: 'depth', late: false, colorFormats: [], depthFormat: 'depth32float', depthCompare: 'less-equal', depthBias: 2, depthBiasSlopeScale: 1.5 });
      for (const pass of passes) {
        status.textContent = `Capture ${material.name} — ${pass.kind}${pass.late ? ' late' : ''}`;
        const shaderPass = {
          kind: pass.kind,
          late: pass.late,
          colorFormats: pass.colorFormats,
          depthFormat: pass.depthFormat,
          depthCompare: pass.depthCompare,
          rasterOverrides: {
            cullMode: pass.cullOverride ?? null,
            depthBias: pass.kind === 'depth' ? (pass.depthBias || 0) : 0,
            depthBiasSlopeScale: pass.kind === 'depth' ? (pass.depthBiasSlopeScale || 0) : 0,
          },
        };
        const variant = {
          material: material.name,
          pass: shaderPass,
          compilation_messages: [],
          validation_error: null,
          pipeline_label: `${material.name}:${pass.kind}:acquisition`,
          pipeline_ready: false,
          pipeline_failed: false,
        };
        let source;
        let composed;
        let pipeline;
        let validation;
        let scopeOpen = false;
        let variantError;
        try {
          source = buildMeshShader(material, vl.layout, pass);
          composed = composeShader({ modules: source.modules, bindings: source.bindings, code: source.code, defines: source.defines, stage: 'render', label: material.name });
          Object.assign(variant, {
            defines: source.defines,
            has_fragment: source.hasFragment,
            wgsl: composed.code,
            sha256: await hash(composed.code),
            modules: await Promise.all(composed.modules.map(async m => ({ name: m.name, sha256: await hash(m.code) }))),
            group0: binding(composed.group0),
            group1: binding(composed.bindings),
            draw_binding: { group: 2, binding: 0, bytes: 160, dynamic_stride: 256 },
          });
          GPU.device.pushErrorScope('validation');
          scopeOpen = true;
          const sm = GPU.device.createShaderModule({ code: composed.code, label: `${material.name} ${pass.kind} acquisition` });
          variant.compilation_messages = [...(await sm.getCompilationInfo()).messages].map(m => ({ type: m.type, message: m.message, line: m.lineNum, position: m.linePos }));
          pipeline = renderer._createPipeline(material, vl, pass, `${material.name}:${pass.kind}:acquisition`);
          const descriptor = pipeline?.handle?.desc;
          if (!descriptor) throw Error(`Pipeline descriptor unavailable for ${material.name}/${pass.kind}`);
          if (Boolean(descriptor.fragment) !== Boolean(source.hasFragment)) throw Error(`Pipeline fragment state differs for ${material.name}/${pass.kind}`);
          // Capture the descriptor while GPU._async still retains it; successful
          // readiness clears handle.desc and must not be used as a source of policy.
          variant.pipeline_state = pipelineStateFromDescriptor(descriptor);
          await GPU.pipelinesReady();
          variant.pipeline_failed = pipeline.handle?.failed === true;
          variant.pipeline_ready = pipeline.handle?.pipeline !== null && pipeline.handle?.pipeline !== undefined;
          if (variant.pipeline_failed || !variant.pipeline_ready) {
            throw Error(`Pipeline was not ready for ${material.name}/${pass.kind}: failed=${variant.pipeline_failed}, ready=${variant.pipeline_ready}`);
          }
          variant.pipeline_label = pipeline.label;
        } catch (error) {
          variantError = error instanceof Error ? error : Error(String(error));
        } finally {
          if (scopeOpen) {
            try {
              validation = await GPU.device.popErrorScope();
            } catch (error) {
              const popError = error instanceof Error ? error : Error(String(error));
              if (variantError) variant.scope_pop_error = popError.message;
              else variantError = popError;
            }
            scopeOpen = false;
          }
        }
        variant.validation_error = validation?.message ?? null;
        if (validation && !variantError) variantError = Error(`Shader/pipeline validation failed for ${material.name}/${pass.kind}: ${validation.message}`);
        if (!variantError && variant.compilation_messages.some(m => m.type === 'error')) variantError = Error(`Shader compilation failed for ${material.name}/${pass.kind}`);
        if (variantError) variant.error = variantError.message;
        result.shaders.push(variant);
        if (variantError) throw variantError;
      }
    }
    result.status = 'complete'; result.completed_utc = new Date().toISOString();
    const receipt = await submit(result);
    status.textContent = `Acquisition terminée : ${result.materials.length} matériaux, ${result.shaders.length} shaders, ${result.hooks.length} hooks réels.\nSHA-256 : ${receipt.sha256}\nLe rendu Rust et la parité visuelle restent à qualifier.`;
  } catch (error) {
    result.status = 'failed'; result.errors.push(String(error)); result.completed_utc = new Date().toISOString();
    try { await submit(result); } catch (saveError) { result.errors.push(String(saveError)); }
    status.textContent = `Échec conservé : ${result.errors.join('\n')}`;
  }
});
