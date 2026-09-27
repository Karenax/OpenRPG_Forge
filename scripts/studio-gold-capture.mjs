// Read-only evidence policy: an available bitmap may still clip a native window.
export function captureDimensions(png, resolution, dpr) {
  if (!Buffer.isBuffer(png) || png.length < 24
    || !png.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))
    || png.toString('ascii', 12, 16) !== 'IHDR') {
    throw new Error('Capture is not a PNG with dimensions');
  }
  const width = png.readUInt32BE(16), height = png.readUInt32BE(20);
  const valid = Array.isArray(resolution) && resolution.length === 2
    && resolution.every(n => Number.isFinite(n) && n > 0)
    && Number.isFinite(dpr) && dpr > 0;
  return { width, height, complete: valid && width > 0 && height > 0
    && Math.abs(width - resolution[0] * dpr) <= 1
    && Math.abs(height - resolution[1] * dpr) <= 1 };
}
