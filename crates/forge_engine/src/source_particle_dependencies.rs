//! Iterative producer/consumer order; no asset names or scene order inferred.
use super::{SourceParticleComputeRequest, SourceParticleComputeStatus};
use std::collections::{BTreeMap, BTreeSet};

pub(super) fn dispatch_order(
    queue: &BTreeMap<u64, Vec<SourceParticleComputeRequest>>,
) -> (Vec<u64>, Vec<u64>) {
    let mut degrees = BTreeMap::new();
    let mut children = BTreeMap::<u64, BTreeSet<u64>>::new();
    for (slot, requests) in queue {
        let dependencies: BTreeSet<_> = requests
            .iter()
            .filter_map(|request| request.after_slot)
            .filter(|dependency| queue.contains_key(dependency))
            .collect();
        degrees.insert(*slot, dependencies.len());
        for dependency in dependencies {
            children.entry(dependency).or_default().insert(*slot);
        }
    }
    let mut ready: BTreeSet<_> = degrees
        .iter()
        .filter_map(|(slot, count)| (*count == 0).then_some(*slot))
        .collect();
    let mut ordered = Vec::new();
    while let Some(slot) = ready.pop_first() {
        ordered.push(slot);
        if let Some(next) = children.get(&slot) {
            for child in next {
                let count = degrees.get_mut(child).unwrap();
                *count -= 1;
                if *count == 0 {
                    ready.insert(*child);
                }
            }
        }
    }
    let cyclic = degrees
        .into_iter()
        .filter_map(|(slot, count)| (count != 0).then_some(slot))
        .collect();
    (ordered, cyclic)
}

pub(super) fn admit_dependencies(
    requests: &[SourceParticleComputeRequest],
    queue: &BTreeMap<u64, Vec<SourceParticleComputeRequest>>,
) -> bool {
    for dependency in requests.iter().filter_map(|request| request.after_slot) {
        let Some(producer) = queue.get(&dependency) else {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::WaitingForResources);
            }
            return false;
        };
        for phase in producer {
            match phase.status() {
                Ok(SourceParticleComputeStatus::Encoded) => {}
                Ok(SourceParticleComputeStatus::Failed(error)) => {
                    for request in requests {
                        request.set_status(SourceParticleComputeStatus::Failed(format!(
                            "Source producer {dependency} failed: {error}"
                        )));
                    }
                    return false;
                }
                Err(error) => {
                    for request in requests {
                        request.set_status(SourceParticleComputeStatus::Failed(error.into()));
                    }
                    return false;
                }
                _ => {
                    for request in requests {
                        request.set_status(SourceParticleComputeStatus::WaitingForResources);
                    }
                    return false;
                }
            }
        }
    }
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{prelude::*, render::storage::ShaderBuffer};

    #[test]
    fn order_ignores_slot_numbers_and_cycles_do_not_block_independent_groups() {
        let mut assets = Assets::<ShaderBuffer>::default();
        let pair: [Handle<ShaderBuffer>; 2] =
            std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let request = || {
            SourceParticleComputeRequest::new(
                Handle::default(),
                "advance".into(),
                Handle::default(),
                [0; 1024],
                pair[0].clone(),
                pair[1].clone(),
                1,
            )
            .unwrap()
        };
        let mut queue = BTreeMap::new();
        queue.insert(90, vec![request()]);
        queue.insert(2, vec![request().after_slot(90).unwrap()]);
        assert_eq!(dispatch_order(&queue), (vec![90, 2], vec![]));
        assert!(!admit_dependencies(&queue[&2], &queue));
        queue[&90][0].set_status(SourceParticleComputeStatus::Encoded);
        assert!(admit_dependencies(&queue[&2], &queue));
        queue.insert(4, vec![request().after_slot(5).unwrap()]);
        queue.insert(5, vec![request().after_slot(4).unwrap()]);
        assert_eq!(dispatch_order(&queue), (vec![90, 2], vec![4, 5]));
        queue.insert(7, vec![request().after_slot(999).unwrap()]);
        assert!(!admit_dependencies(&queue[&7], &queue));
        queue[&90][0].set_status(SourceParticleComputeStatus::Failed("probe".into()));
        assert!(!admit_dependencies(&queue[&2], &queue));
        assert!(matches!(
            queue[&2][0].status(),
            Ok(SourceParticleComputeStatus::Failed(_))
        ));
    }
}
