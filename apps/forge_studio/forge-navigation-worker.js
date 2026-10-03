// Bootstrap/transfer only. All generation and validation live in the shared Rust provider.
import init, { generate_navigation_tile } from "./forge_navigation.js";
const ready = init();
self.onmessage = async ({ data }) => {
  const { token, buffer } = data;
  try {
    await ready;
    const input = new Uint8Array(buffer);
    if (input.byteLength > 32 * 1024 * 1024) throw new Error("navigation worker input byte budget exceeded");
    const result = generate_navigation_tile(input);
    if (result.byteLength > 8 * 1024 * 1024) throw new Error("navigation worker output byte budget exceeded");
    self.postMessage({ token, buffer: result.buffer }, [result.buffer]);
  } catch (error) {
    self.postMessage({ token, error: String(error) });
  }
};
