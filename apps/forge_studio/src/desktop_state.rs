//! Transient presentation geometry. This is not a workspace document or a second project session.

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum DesktopApp {
    Projects,
    World,
    Assets,
    Diagnostics,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct WindowRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct WindowState {
    pub rect: WindowRect,
    pub open: bool,
    pub minimized: bool,
    pub maximized: bool,
    restore: WindowRect,
}

#[derive(Clone, Copy, Debug)]
pub struct DesktopState {
    windows: [WindowState; 4],
    pub active: Option<DesktopApp>,
    width: f64,
    height: f64,
}

impl DesktopState {
    #[must_use]
    pub fn new(width: f64, height: f64) -> Self {
        let width = positive_or(width, 1280.0);
        let height = positive_or(height, 656.0);
        let rect = WindowRect {
            x: ((width - 1060.0) / 2.0).max(12.0),
            y: 36.0,
            width: 1060.0,
            height: (height - 72.0).min(680.0),
        };
        let rect = constrain(rect, width, height);
        let project = WindowState {
            rect,
            restore: rect,
            open: true,
            minimized: false,
            maximized: false,
        };
        let diagnostic_rect = constrain(
            WindowRect {
                x: rect.x + 36.0,
                y: rect.y + 24.0,
                ..rect
            },
            width,
            height,
        );
        let diagnostics = WindowState {
            rect: diagnostic_rect,
            restore: diagnostic_rect,
            open: false,
            ..project
        };
        Self {
            windows: [project, diagnostics, diagnostics, diagnostics],
            active: Some(DesktopApp::Projects),
            width,
            height,
        }
    }

    #[must_use]
    pub fn window(&self, app: DesktopApp) -> WindowState {
        self.windows[index(app)]
    }

    pub fn open(&mut self, app: DesktopApp) {
        let window = &mut self.windows[index(app)];
        window.open = true;
        window.minimized = false;
        self.active = Some(app);
    }

    pub fn focus(&mut self, app: DesktopApp) {
        if self.window(app).open {
            self.open(app);
        }
    }

    pub fn close(&mut self, app: DesktopApp) {
        self.windows[index(app)].open = false;
        self.windows[index(app)].minimized = false;
        self.select_other(app);
    }

    pub fn minimize(&mut self, app: DesktopApp) {
        if self.window(app).open {
            self.windows[index(app)].minimized = true;
        }
        self.select_other(app);
    }

    fn select_other(&mut self, hidden: DesktopApp) {
        if self.active != Some(hidden) {
            return;
        }
        self.active = [
            DesktopApp::Projects,
            DesktopApp::World,
            DesktopApp::Assets,
            DesktopApp::Diagnostics,
        ]
        .into_iter()
        .find(|app| *app != hidden && self.window(*app).open && !self.window(*app).minimized);
    }

    pub fn toggle_maximize(&mut self, app: DesktopApp) {
        if !self.window(app).open {
            return;
        }
        self.focus(app);
        let window = &mut self.windows[index(app)];
        if window.maximized {
            window.rect = constrain(window.restore, self.width, self.height);
        } else {
            window.restore = window.rect;
            window.rect = WindowRect {
                x: 0.0,
                y: 0.0,
                width: self.width,
                height: self.height,
            };
        }
        window.maximized = !window.maximized;
    }

    pub fn move_window(&mut self, app: DesktopApp, x: f64, y: f64) {
        if !x.is_finite() || !y.is_finite() || self.window(app).maximized {
            return;
        }
        let window = &mut self.windows[index(app)];
        window.rect = constrain(
            WindowRect {
                x,
                y,
                ..window.rect
            },
            self.width,
            self.height,
        );
    }

    pub fn resize_window(&mut self, app: DesktopApp, width: f64, height: f64) {
        if !width.is_finite() || !height.is_finite() || self.window(app).maximized {
            return;
        }
        let window = &mut self.windows[index(app)];
        window.rect = constrain(
            WindowRect {
                width,
                height,
                ..window.rect
            },
            self.width,
            self.height,
        );
    }

    pub fn resize_desktop(&mut self, width: f64, height: f64) {
        if !width.is_finite() || !height.is_finite() || width <= 0.0 || height <= 0.0 {
            return;
        }
        self.width = width;
        self.height = height;
        for window in &mut self.windows {
            window.restore = constrain(window.restore, width, height);
            window.rect = if window.maximized {
                WindowRect {
                    x: 0.0,
                    y: 0.0,
                    width,
                    height,
                }
            } else {
                constrain(window.rect, width, height)
            };
        }
    }
}

const fn index(app: DesktopApp) -> usize {
    match app {
        DesktopApp::Projects => 0,
        DesktopApp::World => 1,
        DesktopApp::Assets => 2,
        DesktopApp::Diagnostics => 3,
    }
}
fn positive_or(value: f64, fallback: f64) -> f64 {
    if value.is_finite() && value > 0.0 {
        value
    } else {
        fallback
    }
}
fn constrain(rect: WindowRect, width: f64, height: f64) -> WindowRect {
    let w = rect.width.clamp(640.0_f64.min(width), width);
    let h = rect.height.clamp(360.0_f64.min(height), height);
    WindowRect {
        x: rect.x.clamp(0.0, width - w),
        y: rect.y.clamp(0.0, height - h),
        width: w,
        height: h,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    const PROJECTS: DesktopApp = DesktopApp::Projects;
    const ASSETS: DesktopApp = DesktopApp::Assets;
    const DIAGNOSTICS: DesktopApp = DesktopApp::Diagnostics;

    #[test]
    fn initially_only_projects_is_open() {
        let state = DesktopState::new(1600.0, 756.0);
        assert!(state.window(PROJECTS).open);
        assert!(!state.window(ASSETS).open);
        assert!(!state.window(DIAGNOSTICS).open);
        assert_eq!(state.active, Some(PROJECTS));
    }

    #[test]
    fn hide_and_reopen_preserves_geometry_and_other_window() {
        let mut state = DesktopState::new(1600.0, 756.0);
        state.open(DIAGNOSTICS);
        let before = state.window(PROJECTS);
        let rect = state.window(DIAGNOSTICS).rect;
        state.minimize(DIAGNOSTICS);
        assert_eq!(state.active, Some(PROJECTS));
        state.open(DIAGNOSTICS);
        assert!(!state.window(DIAGNOSTICS).minimized);
        state.close(DIAGNOSTICS);
        state.open(DIAGNOSTICS);
        assert_eq!(state.window(DIAGNOSTICS).rect, rect);
        assert_eq!(state.window(PROJECTS), before);
    }

    #[test]
    fn maximize_restore_preserves_normal_rect() {
        let mut state = DesktopState::new(1600.0, 756.0);
        let normal = state.window(PROJECTS).rect;
        state.toggle_maximize(PROJECTS);
        state.move_window(PROJECTS, 99.0, 99.0);
        state.resize_window(PROJECTS, 22.0, 22.0);
        assert_eq!(
            state.window(PROJECTS).rect.width.to_bits(),
            1600.0_f64.to_bits()
        );
        state.toggle_maximize(PROJECTS);
        assert_eq!(state.window(PROJECTS).rect, normal);
    }

    #[test]
    fn move_resize_and_desktop_shrink_keep_every_window_accessible() {
        let mut state = DesktopState::new(1600.0, 756.0);
        state.move_window(PROJECTS, -1e10, 1e10);
        state.resize_window(PROJECTS, 1e10, -100.0);
        state.open(DIAGNOSTICS);
        state.toggle_maximize(DIAGNOSTICS);
        state.resize_desktop(500.0, 220.0);
        state.toggle_maximize(DIAGNOSTICS);
        for app in [PROJECTS, DesktopApp::World, ASSETS, DIAGNOSTICS] {
            let rect = state.window(app).rect;
            assert!(rect.x >= 0.0 && rect.y >= 0.0);
            assert!(rect.width > 0.0 && rect.height > 0.0);
            assert!(rect.x + rect.width <= 500.0 && rect.y + rect.height <= 220.0);
        }
    }

    #[test]
    fn invalid_geometry_is_ignored_without_panicking() {
        let mut state = DesktopState::new(f64::NAN, f64::INFINITY);
        let before = state.window(PROJECTS);
        state.resize_desktop(0.0, f64::NAN);
        state.resize_window(PROJECTS, f64::NAN, 0.0);
        state.move_window(PROJECTS, 0.0, f64::NEG_INFINITY);
        assert_eq!(state.window(PROJECTS), before);
    }

    #[test]
    fn closing_last_window_leaves_desktop_active() {
        let mut state = DesktopState::new(1600.0, 756.0);
        state.close(PROJECTS);
        assert_eq!(state.active, None);
        state.focus(PROJECTS);
        assert_eq!(state.active, None);
        state.open(PROJECTS);
        assert_eq!(state.active, Some(PROJECTS));
    }

    #[test]
    fn world_window_survives_other_windows_and_is_the_focus_fallback() {
        let mut state = DesktopState::new(1600.0, 756.0);
        state.open(DesktopApp::World);
        state.resize_window(DesktopApp::World, 900.0, 600.0);
        let rect = state.window(DesktopApp::World).rect;
        state.close(PROJECTS);
        state.open(DIAGNOSTICS);
        state.close(DIAGNOSTICS);
        assert_eq!(state.active, Some(DesktopApp::World));
        state.minimize(DesktopApp::World);
        assert_eq!(state.active, None);
        state.open(DesktopApp::World);
        assert_eq!(state.window(DesktopApp::World).rect, rect);
    }

    #[test]
    fn assets_is_a_real_independent_fourth_window() {
        let mut state = DesktopState::new(1600.0, 756.0);
        let projects = state.window(PROJECTS);
        state.open(ASSETS);
        state.move_window(ASSETS, 120.0, 84.0);
        state.resize_window(ASSETS, 880.0, 540.0);
        let assets = state.window(ASSETS);
        assert_eq!(state.active, Some(ASSETS));
        assert_ne!(assets.rect, projects.rect);
        assert_eq!(state.window(PROJECTS), projects);
        state.minimize(ASSETS);
        assert_eq!(state.active, Some(PROJECTS));
        state.open(ASSETS);
        assert_eq!(state.window(ASSETS).rect, assets.rect);
    }
}
