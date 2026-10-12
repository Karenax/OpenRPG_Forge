#![allow(dead_code)]
use forge_assets::source_physics_transport::PhysicsTransportComponent;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::{Value, json};
#[path="@@ROOT@@/crates/forge_engine/src/source_colliders.rs"] mod source_colliders;
#[path="@@ROOT@@/crates/forge_engine/src/source_collider_projection.rs"] mod source_collider_projection;
#[path="@@ROOT@@/crates/forge_engine/src/source_graph_scene.rs"] mod source_graph_scene;
#[path="@@ROOT@@/crates/forge_engine/src/source_physics_contacts.rs"] mod source_physics_contacts;

fn copy_component(c: &PhysicsTransportComponent) -> PhysicsTransportComponent {
    PhysicsTransportComponent { source_node:c.source_node, original:c.original.clone(), native_metrics:c.native_metrics.clone(), candidate_positions:c.candidate_positions.clone(), candidate_sha256:c.candidate_sha256, evidence:c.evidence.clone() }
}
fn main() {
    use source_physics_contacts::{ContactSceneSettings, contacts_from_transport};
    let work=std::path::Path::new("F:/OpenRPG_ImportWork");
    let rows:Vec<Value>=serde_json::from_slice(&std::fs::read(work.join("physics-package-v727/PACKAGES.v1.json")).unwrap()).unwrap();
    assert_eq!(rows.len(),24);
    let mut policies=0;let mut empty=0;let mut negatives=0;
    for row in rows {
        let carrier=std::fs::read(work.join(format!("house-runtime-state-glb-v675/{}.native-state.glb",row["prefab_guid"].as_str().unwrap()))).unwrap();
        let expected=ContentHash::parse(row["carrier_sha256"].as_str().unwrap()).unwrap();assert_eq!(bytes_sha256(&carrier),expected);
        let wire=std::fs::read(work.join("physics-package-v727").join(row["file"].as_str().unwrap())).unwrap();assert_eq!(bytes_sha256(&wire).to_string(),row["sha256"].as_str().unwrap());
        let pin=ContentHash::parse(row["manifest_sha256"].as_str().unwrap()).unwrap();
        let package=forge_assets::source_physics_package::SourcePhysicsPackage::decode(&wire,expected,pin).unwrap();
        let glb=forge_assets::source_graph_glb::read_source_graph_glb_with_budget(&carrier,forge_assets::source_graph_glb::SourceGraphGlbReadBudget::LargeDesktop).unwrap().unwrap();
        let components=forge_assets::source_physics_transport::read_source_physics_transport(&package.manifest,pin,expected,&glb,|path|package.candidates.get(path).cloned().ok_or("absent".into())).unwrap();
        let settings=ContactSceneSettings{layer_matrix:[u32::MAX;32],contact_skin_m:0.,evidence:"explicit controlled target scene; acquisition material measurements; native equivalence unqualified".into()};
        let contacts=contacts_from_transport(&components,&settings).unwrap();
        for contact in contacts.values() {
            assert_eq!(contact.friction.coefficient,0.6);assert_eq!(contact.restitution.coefficient,0.);
            assert_eq!(contact.collision_groups.memberships.bits(),1);assert_eq!(contact.collision_groups.filters.bits(),u32::MAX);
            assert_eq!(contact.solver_groups.memberships.bits(),1);assert_eq!(contact.contact_skin.0,0.);
        }
        policies+=contacts.len();if contacts.is_empty(){empty+=1;}
        let mut explicit=settings.clone();explicit.contact_skin_m=0.025;
        for contact in contacts_from_transport(&components,&explicit).unwrap().values(){assert_eq!(contact.contact_skin.0,0.025);}
        let mut bad=settings.clone();bad.evidence.clear();assert!(contacts_from_transport(&components,&bad).is_err());negatives+=1;
        bad=settings.clone();bad.contact_skin_m=-1.;assert!(contacts_from_transport(&components,&bad).is_err());negatives+=1;
        bad=settings.clone();bad.layer_matrix[1]&=!1;assert!(contacts_from_transport(&components,&bad).is_err());negatives+=1;
        for index in 0..components.len() {
            if components[index].native_metrics.is_none(){continue;}
            for fault in 0..7 {
                let mut altered:Vec<_>=components.iter().map(copy_component).collect();
                let target=&mut altered[index];
                match fault {
                    0=>target.native_metrics.as_mut().unwrap()["implicit_material"]["bounciness"]=json!(2.0),
                    1=>target.native_metrics.as_mut().unwrap()["implicit_material"]["static_friction"]=json!(0.5),
                    2=>target.native_metrics.as_mut().unwrap()["implicit_material"]["friction_combine"]=json!("Multiply"),
                    3=>{let p=target.original["serialized"].as_array_mut().unwrap().iter_mut().find(|p|p["property_path"]=="m_IncludeLayers.m_Bits").unwrap();p["number"]=json!(1.0);},
                    4=>target.native_metrics.as_mut().unwrap()["game_object_layer"]=json!(32),
                    5=>{let p=target.original["serialized"].as_array_mut().unwrap().iter_mut().find(|p|p["property_path"]=="m_Material").unwrap();p["reference"]["guid"]=json!("11111111111111111111111111111111");},
                    _=>{let duplicate=target.original["serialized"][0].clone();target.original["serialized"].as_array_mut().unwrap().push(duplicate);},
                }
                assert!(contacts_from_transport(&altered,&settings).is_err(),"fault {fault}");negatives+=1;
            }
        }
    }
    assert_eq!(policies,22);assert_eq!(empty,2);assert_eq!(negatives,226);
    println!("{}",json!({"carriers":24,"contact_policies":policies,"empty_references":empty,"negative_controls":negatives,"source_offset_not_used_as_target_skin":true,"native_simulation_qualified":false,"studio_visual_verified":false}));
}
