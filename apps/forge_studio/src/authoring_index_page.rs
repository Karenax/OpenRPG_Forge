//! Presentation-only bounds shared by the Web index and native tests.
pub(super) const PAGE_SIZE: usize = 40;

pub(super) fn page_range(total: usize, requested: usize) -> std::ops::Range<usize> {
    let page = requested.min(total.saturating_sub(1) / PAGE_SIZE);
    let start = page * PAGE_SIZE;
    start..start.saturating_add(PAGE_SIZE).min(total)
}

/// A point inspector always mounts at most one editor, regardless of path size.
pub(super) fn point_range(total: usize, requested: usize) -> std::ops::Range<usize> {
    let index = requested.min(total.saturating_sub(1));
    index..index.saturating_add(usize::from(total > 0))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn point_inspector_stays_bounded_and_clamps_after_removal() {
        assert_eq!(point_range(0, 128), 0..0);
        assert_eq!(point_range(2, 0), 0..1);
        assert_eq!(point_range(128, 127), 127..128);
        assert_eq!(point_range(127, 127), 126..127);
        assert_eq!(point_range(2, usize::MAX), 1..2);
    }

    #[test]
    fn scene_index_page_ranges_cover_objects_and_clamp_after_removal() {
        assert_eq!(page_range(0, usize::MAX), 0..0);
        assert_eq!(page_range(40, 0), 0..40);
        assert_eq!(page_range(41, 1), 40..41);
        assert_eq!(page_range(81, usize::MAX), 80..81);
        assert_eq!(page_range(39, 2), 0..39);
        let covered: Vec<_> = (0..3).flat_map(|page| page_range(81, page)).collect();
        assert_eq!(covered, (0..81).collect::<Vec<_>>());
    }
}
