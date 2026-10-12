use std::{fs, path::Path, future::Future, sync::Arc, task::{Wake, Context, Poll}, time::Duration};
use serde_json::{Value, json};
struct ThreadWake(std::thread::Thread);
impl Wake for ThreadWake { fn wake(self:Arc<Self>) { self.0.unpark(); } }
fn block_on<F:Future>(f:F)->F::Output {
    let waker=Arc::new(ThreadWake(std::thread::current())).into();
    let mut context=Context::from_waker(&waker); let mut f=Box::pin(f);
    loop { match f.as_mut().poll(&mut context) { Poll::Ready(v)=>return v, Poll::Pending=>std::thread::park_timeout(Duration::from_millis(5)) } }
}
fn main() {
    let root=std::env::args().nth(1).unwrap(); let root=Path::new(&root);
    let inputs:Value=serde_json::from_slice(&fs::read(root.join("INPUT.v1.json")).unwrap()).unwrap();
    let instance=wgpu::Instance::new(wgpu::InstanceDescriptor { backends:wgpu::Backends::DX12, ..wgpu::InstanceDescriptor::new_without_display_handle() });
    let adapter=block_on(instance.request_adapter(&wgpu::RequestAdapterOptions { power_preference:wgpu::PowerPreference::HighPerformance, force_fallback_adapter:false, compatible_surface:None })).unwrap();
    let features=wgpu::Features::TEXTURE_COMPRESSION_BC;
    assert!(adapter.features().contains(features));
    let (device,_queue)=block_on(adapter.request_device(&wgpu::DeviceDescriptor { required_features:features, required_limits:adapter.limits(), ..Default::default() })).unwrap();
    let attributes=[
        wgpu::VertexAttribute { format:wgpu::VertexFormat::Float32x3, offset:0, shader_location:0 },
        wgpu::VertexAttribute { format:wgpu::VertexFormat::Float32x2, offset:12, shader_location:2 },
        wgpu::VertexAttribute { format:wgpu::VertexFormat::Float32x3, offset:20, shader_location:8 },
        wgpu::VertexAttribute { format:wgpu::VertexFormat::Float32x4, offset:32, shader_location:9 },
        wgpu::VertexAttribute { format:wgpu::VertexFormat::Float32x4, offset:48, shader_location:10 },
    ];
    let buffers=[wgpu::VertexBufferLayout { array_stride:64, step_mode:wgpu::VertexStepMode::Vertex, attributes:&attributes }];
    let mut rows=Vec::new();
    for row in inputs["cases"].as_array().unwrap() {
        let label=row["permutation"].as_str().unwrap();
        device.push_error_scope(wgpu::ErrorFilter::Validation);
        let shader=device.create_shader_module(wgpu::ShaderModuleDescriptor { label:Some(label), source:wgpu::ShaderSource::Wgsl(fs::read_to_string(row["export"].as_str().unwrap()).unwrap().into()) });
        let forward=row["shader_stage"]=="forward";
        let normal=label=="normal_depth" || label=="ortho_normal_depth";
        let targets=if forward { vec![Some(wgpu::ColorTargetState { format:wgpu::TextureFormat::Rgba16Float, blend:None, write_mask:wgpu::ColorWrites::ALL })] }
                    else if normal { vec![Some(wgpu::ColorTargetState { format:wgpu::TextureFormat::Rgb10a2Unorm, blend:None, write_mask:wgpu::ColorWrites::ALL })] }
                    else { Vec::new() };
        let _pipeline=device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
            label:Some(label), layout:None,
            vertex:wgpu::VertexState { module:&shader, entry_point:Some("vertex"), compilation_options:Default::default(), buffers:&buffers },
            primitive:wgpu::PrimitiveState { cull_mode:Some(wgpu::Face::Back), ..Default::default() },
            depth_stencil:Some(wgpu::DepthStencilState { format:wgpu::TextureFormat::Depth32Float, depth_write_enabled:Some(true), depth_compare:Some(wgpu::CompareFunction::GreaterEqual), stencil:Default::default(), bias:Default::default() }),
            multisample:wgpu::MultisampleState::default(),
            fragment:Some(wgpu::FragmentState { module:&shader, entry_point:Some("fragment"), compilation_options:Default::default(), targets:&targets }),
            multiview_mask:None, cache:None,
        });
        let error=block_on(device.pop_error_scope()).map(|e|e.to_string());
        rows.push(json!({"permutation":label,"shader":row["export"],"shader_sha256":row["export_sha256"],"pipeline_created":error.is_none(),"validation_error":error,
                         "layout_policy":"automatic inference, not actual Bevy bind-group admission","sample_count":1,"depth_format":"Depth32Float","source_pipeline_state_qualified":false}));
    }
    let passed=rows.iter().filter(|r|r["pipeline_created"]==true).count(); let info=adapter.get_info();
    let result=json!({"status":if passed==rows.len(){"standalone_native_pipeline_admission_passed"}else{"standalone_native_pipeline_refusals_retained"},
                      "adapter":info.name,"backend":format!("{:?}",info.backend),"pipelines_created":passed,"rows":rows,
                      "actual_bevy_bind_group_admission_qualified":false,"source_pipeline_state_qualified":false,
                      "gpu_draw_executed":false,"source_unity_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
    fs::write(root.join("NATIVE-RESULT.v1.json"),serde_json::to_vec_pretty(&result).unwrap()).unwrap();
}
