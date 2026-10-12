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

fn near(a:Vec3,b:Vec3)->bool {a.to_array().into_iter().zip(b.to_array()).all(|(a,b)|(a-b).abs()<=1e-6*b.abs().max(1.0))}
fn v3(v:&Value)->Vec3 {Vec3::new(v[0].as_f64().unwrap() as f32,v[1].as_f64().unwrap() as f32,v[2].as_f64().unwrap() as f32)}
fn box_queries(case:&Value,glb:&SourceGraphGlb<'_>)->(usize,usize) {
 let native=case["boxes"].as_array().unwrap();let basis=Mat4::from_scale(Vec3::new(1.0,1.0,-1.0));
 if native.is_empty(){assert!(source_colliders::prepare(glb.document(),basis).is_err());return (0,0);}
 let mut shapes=source_colliders::prepare(glb.document(),basis).unwrap();assert_eq!(shapes.len(),native.len());let mut points=0;let mut rays=0;
 for (shape,n) in shapes.iter_mut().zip(native){
  assert_eq!(shape.guid,n["source_guid"]);assert_eq!(shape.file_id,n["source_file_id"]);
  for p in n["points"].as_array().unwrap(){assert!(near(shape.closest_point(basis.transform_point3(v3(&p["world_query"]))).unwrap(),basis.transform_point3(v3(&p["closest_world"]))));points+=1;}
  for r in n["rays"].as_array().unwrap(){let hit=shape.raycast(basis.transform_point3(v3(&r["origin"])),basis.transform_vector3(v3(&r["direction"])),r["max_distance"].as_f64().unwrap() as f32).unwrap();assert_eq!(hit.is_some(),r["hit"].as_bool().unwrap());if let Some(h)=hit{assert!((h.time_of_impact-r["distance"].as_f64().unwrap() as f32).abs()<=1e-6);assert!(near(h.normal,basis.transform_vector3(v3(&r["normal"]))));}rays+=1;}
  shape.enabled=false;let query=basis.transform_point3(v3(&n["disabled_query"]));assert_eq!(shape.closest_point(query).unwrap(),query);assert!(shape.raycast(query,Vec3::X,10.0).unwrap().is_none());
 }
 (points,rays)
}
fn backend_census(app:&mut App,entities:&[Entity],disabled:bool) -> usize {
 let world=app.world_mut();let mut query=world.query::<&RapierContextColliders>();let ctx=query.single(world).unwrap();let mut count=0;
 for entity in entities {
  if world.get::<Collider>(*entity).is_none(){assert!(world.get::<RapierColliderHandle>(*entity).is_none());continue;}
  let handle=world.get::<RapierColliderHandle>(*entity).expect("actual backend handle missing").0;
  let c=ctx.colliders.get(handle).expect("backend handle absent from ColliderSet");assert_eq!(ctx.collider_entity(handle),Some(*entity));assert_eq!(c.is_enabled(),!disabled);
  assert_eq!(c.is_sensor(),world.get::<Sensor>(*entity).is_some());assert!(c.parent().is_none());
  let pos=world.get::<GlobalTransform>(*entity).unwrap().translation();assert!(near(Vec3::from_array(c.translation().to_array()),pos));
  assert!((c.friction()-0.6).abs()<1e-6);assert_eq!(c.restitution(),0.0);count+=1;
 }
 assert_eq!(ctx.colliders.len(),count);count
}
fn backend_empty(app:&mut App){let world=app.world_mut();let mut q=world.query::<&RapierContextColliders>();let ctx=q.single(world).unwrap();assert_eq!(ctx.colliders.len(),0);assert!(ctx.entity2collider().is_empty());}
fn backend_rays(app:&mut App,case:&Value,root_pose:Transform,disabled:bool)->Value {
 let world=app.world_mut();let mut q=world.query::<RapierContext>();let item=q.single(world).unwrap();let ctx=RapierContext{simulation:item.simulation,colliders:item.colliders,joints:item.joints,rigidbody_set:item.rigidbody_set};let basis=Mat4::from_scale(Vec3::new(1.0,1.0,-1.0));let mut count=0;let mut failures=Vec::new();let mut presence=0;
 let references:Vec<&Value>=case["native_meshes"].as_array().unwrap().iter().chain(case["boxes"].as_array().unwrap()).collect();
 for native in references {for (index,r) in native["rays"].as_array().unwrap().iter().enumerate(){
  let origin=root_pose.to_matrix().transform_point3(basis.transform_point3(v3(&r["origin"])));let direction=root_pose.rotation*basis.transform_vector3(v3(&r["direction"]));let max=r["max_distance"].as_f64().unwrap() as f32*root_pose.scale.x;
  let actual=ctx.cast_ray_and_get_normal(origin,direction,max,true,QueryFilter::default());let expected=!disabled&&r["hit"].as_bool().unwrap();
  if actual.is_some()!=expected{presence+=1;failures.push(json!({"kind":"presence","sample":index,"actual":actual.is_some(),"expected":expected}));}
  if !disabled {if let Some((entity,hit))=actual{assert!(world.get::<source_collider_projection::SourceColliderIdentity>(entity).is_some());let distance=r["distance"].as_f64().unwrap() as f32*root_pose.scale.x;let normal=root_pose.rotation*basis.transform_vector3(v3(&r["normal"]));if (hit.time_of_impact-distance).abs()>1e-6*distance.abs().max(1.0)||!near(hit.normal,normal){failures.push(json!({"kind":"native_value","sample":index,"actual_distance":hit.time_of_impact,"expected_distance":distance,"actual_normal":hit.normal.to_array(),"expected_normal":normal.to_array()}));}}}
  count+=1;
 }}
 assert_eq!(presence,0,"live backend hit-presence differs from source/disabled state");json!({"count":count,"disabled":disabled,"failures":failures})
}
fn box_projection(app:&mut App,root:Entity,glb:&SourceGraphGlb<'_>,nodes:&BTreeMap<usize,Entity>)->Option<source_collider_projection::ColliderProjection>{
 if source_colliders::prepare(glb.document(),Mat4::from_scale(Vec3::new(1.0,1.0,-1.0))).is_err(){return None;}
 let boxes=source_colliders::prepare(glb.document(),Mat4::from_scale(Vec3::new(1.0,1.0,-1.0))).unwrap();
 let policies=boxes.iter().map(|s|((s.guid.clone(),s.file_id.clone()),source_collider_projection::ContactPolicy{friction:Friction{coefficient:0.6,combine_rule:CoefficientCombineRule::Average},restitution:Restitution{coefficient:0.0,combine_rule:CoefficientCombineRule::Average},contact_skin:ContactSkin(0.0),collision_groups:CollisionGroups::new(Group::ALL,Group::ALL),solver_groups:SolverGroups::new(Group::ALL,Group::ALL),evidence:"controlled staging: native material coefficients; no contact/filter equivalence".into()})).collect();
 Some(source_collider_projection::attach(app.world_mut(),root,glb.document(),Mat4::from_scale(Vec3::new(1.0,1.0,-1.0)),nodes,&policies).unwrap())
}
fn contact_probe(app:&mut App,case:&Value,pose:Transform)->Value {
 let reference=case["native_meshes"].as_array().unwrap().first().or_else(||case["boxes"].as_array().unwrap().first()).unwrap();
 let ray=reference["rays"].as_array().unwrap().iter().find(|r|r["hit"]==true).expect("native source must provide a hit for controlled contact");
 let basis=Mat4::from_scale(Vec3::new(1.0,1.0,-1.0));let origin=pose.to_matrix().transform_point3(basis.transform_point3(v3(&ray["origin"])));let direction=(pose.rotation*basis.transform_vector3(v3(&ray["direction"]))).normalize();
 let (target,hit)={let world=app.world_mut();let mut q=world.query::<RapierContext>();let item=q.single(world).unwrap();let ctx=RapierContext{simulation:item.simulation,colliders:item.colliders,joints:item.joints,rigidbody_set:item.rigidbody_set};ctx.cast_ray_and_get_normal(origin,direction,ray["max_distance"].as_f64().unwrap() as f32*pose.scale.x,true,QueryFilter::default()).expect("registered source contact ray must hit")};
 let target_handle=app.world().get::<RapierColliderHandle>(target).unwrap().0;let radius=0.003_f32;let gap=0.02_f32;let speed=0.2_f32;
 let start=origin+direction*hit.time_of_impact-direction*(gap+radius);let normal=hit.normal.normalize();let initial_normal_speed=-(direction*speed).dot(normal);assert!(initial_normal_speed>0.01);
 let mut modes=Vec::new();
 for mode in ["solid","disabled","sensor"] {
  {let mut entity=app.world_mut().entity_mut(target);entity.remove::<ColliderDisabled>();entity.remove::<Sensor>();if mode=="disabled"{entity.insert(ColliderDisabled);}if mode=="sensor"{entity.insert(Sensor);}}
  app.update();app.update();
  {let world=app.world_mut();let mut q=world.query::<&RapierContextColliders>();let ctx=q.single(world).unwrap();let c=ctx.colliders.get(target_handle).unwrap();assert_eq!(c.is_enabled(),mode!="disabled");assert_eq!(c.is_sensor(),mode=="sensor");}
  let probe=app.world_mut().spawn((RigidBody::Dynamic,Collider::ball(radius),Transform::from_translation(start),GlobalTransform::IDENTITY,GravityScale(0.0),Sleeping::disabled(),Ccd::enabled(),Velocity::linear(direction*speed),Friction{coefficient:0.0,combine_rule:CoefficientCombineRule::Min},Restitution{coefficient:0.0,combine_rule:CoefficientCombineRule::Min})).id();
  app.update();app.update();let probe_handle=app.world().get::<RapierColliderHandle>(probe).unwrap().0;let initial=app.world().get::<GlobalTransform>(probe).unwrap().translation();let mut contacts=0;let mut intersections=0;let mut min_normal_speed=initial_normal_speed;
  for _ in 0..180 {app.update();let world=app.world_mut();let mut q=world.query::<&RapierContextSimulation>();let sim=q.single(world).unwrap();if sim.narrow_phase.contact_pair(target_handle,probe_handle).is_some_and(|p|p.has_any_active_contact()){contacts+=1;}if sim.narrow_phase.intersection_pair(target_handle,probe_handle)==Some(true){intersections+=1;}let v=world.get::<Velocity>(probe).unwrap().linear;assert!(v.is_finite());min_normal_speed=min_normal_speed.min(-v.dot(normal));assert!(world.get::<GlobalTransform>(probe).unwrap().translation().is_finite());}
  let end=app.world().get::<GlobalTransform>(probe).unwrap().translation();let travel=(end-initial).dot(direction);let free_travel=speed*180.0/240.0;
  let result=json!({"mode":mode,"steps":180,"dt":1.0/240.0,"active_contact_frames":contacts,"intersection_frames":intersections,"initial_normal_speed":initial_normal_speed,"minimum_normal_speed":min_normal_speed,"travel":travel,"free_travel":free_travel});
  eprintln!("CONTACT {} {}",case["glb"],result);
  if mode=="solid"{assert!(contacts>0,"solid source must generate solver contacts");assert!(min_normal_speed<initial_normal_speed*0.5,"solid source must reduce incoming normal velocity");}
  else{assert_eq!(contacts,0,"disabled/sensor source must not create solver contacts");assert!((travel-free_travel).abs()<1e-4,"disabled/sensor source must preserve controlled free flight");if mode=="sensor"{assert!(intersections>0,"sensor source must generate intersections");}else{assert_eq!(intersections,0);}}
  app.world_mut().despawn(probe);app.update();app.update();
  {let world=app.world_mut();let mut q=world.query::<(&RapierContextColliders,&RapierRigidBodySet)>();let (c,b)=q.single(world).unwrap();assert_eq!(c.colliders.len(),1);assert_eq!(b.bodies.len(),0);assert!(b.entity2body().is_empty());}
  modes.push(result);
 }
 app.world_mut().entity_mut(target).remove::<ColliderDisabled>().remove::<Sensor>();app.update();app.update();
 json!({"glb":case["glb"],"target_source_identity":{"guid":reference["source_guid"],"file_id":reference["source_file_id"]},"radius":radius,"gap":gap,"speed":speed,"gravity_scale":0.0,"modes":modes,"controlled_contacts_verified":true,"native_simulation_equivalence":false})
}
fn main() {
    let cases:Vec<Value>=serde_json::from_slice(&std::fs::read(std::env::args().nth(1).unwrap()).unwrap()).unwrap();
    let mut rows=Vec::new();let mut assigned=0;let mut empty=0;let mut negatives=0;let mut box_points=0;let mut box_rays=0;let mut backend_registered=0;let mut backend_ray_results=Vec::new();let mut contact_results=Vec::new();
    for case in cases {
        let raw=std::fs::read(case["glb"].as_str().unwrap()).unwrap();
        assert_eq!(bytes_sha256(&raw),ContentHash::parse(case["glb_sha256"].as_str().unwrap()).unwrap());
        let glb=read_source_graph_glb(&raw).unwrap().unwrap();let recipes=cooking(&case);let policies=contacts(&glb,&recipes);
        let (p,r)=box_queries(&case,&glb);box_points+=p;box_rays+=r;let prepared=source_mesh_colliders::prepare(&glb,&recipes).unwrap();
        assert_eq!(prepared.len(),case["meshes"].as_array().unwrap().len());
        let mut app=App::new();app.add_plugins((MinimalPlugins,TransformPlugin,RapierPhysicsPlugin::<NoUserData>::default()));app.insert_resource(TimestepMode::Fixed{dt:1.0/240.0,substeps:1});app.finish();app.cleanup();app.update();
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
        assert_eq!(projection.entities.len(),prepared.len());app.update();app.update();backend_registered+=backend_census(&mut app,&projection.entities,false);if !case["native_meshes"].as_array().unwrap().is_empty(){backend_ray_results.push(backend_rays(&mut app,&case,root_pose,false));contact_results.push(contact_probe(&mut app,&case,root_pose));std::fs::write("CONTACT-PARTIAL.v1.json",serde_json::to_vec_pretty(&contact_results).unwrap()).unwrap();}
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
        projection.clear(app.world_mut());app.update();backend_empty(&mut app);assert_eq!(app.world().entities().len(),before);
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
        for entity in &projection.entities {assert!(app.world().get::<ColliderDisabled>(*entity).is_some());assert!(app.world().get::<Sensor>(*entity).is_some());}app.update();app.update();backend_census(&mut app,&projection.entities,true);if !case["native_meshes"].as_array().unwrap().is_empty(){backend_ray_results.push(backend_rays(&mut app,&case,root_pose,true));}
        projection.clear(app.world_mut());app.update();backend_empty(&mut app);assert_eq!(app.world().entities().len(),before);
        if !case["boxes"].as_array().unwrap().is_empty(){let boxes=box_projection(&mut app,root,&glb,&nodes).unwrap();app.update();app.update();backend_registered+=backend_census(&mut app,&boxes.entities,false);backend_ray_results.push(backend_rays(&mut app,&case,root_pose,false));contact_results.push(contact_probe(&mut app,&case,root_pose));std::fs::write("CONTACT-PARTIAL.v1.json",serde_json::to_vec_pretty(&contact_results).unwrap()).unwrap();for entity in &boxes.entities{app.world_mut().entity_mut(*entity).insert((ColliderDisabled,Sensor));}app.update();app.update();backend_census(&mut app,&boxes.entities,true);backend_ray_results.push(backend_rays(&mut app,&case,root_pose,true));boxes.clear(app.world_mut());app.update();backend_empty(&mut app);assert_eq!(app.world().entities().len(),before);}assigned+=physical;empty+=nulls;
        rows.push(json!({"glb":case["glb"],"assigned":physical,"empty_observations":nulls,"identity_and_hierarchy_verified":true,"activation_trigger_clear_reimport_verified":true,"simulation_qualified":false}));
    }
    assert_eq!(rows.len(),24);assert_eq!(assigned,19);assert_eq!(empty,2);assert_eq!(box_points,63);assert_eq!(box_rays,18);assert_eq!(backend_registered,22);assert_eq!(contact_results.len(),22);
    println!("{}",json!({"rows":rows,"carriers":24,"assigned_mesh_colliders":assigned,"empty_source_meshes":empty,"negative_controls":negatives,"native_box_points":box_points,"native_box_rays":box_rays,"common_mesh_projection_verified":true,"backend_registration_verified":true,"controlled_contacts_verified":true,"contact_cases":contact_results.len(),"contact_results":contact_results,"backend_registered":backend_registered,"backend_ray_results":backend_ray_results,"native_query_equivalence":false,"physics_simulation_qualified":false,"studio_verified":false,"full_import":false}));
}
