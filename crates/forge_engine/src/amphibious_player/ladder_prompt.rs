//! Shared in-game prompt for the common ladder controller.
use super::AmphibiousPlayerState;
use bevy::prelude::*;

#[derive(Component)]
struct LadderPrompt;

/// Show the same ladder interaction prompt in any playable module.
pub struct LadderPromptPlugin;

impl Plugin for LadderPromptPlugin {
    fn build(&self, app: &mut App) {
        app.add_systems(PostStartup, spawn)
            .add_systems(Update, update);
    }
}

fn spawn(mut commands: Commands) {
    commands
        .spawn((
            LadderPrompt,
            Visibility::Hidden,
            Node {
                position_type: PositionType::Absolute,
                bottom: Val::Px(72.0),
                left: Val::Percent(50.0),
                padding: UiRect::axes(Val::Px(14.0), Val::Px(8.0)),
                ..default()
            },
            BackgroundColor(Color::srgba(0.02, 0.03, 0.04, 0.78)),
        ))
        .with_child((
            Text::new("Espace — Monter à l’échelle"),
            TextFont {
                font_size: FontSize::Px(26.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

fn update(
    state: Option<Res<AmphibiousPlayerState>>,
    mut prompt: Query<&mut Visibility, With<LadderPrompt>>,
) {
    if let Ok(mut visibility) = prompt.single_mut() {
        *visibility = if state.as_ref().is_some_and(|state| state.ladder_nearby) {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
    }
}
