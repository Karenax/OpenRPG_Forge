#![allow(dead_code)]
use bevy::prelude::*;
use bevy_rapier3d::prelude::*;
use forge_assets::source_graph_glb::{read_source_graph_glb, SourceGraphGlb};
use forge_core::{bytes_sha256, ContentHash};
use serde_json::{Value, json};
use std::collections::BTreeMap;
#[path="@@ROOT@@/crates/forge_engine/src/source_colliders.rs"] mod source_colliders;
#[path="@@ROOT@@/crates/forge_engine/src/source_mesh_colliders.rs"] mod source_mesh_colliders;
#[path="@@ROOT@@/crates/forge_engine/src/source_collider_projection.rs"] mod source_collider_projection;
#[path="@@ROOT@@/crates/forge_engine/src/source_mesh_collider_projection.rs"] mod source_mesh_collider_projection;
#[path="@@ROOT@@/crates/forge_engine/src/source_graph_scene.rs"] mod source_graph_scene;

fn altered(raw: &[u8], doc: &Value) -> Vec<u8> {
    let old_len = u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
    let mut json = serde_json::to_vec(doc).unwrap();
    while json.len()%4 != 0 { json.push(b' '); }
    let mut out = b"glTF".to_vec(); out.extend(2_u32.to_le_bytes());
    out.extend(u32::try_from(20+json.len()+raw.len()-20-old_len).unwrap().to_le_bytes());
    out.extend(u32::try_from(json.len()).unwrap().to_le_bytes()); out.extend(b"JSON");
    out.extend(json); out.extend(&raw[20+old_len..]); out
}

fn cooking(case: &Value) -> BTreeMap<(String,String),source_mesh_colliders::MeshCookingPolicy> {
    use source_mesh_colliders::{MeshCookingPolicy,MeshShapeInput};
    let mut out=BTreeMap::new();
    for row in case["meshes"].as_array().unwrap() {
        if row["kind"] == "unassigned_mesh" { continue; }
        let r=&row["source_mesh_reference"];
        let shape=if row["source_convex"].as_bool().unwrap() {
            let candidate=&row["candidate"];
            let raw=std::fs::read(candidate["target_vertices"].as_str().unwrap()).unwrap();
            let pin=ContentHash::parse(candidate["target_vertices_sha256"].as_str().unwrap()).unwrap();
            assert_eq!(bytes_sha256(&raw),pin); assert_eq!(raw.len()%12,0);
            let positions=raw.chunks_exact(12).map(|b| std::array::from_fn(|i| f32::from_le_bytes(b[i*4..i*4+4].try_into().unwrap()))).collect();
            MeshShapeInput::NativePlaneCandidate {positions,sha256:pin}
        } else { MeshShapeInput::SourceTriangles };
        let key=(row["source_component"]["guid"].as_str().unwrap().into(),row["source_component"]["file_id"].as_str().unwrap().into());
        out.insert(key,MeshCookingPolicy {mesh_guid:r["guid"].as_str().unwrap().into(),mesh_file_id:r["file_id"].as_str().unwrap().into(),
            source_cooking_options:row["source_cooking_options"].as_f64().unwrap() as u32,
            evidence:case["cooking_evidence"].as_str().unwrap().into(),shape});
    }
    out
}

fn contacts(glb: &SourceGraphGlb<'_>, recipes: &BTreeMap<(String,String),source_mesh_colliders::MeshCookingPolicy>) -> BTreeMap<(String,String),source_collider_projection::ContactPolicy> {
    source_mesh_colliders::prepare(glb,recipes).unwrap().into_iter().filter(|s|s.collider.is_some()).map(|s| ((s.guid,s.file_id),source_collider_projection::ContactPolicy {
        friction:Friction {coefficient:0.6,combine_rule:CoefficientCombineRule::Average},
        restitution:Restitution {coefficient:0.0,combine_rule:CoefficientCombineRule::Average},
        contact_skin:ContactSkin(0.0),collision_groups:CollisionGroups::new(Group::ALL,Group::ALL),solver_groups:SolverGroups::new(Group::ALL,Group::ALL),
        evidence:"controlled staging only: native friction/restitution; skin zero and all groups; no contact/filter equivalence claimed".into()
    })).collect()
}

fn main() {
    let cases:Vec<Value>=serde_json::from_slice(&std::fs::read(std::env::args().nth(1).unwrap()).unwrap()).unwrap();
    let mut rows=Vec::new();let mut assigned=0;let mut empty=0;let mut negatives=0;
    for case in cases {
        let raw=std::fs::read(case["glb"].as_str().unwrap()).unwrap();
        assert_eq!(bytes_sha256(&raw),ContentHash::parse(case["glb_sha256"].as_str().unwrap()).unwrap());
        let glb=read_source_graph_glb(&raw).unwrap().unwrap();let recipes=cooking(&case);let policies=contacts(&glb,&recipes);
        let prepared=source_mesh_colliders::prepare(&glb,&recipes).unwrap();
        assert_eq!(prepared.len(),case["meshes"].as_array().unwrap().len());
        let mut app=App::new();app.add_plugins((MinimalPlugins,TransformPlugin));
        let root_pose=Transform::from_xyz(4.0,2.0,-3.0).with_rotation(Quat::from_rotation_y(0.3)).with_scale(Vec3::splat(1.7));
        let root=app.world_mut().spawn((root_pose,GlobalTransform::IDENTITY,Visibility::Hidden)).id();
        let scene=source_graph_scene::read_source_graph_scene(&glb,None).unwrap();
        let nodes=source_graph_scene::spawn_source_graph_hierarchy(&scene,app.world_mut(),root).unwrap();
        let before=app.world().entities().len();
        if !recipes.is_empty() {
            let mut missing=contacts(&glb,&recipes);missing.pop_first();
            assert!(source_mesh_collider_projection::attach(app.world_mut(),root,&glb,&nodes,&recipes,&missing).is_err());
            assert_eq!(app.world().entities().len(),before); negatives+=1;
            let key=recipes.first_key_value().unwrap().0.clone();let mut bad=cooking(&case);bad.get_mut(&key).unwrap().source_cooking_options+=1;
            assert!(source_mesh_colliders::prepare(&glb,&bad).is_err());negatives+=1;
            let mut bad=cooking(&case);bad.get_mut(&key).unwrap().mesh_file_id="1".into();assert!(source_mesh_colliders::prepare(&glb,&bad).is_err());negatives+=1;
            let mut bad=cooking(&case);
            if let source_mesh_colliders::MeshShapeInput::NativePlaneCandidate{sha256,..}=&mut bad.get_mut(&key).unwrap().shape { *sha256=ContentHash::from_bytes([0;32]);assert!(source_mesh_colliders::prepare(&glb,&bad).is_err());negatives+=1; }
        }
        let projection=source_mesh_collider_projection::attach(app.world_mut(),root,&glb,&nodes,&recipes,&policies).unwrap();
        assert_eq!(projection.entities.len(),prepared.len());app.update();
        let mut physical=0;let mut nulls=0;
        for (s,entity) in prepared.iter().zip(&projection.entities) {
            let identity=app.world().get::<source_collider_projection::SourceColliderIdentity>(*entity).unwrap();
            assert_eq!((&identity.guid,&identity.file_id),(&s.guid,&s.file_id));assert_eq!(identity.original,s.original);
            assert_eq!(app.world().get::<ChildOf>(*entity).unwrap().parent(),nodes[&s.node]);
            assert_eq!(app.world().get::<Collider>(*entity).is_some(),s.collider.is_some());
            assert_eq!(app.world().get::<ColliderDisabled>(*entity).is_some(),!s.enabled);
            assert_eq!(app.world().get::<Sensor>(*entity).is_some(),s.is_trigger);
            let metadata=app.world().get::<source_mesh_collider_projection::SourceMeshCooking>(*entity).unwrap();
            assert_eq!(metadata.source_cooking_options,s.source_cooking_options);assert_eq!(metadata.source_convex,s.source_convex);
            let actual=app.world().get::<GlobalTransform>(*entity).unwrap().to_matrix();
            let parent=app.world().get::<GlobalTransform>(nodes[&s.node]).unwrap().to_matrix();
            assert!(actual.to_cols_array().iter().zip(parent.to_cols_array()).all(|(a,b)|(a-b).abs()<1e-6));
            assert!(actual.w_axis.truncate().distance(Vec3::ZERO)>1.0);
            if s.collider.is_some(){physical+=1;}else{nulls+=1;assert!(metadata.geometry_evidence.is_none());}
        }
        assert!(source_mesh_collider_projection::attach(app.world_mut(),root,&glb,&nodes,&recipes,&policies).is_err());negatives+=1;
        projection.clear(app.world_mut());assert_eq!(app.world().entities().len(),before);
        let mut doc=glb.document().clone();
        for node in doc["nodes"].as_array_mut().unwrap() {
            if let Some(obs)=node.pointer_mut("/extras/unity_source_node/native_collider_observations").and_then(Value::as_array_mut) {
                for c in obs { if c["identity"]["type"]=="UnityEngine.MeshCollider" {
                    for p in c["serialized"].as_array_mut().unwrap() { if p["property_path"]=="m_Enabled"{p["boolean"]=json!(false);}if p["property_path"]=="m_IsTrigger"{p["boolean"]=json!(true);} }
                }}
            }
        }
        let disabled_raw=altered(&raw,&doc);let disabled=read_source_graph_glb(&disabled_raw).unwrap().unwrap();
        let projection=source_mesh_collider_projection::attach(app.world_mut(),root,&disabled,&nodes,&recipes,&policies).unwrap();
        for entity in &projection.entities {assert!(app.world().get::<ColliderDisabled>(*entity).is_some());assert!(app.world().get::<Sensor>(*entity).is_some());}
        projection.clear(app.world_mut());assert_eq!(app.world().entities().len(),before);
        assigned+=physical;empty+=nulls;
        rows.push(json!({"glb":case["glb"],"assigned":physical,"empty_observations":nulls,"identity_and_hierarchy_verified":true,"activation_trigger_clear_reimport_verified":true,"simulation_qualified":false}));
    }
    assert_eq!(rows.len(),24);assert_eq!(assigned,19);assert_eq!(empty,2);
    println!("{}",json!({"rows":rows,"carriers":24,"assigned_mesh_colliders":assigned,"empty_source_meshes":empty,"negative_controls":negatives,"common_mesh_projection_verified":true,"native_query_equivalence":false,"physics_simulation_qualified":false,"studio_verified":false,"full_import":false}));
}
