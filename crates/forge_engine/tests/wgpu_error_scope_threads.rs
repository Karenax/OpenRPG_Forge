//! Native GPU regression: simultaneous validation must not share another thread's scope.
#![cfg(not(target_arch = "wasm32"))]

use std::{
    borrow::Cow,
    panic::{AssertUnwindSafe, catch_unwind},
    sync::mpsc,
    time::Duration,
};

#[test]
#[ignore = "Requires a native graphics adapter; run explicitly during GPU qualification"]
fn concurrent_validation_scopes_are_isolated_by_thread() {
    let instance = wgpu::Instance::new(wgpu::InstanceDescriptor::new_without_display_handle());
    let adapter = bevy::tasks::block_on(instance.request_adapter(&wgpu::RequestAdapterOptions {
        power_preference: wgpu::PowerPreference::HighPerformance,
        force_fallback_adapter: false,
        compatible_surface: None,
    }))
    .expect("native adapter");
    let (device, _queue) =
        bevy::tasks::block_on(adapter.request_device(&wgpu::DeviceDescriptor::default()))
            .expect("native device");
    let (first_ready, first_wait) = mpsc::channel();
    let (second_ready, second_wait) = mpsc::channel();
    let (first_finished, first_finish_wait) = mpsc::channel();
    let first_device = device.clone();
    let first = std::thread::spawn(move || {
        let scope = first_device.push_error_scope(wgpu::ErrorFilter::Validation);
        let _invalid = first_device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("validation isolation probe"),
            source: wgpu::ShaderSource::Wgsl(Cow::Borrowed(
                "@compute @workgroup_size(1) fn main() { let invalid: u32 = true; }",
            )),
        });
        first_ready.send(()).unwrap();
        second_wait.recv_timeout(Duration::from_secs(30)).unwrap();
        let result = catch_unwind(AssertUnwindSafe(|| {
            bevy::tasks::block_on(scope.pop()).is_some()
        }));
        first_finished.send(()).unwrap();
        result
    });
    let second = std::thread::spawn(move || {
        first_wait.recv_timeout(Duration::from_secs(30)).unwrap();
        let scope = device.push_error_scope(wgpu::ErrorFilter::Validation);
        second_ready.send(()).unwrap();
        first_finish_wait
            .recv_timeout(Duration::from_secs(30))
            .unwrap();
        bevy::tasks::block_on(scope.pop()).is_some()
    });
    let first_result = first.join().expect("first worker finished");
    let second_has_error = second.join().expect("second worker finished");
    assert!(
        first_result.is_ok(),
        "Another thread corrupted the first GPU validation scope"
    );
    assert!(
        first_result.unwrap(),
        "The first scope did not retain its own validation error"
    );
    assert!(
        !second_has_error,
        "The second scope received another thread's validation error"
    );
}
