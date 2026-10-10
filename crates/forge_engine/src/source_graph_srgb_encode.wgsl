#define_import_path forge_engine::source_graph_srgb_encode

// The table is measured from the current native device, not from source images.
// Only a validated 256x1 float32 threshold texture may supply this parameter.
fn source_graph_srgb8_code(thresholds: texture_2d<f32>, value: f32) -> u32 {
    let x = clamp(value, 0.0, 1.0);
    var lower = 0u;
    var upper = 255u;
    for (var step = 0u; step < 8u; step += 1u) {
        if (lower >= upper) { break; }
        let middle = (lower + upper + 1u) / 2u;
        let boundary = textureLoad(thresholds, vec2<i32>(i32(middle), 0), 0).r;
        if (x >= boundary) { lower = middle; }
        else { upper = middle - 1u; }
    }
    return lower;
}
