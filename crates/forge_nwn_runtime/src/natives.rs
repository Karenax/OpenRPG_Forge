//! Explicit registry for the `NWScript` natives exercised by the synthetic fixture.
//!
//! Routine identifiers come from the `nwscript.nss` function ordinal used by the
//! NWN:EE compiler. A routine that is absent from this registry is never treated
//! as supported: the VM records a diagnostic and fails the call.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum NativeStatus {
    Supported,
    Unsupported,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct NativeSpec {
    pub routine: u16,
    pub name: &'static str,
    pub arguments: u8,
    pub status: NativeStatus,
}

pub const REGISTRY_VERSION: &str = "nwscript-ee-ordinals-v1";

pub const REGISTRY: &[NativeSpec] = &[
    NativeSpec {
        routine: 242,
        name: "GetModule",
        arguments: 0,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 51,
        name: "GetLocalInt",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 55,
        name: "SetLocalInt",
        arguments: 3,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 548,
        name: "GetFirstPC",
        arguments: 0,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 367,
        name: "AddJournalQuestEntry",
        arguments: 6,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 25,
        name: "GetEnteringObject",
        arguments: 0,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 217,
        name: "GetIsPC",
        arguments: 1,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 374,
        name: "SendMessageToPC",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 330,
        name: "GetLastUsedBy",
        arguments: 0,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 204,
        name: "ActionStartConversation",
        arguments: 4,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 322,
        name: "GiveGoldToCreature",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 6,
        name: "AssignCommand",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 7,
        name: "DelayCommand",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 8,
        name: "ExecuteScript",
        arguments: 2,
        status: NativeStatus::Supported,
    },
    NativeSpec {
        routine: 294,
        name: "ActionDoCommand",
        arguments: 1,
        status: NativeStatus::Supported,
    },
];

#[must_use]
pub fn lookup(routine: u16) -> Option<&'static NativeSpec> {
    REGISTRY.iter().find(|spec| spec.routine == routine)
}

pub fn supported() -> impl Iterator<Item = &'static NativeSpec> {
    REGISTRY
        .iter()
        .filter(|spec| spec.status == NativeStatus::Supported)
}

pub fn unsupported() -> impl Iterator<Item = &'static NativeSpec> {
    REGISTRY
        .iter()
        .filter(|spec| spec.status == NativeStatus::Unsupported)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fixture_natives_have_the_expected_ids_and_arities() {
        let get_module = lookup(242).expect("GetModule");
        assert_eq!(get_module.name, "GetModule");
        assert_eq!(get_module.arguments, 0);
        let journal = lookup(367).expect("AddJournalQuestEntry");
        assert_eq!(journal.arguments, 6);
        assert_eq!(journal.status, NativeStatus::Supported);
    }

    #[test]
    fn scheduler_natives_are_registered_as_supported() {
        for routine in [6_u16, 7, 8, 294] {
            let spec = lookup(routine).expect("declared native");
            assert_eq!(spec.status, NativeStatus::Supported);
            assert_eq!(
                spec.name,
                match routine {
                    6 => "AssignCommand",
                    7 => "DelayCommand",
                    8 => "ExecuteScript",
                    _ => "ActionDoCommand",
                }
            );
        }
        assert!(lookup(65_535).is_none());
    }
}
