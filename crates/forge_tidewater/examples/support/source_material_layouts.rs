use bevy::pbr::{MeshPipeline, MeshPipelineViewLayoutKey};
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{
            AsBindGroup, BindGroupLayoutEntry, ColorTargetState, ColorWrites, CompareFunction,
            DepthStencilState, Face, PipelineLayoutDescriptor, PollType, PrimitiveState,
            RawFragmentState, RawRenderPipelineDescriptor, RawVertexBufferLayout, RawVertexState,
            ShaderModuleDescriptor, ShaderSource, TextureFormat, VertexAttribute, VertexFormat,
            VertexStepMode,
        },
        renderer::{RenderAdapterInfo, RenderDevice},
        settings::{Backends, RenderCreation, WgpuSettings},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::source_graph::SourceGraphMaterial;
use std::{
    path::Path,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

pub fn main() -> Result<(), Box<dyn std::error::Error>> {
    let root = std::env::args().nth(1).unwrap();
    let root = Path::new(&root);
    let inputs: serde_json::Value =
        serde_json::from_slice(&std::fs::read(root.join("INPUT.v1.json"))?)?;
    let app = initialize_gpu()?;
    let world = app.get_sub_app(RenderApp).unwrap().world();
    let device = world.resource::<RenderDevice>();
    let info = world.resource::<RenderAdapterInfo>();
    let mesh_pipeline = world.resource::<MeshPipeline>();
    let material = SourceGraphMaterial::bind_group_layout_entries(device, true);
    for binding in [
        100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117,
        118, 125, 126,
    ] {
        assert!(
            material.iter().any(|e| e.binding == binding),
            "Installed common material binding missing: {binding}"
        );
    }
    let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
    let sink = errors.clone();
    device
        .wgpu_device()
        .on_uncaptured_error(Arc::new(move |error| {
            sink.lock().unwrap().push(format!("{error}"));
        }));
    let buffers = [RawVertexBufferLayout {
        array_stride: 64,
        step_mode: VertexStepMode::Vertex,
        attributes: &ATTRIBUTES,
    }];
    let mut rows = Vec::new();
    for row in inputs["cases"].as_array().unwrap() {
        rows.push(inspect_case(
            device,
            mesh_pipeline,
            &material,
            &buffers,
            &errors,
            row,
        )?);
        std::fs::write(
            root.join("NATIVE-PARTIAL.v1.json"),
            serde_json::to_vec_pretty(&rows)?,
        )?;
    }
    let passed = rows
        .iter()
        .filter(|r| r["pipeline_created"] == true)
        .count();
    let result = serde_json::json!({"status":if passed==7{"actual_bevy_four_group_material_pipelines_created"}else{"actual_bevy_pipeline_refusals_retained"},
        "adapter":info.name,"backend":format!("{:?}",info.backend),"pipelines_created":passed,"rows":rows,
        "layout_policy":"Installed SourceGraphMaterial plus actual MeshPipeline view/model layouts; no inferred layout",
        "actual_resource_bind_groups_created":false,"source_pipeline_state_qualified":false,"gpu_draw_executed":false,
        "source_unity_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    Ok(())
}

const ATTRIBUTES: [VertexAttribute; 5] = [
    VertexAttribute {
        format: VertexFormat::Float32x3,
        offset: 0,
        shader_location: 0,
    },
    VertexAttribute {
        format: VertexFormat::Float32x2,
        offset: 12,
        shader_location: 2,
    },
    VertexAttribute {
        format: VertexFormat::Float32x3,
        offset: 20,
        shader_location: 8,
    },
    VertexAttribute {
        format: VertexFormat::Float32x4,
        offset: 32,
        shader_location: 9,
    },
    VertexAttribute {
        format: VertexFormat::Float32x4,
        offset: 48,
        shader_location: 10,
    },
];

fn initialize_gpu() -> Result<App, Box<dyn std::error::Error>> {
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Some(Backends::DX12),
                    force_fallback_adapter: false,
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.update();
    Ok(app)
}

fn view_layout_key(row: &serde_json::Value) -> MeshPipelineViewLayoutKey {
    let mut key = MeshPipelineViewLayoutKey::empty();
    if row["shader_defines"]
        .as_array()
        .unwrap()
        .iter()
        .any(|v| v == "ENVIRONMENT_MAP")
    {
        key |= MeshPipelineViewLayoutKey::ENVIRONMENT_MAP;
    }
    if row["shader_defines"]
        .as_array()
        .unwrap()
        .iter()
        .any(|v| v == "SCREEN_SPACE_AMBIENT_OCCLUSION")
    {
        key |= MeshPipelineViewLayoutKey::SCREEN_SPACE_AMBIENT_OCCLUSION;
    }
    if row["shader_defines"]
        .as_array()
        .unwrap()
        .iter()
        .any(|v| v == "DEPTH_PREPASS")
    {
        key |= MeshPipelineViewLayoutKey::DEPTH_PREPASS;
    }
    if row["shader_defines"]
        .as_array()
        .unwrap()
        .iter()
        .any(|v| v == "NORMAL_PREPASS")
    {
        key |= MeshPipelineViewLayoutKey::NORMAL_PREPASS;
    }
    key
}

fn inspect_case(
    device: &RenderDevice,
    mesh_pipeline: &MeshPipeline,
    material: &[BindGroupLayoutEntry],
    buffers: &[RawVertexBufferLayout<'_>],
    errors: &Arc<Mutex<Vec<String>>>,
    row: &serde_json::Value,
) -> Result<serde_json::Value, Box<dyn std::error::Error>> {
    errors.lock().unwrap().clear();
    let name = row["permutation"].as_str().unwrap();
    let key = view_layout_key(row);
    let view = mesh_pipeline.get_view_layout(key);
    let groups = [
        view.main_layout.entries.as_slice(),
        view.binding_array_layout.entries.as_slice(),
        mesh_pipeline.mesh_layouts.model_only.entries.as_slice(),
        material,
    ];
    let layouts = groups
        .iter()
        .enumerate()
        .map(|(i, g)| {
            device.create_bind_group_layout(Some(format!("{name}-actual-group-{i}").as_str()), g)
        })
        .collect::<Vec<_>>();
    let refs = layouts.iter().map(|l| Some(&**l)).collect::<Vec<_>>();
    let layout = device.create_pipeline_layout(&PipelineLayoutDescriptor {
        label: Some(name),
        bind_group_layouts: &refs,
        immediate_size: 0,
    });
    let shader = device
        .wgpu_device()
        .create_shader_module(ShaderModuleDescriptor {
            label: Some(name),
            source: ShaderSource::Wgsl(
                std::fs::read_to_string(row["export"].as_str().unwrap())?.into(),
            ),
        });
    let forward = row["shader_stage"] == "forward";
    let normal = name == "normal_depth" || name == "ortho_normal_depth";
    let targets = if forward {
        vec![Some(ColorTargetState {
            format: TextureFormat::Rgba16Float,
            blend: None,
            write_mask: ColorWrites::ALL,
        })]
    } else if normal {
        vec![Some(ColorTargetState {
            format: bevy::core_pipeline::prepass::NORMAL_PREPASS_FORMAT,
            blend: None,
            write_mask: ColorWrites::ALL,
        })]
    } else {
        Vec::new()
    };
    let _pipeline = device
        .wgpu_device()
        .create_render_pipeline(&RawRenderPipelineDescriptor {
            label: Some(name),
            layout: Some(&layout),
            vertex: RawVertexState {
                module: &shader,
                entry_point: Some("vertex"),
                compilation_options: default(),
                buffers,
            },
            primitive: PrimitiveState {
                cull_mode: Some(Face::Back),
                ..default()
            },
            depth_stencil: Some(DepthStencilState {
                format: TextureFormat::Depth32Float,
                depth_write_enabled: Some(true),
                depth_compare: Some(CompareFunction::GreaterEqual),
                stencil: default(),
                bias: default(),
            }),
            multisample: default(),
            fragment: Some(RawFragmentState {
                module: &shader,
                entry_point: Some("fragment"),
                compilation_options: default(),
                targets: &targets,
            }),
            multiview_mask: None,
            cache: None,
        });
    device.wgpu_device().poll(PollType::Poll)?;
    let captured = errors.lock().unwrap().clone();
    Ok(
        serde_json::json!({"permutation":name,"shader_sha256":row["export_sha256"],"view_layout_key_bits":key.bits(),
            "actual_group_binding_ids":groups.iter().map(|g|g.iter().map(|e|e.binding).collect::<Vec<_>>()).collect::<Vec<_>>(),
            "pipeline_created":captured.is_empty(),"validation_errors":captured,"source_pipeline_state_qualified":false}),
    )
}
