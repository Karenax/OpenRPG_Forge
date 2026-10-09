//! FIFO admission for one active Vault binary request on the browser thread.
//! This bounds transport concurrency, not resident scene or GPU memory.
use std::{
    cell::RefCell,
    collections::VecDeque,
    future::Future,
    pin::Pin,
    rc::Rc,
    task::{Context, Poll, Waker},
};

const MAX_WAITERS: usize = 128;

#[derive(Default)]
struct State {
    held: bool,
    next: u64,
    waiters: VecDeque<(u64, Option<Waker>)>,
}

#[derive(Clone, Default)]
pub(super) struct ModelReadGate(Rc<RefCell<State>>);

pub(super) struct Admission {
    gate: ModelReadGate,
    ticket: Option<u64>,
}

pub(super) struct Permit(ModelReadGate);

impl ModelReadGate {
    pub fn acquire(&self) -> Result<Admission, String> {
        let mut state = self.0.borrow_mut();
        if state.waiters.len() >= MAX_WAITERS {
            return Err("Trop de modèles en attente ; attendre la fin des chargements.".into());
        }
        let ticket = state.next;
        state.next = state
            .next
            .checked_add(1)
            .ok_or("Identité de transfert épuisée")?;
        state.waiters.push_back((ticket, None));
        Ok(Admission {
            gate: self.clone(),
            ticket: Some(ticket),
        })
    }
}

impl Future for Admission {
    type Output = Permit;

    fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Permit> {
        let ticket = self.ticket.expect("Completed admission polled again");
        let admitted = {
            let mut state = self.gate.0.borrow_mut();
            if !state.held && state.waiters.front().is_some_and(|(id, _)| *id == ticket) {
                state.waiters.pop_front();
                state.held = true;
                true
            } else {
                let waiter = state
                    .waiters
                    .iter_mut()
                    .find(|(id, _)| *id == ticket)
                    .expect("Pending admission must remain queued");
                waiter.1 = Some(cx.waker().clone());
                false
            }
        };
        if admitted {
            self.ticket = None;
            Poll::Ready(Permit(self.gate.clone()))
        } else {
            Poll::Pending
        }
    }
}

impl Drop for Admission {
    fn drop(&mut self) {
        let Some(ticket) = self.ticket else { return };
        let wake = {
            let mut state = self.gate.0.borrow_mut();
            state.waiters.retain(|(id, _)| *id != ticket);
            if state.held {
                None
            } else {
                state.waiters.front().and_then(|(_, w)| w.clone())
            }
        };
        if let Some(waker) = wake {
            waker.wake();
        }
    }
}

impl Drop for Permit {
    fn drop(&mut self) {
        let wake = {
            let mut state = self.0.0.borrow_mut();
            state.held = false;
            state.waiters.front().and_then(|(_, w)| w.clone())
        };
        if let Some(waker) = wake {
            waker.wake();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    };
    use std::task::Wake;
    #[derive(Default)]
    struct WakeCount(AtomicUsize);
    impl Wake for WakeCount {
        fn wake(self: Arc<Self>) {
            self.0.fetch_add(1, Ordering::SeqCst);
        }
    }
    fn poll(admission: &mut Admission, count: &Arc<WakeCount>) -> Poll<Permit> {
        let waker = Waker::from(count.clone());
        Pin::new(admission).poll(&mut Context::from_waker(&waker))
    }
    #[test]
    fn overlapping_requests_wait_and_resume_in_fifo_order() {
        let gate = ModelReadGate::default();
        let count = Arc::new(WakeCount::default());
        let mut first = gate.acquire().unwrap();
        let mut second = gate.acquire().unwrap();
        let mut third = gate.acquire().unwrap();
        assert!(poll(&mut third, &count).is_pending());
        let Poll::Ready(permit) = poll(&mut first, &count) else {
            panic!("first must enter")
        };
        assert!(poll(&mut second, &count).is_pending());
        drop(permit);
        assert_eq!(count.0.load(Ordering::SeqCst), 1);
        assert!(poll(&mut third, &count).is_pending());
        let Poll::Ready(permit) = poll(&mut second, &count) else {
            panic!("second must enter")
        };
        drop(permit);
        assert!(poll(&mut third, &count).is_ready());
    }
    #[test]
    fn canceled_head_does_not_stall_following_requests() {
        let gate = ModelReadGate::default();
        let count = Arc::new(WakeCount::default());
        let first = gate.acquire().unwrap();
        let mut second = gate.acquire().unwrap();
        assert!(poll(&mut second, &count).is_pending());
        drop(first);
        assert_eq!(count.0.load(Ordering::SeqCst), 1);
        assert!(poll(&mut second, &count).is_ready());
    }
    #[test]
    fn waiting_and_active_cancellation_release_queue_capacity() {
        let gate = ModelReadGate::default();
        let count = Arc::new(WakeCount::default());
        let mut first = gate.acquire().unwrap();
        let Poll::Ready(active) = poll(&mut first, &count) else {
            panic!("active request")
        };
        let waiting: Vec<_> = (0..MAX_WAITERS).map(|_| gate.acquire().unwrap()).collect();
        assert!(gate.acquire().is_err());
        drop(waiting);
        let mut next = gate.acquire().unwrap();
        assert!(poll(&mut next, &count).is_pending());
        drop(active);
        assert!(poll(&mut next, &count).is_ready());
    }
}
