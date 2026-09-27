//! Compact object changes by stable identity, derived only from verified receipts.
use crate::zone_authoring::{
    ZoneCorrections, buildings::AuthoredBuilding, paths::AuthoredPath, rivers::AuthoredRiver,
};
use forge_core::{ContentHash, EntityId, canonical_json_sha256};
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ObjectDomain {
    Buildings,
    Paths,
    Rivers,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectSummary {
    pub name: String,
    pub locked: bool,
    pub hash: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectChange {
    pub id: EntityId,
    pub before: Option<ObjectSummary>,
    pub after: Option<ObjectSummary>,
}

trait Object: Serialize {
    fn id(&self) -> EntityId;
    fn name(&self) -> &str;
    fn locked(&self) -> bool;
}
impl Object for AuthoredBuilding {
    fn id(&self) -> EntityId {
        self.id
    }
    fn name(&self) -> &str {
        &self.name
    }
    fn locked(&self) -> bool {
        self.locked
    }
}
impl Object for AuthoredPath {
    fn id(&self) -> EntityId {
        self.id
    }
    fn name(&self) -> &str {
        &self.name
    }
    fn locked(&self) -> bool {
        self.locked
    }
}
impl Object for AuthoredRiver {
    fn id(&self) -> EntityId {
        self.id
    }
    fn name(&self) -> &str {
        &self.water.name
    }
    fn locked(&self) -> bool {
        self.locked
    }
}

fn compare<T: Object>(
    before: Option<&[T]>,
    after: Option<&[T]>,
    domain: ObjectDomain,
) -> Result<Option<super::ZoneHistoryChange>, String> {
    let summarize = |values: Option<&[T]>| -> Result<std::collections::BTreeMap<_, _>, String> {
        values
            .into_iter()
            .flatten()
            .map(|v| {
                Ok((
                    v.id(),
                    ObjectSummary {
                        name: v.name().to_owned(),
                        locked: v.locked(),
                        hash: canonical_json_sha256(v).map_err(|e| e.to_string())?,
                    },
                ))
            })
            .collect()
    };
    let old = summarize(before)?;
    let new = summarize(after)?;
    let ids: std::collections::BTreeSet<_> = old.keys().chain(new.keys()).copied().collect();
    let changes: Vec<_> = ids
        .into_iter()
        .filter(|id| old.get(id) != new.get(id))
        .map(|id| ObjectChange {
            id,
            before: old.get(&id).cloned(),
            after: new.get(&id).cloned(),
        })
        .collect();
    Ok(
        (!changes.is_empty() || before.is_some() != after.is_some()).then_some(
            super::ZoneHistoryChange::Objects {
                domain,
                before_explicit: before.is_some(),
                after_explicit: after.is_some(),
                changes,
            },
        ),
    )
}

pub(super) fn changes(
    before: &ZoneCorrections,
    after: &ZoneCorrections,
) -> Result<Vec<super::ZoneHistoryChange>, String> {
    Ok([
        compare(
            before.buildings.as_deref(),
            after.buildings.as_deref(),
            ObjectDomain::Buildings,
        )?,
        compare(
            before.paths.as_deref(),
            after.paths.as_deref(),
            ObjectDomain::Paths,
        )?,
        compare(
            before.rivers.as_deref(),
            after.rivers.as_deref(),
            ObjectDomain::Rivers,
        )?,
    ]
    .into_iter()
    .flatten()
    .collect())
}
