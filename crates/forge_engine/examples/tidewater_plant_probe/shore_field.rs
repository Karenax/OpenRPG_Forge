//! Pinned Tidewater ShoreField.js propagation, preserving f64 arithmetic and f32 stores.
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss,
    clippy::many_single_char_names
)] // Source equation names and explicit Float32Array stores.
const SIDE: usize = 512;
const CELL: f64 = 4.0;

#[derive(Default)]
struct Heap(Vec<(f64, usize)>);
impl Heap {
    fn push(&mut self, key: f64, value: usize) {
        let mut i = self.0.len();
        self.0.push((key, value));
        while i > 0 {
            let p = (i - 1) / 2;
            if self.0[p].0 <= key {
                break;
            }
            self.0[i] = self.0[p];
            i = p;
        }
        self.0[i] = (key, value);
    }
    fn pop(&mut self) -> Option<usize> {
        let top = self.0.first()?.1;
        let last = self.0.pop()?;
        if self.0.is_empty() {
            return Some(top);
        }
        let mut i = 0;
        loop {
            let mut c = 2 * i + 1;
            if c >= self.0.len() {
                break;
            }
            if c + 1 < self.0.len() && self.0[c + 1].0 < self.0[c].0 {
                c += 1;
            }
            if self.0[c].0 >= last.0 {
                break;
            }
            self.0[i] = self.0[c];
            i = c;
        }
        self.0[i] = last;
        Some(top)
    }
}
fn neighbors(k: usize) -> [Option<usize>; 4] {
    let x = k % SIDE;
    let z = k / SIDE;
    [
        (x > 0).then(|| k - 1),
        (x + 1 < SIDE).then(|| k + 1),
        (z > 0).then(|| k - SIDE),
        (z + 1 < SIDE).then(|| k + SIDE),
    ]
}
fn solve(k: usize, times: &[f32], state: &[u8], speed: &[f32]) -> f64 {
    let mut n = [f64::INFINITY; 4];
    for (i, neighbor) in neighbors(k).into_iter().enumerate() {
        if let Some(v) = neighbor
            && state[v] == 2
        {
            n[i] = f64::from(times[v]);
        }
    }
    let tx = n[0].min(n[1]);
    let tz = n[2].min(n[3]);
    let a = tx.min(tz);
    let b = tx.max(tz);
    let f = CELL / f64::from(speed[k]);
    if !b.is_finite() || b - a >= f {
        a + f
    } else {
        0.5 * (a + b + (2.0 * f * f - (a - b) * (a - b)).sqrt())
    }
}
fn extend(field: &mut [f32], passes: usize, increment: f64) {
    for _ in 0..passes {
        let previous = field.to_vec();
        let mut changed = false;
        for (k, value) in field.iter_mut().enumerate() {
            if previous[k].is_finite() {
                continue;
            }
            let mut sum = 0.0;
            let mut count = 0;
            for n in neighbors(k).into_iter().flatten() {
                if previous[n].is_finite() {
                    sum += f64::from(previous[n]);
                    count += 1;
                }
            }
            if count > 0 {
                *value = (sum / f64::from(count) + increment) as f32;
                changed = true;
            }
        }
        if !changed {
            break;
        }
    }
}
/// Fixed source domain: 2048 m, origin -1024 m, 512 cells, sea level zero.
/// The callback evaluates the effective canonical height without resampling it.
#[allow(clippy::too_many_lines)]
pub fn bake<E>(height: impl Fn(f64, f64) -> Result<f64, E>) -> Result<Vec<f32>, E> {
    let n = SIDE * SIDE;
    let mut speed = vec![0.0_f32; n];
    let mut times = vec![f32::INFINITY; n];
    let mut state = vec![0_u8; n];
    let mut heap = Heap::default();
    let length = (0.12_f64 * 0.12 + 1.0).sqrt();
    let (sdx, sdz) = (-0.12 / length, -1.0 / length);
    let c0 = (9.81_f64 * 25.0).sqrt();
    for k in 0..n {
        let (i, j) = (k % SIDE, k / SIDE);
        let x = -1024.0 + (i as f64 + 0.5) * CELL;
        let z = -1024.0 + (j as f64 + 0.5) * CELL;
        let depth = -height(x, z)?;
        speed[k] = if depth > 0.0 {
            (9.81 * depth.clamp(0.25, 25.0)).sqrt() as f32
        } else {
            0.0
        };
        if (i == 0 || j == 0 || i == SIDE - 1 || j == SIDE - 1) && speed[k] > 0.0 {
            times[k] = ((x * sdx + z * sdz) / c0 + 2048.0) as f32;
            state[k] = 1;
            heap.push(f64::from(times[k]), k);
        }
    }
    while let Some(k) = heap.pop() {
        if state[k] == 2 {
            continue;
        }
        state[k] = 2;
        for nk in neighbors(k).into_iter().flatten() {
            if state[nk] == 2 || speed[nk] <= 0.0 {
                continue;
            }
            let t = solve(nk, &times, &state, &speed);
            if t < f64::from(times[nk]) {
                times[nk] = t as f32;
                state[nk] = 1;
                heap.push(t, nk);
            }
        }
    }
    let mut shore = times.clone();
    extend(&mut shore, 40, 0.0);
    extend(&mut times, 24, CELL / 1.5);
    for _ in 0..3 {
        let mut out = times.clone();
        for k in 0..n {
            if !times[k].is_finite() {
                continue;
            }
            let mut sum = f64::from(times[k]) * 4.0;
            let mut weight = 4;
            for nk in neighbors(k).into_iter().flatten() {
                if times[nk].is_finite() {
                    sum += f64::from(times[nk]);
                    weight += 1;
                }
            }
            out[k] = (sum / f64::from(weight)) as f32;
        }
        times = out;
    }
    let sl = sdx.hypot(sdz);
    let mut data = vec![0.0; n * 4];
    let gradient = |a: f64, b: f64| {
        if a.is_finite() && b.is_finite() {
            a - b
        } else {
            0.0
        }
    };
    for k in 0..n {
        let t = f64::from(times[k]);
        let nb = neighbors(k).map(|n| n.map_or(t, |v| f64::from(times[v])));
        let mut gx = gradient(nb[1], nb[0]);
        let mut gz = gradient(nb[3], nb[2]);
        if gx == 0.0 && nb[1].is_finite() && t.is_finite() {
            gx = nb[1] - t;
        }
        if gz == 0.0 && nb[3].is_finite() && t.is_finite() {
            gz = nb[3] - t;
        }
        let len = gx.hypot(gz);
        let len = if len == 0.0 { 1.0 } else { len };
        let (dx, dz) = (gx / len, gz / len);
        let exposure = (((dx * sdx + dz * sdz) / sl) * 1.4 + 0.1).clamp(0.02, 1.0);
        data[k * 4] = if t.is_finite() { t as f32 } else { 1e5 };
        data[k * 4 + 1] = (dx * exposure) as f32;
        data[k * 4 + 2] = (dz * exposure) as f32;
        data[k * 4 + 3] = if shore[k].is_finite() {
            shore[k]
        } else {
            data[k * 4]
        };
    }
    Ok(data)
}
