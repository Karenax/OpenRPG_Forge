mod source_depth_stage {
 use bevy::{prelude::*,render::{render_resource::*,renderer::{RenderDevice,RenderContext,ViewQuery},RenderApp},core_pipeline::{schedule::Core3d,Core3dSystems},pbr::{ViewShadowBindings,per_view_shadow_pass,LATE_SHADOW_PASS}};
 use std::sync::atomic::{AtomicU64,Ordering};
 #[derive(Resource)] pub struct Probe {pub staging:Buffer,output:Buffer,pipeline:ComputePipeline,pub submissions:AtomicU64}
 fn copy_after_shadow(view:ViewQuery<&ViewShadowBindings>,device:Res<RenderDevice>,probe:Res<Probe>,mut ctx:RenderContext){
  let bindings=view.into_inner();let texture=&bindings.directional_light_depth_texture;
  assert_eq!((texture.width(),texture.height(),texture.depth_or_array_layers()),(512,512,4));
  let atlas=texture.create_view(&TextureViewDescriptor{dimension:Some(TextureViewDimension::D2Array),..default()});let layout=probe.pipeline.get_bind_group_layout(0);
  let group=device.wgpu_device().create_bind_group(&BindGroupDescriptor{label:Some("shadow-depth-stage-bindings"),layout:&layout,entries:&[BindGroupEntry{binding:0,resource:BindingResource::TextureView(&atlas)},BindGroupEntry{binding:1,resource:probe.output.as_entire_binding()}]});
  let encoder=ctx.command_encoder();{let mut pass=encoder.begin_compute_pass(&ComputePassDescriptor{label:Some("shadow-depth-copy-after-shadow-pass"),timestamp_writes:None});pass.set_pipeline(&probe.pipeline);pass.set_bind_group(0,&group,&[]);pass.dispatch_workgroups(64,64,4);}
  encoder.copy_buffer_to_buffer(&probe.output,0,&probe.staging,0,4194304);probe.submissions.fetch_add(1,Ordering::SeqCst);
 }
 pub fn install(app:&mut App,path:&std::path::Path){
  let render=app.get_sub_app_mut(RenderApp).unwrap();let device=render.world().resource::<RenderDevice>();
  let shader=device.create_and_validate_shader_module(ShaderModuleDescriptor{label:Some("source-shadow-depth-stage"),source:ShaderSource::Wgsl(std::fs::read_to_string(path).unwrap().into())});
  let pipeline=device.create_compute_pipeline(&RawComputePipelineDescriptor{label:Some("source-shadow-depth-stage"),layout:None,module:&shader,entry_point:Some("main"),compilation_options:default(),cache:None});
  let output=device.create_buffer(&BufferDescriptor{label:Some("source-shadow-depth-stage-output"),size:4194304,usage:BufferUsages::STORAGE|BufferUsages::COPY_SRC,mapped_at_creation:false});
  let staging=device.create_buffer(&BufferDescriptor{label:Some("source-shadow-depth-stage-readback"),size:4194304,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
  render.world_mut().insert_resource(Probe{staging,output,pipeline,submissions:AtomicU64::new(0)});
  render.add_systems(Core3d,copy_after_shadow.after(per_view_shadow_pass::<LATE_SHADOW_PASS>).before(Core3dSystems::MainPass));
 }
}
