//! Opt-in native ECS/UI diagnostic, isolated by the external test driver.
use super::{Action, Indicator, State, config, daylight, graphics, solar_frame, ui};
use bevy::prelude::*;
use bevy::{
    diagnostic::FrameCount,
    render::view::screenshot::{Screenshot, save_to_disk},
};
use forge_engine::PlayerInputFocus;
#[derive(Resource, Default)]
struct Proof {
    phase: u8,
    wait: u8,
    checks: Vec<&'static str>,
    day: Option<[f64; 3]>,
    images: Vec<AssetId<Image>>,
}
pub(super) fn requested() -> bool {
    std::env::var("FORGE_TIDEWATER_DISPLAY_PROOF").as_deref() == Ok("1")
}
pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>()
        .add_systems(Update, drive.before(graphics::toggle));
}
fn press(world: &mut World, target: Action) -> Result<(), String> {
    let mut query = world.query::<(&Action, &mut Interaction)>();
    let mut found = false;
    for (action, mut interaction) in query.iter_mut(world) {
        let same = match (action, target) {
            (Action::Toggle(a), Action::Toggle(b)) => *a == b,
            (Action::Hour(a), Action::Hour(b)) => *a == b,
            (Action::Resume, Action::Resume) => true,
            _ => false,
        };
        if same {
            *interaction = Interaction::Pressed;
            found = true;
        }
    }
    if found {
        Ok(())
    } else {
        Err("display action absent".into())
    }
}
fn slider(world: &mut World, fraction: f32) -> Result<(), String> {
    let mut q=world.query_filtered::<(&mut Interaction,&mut bevy::ui::RelativeCursorPosition),With<ui::HourTrack>>();
    let (mut interaction, mut cursor) = q.single_mut(world).map_err(|e| e.to_string())?;
    cursor.normalized = Some(Vec2::new(fraction - 0.5, 0.0));
    *interaction = Interaction::Pressed;
    Ok(())
}
fn check_hud(world: &mut World, expected: [bool; 3]) -> Result<(), String> {
    let mut q = world.query::<(&ui::Hud, &Visibility)>();
    for (hud, visibility) in q.iter(world) {
        let index = match hud.0 {
            Indicator::Fps => 0,
            Indicator::Frame => 1,
            Indicator::Clock => 2,
        };
        if (*visibility == Visibility::Visible) != expected[index] {
            return Err("independent HUD visibility failed".into());
        }
    }
    Ok(())
}
fn drive(world: &mut World) {
    if !world.resource::<forge_engine::PlayerSimulationReady>().0
        || world.resource::<FrameCount>().0 < 180
    {
        return;
    }
    if world.resource::<FrameCount>().0 > 1800 {
        error!("display proof timeout");
        world.write_message(bevy::app::AppExit::error());
        return;
    }
    let result=world.resource_scope(|world,mut proof:Mut<Proof>|->Result<(),String>{
        if proof.wait>0 {proof.wait-=1;return Ok(());}
        world.resource_mut::<ButtonInput<KeyCode>>().reset(KeyCode::Escape);
        let image=world.resource::<crate::Probe>().screenshot.clone();
        match proof.phase {
            0=>{proof.images=world.resource::<forge_tidewater::tidewater_atmosphere::Targets>().images.iter().map(Handle::id).collect();world.resource_mut::<ButtonInput<KeyCode>>().press(KeyCode::Escape);},
            1=>{if !world.resource::<State>().open || world.resource::<PlayerInputFocus>().0 {return Err("display menu/input failed".into());} proof.checks.push("escape_opens_display_menu");press(world,Action::Toggle(Indicator::Fps))?;},
            2=>{check_hud(world,[false,true,true])?;press(world,Action::Toggle(Indicator::Frame))?;},
            3=>{check_hud(world,[false,false,true])?;press(world,Action::Toggle(Indicator::Clock))?;},
            4=>{check_hud(world,[false,false,false])?;proof.checks.push("three_independent_indicator_switches");for i in [Indicator::Fps,Indicator::Frame,Indicator::Clock] {press(world,Action::Toggle(i))?;} slider(world,0.5)?;},
            5=>{
                check_hud(world,[true,true,true])?;
                if world.resource::<State>().preferences.minutes!=720 {return Err("slider did not select noon".into());}
                if world.resource::<daylight::Daylight>().completed_hours!=Some(12.0) {return Ok(());}
                let frame=solar_frame(world).ok_or("clock absent")?;
                proof.day=Some(forge_tidewater::tidewater_atmosphere::readback::latest(world,frame).ok_or("noon GPU completion absent")??.lighting.sun_color);
                proof.checks.push("slider_updates_real_solar_and_gpu_lighting");
                world.spawn(Screenshot::primary_window()).observe(save_to_disk(image.with_extension("noon-menu.png")));proof.wait=15;
            },
            6=>press(world,Action::Hour(480))?,
            7=>{if world.resource::<State>().preferences.minutes!=480 {return Err("morning preset failed".into());}press(world,Action::Hour(1080))?;},
            8=>{if world.resource::<State>().preferences.minutes!=1080 {return Err("evening preset failed".into());}press(world,Action::Hour(720))?;},
            9=>{if world.resource::<State>().preferences.minutes!=720 {return Err("noon preset failed".into());}proof.checks.push("morning_noon_evening_presets");slider(world,23.0/24.0)?;},
            10=>{
                if world.resource::<State>().preferences.minutes!=1380 {return Err("night slider failed".into());}
                if world.resource::<daylight::Daylight>().completed_hours!=Some(23.0) {return Ok(());}
                let frame=solar_frame(world).ok_or("clock absent")?;
                let night=forge_tidewater::tidewater_atmosphere::readback::latest(world,frame).ok_or("night GPU completion absent")??.lighting.sun_color;
                if frame.night<0.9 || Some(night)==proof.day || night.iter().any(|v|!v.is_finite()) {return Err("day/night lighting failed".into());}
                if config::load()!=world.resource::<State>().preferences {return Err("display preferences did not persist".into());}
                let images:Vec<_>=world.resource::<forge_tidewater::tidewater_atmosphere::Targets>().images.iter().map(Handle::id).collect();
                if images!=proof.images {return Err("clock recreated resident atmosphere images".into());}
                proof.checks.push("night_light_and_persistence_with_resident_images");
                world.spawn(Screenshot::primary_window()).observe(save_to_disk(image.with_extension("night-menu.png")));proof.wait=15;
            },
            11=>press(world,Action::Resume)?,
            _=>{
                if world.resource::<State>().open || world.resource::<graphics::GraphicsState>().open {return Err("Resume failed".into());}
                let metrics=world.resource::<ui::Metrics>();
                if metrics.samples==0 || !metrics.fps.is_finite() || metrics.fps<=0.0 || (metrics.fps*metrics.frame_ms-1000.0).abs()>1e-6 {return Err("measured frame metrics failed".into());}
                proof.checks.push("resume_and_measured_frame_metrics");
                let receipt=serde_json::json!({"pass":true,"checks":proof.checks,"fps":metrics.fps,"frame_ms":metrics.frame_ms,"metrics_samples":metrics.samples,"day_sun_color":proof.day,"selected_minutes":world.resource::<State>().preferences.minutes,"resident_atmosphere_images_preserved":true,"os_input_injection":false,"gate3_closed":false});
                if !image.with_extension("noon-menu.png").exists() || !image.with_extension("night-menu.png").exists() {return Err("display screenshots absent".into());}
                std::fs::write(image.with_extension("display-proof.json"),serde_json::to_vec_pretty(&receipt).map_err(|e|e.to_string())?).map_err(|e|e.to_string())?;
                world.write_message(bevy::app::AppExit::Success);return Ok(());
            }
        }
        proof.phase+=1;if proof.wait==0 {proof.wait=3;}Ok(())
    });
    if let Err(error) = result {
        error!(%error,"native display proof failed");
        world.write_message(bevy::app::AppExit::error());
    }
}
