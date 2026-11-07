//! Synthetic numerical oracles, not native GPU or complete-map qualification.
#![expect(
    clippy::approx_constant,
    reason = "Keep the source shader's exact 6.2832 constant in the independent oracle"
)]
use super::*;

#[test]
fn replacement_geometry_enlarges_the_same_projection_and_refuses_foreign_bounds_atomically() {
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let prepared = prepared(&f);
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let mut bounds = prepared.world_envelopes(&f.population, &view).unwrap();
    let snapshot = view.into_snapshot();
    let entity = &snapshot.layers().next().unwrap().1[0];
    let radius = 200.0;
    let before = bounds.prototypes.clone();
    let scale = entity.source.scale.iter().copied().fold(0.0, f64::max);
    bounds
        .include_models(&snapshot, &[(entity.id, radius)].into())
        .unwrap();
    assert_eq!(bounds.identity(), prepared.identity);
    for (key, bound) in &bounds.prototypes {
        assert!(bound.radius_m >= before[key].radius_m);
        assert_eq!(
            bound.deformation_m.to_bits(),
            before[key].deformation_m.to_bits()
        );
    }
    assert!(
        bounds
            .prototypes
            .values()
            .any(|b| b.radius_m * scale >= radius)
    );
    let fields = |bounds: &WorldEnvelopes| {
        bounds
            .prototypes
            .iter()
            .map(|(key, bound)| {
                (
                    key.clone(),
                    bound.radius_m.to_bits(),
                    bound.deformation_m.to_bits(),
                )
            })
            .collect::<Vec<_>>()
    };
    let accepted = fields(&bounds);
    assert!(
        bounds
            .include_models(
                &snapshot,
                &[
                    (entity.id, radius * 2.0),
                    (EntityId::deterministic("foreign-model"), radius)
                ]
                .into()
            )
            .is_err()
    );
    assert_eq!(fields(&bounds), accepted);
    assert!(
        bounds
            .include_models(&snapshot, &[(entity.id, f64::NAN)].into())
            .is_err()
    );
    assert_eq!(fields(&bounds), accepted);
    let wrong = ImportedTerrainView {
        head_hash: ContentHash::from_bytes([255; 32]),
        excluded_count: 0,
        density_removed_count: 0,
        moved_count: 0,
        placed_count: 0,
        layers: Vec::new(),
    }
    .into_snapshot();
    assert!(bounds.include_models(&wrong, &BTreeMap::new()).is_err());
}
use crate::source_vegetation::LOBE;
use crate::source_vegetation::MAT;
use crate::source_vegetation::Record;
use crate::source_vegetation::VEG;

fn prepared(f: &super::super::tests::Fixture) -> Prepared {
    let mut prepared = f.prepare();
    // Synthetic CPU fixture deliberately supplies the profile identity; this
    // tests closure/selection, and never claims source-package or GPU validation.
    prepared.shader = Some(forge_assets::source_vegetation_package::profile::world_shader_digest());
    for group in &mut prepared.scene.groups {
        for (index, level) in group.levels.iter_mut().enumerate() {
            level.description.kind = if index == 0 { 1 } else { 2 };
        }
    }
    prepared
}

#[test]
fn derived_envelopes_select_only_the_same_admitted_authoring_inputs() {
    use forge_world::terrain_hlod::TerrainHlodLayout;
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let prepared = prepared(&f);
    let mut view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let bounds = prepared.world_envelopes(&f.population, &view).unwrap();
    assert_eq!(bounds.identity(), prepared.identity);
    let envelope = &bounds.prototypes()
        [forge_assets::source_vegetation_package::profile::PlantPrototype::Trees.key()];
    assert!(envelope.radius_m > 1.0 && envelope.radius_m < 100.0);
    assert!(envelope.deformation_m > 0.0 && envelope.deformation_m < 10.0);
    let cells = f.cells();
    let details = cells
        .iter()
        .map(|(&key, cell)| (key, cell.detail))
        .collect();
    let distant = TerrainHlodLayout::new(&f.policy, &details).unwrap();
    let admission = AdmittedPlantCells {
        policy: &f.policy,
        cells: &cells,
        distant: &distant,
        terrain_hash: f.terrain,
        head_hash: view.head_hash,
    };
    let selected = bounds.select(&f.population, &view, admission).unwrap();
    assert_eq!(selected.plants.len(), 4);
    let ordinary = prepared.admit(&selected).unwrap();
    let verified = bounds
        .select_verified(&f.population, &view, admission)
        .unwrap();
    let reused = prepared.admit_verified(&verified).unwrap();
    assert_eq!(ordinary.identity, reused.identity);
    assert_eq!(ordinary.instances, reused.instances);
    let physical = forge_core::bytes_sha256(b"physical source one");
    let proof = verified.collision_source(physical);
    assert!(verified.matches_collision_source(&proof, physical));
    assert!(!verified.matches_collision_source(
        &proof,
        forge_core::bytes_sha256(b"replaced physical source")
    ));
    let independently_prepared = self::prepared(&f);
    assert_eq!(independently_prepared.identity, prepared.identity);
    assert!(independently_prepared.admit_verified(&verified).is_err());
    let other_bounds = independently_prepared
        .world_envelopes(&f.population, &view)
        .unwrap();
    let other_selection = other_bounds
        .select_verified(&f.population, &view, admission)
        .unwrap();
    assert!(!other_selection.matches_collision_source(&proof, physical));
    drop(other_selection);
    drop(verified);
    drop(selected);
    view.layers[0].instances[0].source.position_m[0] += 1e-8;
    assert!(bounds.select(&f.population, &view, admission).is_err());
    assert!(
        bounds
            .select_verified(&f.population, &view, admission)
            .is_err()
    );
    assert!(prepared.world_envelopes(&f.population, &view).is_err());
}

#[test]
fn bound_snapshot_reuses_only_the_exact_validated_immutable_sources() {
    use forge_world::terrain_hlod::TerrainHlodLayout;
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let prepared = prepared(&f);
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let bounds = Arc::new(prepared.world_envelopes(&f.population, &view).unwrap());
    let snapshot = Arc::new(view.into_snapshot());
    let cells = f.cells();
    let policy = f.policy.clone();
    let terrain = f.terrain;
    let distant = TerrainHlodLayout::new(
        &policy,
        &cells
            .iter()
            .map(|(&key, cell)| (key, cell.detail))
            .collect(),
    )
    .unwrap();
    let population = Arc::new(f.population);
    let admission = AdmittedPlantCells {
        policy: &policy,
        cells: &cells,
        distant: &distant,
        terrain_hash: terrain,
        head_hash: snapshot.head_hash(),
    };
    let first = bounds
        .select_snapshot_verified(&population, &snapshot, admission)
        .unwrap();
    let bound = first.bind_sources().unwrap();
    assert!(bound.matches(&bounds, &population, &snapshot));
    let cached = bound.select(admission).unwrap();
    let expected = prepared.admit_verified(&first).unwrap();
    let actual = prepared.admit_verified(&cached).unwrap();
    assert_eq!(expected.identity, actual.identity);
    assert_eq!(expected.instances, actual.instances);
    let wrong_terrain = AdmittedPlantCells {
        terrain_hash: forge_core::bytes_sha256(b"different terrain"),
        ..admission
    };
    assert!(bound.select(wrong_terrain).is_err());
    let another_snapshot = Arc::new(
        population
            .terrain_view(f.zone.head(), &f.heights)
            .unwrap()
            .into_snapshot(),
    );
    assert!(!bound.matches(&bounds, &population, &another_snapshot));
    let other_fixture = super::super::tests::Fixture::with_canopy_stretch(1.0);
    assert!(!bound.matches(&bounds, &Arc::new(other_fixture.population), &snapshot));
}

#[test]
fn unchanged_poses_cannot_reuse_bounds_with_another_vegetation_document() {
    use forge_world::terrain_hlod::TerrainHlodLayout;
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let prepared = prepared(&f);
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let bounds = prepared.world_envelopes(&f.population, &view).unwrap();
    let mut document = f.population.document().clone();
    document.name.push_str(" changed");
    let mut bytes = Vec::new();
    for entity in f.population.layers()[0].instances() {
        bytes.extend_from_slice(entity.source.source_key.as_bytes());
        for value in entity
            .source
            .position_m
            .iter()
            .chain(&entity.source.rotation_xyzw)
            .chain(&entity.source.scale)
            .chain(&entity.source.parameters)
        {
            bytes.extend(value.to_le_bytes());
        }
    }
    let other = ImportedPopulation::resolve(&document, |_, _| Ok(bytes.clone())).unwrap();
    assert!(prepared.world_envelopes(&other, &view).is_err());
    let cells = f.cells();
    let details = cells
        .iter()
        .map(|(&key, cell)| (key, cell.detail))
        .collect();
    let distant = TerrainHlodLayout::new(&f.policy, &details).unwrap();
    let admission = AdmittedPlantCells {
        policy: &f.policy,
        cells: &cells,
        distant: &distant,
        terrain_hash: f.terrain,
        head_hash: view.head_hash,
    };
    assert!(bounds.select(&other, &view, admission).is_err());
}

#[test]
fn unknown_profiles_zero_stretch_and_excess_deformation_refuse_bounds() {
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    assert!(f.prepare().world_envelopes(&f.population, &view).is_err());
    let mut source = prepared(&f);
    source.scene.groups[0].records[0].i_dat.z = 1e8;
    assert!(source.world_envelopes(&f.population, &view).is_err());
    let f = super::super::tests::Fixture::with_canopy_stretch(0.0);
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    assert!(prepared(&f).world_envelopes(&f.population, &view).is_err());
}

#[test]
fn distinct_seed_classes_cannot_trigger_unbounded_geometry_rescans() {
    let f = super::super::tests::Fixture::with_canopy_stretch(1.0);
    let mut prepared = prepared(&f);
    let mut view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let template = view.layers[0].instances[0].clone();
    let pin = prepared.pins[&template.id].clone();
    let group = &mut prepared.scene.groups[pin.group];
    let record = group.records[pin.index].clone();
    let model = group.models[pin.index];
    group.records.clear();
    group.models.clear();
    group.identities = Some(Vec::new());
    for level in &mut group.levels {
        level.description.kind = 0;
    }
    view.layers[0].instances.clear();
    Arc::make_mut(&mut prepared.pins).clear();
    for index in 0..=MAX_GEOMETRY_SUMMARIES {
        let mut entity = template.clone();
        entity.id = EntityId::deterministic(format!("bounds/scan-budget/{index}"));
        entity.source.parameters[3] = f64::from(u32::try_from(index).unwrap());
        let mut record = record.clone();
        record.i_dat.w = f32::from(u16::try_from(index).unwrap());
        let mut pin = pin.clone();
        pin.index = index;
        pin.content = content(&entity);
        group.records.push(record);
        group.models.push(model);
        group.identities.as_mut().unwrap().push(entity.id);
        Arc::make_mut(&mut prepared.pins).insert(entity.id, pin);
        view.layers[0].instances.push(entity);
    }
    let error = prepared
        .world_envelopes(&f.population, &view)
        .err()
        .unwrap();
    assert_eq!(error, "Plant geometry-summary budget exceeded");
}
use bevy::{
    asset::RenderAssetUsages,
    math::{DVec2, DVec4},
    prelude::*,
    render::render_resource::PrimitiveTopology,
};

fn mesh(kind: u8) -> Mesh {
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![
            [0.3, 3.0, 0.1],
            [3.0, 1.0, 0.2],
            [-1.0, -1.0, 0.0],
            [1.0, 1.0, 0.0],
        ],
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, vec![[0.0, 1.0, 0.0]; 4]);
    mesh.insert_attribute(
        VEG,
        vec![
            [0.5, 0.2, 0.1, 0.7],
            [1.0, -0.4, 0.3, 0.2],
            [0.0; 4],
            [0.0; 4],
        ],
    );
    mesh.insert_attribute(
        MAT,
        vec![
            [0.0, 0.0, 0.0, 0.0],
            [1.0, 0.0, 0.0, 0.0],
            [0.0; 4],
            [0.0; 4],
        ],
    );
    mesh.insert_attribute(
        LOBE,
        vec![
            [0.0, 0.0, 0.0, -1.0],
            [1.0, 2.0, -1.0, 0.0],
            [0.0; 4],
            [0.0; 4],
        ],
    );
    if kind == 2 {
        mesh.insert_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![
                [-1.0, -1.0, 0.0],
                [1.0, -1.0, 0.0],
                [-1.0, 1.0, 0.0],
                [1.0, 1.0, 0.0],
            ],
        );
    }
    mesh
}

fn record() -> Record {
    Record {
        i_pos: Vec4::new(0.0, 0.0, 0.0, 1.5),
        i_dat: Vec4::new(0.4, 0.75, 8.0, 0.25),
    }
}
fn transform() -> TransformBounds {
    TransformBounds::new(
        DMat3::from_rotation_y(0.7) * DMat3::from_diagonal(DVec3::new(1.5, 0.7, 2.0)),
        DVec3::ZERO,
    )
    .unwrap()
}
fn rotate_up(v: DVec3, t: DVec3) -> DVec3 {
    let k = DVec3::new(t.z, 0.0, -t.x);
    let c = k.cross(v);
    v + c + k.cross(c) / (t.y + 1.0)
}

// Independent evaluation of source vegPlantDeform, rather than the bound formula.
#[expect(
    clippy::too_many_arguments,
    clippy::many_single_char_names,
    reason = "Independent source vertex oracle retains shader inputs and mathematical names"
)]
fn palm_point(
    local: DVec3,
    normal: DVec3,
    weights: DVec4,
    stem: bool,
    record: &Record,
    transform: &TransformBounds,
    wind: f64,
    time: f64,
    gust: f64,
    direction: DVec3,
) -> DVec3 {
    let height = f64::from(record.i_dat.z);
    let lean = f64::from(record.i_dat.y);
    let seed = f64::from(record.i_dat.w);
    let seed = seed - seed.floor();
    let phase = seed * 6.2832;
    let u = weights.x;
    let s = weights.y;
    let flut = weights.z;
    let ph = weights.w;
    let perpendicular = DVec3::new(-direction.z, 0.0, direction.x);
    let lean_dir = DVec3::new(
        f64::from(record.i_dat.x).cos(),
        0.0,
        f64::from(record.i_dat.x).sin(),
    );
    let wind_offset = direction
        * (wind * wind * 0.014 * (gust * 0.9 + 0.3)
            + (time * 0.83 + phase).sin() * wind * 0.0065 * (gust + 0.45))
        + perpendicular * ((time * 0.61 + phase * 1.7).sin() * wind * 0.0028);
    let c = seed * 7.31 - (seed * 7.31).floor();
    let fc = u * (1.0 - c) + u * (2.0 - u) * c;
    let t = (DVec3::Y
        + lean_dir * (lean * ((1.0 - c) + (1.0 - u) * 2.0 * c))
        + wind_offset * (u * 2.0))
        .normalize();
    let p = transform.matrix * local;
    if stem {
        return DVec3::Y * (u * height)
            + (lean_dir * (lean * fc) + wind_offset * (u * u)) * height
            + rotate_up(DVec3::new(p.x, 0.0, p.z), t);
    }
    let top = (DVec3::Y + lean_dir * (lean * (1.0 - c)) + wind_offset * 2.0).normalize();
    let tilt = (DVec3::Y + top).normalize();
    let original = rotate_up(p, tilt);
    let n = rotate_up(normal, tilt);
    let scale = f64::from(record.i_pos.w);
    let bend = s
        * s
        * scale
        * 4.5
        * (wind * wind * 0.16 * (gust * 0.8 + 0.35)
            + (time * (ph * 0.5 + 1.3) + ph * 23.0 + phase).sin() * wind * 0.075 * (gust + 0.3));
    let bounce = (time * (ph + 2.1) + ph * 41.0).sin() * s * s * wind * scale * 0.1;
    let flutter = flut
        * scale
        * ((time * (ph * 5.0 + 11.0) + ph * 60.0 + s * 9.0).sin() * (wind * 0.045 + 0.008)
            + (time * 23.0 + ph * 13.0 + s * 17.0).sin() * (wind - 1.0).max(0.0) * 0.05);
    let moved = (original
        + direction * bend
        + DVec3::Y * (bounce - bend * 0.3)
        + n * flutter
        + DVec3::Y * 1e-5)
        .normalize()
        * original.length();
    DVec3::Y * height + (lean_dir * lean + wind_offset) * height + moved
}

#[test]
fn palm_bounds_enclose_source_rotations_and_stem_curves_through_storm() {
    let mesh = mesh(0);
    let summary = Summary::from_mesh(&mesh, 0, 0.0, false).unwrap();
    let transform = transform();
    let record = record();
    let (radius, movement) = summary.enclose(0, &record, &transform).unwrap();
    for wind in [0.03, 0.4, 2.5] {
        for gust in [0.0, 0.5, 1.0] {
            for tick in 0..64 {
                let angle = f64::from(tick) * 0.37;
                let time = f64::from(tick) * 3.71;
                let direction = DVec3::new(angle.cos(), 0.0, angle.sin()) * WIND_DIRECTION_LENGTH;
                for (p, weights, stem) in [
                    (
                        DVec3::new(0.3, 3.0, 0.1),
                        DVec4::new(0.5, 0.2, 0.1, 0.7),
                        true,
                    ),
                    (
                        DVec3::new(3.0, 1.0, 0.2),
                        DVec4::new(1.0, -0.4, 0.3, 0.2),
                        false,
                    ),
                ] {
                    assert!(
                        palm_point(
                            p,
                            DVec3::Y,
                            weights,
                            stem,
                            &record,
                            &transform,
                            wind,
                            time,
                            gust,
                            direction
                        )
                        .length()
                            <= radius + movement
                    );
                }
            }
        }
    }
}

#[test]
fn canopy_bounds_cover_lobes_nonuniform_normals_and_signed_motion() {
    let summary = Summary::from_mesh(&mesh(1), 1, 0.0, false).unwrap();
    let transform = transform();
    let record = record();
    let (radius, movement) = summary.enclose(1, &record, &transform).unwrap();
    for tick in 0..128 {
        for gust in [0.0, 1.0] {
            for lobe_scale in [0.0, 0.8, 1.148_377_180_099_487_3] {
                let time = f64::from(tick) * 2.31;
                let angle = f64::from(tick) * 0.43;
                let direction = DVec3::new(angle.cos(), 0.0, angle.sin()) * WIND_DIRECTION_LENGTH;
                let perpendicular = DVec3::new(-direction.z, 0.0, direction.x);
                let wind = 2.5;
                let scale = f64::from(record.i_pos.w);
                let height = f64::from(record.i_dat.z);
                let stretch = f64::from(record.i_dat.y);
                let p = transform.matrix * DVec3::new(3.0, 1.0, 0.2)
                    + DMat3::from_rotation_y(f64::from(record.i_dat.x))
                        * (DVec3::new(1.0, 2.0 * stretch, -1.0) * (1.0 - lobe_scale) * scale);
                let phase = f64::from(record.i_dat.w) * 6.2832;
                let sway = (wind * wind * 0.009 * (gust * 0.8 + 0.3)
                    + (time * 0.9 + phase).sin() * wind * 0.0045 * (gust + 0.4))
                    * height;
                let sway_p = (time * 0.67 + phase * 1.3).sin() * wind * 0.002 * height;
                let branch =
                    (time * 1.9 + 4.0 + phase).sin() * (-0.4) * wind * scale * 0.07 * (gust + 0.5)
                        - (-0.4) * wind * wind * scale * 0.04 * (gust + 0.3);
                let normal = transform.matrix.y_axis / transform.matrix.y_axis.length_squared();
                let flutter = (time * 9.5 + 10.0 + p.x * 1.9 + p.z * 2.3).sin()
                    * 0.3
                    * scale
                    * (wind * 0.035 + 0.005);
                let actual = p
                    + direction * sway
                    + perpendicular * sway_p
                    + DVec3::Y * branch
                    + normal * flutter;
                assert!(actual.length() <= radius + movement);
            }
        }
    }
}

#[test]
fn impostor_bounds_enclose_all_billboard_angles_and_both_source_families() {
    for shrub in [false, true] {
        let summary = Summary::from_mesh(&mesh(2), 2, 0.0, shrub).unwrap();
        let mut record = record();
        record.i_dat.y = if shrub { -0.75 } else { 0.75 };
        let transform = transform();
        let (radius, movement) = summary.enclose(2, &record, &transform).unwrap();
        let (rh, hv, cy) = if shrub {
            (
                1.706_517_677_293_758_3,
                1.088_007_862_269_878_3,
                0.721_987_217_664_718_6,
            )
        } else {
            (
                6.556_488_643_724_781,
                6.740_293_570_160_866,
                6.025_867_998_600_006,
            )
        };
        for pitch in 0..64 {
            let angle = f64::from(pitch) / 64.0 * std::f64::consts::FRAC_PI_2;
            let to_camera = DVec3::new(angle.cos(), angle.sin(), 0.1).normalize();
            let right = (DVec3::Y.cross(to_camera) + DVec3::X * 1e-4).normalize();
            let up = to_camera.cross(right);
            let scale = f64::from(record.i_pos.w);
            let stretch = f64::from(record.i_dat.y).abs();
            for corner in [
                DVec2::ONE,
                -DVec2::ONE,
                DVec2::new(1.0, -1.0),
                DVec2::new(-1.0, 1.0),
            ] {
                let point = DVec3::Y * (cy * scale * stretch)
                    + right * (corner.x * rh * scale)
                    + up * (corner.y
                        * (hv * stretch * (1.0 - to_camera.y * to_camera.y).sqrt()
                            + rh * to_camera.y.abs())
                        * scale);
                assert!(point.length() <= radius);
                assert!((point + DVec3::X * movement).length() <= radius + movement);
            }
        }
    }
}

#[test]
fn transformed_radius_and_normal_operator_cover_shear_and_axis_reflections() {
    let matrix = DMat3::from_cols(
        DVec3::new(-2.0, 0.5, 0.0),
        DVec3::new(0.1, 0.3, 0.4),
        DVec3::new(0.5, 0.0, 3.0),
    );
    let transform = TransformBounds::new(matrix, DVec3::new(0.2, 0.3, 0.4)).unwrap();
    for index in 0..128 {
        let angle = f64::from(index) * 0.31;
        let p = DVec3::new(angle.cos(), angle.sin(), (angle * 0.7).sin());
        assert!(
            (matrix * p + transform.offset).length()
                <= transform.radius(DVec3::ONE, 3.0_f64.sqrt())
        );
    }
    assert!(
        TransformBounds::new(
            DMat3::from_diagonal(DVec3::new(1e-30, 1.0, 1.0)),
            DVec3::ZERO
        )
        .is_err()
    );
}
