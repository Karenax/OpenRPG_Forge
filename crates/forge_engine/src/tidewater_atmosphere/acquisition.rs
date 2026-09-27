//! Verified in-memory acquisition to atmosphere parameters and qualified kernels.
use super::Input;
use serde_json::Value;
use std::error::Error;
const NAMES: [&str; 4] = ["transmittance", "multiScat", "skyView", "irradiance"];
fn checked(
    meta: &Value,
    read: &impl Fn(&str) -> Result<Vec<u8>, String>,
    name: &str,
) -> Result<Vec<u8>, Box<dyn Error>> {
    let bytes = read(name)?;
    if bytes.len() > 1_000_000
        || meta["files"][name] != forge_core::bytes_sha256(&bytes).to_string()
    {
        return Err("atmosphere acquisition hash or size mismatch".into());
    }
    Ok(bytes)
}
#[allow(clippy::cast_possible_truncation)]
pub fn from_acquisition(
    meta: &Value,
    raw_source: bool,
    read: impl Fn(&str) -> Result<Vec<u8>, String>,
) -> Result<Input, Box<dyn Error>> {
    if meta["schema"] != "forge.tidewater.atmosphere-probe"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
    {
        return Err("invalid atmosphere source".into());
    }
    let y = meta["camera_y"].as_f64().ok_or("missing camera height")?;
    let sun = serde_json::from_value::<[f64; 3]>(meta["real_sun_direction"].clone())?;
    let direction = bevy::math::DVec3::from_array(sun);
    if !y.is_finite()
        || !(-1000.0..=60000.0).contains(&y)
        || !direction.is_finite()
        || (direction.length_squared() - 1.0).abs() > 0.0001
    {
        return Err("invalid atmosphere inputs".into());
    }
    let y = (y + 0.5).max(0.5);
    let step = 1.02_f64.ln();
    let yq = if y < 100.0 {
        (y / 2.0).round() * 2.0
    } else {
        ((y.ln() / step).round() * step).exp()
    };
    let height = 6360.0 + (yq / 1000.0).max(0.001);
    let values = [
        1.0, 1.0, 0.8, 1.0, 0.06, 0.08, 0.1, height, 11.0, 11.0, 11.0, 0.0, sun[0], sun[1], sun[2],
        0.0,
    ];
    let params: Vec<u8> = values
        .into_iter()
        .flat_map(|v| (v as f32).to_le_bytes())
        .collect();
    if params != checked(meta, &read, "params.bin")? {
        return Err("native atmosphere parameters differ".into());
    }
    let mut shaders = NAMES
        .iter()
        .map(|n| {
            Ok(String::from_utf8(checked(
                meta,
                &read,
                &format!("{n}.wgsl"),
            )?)?)
        })
        .collect::<Result<Vec<_>, Box<dyn Error>>>()?;
    if !raw_source {
        // At the final LUT row, the start point lies on the atmosphere's outer
        // sphere. Vulkan can leave a small positive residual in r² - RT² and
        // select a spurious near intersection. The LUT parameterization already
        // gives the full inward path length d. Preserve the source's d=0 case.
        let original = "let tMax = atmosphereRaySphereNearest( ro, rd, ATMO_RT );";
        if shaders[0].matches(original).count() != 1 {
            return Err("atmosphere boundary adapter anchor changed".into());
        }
        shaders[0] = shaders[0].replace(original,
            "let tMax = select(atmosphereRaySphereNearest(ro, rd, ATMO_RT), d, px.y == 63u && d > 0.0 && dot(ro,ro) - ATMO_RT * ATMO_RT > 0.0);");
    }
    let shaders = shaders
        .try_into()
        .map_err(|_| "four atmosphere kernels required")?;
    let values = std::array::from_fn(|i| {
        f32::from_le_bytes(
            params[i * 4..i * 4 + 4]
                .try_into()
                .expect("validated parameters"),
        )
    });
    Ok(Input::new(shaders, values)?)
}
