//! Opt-in native UI proof; no operating-system input injection.
use super::{Action, State, graphics};
use bevy::prelude::*;
use bevy::{
    diagnostic::FrameCount,
    render::view::screenshot::{Screenshot, save_to_disk},
    window::{CursorGrabMode, CursorOptions},
};
use forge_engine::{PlayerInputFocus, PlayerSimulationReady};
#[derive(Resource, Default)]
struct Proof {
    phase: u8,
    wait: u8,
    expected_contrast: Option<f32>,
    checks: Vec<&'static str>,
}
pub(super) fn proof_requested() -> bool {
    std::env::var("FORGE_TIDEWATER_MENU_PROOF").as_deref() == Ok("1")
}
pub(super) fn install_proof(app: &mut App) {
    app.init_resource::<Proof>()
        .add_systems(Update, drive.before(graphics::toggle));
}
fn drive(world: &mut World) {
    if !world.resource::<PlayerSimulationReady>().0 || world.resource::<FrameCount>().0 < 180 {
        return;
    }
    if world.resource::<FrameCount>().0 > 1800 {
        error!("menu proof timeout");
        world.write_message(bevy::app::AppExit::error());
        return;
    }
    let result=world.resource_scope(|world,mut proof:Mut<Proof>|->Result<(),String>{
        if proof.wait>0 { proof.wait-=1;return Ok(()); }
        world.resource_mut::<ButtonInput<KeyCode>>().reset(KeyCode::Escape);
        let main=world.resource::<State>().open;
        let graphic=world.resource::<graphics::GraphicsState>().open;
        let image=world.resource::<crate::Probe>().screenshot.clone();
        match proof.phase {
            0 | 4 | 6=>world.resource_mut::<ButtonInput<KeyCode>>().press(KeyCode::Escape),
            1=>{
                if !main || graphic || world.resource::<PlayerInputFocus>().0 { return Err("Escape did not open compact menu".into()); }
                proof.checks.push("escape_opens_menu_and_releases_input");
                let mut query=world.query::<(&Action,&mut Interaction)>();
                let mut found=false;
                for (action,mut interaction) in query.iter_mut(world) { if matches!(action,Action::Graphics) { *interaction=Interaction::Pressed;found=true; } }
                if !found { return Err("graphics action absent".into()); }
            },
            2=>{
                if main || !graphic || world.resource::<PlayerInputFocus>().0 {return Err("graphics action failed".into());}
                proof.checks.push("graphics_button_opens_existing_panel");
                proof.expected_contrast=Some(graphics::press_contrast_for_proof(world)?);
            },
            3=>{
                let actual=world.resource::<graphics::GraphicsState>().values_receipt()["contrast"].as_f64().ok_or("contrast absent")?;
                if (actual-f64::from(proof.expected_contrast.ok_or("expected contrast absent")?)).abs()>1e-6 {return Err("contrast button was not applied".into());}
                let mut camera=world.query_filtered::<&bevy::render::view::ColorGrading,With<forge_engine::ForgePlayerCamera>>();
                let grading=camera.single(world).map_err(|e|e.to_string())?;
                if (f64::from(grading.midtones.contrast)-actual).abs()>1e-6 {return Err("contrast was not applied to the rendered camera".into());}
                let path=std::path::PathBuf::from(std::env::var_os("LOCALAPPDATA").ok_or("isolated profile absent")?).join("OpenRPG Forge/Tidewater/graphics.json");
                let saved:serde_json::Value=serde_json::from_slice(&std::fs::read(path).map_err(|e|e.to_string())?).map_err(|e|e.to_string())?;
                if (saved["contrast"].as_f64().ok_or("saved contrast absent")?-actual).abs()>1e-6 {return Err("contrast was not persisted".into());}
                proof.checks.push("real_contrast_button_applies_and_persists");
                world.spawn(Screenshot::primary_window()).observe(save_to_disk(image.with_extension("graphics-menu.png")));
                proof.wait=12;
            },
            5=>{
                if !main || graphic {return Err("Escape did not return from graphics".into());}
                proof.checks.push("escape_returns_to_compact_menu");
                world.spawn(Screenshot::primary_window()).observe(save_to_disk(image.with_extension("escape-menu.png")));
                proof.wait=12;
            },
            7=>{
                if main || graphic {return Err("Escape did not resume".into());}
                let mut windows=world.query::<(&Window,&CursorOptions)>();
                let (window,cursor)=windows.single(world).map_err(|e|e.to_string())?;
                if window.focused && (!world.resource::<PlayerInputFocus>().0 || cursor.visible || cursor.grab_mode!=CursorGrabMode::Locked) {return Err("Escape failed to restore cursor/input".into());}
                proof.checks.push("escape_resumes_and_restores_focused_window_input");
                world.resource_mut::<ButtonInput<KeyCode>>().press(KeyCode::Escape);
            },
            8=>{
                if !main || graphic {return Err("Escape did not reopen for Resume test".into());}
                let mut query=world.query::<(&Action,&mut Interaction)>();
                let mut found=false;
                for (action,mut interaction) in query.iter_mut(world) { if matches!(action,Action::Resume) { *interaction=Interaction::Pressed;found=true; } }
                if !found {return Err("Resume action absent".into());}
            },
            9=>{
                if main || graphic {return Err("Escape did not resume".into());}
                let mut windows=world.query::<(&Window,&CursorOptions)>();
                let (window,cursor)=windows.single(world).map_err(|e|e.to_string())?;
                if window.focused && (!world.resource::<PlayerInputFocus>().0 || cursor.visible || cursor.grab_mode!=CursorGrabMode::Locked) {return Err("resume failed to restore cursor/input".into());}
                proof.checks.push("resume_button_restores_focused_window_input");
                if !image.with_extension("graphics-menu.png").exists() || !image.with_extension("escape-menu.png").exists() {return Err("menu captures absent".into());}
                world.resource_mut::<ButtonInput<KeyCode>>().press(KeyCode::Escape);
            },
            _=>{
                if !main || graphic {return Err("Escape did not reopen for Quit test".into());}
                let mut query=world.query::<(&Action,&mut Interaction,&InheritedVisibility)>();
                let mut found=false;
                for (action,mut interaction,visibility) in query.iter_mut(world) {
                    if matches!(action,Action::Quit) && visibility.get() { *interaction=Interaction::Pressed;found=true; }
                }
                if !found {return Err("Quit action absent or invisible".into());}
                proof.checks.push("visible_quit_button_dispatches_actual_game_exit");
                let receipt=serde_json::json!({"pass":true,"checks":proof.checks,"graphics":world.resource::<graphics::GraphicsState>().values_receipt(),"os_keyboard_mouse_injection":false,"gate3_closed":false});
                std::fs::write(image.with_extension("menu-proof.json"),serde_json::to_vec_pretty(&receipt).map_err(|e|e.to_string())?).map_err(|e|e.to_string())?;
                // The menu action, rather than the test, terminates the native game.
                proof.wait=30;return Ok(());
            }
        }
        proof.phase+=1;if proof.wait==0 {proof.wait=3; } Ok(())
    });
    if let Err(error) = result {
        error!(%error,"native menu proof failed");
        world.write_message(bevy::app::AppExit::error());
    }
}
