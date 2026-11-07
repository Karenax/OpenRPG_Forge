//! One owner for the acquired lighting algorithms; scene literals remain inputs.
pub const FUNCTIONS: &str = include_str!("surface_library.wgsl");
const CORE: [&str; 7] = [
    "F_Schlick",
    "V_GGX_SmithCorrelated",
    "D_GGX",
    "BRDF_GGX",
    "sourceDirectLight",
    "DFGApprox",
    "multiscatter",
];

fn function_range(source: &str, name: &str) -> Result<std::ops::Range<usize>, String> {
    let anchor = format!("fn {name}(");
    if source.matches(&anchor).count() != 1 {
        return Err(format!(
            "Source lighting function is absent or ambiguous: {name}"
        ));
    }
    let start = source
        .find(&anchor)
        .ok_or("Source lighting function absent")?;
    let body = start
        + source[start..]
            .find('{')
            .ok_or("Source lighting function body absent")?;
    let mut depth = 1;
    for (index, byte) in source.as_bytes().iter().enumerate().skip(body + 1) {
        match byte {
            b'{' => depth += 1,
            b'}' => depth -= 1,
            _ => {}
        }
        if depth == 0 {
            return Ok(start..index + 1);
        }
    }
    Err("Unclosed source lighting function".into())
}

fn same_tokens(left: &str, right: &str) -> bool {
    left.chars()
        .filter(|c| !c.is_whitespace())
        .eq(right.chars().filter(|c| !c.is_whitespace()))
}

/// Preserve the seven source-independent BRDF functions and refuse algorithm drift.
/// The reference terrain, procedural surfaces and plants all use this owner.
pub fn reuse_brdf(source: &str) -> Result<String, String> {
    if !source.contains("fn F_Schlick(") {
        return Ok(source.to_owned());
    }
    let mut result = source.to_owned();
    for name in CORE {
        let original = function_range(&result, name)?;
        let common = function_range(FUNCTIONS, name)?;
        if !same_tokens(&result[original.clone()], &FUNCTIONS[common.clone()]) {
            return Err(format!("Source lighting algorithm drift: {name}"));
        }
        result.replace_range(original, &FUNCTIONS[common]);
    }
    Ok(result)
}

/// Shared SH/HDR and PCSS/cloud receiver body after explicit scene-light preparation.
pub fn reuse_plant_receivers(source: &str) -> Result<String, String> {
    let start = function_range(source, "F_Schlick")?.start;
    let end = function_range(source, "cloudsShadow")?.end;
    let common_start = function_range(FUNCTIONS, "F_Schlick")?.start;
    let common_end = function_range(FUNCTIONS, "cloudsShadow")?.end;
    if !same_tokens(&source[start..end], &FUNCTIONS[common_start..common_end]) {
        return Err("Source surface lighting or receiver algorithm drift".into());
    }
    let mut result = source.to_owned();
    result.replace_range(start..end, &FUNCTIONS[common_start..common_end]);
    Ok(result)
}

/// Instantiate the same algorithms at the non-conflicting extension bindings.
pub fn extension_functions() -> Result<String, String> {
    let coordinates = "((P.xz+1024.0)/2048.0)";
    if FUNCTIONS.matches(coordinates).count() != 1 {
        return Err("Source receiver domain seam drift".into());
    }
    // Receiver composition is shared with every source material. Remap only
    // after that composition so its existing checked declaration remains intact.
    let original = FUNCTIONS.to_owned();
    let prepared = crate::source_shadows::receiver::shader_source(&original)?;
    let mut composed = prepared;
    for binding in [10, 11, 12, 15, 16, 17, 40, 41, 44] {
        composed = composed.replace(
            &format!("@binding({binding})"),
            &format!("@binding({})", 200 + binding),
        );
    }
    composed = composed.replace(coordinates, "((P.xz-sourceDomain.xy)/sourceDomain.z)");
    Ok(composed)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn shared_equations_refuse_changed_microfacet_or_energy_compensation() {
        assert!(reuse_brdf(FUNCTIONS).is_ok());
        for (before, after) in [("0.047619", "0.047"), ("0.04", "0.05"), ("-9.28", "-9.0")] {
            assert!(FUNCTIONS.contains(before));
            assert!(reuse_brdf(&FUNCTIONS.replacen(before, after, 1)).is_err());
        }
        assert!(
            reuse_brdf(
                &(FUNCTIONS.to_owned()
                    + &FUNCTIONS[function_range(FUNCTIONS, "DFGApprox").unwrap()])
            )
            .is_err()
        );
    }
}
