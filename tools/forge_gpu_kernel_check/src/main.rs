//! Execute a pinned diagnostic compute kernel and retain raw GPU readback.
use std::{
    future::Future,
    sync::Arc,
    task::{Context, Poll, Wake, Waker},
    time::Duration,
};
use wgpu::util::DeviceExt;

struct ThreadWake(std::thread::Thread);
impl Wake for ThreadWake {
    fn wake(self: Arc<Self>) {
        self.0.unpark();
    }
    fn wake_by_ref(self: &Arc<Self>) {
        self.0.unpark();
    }
}
fn block_on<T>(future: impl Future<Output = T>) -> T {
    let mut future = std::pin::pin!(future);
    let waker = Waker::from(Arc::new(ThreadWake(std::thread::current())));
    let mut cx = Context::from_waker(&waker);
    loop {
        match future.as_mut().poll(&mut cx) {
            Poll::Ready(value) => return value,
            Poll::Pending => std::thread::park_timeout(Duration::from_millis(1)),
        }
    }
}
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().collect();
    if args.len() != 5 {
        return Err("Usage: kernel.wgsl input.f32 output.f32 receipt.json".into());
    }
    let input = std::fs::read(&args[2])?;
    if input.is_empty() || input.len() % 16 != 0 || input.len() > 65536 {
        return Err("Expected a bounded array of float4 inputs".into());
    }
    let instance = wgpu::Instance::new(wgpu::InstanceDescriptor {
        backends: wgpu::Backends::DX12,
        ..wgpu::InstanceDescriptor::new_without_display_handle()
    });
    let adapter = block_on(instance.request_adapter(&wgpu::RequestAdapterOptions {
        power_preference: wgpu::PowerPreference::HighPerformance,
        force_fallback_adapter: false,
        compatible_surface: None,
    }))?;
    let info = adapter.get_info();
    let (device, queue) = block_on(adapter.request_device(&wgpu::DeviceDescriptor {
        label: Some("Pinned kernel diagnostic"),
        required_limits: adapter.limits(),
        ..Default::default()
    }))?;
    let error_scope = device.push_error_scope(wgpu::ErrorFilter::Validation);
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("Pinned source helper"),
        source: wgpu::ShaderSource::Wgsl(std::fs::read_to_string(&args[1])?.into()),
    });
    let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
        label: Some("Source helper diagnostic"),
        layout: None,
        module: &shader,
        entry_point: Some("kernel_main"),
        compilation_options: Default::default(),
        cache: None,
    });
    if let Some(error) = block_on(error_scope.pop()) {
        return Err(error.to_string().into());
    }
    let error_scope = device.push_error_scope(wgpu::ErrorFilter::Validation);
    let source = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
        label: None,
        contents: &input,
        usage: wgpu::BufferUsages::STORAGE,
    });
    let size = u64::try_from(input.len())?;
    let output = device.create_buffer(&wgpu::BufferDescriptor {
        label: None,
        size,
        usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
        mapped_at_creation: false,
    });
    let readback = device.create_buffer(&wgpu::BufferDescriptor {
        label: None,
        size,
        usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    let binding = device.create_bind_group(&wgpu::BindGroupDescriptor {
        label: None,
        layout: &pipeline.get_bind_group_layout(0),
        entries: &[
            wgpu::BindGroupEntry {
                binding: 0,
                resource: source.as_entire_binding(),
            },
            wgpu::BindGroupEntry {
                binding: 1,
                resource: output.as_entire_binding(),
            },
        ],
    });
    let mut encoder = device.create_command_encoder(&Default::default());
    {
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
            label: None,
            timestamp_writes: None,
        });
        pass.set_pipeline(&pipeline);
        pass.set_bind_group(0, &binding, &[]);
        pass.dispatch_workgroups(u32::try_from(input.len() / 16)?, 1, 1);
    }
    encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, size);
    let submitted = queue.submit([encoder.finish()]);
    if let Some(error) = block_on(error_scope.pop()) {
        return Err(error.to_string().into());
    }
    let (tx, rx) = std::sync::mpsc::channel();
    readback
        .slice(..)
        .map_async(wgpu::MapMode::Read, move |result| {
            let _ = tx.send(result);
        });
    device.poll(wgpu::PollType::Wait {
        submission_index: Some(submitted),
        timeout: Some(Duration::from_secs(30)),
    })?;
    rx.recv_timeout(Duration::from_secs(30))??;
    let bytes = readback.slice(..).get_mapped_range();
    use std::io::Write;
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[3])?
        .write_all(&bytes)?;
    let result = serde_json::json!({"status":"PINNED_KERNEL_EXECUTED", "adapter":info.name,
        "backend":format!("{:?}",info.backend), "input_float4_count":input.len()/16,
        "readback_bytes":bytes.len(), "validation_errors":0, "full_import":false});
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[4])?
        .write_all(&serde_json::to_vec_pretty(&result)?)?;
    println!("{result}");
    Ok(())
}
