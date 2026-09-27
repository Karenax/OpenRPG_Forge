// Preserve the original shader's separately rounded multiplication and addition.
// The Frame.pad0 bit pattern MUST be zero. A runtime integer operation prevents
// contraction across the binary32 rounding point without changing any bits.
// See shore-hash arithmetic diagnostics; this is not a replacement random seed.
fn shoreHash1( x: f32 ) -> f32 {
    let productBits = bitcast<u32>(x * 127.1) | bitcast<u32>(frame.pad0);
    let angle = bitcast<f32>(productBits) + 311.7;
    return fract(sin(angle) * 43758.5453);
}
