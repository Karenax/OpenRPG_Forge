"""Measure all four Bevy groups; V2 only admitted the material group."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
rust = (S / 'candidates/SourceTextureLayoutAdmissionV2.rs').read_text(encoding='utf-8')
rust = rust.replace('use forge_engine::source_graph::SourceGraphMaterial;',
    'use forge_engine::source_graph::SourceGraphMaterial;\nuse bevy::pbr::{MeshPipeline,MeshPipelineViewLayoutKey};')
assert rust.count('app.finish();app.cleanup();') == 1
rust = rust.replace('app.finish();app.cleanup();', 'app.finish();app.cleanup();app.update();')
anchor = ' std::fs::write(path,serde_json::to_vec_pretty(&result)?)?;Ok(())'
assert rust.count(anchor) == 1
block = '''
 let pipeline=rw.get_resource::<MeshPipeline>().ok_or("Actual Bevy MeshPipeline absent")?;
 let standard_limits=bevy::render::settings::WgpuLimits::default();
 let mut cases=Vec::new();
 for (name,key) in [
  ("static_forward",MeshPipelineViewLayoutKey::empty()),
  ("depth_ssao_tonemap",MeshPipelineViewLayoutKey::DEPTH_PREPASS|MeshPipelineViewLayoutKey::SCREEN_SPACE_AMBIENT_OCCLUSION|MeshPipelineViewLayoutKey::TONEMAP_IN_SHADER),
  ("depth_normal_motion_ssao_environment_tonemap",MeshPipelineViewLayoutKey::DEPTH_PREPASS|MeshPipelineViewLayoutKey::NORMAL_PREPASS|MeshPipelineViewLayoutKey::MOTION_VECTOR_PREPASS|MeshPipelineViewLayoutKey::SCREEN_SPACE_AMBIENT_OCCLUSION|MeshPipelineViewLayoutKey::ENVIRONMENT_MAP|MeshPipelineViewLayoutKey::TONEMAP_IN_SHADER)
 ] {
  let view=pipeline.get_view_layout(key);
  let base_groups=[&view.main_layout.entries,&view.binding_array_layout.entries,&pipeline.mesh_layouts.model_only.entries];
  let mut totals=Vec::new();
  for (label,material) in [("existing",&entries),("ten_textures",&proposed)] {
   let groups=[base_groups[0].as_slice(),base_groups[1].as_slice(),base_groups[2].as_slice(),material.as_slice()];
   let all=groups.iter().flat_map(|group|group.iter().cloned()).collect::<Vec<_>>();
   let fragment=counts(&all,ShaderStages::FRAGMENT);let vertex=counts(&all,ShaderStages::VERTEX);
   let baseline_webgpu_limits_admitted=[&fragment,&vertex].iter().all(|stage|stage["sampled_textures"].as_u64().unwrap()<=u64::from(standard_limits.max_sampled_textures_per_shader_stage)&&stage["samplers"].as_u64().unwrap()<=u64::from(standard_limits.max_samplers_per_shader_stage));
   let layouts=groups.iter().enumerate().map(|(i,group)|device.create_bind_group_layout(Some(format!("{name}-{label}-group{i}").as_str()),group)).collect::<Vec<_>>();
   let references=layouts.iter().map(|l|Some(&**l)).collect::<Vec<_>>();
   let _layout=device.create_pipeline_layout(&PipelineLayoutDescriptor{label:Some("actual-bevy-four-groups-source-material"),bind_group_layouts:&references,immediate_size:0});
   totals.push(serde_json::json!({"material":label,"fragment":fragment,"vertex":vertex,"baseline_webgpu_texture_sampler_limits_admitted":baseline_webgpu_limits_admitted,"native_four_group_pipeline_layout_created":true}));
  }
  cases.push(serde_json::json!({"view_profile":name,"key_bits":key.bits(),"groups_before_material":base_groups.iter().map(|g|g.iter().map(|e|e.binding).collect::<Vec<_>>()).collect::<Vec<_>>(),"results":totals}));
 }
 result["four_group_cases"]=cases.into();
 result["baseline_webgpu_sampled_texture_limit"]=standard_limits.max_sampled_textures_per_shader_stage.into();
 result["baseline_webgpu_sampler_limit"]=standard_limits.max_samplers_per_shader_stage.into();
 result["native_four_group_pipeline_layouts_created"]=true.into();
 result["status"]="actual_bevy_four_group_layouts_measured_and_created_render_pipeline_pending".into();
'''
# The public wgpu descriptor is imported explicitly to avoid Bevy's material descriptor.
rust = rust.replace('use std::{', 'use bevy::render::render_resource::PipelineLayoutDescriptor;\nuse std::{')
rust = rust.replace(anchor, block + anchor)
candidate = S / 'candidates/SourceTextureLayoutAdmissionV3.rs'
assert not candidate.exists()
candidate.write_text(rust, encoding='utf-8')
controller = (S / 'check_source_texture_layout_admission_v2.py').read_text(encoding='utf-8')
controller = controller.replace('source-texture-layout-admission-v2','source-texture-layout-admission-v3').replace('SourceTextureLayoutAdmissionV2.rs','SourceTextureLayoutAdmissionV3.rs')
controller = controller.replace('assert native["baseline_layout_created"] and native["proposed_layout_created"]',
    'assert native["baseline_layout_created"] and native["proposed_layout_created"] and native["native_four_group_pipeline_layouts_created"]')
controller = controller.replace('actual_common_and_proposed_ten_texture_native_layouts_created_inputs_unchanged',
    'actual_bevy_four_group_layouts_created_requirements_measured_inputs_unchanged')
path = S / 'check_source_texture_layout_admission_v3.py'
assert not path.exists()
path.write_text(controller, encoding='utf-8')
subprocess.run([sys.executable,'-B',str(path)],check=True)
