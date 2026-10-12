// Bootstrap/transfer only. All generation and validation live in the shared Rust provider.
import init, { generate_navigation_tile, generate_navigation_tile_with_timing } from "./forge_navigation.js";
const ready = init();
self.onmessage = async ({ data }) => {
  const { token, buffer } = data;
  try {
    await ready;
    const input = new Uint8Array(buffer);
    if (input.byteLength > 32 * 1024 * 1024) throw new Error("navigation worker input byte budget exceeded");
    const observed = data.observe_timing === true;
    const started = observed && self.performance ? self.performance.now() : NaN;
    const result = observed ? generate_navigation_tile_with_timing(input) : generate_navigation_tile(input);
    const finished = observed && self.performance ? self.performance.now() : NaN;
    const wrapper_ms = Number.isFinite(started) && Number.isFinite(finished) && finished >= started ? finished - started : null;
    if (result.byteLength > 8 * 1024 * 1024) throw new Error("navigation worker output byte budget exceeded");
    self.postMessage({ token, observed, wrapper_ms, buffer: result.buffer }, [result.buffer]);
  } catch (error) {
    self.postMessage({ token, error: String(error) });
  }
};
