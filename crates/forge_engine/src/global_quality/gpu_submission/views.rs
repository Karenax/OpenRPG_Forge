//! Public camera resources only; not an acknowledgement of every render effect.
use super::Key;
use bevy::{
    ecs::system::SystemParam,
    pbr::MeshViewBindGroup,
    prelude::*,
    render::{
        camera::ExtractedCamera,
        sync_world::MainEntity,
        view::{ViewDepthTexture, ViewTarget, ViewUniformOffset, ViewUniforms},
    },
};
use serde::Serialize;

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent observable GPU resources, not a state machine"
)]
pub struct GpuViewEvidence {
    pub matching_views: usize,
    pub uniform_buffer_bound: bool,
    pub uniform_offset_present: bool,
    pub mesh_bind_groups_present: bool,
    pub depth_texture_covers_viewport: bool,
    pub camera_resources_ready: bool,
}

impl GpuViewEvidence {
    fn ready(&self) -> bool {
        self.matching_views == 1
            && self.uniform_buffer_bound
            && self.uniform_offset_present
            && self.mesh_bind_groups_present
            && self.depth_texture_covers_viewport
    }
}

#[derive(SystemParam)]
#[allow(
    clippy::type_complexity,
    reason = "Typed read-only view resources avoid broad EntityRef access"
)]
pub(super) struct GpuViews<'w, 's> {
    uniforms: Option<Res<'w, ViewUniforms>>,
    views: Query<
        'w,
        's,
        (
            &'static MainEntity,
            &'static ExtractedCamera,
            Option<&'static ViewUniformOffset>,
            Option<&'static MeshViewBindGroup>,
            Option<&'static ViewDepthTexture>,
        ),
        With<ViewTarget>,
    >,
}

impl GpuViews<'_, '_> {
    pub(super) fn observe(&self, key: Option<&Key>) -> GpuViewEvidence {
        let mut evidence = GpuViewEvidence::default();
        let Some(key) = key else {
            return evidence;
        };
        for (main, camera, offset, groups, depth) in &self.views {
            if main.id() != key.camera
                || camera.target.as_ref() != Some(&key.target)
                || camera.physical_viewport_size != Some(key.viewport)
                || camera
                    .viewport
                    .as_ref()
                    .map_or(UVec2::ZERO, |v| v.physical_position)
                    != key.origin
            {
                continue;
            }
            evidence.matching_views += 1;
            evidence.uniform_offset_present = offset.is_some();
            // This component owns the actual main, binding-array and empty bind groups.
            evidence.mesh_bind_groups_present = groups.is_some();
            evidence.depth_texture_covers_viewport = depth.is_some_and(|depth| {
                let size = depth.texture.size();
                covers_viewport(
                    UVec2::new(size.width, size.height),
                    camera
                        .viewport
                        .as_ref()
                        .map_or(UVec2::ZERO, |v| v.physical_position),
                    key.viewport,
                )
            });
        }
        evidence.uniform_buffer_bound = self
            .uniforms
            .as_ref()
            .is_some_and(|uniforms| uniforms.uniforms.binding().is_some());
        evidence.camera_resources_ready = evidence.ready();
        evidence
    }
}

fn covers_viewport(size: UVec2, origin: UVec2, viewport: UVec2) -> bool {
    viewport.min_element() > 0 && size.cmpge(origin).all() && (size - origin).cmpge(viewport).all()
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::ecs::system::SystemState;

    #[test]
    fn absent_renderer_never_reports_camera_resources_ready() {
        let mut world = World::new();
        let mut state = SystemState::<GpuViews>::new(&mut world);
        let resources = state.get(&world).unwrap();
        assert_eq!(resources.observe(None), GpuViewEvidence::default());
        assert!(
            !resources
                .observe(Some(&super::super::tests::key()))
                .camera_resources_ready
        );
    }

    #[test]
    fn every_camera_resource_and_exactly_one_view_are_required() {
        let complete = GpuViewEvidence {
            matching_views: 1,
            uniform_buffer_bound: true,
            uniform_offset_present: true,
            mesh_bind_groups_present: true,
            depth_texture_covers_viewport: true,
            ..default()
        };
        assert!(complete.ready());
        for change in [
            |e: &mut GpuViewEvidence| e.matching_views = 0,
            |e: &mut GpuViewEvidence| e.matching_views = 2,
            |e: &mut GpuViewEvidence| e.uniform_buffer_bound = false,
            |e: &mut GpuViewEvidence| e.uniform_offset_present = false,
            |e: &mut GpuViewEvidence| e.mesh_bind_groups_present = false,
            |e: &mut GpuViewEvidence| e.depth_texture_covers_viewport = false,
        ] {
            let mut missing = complete.clone();
            change(&mut missing);
            assert!(!missing.ready());
        }
    }

    #[test]
    fn depth_extent_includes_viewport_origin_without_integer_overflow() {
        let size = UVec2::new(1920, 1080);
        let origin = UVec2::new(100, 80);
        let viewport = size - origin;
        assert!(covers_viewport(size, origin, viewport));
        assert!(!covers_viewport(viewport, origin, viewport));
        assert!(!covers_viewport(size, origin, size));
        assert!(!covers_viewport(size, UVec2::ZERO, UVec2::ZERO));
        assert!(!covers_viewport(size, UVec2::splat(u32::MAX), size));
    }
}
