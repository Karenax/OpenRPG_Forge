//! Pack source key light while preserving the independently refreshed SH tail.
pub(super) fn direct(global: &[u8; 720]) -> Result<[u8; 32], String> {
    let value = |offset| {
        f32::from_le_bytes(
            global[offset..offset + 4]
                .try_into()
                .expect("fixed frame ABI"),
        )
    };
    let direction = [value(592), value(596), value(600)];
    let color = [value(608), value(612), value(616)];
    if direction.iter().chain(&color).any(|v| !v.is_finite())
        || (direction.iter().map(|v| v * v).sum::<f32>() - 1.0).abs() > 0.001
        || color.iter().any(|v| *v < 0.0)
    {
        return Err("Invalid environment key light".into());
    }
    let mut bytes = [0; 32];
    for (index, value) in [
        direction[0],
        direction[1],
        direction[2],
        0.0,
        color[0],
        color[1],
        color[2],
        0.0,
    ]
    .into_iter()
    .enumerate()
    {
        bytes[index * 4..index * 4 + 4].copy_from_slice(&value.to_le_bytes());
    }
    Ok(bytes)
}
