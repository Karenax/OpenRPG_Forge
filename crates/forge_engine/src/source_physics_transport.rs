//! Resolve verified transport geometry into explicit cooking inputs. Contact
//! policy stays caller-owned; this adapter never infers a native physics verdict.
use crate::source_mesh_colliders::{self, MeshCookingPolicy, MeshShapeInput};
use forge_assets::{
    source_graph_glb::SourceGraphGlb, source_physics_transport::PhysicsTransportComponent,
};
use std::collections::BTreeMap;

pub fn cooking_from_transport(
    glb: &SourceGraphGlb<'_>,
    components: Vec<PhysicsTransportComponent>,
    evidence: &str,
) -> Result<BTreeMap<(String, String), MeshCookingPolicy>, String> {
    if evidence.trim().is_empty() {
        return Err("physics transport cooking evidence absent".into());
    }
    let mut policies = BTreeMap::new();
    for component in components {
        let binding = &component.evidence["source_binding"];
        match binding["kind"].as_str() {
            Some("box" | "unassigned_mesh") => {
                if component.candidate_positions.is_some() || component.candidate_sha256.is_some() {
                    return Err("non-mesh cooking input differs".into());
                }
            }
            Some("assigned_mesh") => {
                let identity = &binding["source_component"];
                let key = crate::source_colliders::identity(identity)?;
                let mesh = &binding["source_mesh_reference"];
                let mesh_identity = crate::source_colliders::identity(mesh)?;
                let options = binding["source_cooking_options"]
                    .as_f64()
                    .ok_or("transport cooking options absent")?;
                if !options.is_finite()
                    || options.fract() != 0.
                    || !(0. ..=f64::from(u32::MAX)).contains(&options)
                {
                    return Err("transport cooking options invalid".into());
                }
                let shape = match (component.candidate_positions, component.candidate_sha256) {
                    (Some(positions), Some(sha256)) => {
                        MeshShapeInput::NativePlaneCandidate { positions, sha256 }
                    }
                    (None, None) if binding["source_convex"] == false => {
                        MeshShapeInput::SourceTriangles
                    }
                    _ => return Err("transport lacks an explicit source cooking shape".into()),
                };
                // The complete integer range was checked before the ABI conversion.
                #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
                let source_cooking_options = options as u32;
                let policy = MeshCookingPolicy {
                    mesh_guid: mesh_identity.0,
                    mesh_file_id: mesh_identity.1,
                    source_cooking_options,
                    evidence: evidence.into(),
                    shape,
                };
                if policies.insert(key, policy).is_some() {
                    return Err("duplicate transport cooking input".into());
                }
            }
            _ => return Err("unsupported transport cooking kind".into()),
        }
    }
    // Recheck native observations, references, options, buffer ownership and
    // shape hashes against this actual carrier before any World allocation.
    source_mesh_colliders::prepare(glb, &policies)?;
    Ok(policies)
}
