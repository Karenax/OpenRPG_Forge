//! Map acquisition content; environment package codecs belong to common assets.
/// Retained reference content; engine clocks consume only the returned explicit payload.
pub fn animation_reference() -> Result<(Vec<u8>, u32), String> {
    let capture: serde_json::Value = serde_json::from_str(include_str!(
        "../../../third_party/tidewater/acquisitions/animation-reference-village-1438b1ab.json"
    ))
    .map_err(|error| error.to_string())?;
    let gpu_frame = capture["gpu_frame"]
        .as_u64()
        .and_then(|v| u32::try_from(v).ok())
        .filter(|v| *v > 0)
        .ok_or("Animation reference frame absent")?;
    if capture["status"] != "ready_reference_only"
        || capture["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || capture["warmup_frames"] != 120
    {
        return Err("Animation reference has an invalid source or frame sequence".into());
    }
    let clocks = capture
        .get("animation_clocks")
        .ok_or("Animation reference clocks absent")?;
    Ok((
        serde_json::to_vec(clocks).map_err(|error| error.to_string())?,
        gpu_frame,
    ))
}
