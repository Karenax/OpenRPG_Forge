//! Coverage policy for original alpha-to-mask source programs. The source
//! fragment adapter can remain independently versioned during its qualification.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum SourceCoveragePolicy {
    Unchanged,
    PrepassCoverage,
    ForwardDepthEqual,
    RejectMissingPrepass,
}

#[allow(
    clippy::fn_params_excessive_bools,
    reason = "Coverage enablement, depth availability, normals and prepass phase are independent rendering inputs."
)]
pub(super) fn source_coverage_policy(
    enabled: bool,
    samples: u32,
    depth: bool,
    normal: bool,
    prepass: bool,
) -> SourceCoveragePolicy {
    if !enabled || samples == 1 {
        return SourceCoveragePolicy::Unchanged;
    }
    if !depth || !normal {
        return SourceCoveragePolicy::RejectMissingPrepass;
    }
    if prepass {
        SourceCoveragePolicy::PrepassCoverage
    } else {
        SourceCoveragePolicy::ForwardDepthEqual
    }
}
