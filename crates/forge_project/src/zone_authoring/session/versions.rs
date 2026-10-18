//! Frozen wire registry: versions select both capabilities and an archive codec.
//! Adding a head domain must not silently renumber existing archives.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum Encoding {
    Full,
    Compact,
    Delta,
}

// (full, compact, delta). Early formats intentionally share compact/delta
// versions; in particular path archives use 8/9/7, not an arithmetic triplet.
const FAMILIES: &[(u32, u32, u32)] = &[
    (1, 3, 6),
    (2, 3, 6),
    (4, 5, 6),
    (8, 9, 7),
    (10, 11, 12),
    (13, 14, 15),
    (16, 17, 18),
    (19, 20, 21),
    (22, 23, 24),
    (25, 26, 27),
    (28, 29, 30),
    (31, 32, 33),
    (34, 35, 36),
    (37, 38, 39),
    (40, 41, 42),
    (43, 44, 45),
    (46, 47, 48),
];

pub(super) fn encoding(version: u32) -> Result<Encoding, String> {
    for &(full, compact, delta) in FAMILIES {
        if version == full {
            return Ok(Encoding::Full);
        }
        if version == compact {
            return Ok(Encoding::Compact);
        }
        if version == delta {
            return Ok(Encoding::Delta);
        }
    }
    Err(format!("Unsupported zone archive version {version}"))
}

pub(super) fn encoded_version(full_version: u32, encoding: Encoding) -> Result<u32, String> {
    let &(_, compact, delta) = FAMILIES
        .iter()
        .find(|&&(full, _, _)| full == full_version)
        .ok_or_else(|| format!("Unsupported full zone archive version {full_version}"))?;
    Ok(match encoding {
        Encoding::Full => full_version,
        Encoding::Compact => compact,
        Encoding::Delta => delta,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn historical_wire_numbers_route_to_exactly_one_codec() {
        // Independent frozen protocol lists, including the early irregular codecs.
        let full = [
            1, 2, 4, 8, 10, 13, 16, 19, 22, 25, 28, 31, 34, 37, 40, 43, 46,
        ];
        let compact = [3, 5, 9, 11, 14, 17, 20, 23, 26, 29, 32, 35, 38, 41, 44, 47];
        let delta = [6, 7, 12, 15, 18, 21, 24, 27, 30, 33, 36, 39, 42, 45, 48];
        for version in 1..=48 {
            let expected = if full.contains(&version) {
                Encoding::Full
            } else if compact.contains(&version) {
                Encoding::Compact
            } else {
                assert!(delta.contains(&version));
                Encoding::Delta
            };
            assert_eq!(encoding(version), Ok(expected));
            let kinds: Vec<_> = FAMILIES
                .iter()
                .flat_map(|&(f, c, d)| {
                    [
                        (f, Encoding::Full),
                        (c, Encoding::Compact),
                        (d, Encoding::Delta),
                    ]
                })
                .filter(|&(v, _)| v == version)
                .map(|(_, k)| k)
                .collect();
            assert!(kinds.iter().all(|&kind| kind == expected));
        }
        for version in [0, 49, u32::MAX] {
            assert!(encoding(version).is_err());
        }
        assert!(encoded_version(49, Encoding::Compact).is_err());
    }
}
