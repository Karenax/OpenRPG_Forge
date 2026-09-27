const PENDING: &str = "Mémoire GPU : relevé en attente";
const UNAVAILABLE: &str = "Mémoire GPU : mesure indisponible sur ce backend";

fn available_summary(frame: u32, allocated: u64, reserved: u64) -> String {
    format!(
        "Allocateur GPU : {} Mio alloués · {} Mio réservés · frame {}",
        mib(allocated),
        mib(reserved),
        frame,
    )
}

fn mib(bytes: u64) -> String {
    const UNIT: u64 = 1_048_576;
    format!("{},{:02}", bytes / UNIT, (bytes % UNIT) * 100 / UNIT)
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod browser {
    use super::{PENDING, UNAVAILABLE, available_summary};
    use bevy::prelude::{App, Last, Local, Res};
    use forge_engine::{GpuDiagnostic, GpuMemoryDiagnostic};
    use leptos::prelude::*;
    use std::cell::RefCell;

    thread_local! {
        static OUTPUT: RefCell<Option<WriteSignal<Option<GpuMemoryDiagnostic>>>> = const { RefCell::new(None) };
    }

    fn memory_summary(memory: Option<&GpuMemoryDiagnostic>) -> String {
        let Some(memory) = memory else {
            return PENDING.to_owned();
        };
        memory.allocator.as_ref().map_or_else(
            || UNAVAILABLE.to_owned(),
            |totals| {
                available_summary(
                    memory.sampled_frame,
                    totals.allocated_bytes,
                    totals.reserved_bytes,
                )
            },
        )
    }

    #[component]
    pub fn MemoryStatus() -> impl IntoView {
        let (observed, set_observed) = signal::<Option<GpuMemoryDiagnostic>>(None);
        OUTPUT.with(|slot| slot.replace(Some(set_observed)));
        view! {
            <output id="studio-gpu-memory-status">{move || memory_summary(observed.get().as_ref())}</output>
            <p class="hint">"Allocations du backend, pas VRAM totale. Relevé toutes les 5 s si disponible."</p>
            <pre id="studio-gpu-memory-ledger" hidden>{move || observed.get().map_or_else(String::new,
                |value| serde_json::to_string(&value).unwrap_or_else(|error| format!("serialization failed: {error}")))}</pre>
        }
    }

    pub fn install(app: &mut App) {
        app.add_systems(Last, publish);
    }

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy system parameter ownership"
    )]
    fn publish(gpu: Option<Res<GpuDiagnostic>>, mut last: Local<Option<GpuMemoryDiagnostic>>) {
        let current = gpu.as_ref().and_then(|gpu| gpu.memory.as_ref());
        if current == last.as_ref() {
            return;
        }
        OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(current.cloned());
            }
        });
        *last = current.cloned();
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub use browser::{MemoryStatus, install};

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unavailable_and_pending_memory_never_display_zero_usage() {
        assert!(PENDING.contains("attente"));
        assert!(UNAVAILABLE.contains("indisponible"));
        assert!(!UNAVAILABLE.contains("0 Mio"));
        assert_eq!(
            available_summary(731, 888_353_628, 1_182_734_692),
            "Allocateur GPU : 847,20 Mio alloués · 1127,94 Mio réservés · frame 731"
        );
    }

    #[test]
    fn byte_formatting_is_bounded_without_float_precision_loss() {
        assert_eq!(mib(0), "0,00");
        assert_eq!(mib(1_048_576), "1,00");
        assert_eq!(mib(u64::MAX), "17592186044415,99");
    }
}
