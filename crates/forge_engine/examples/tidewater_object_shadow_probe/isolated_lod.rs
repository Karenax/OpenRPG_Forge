//! Exclude other occluders so a disappearing depth footprint is attributable.
use super::*;

pub fn run() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if !matches!(args.len(), 4 | 5) {
        return Err("FIXTURE NEW_DEPTH_BIN GROUP MAIN_CAMERA_DISTANCE [acquired] required".into());
    }
    let acquired = match args.get(4).map(String::as_str) {
        None => None,
        Some("acquired") => Some(forge_engine::tidewater_wind::AcquiredFrame::reference()?),
        Some(_) => return Err("Unknown initial motion mode".into()),
    };
    let root = PathBuf::from(&args[0]);
    let output = PathBuf::from(&args[1]);
    if output.exists() {
        return Err("Fresh depth output required".into());
    }
    let distance: f32 = args[3].parse()?;
    if !distance.is_finite() || !(0.0..=500.0).contains(&distance) {
        return Err("Invalid main-camera distance".into());
    }
    let fixture: Value = serde_json::from_slice(&fs::read(root.join("fixture.json"))?)?;
    if fixture["source"]["commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c" {
        return Err("Frozen source commit differs".into());
    }
    let scene: Value =
        serde_json::from_slice(&checked_file(&root, &fixture, "vegetation-scene.json")?)?;
    let groups = scene["groups"].as_array().ok_or("Missing source groups")?;
    let tree_only = args[2] == "canopy-tree";
    let group_name = if tree_only { "canopy" } else { &args[2] };
    let index = groups
        .iter()
        .filter(|g| g["name"] != "understory")
        .position(|g| g["name"] == group_name)
        .ok_or("Unknown shadow group")?;
    // Keep one actual selected record and its exact model. CPU querying is held
    // fixed here: this measures the shared shader, not Studio's live collector.
    let selection_camera = if tree_only {
        Vec3::from_array(serde_json::from_value::<[f32; 3]>(
            fixture["camera_pose"]["position"].clone(),
        )?)
        .extend(0.0)
    } else {
        Vec4::new(42.0, 14.0, -90.0, 0.0)
    };
    let mut plants = plants::load(&root, &fixture, selection_camera, None)?;
    let mut batch = plants.batches.remove(index);
    if batch.count == 0 {
        return Err("No actual source instance at selection camera".into());
    }
    let record_index = if tree_only {
        batch
            .records
            .chunks_exact(32)
            .position(|record| {
                f32::from_le_bytes(record[20..24].try_into().expect("four bytes")) >= 0.0
            })
            .ok_or("No tree instance at forest selection camera")?
    } else {
        0
    };
    batch.count = 1;
    batch.records = batch.records[record_index * 32..(record_index + 1) * 32].to_vec();
    batch.models = batch.models[record_index * 64..(record_index + 1) * 64].to_vec();
    let numbers: Vec<_> = batch.records[..16]
        .chunks_exact(4)
        .map(|b| f32::from_le_bytes(b.try_into().expect("four bytes")))
        .collect();
    let base = Vec3::new(numbers[0], numbers[1], numbers[2]);
    let metadata = serde_json::json!({
        "group": group_name, "tree_only": tree_only,
        "selected_record_index": record_index,
        "record_sha256": forge_core::bytes_sha256(&batch.records).to_string(),
        "model_sha256": forge_core::bytes_sha256(&batch.models).to_string(),
        "base": base.to_array(), "selection_camera": selection_camera.to_array(),
        "main_camera_distance_m": distance,
        "i_dat_y": f32::from_le_bytes(batch.records[20..24].try_into().expect("four bytes")),
        "scope": "One acquired source record; no authored Studio collector proof",
        "initial_motion_mode": if acquired.is_some() { "acquired" } else { "legacy" },
        "acquired_frame": acquired.as_ref().map(|frame| serde_json::json!({
            "global_time_s": frame.global_time_s,
            "global_frame": frame.global_frame,
            "wind_speed": frame.wind_speed,
            "wind_direction": frame.wind_direction,
            "vegetation_gust_offset": frame.vegetation_gust_offset,
        }))
    });
    let sidecar = output.with_extension("instance.json");
    fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(sidecar)?
        .write_all(serde_json::to_string_pretty(&metadata)?.as_bytes())?;
    plants.camera = (base + Vec3::X * distance).extend(0.0);
    if let Some(frame) = &acquired {
        plants.initial_motion = Some(
            forge_engine::tidewater_object_shadow::graph::animation::Motion {
                main_camera: plants.camera,
                wind_time: bevy::math::DVec4::new(
                    frame.wind_direction[0],
                    frame.wind_direction[1],
                    frame.wind_speed,
                    frame.global_time_s,
                )
                .as_vec4(),
                gust_frame: bevy::math::DVec4::new(
                    frame.vegetation_gust_offset[0],
                    frame.vegetation_gust_offset[1],
                    f64::from(frame.global_frame % 64),
                    0.0,
                )
                .as_vec4(),
            },
        );
    }
    plants.batches = vec![batch];
    let center = base + Vec3::Y * 10.0;
    let eye = center + Vec3::new(50.0, 80.0, 50.0);
    let clip = Mat4::orthographic_rh(-35.0, 35.0, -35.0, 35.0, 0.1, 250.0)
        * Mat4::look_at_rh(eye, center, Vec3::Y);
    let settings = Settings {
        enabled: 1.0,
        camera: plants.camera,
        matrices: [clip; 4],
        ..default()
    };
    let input = Input {
        settings,
        draws: Vec::new(),
        matrices: vec![clip; 3],
        terrain: None,
        plants: Some(plants),
        eyes: vec![eye.extend(0.0); 3],
    };
    println!(
        "isolated group {} base {:?} main distance {} fixed light {:?}",
        args[2], base, distance, eye
    );
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    )
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)))
    .insert_resource(Output {
        path: output,
        requested: false,
        completed_frame: None,
    })
    .add_systems(Update, readback);
    forge_engine::tidewater_object_shadow::graph::install(&mut app, input)?;
    if app.run() != AppExit::Success {
        return Err("Isolated plant depth readback failed".into());
    }
    Ok(())
}
