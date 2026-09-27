//! Real host transport with pending futures, cancellation and corrupt replies.
use forge_core::bytes_sha256;
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    workspace::source_blobs::SourceBlobStore,
};
use forge_world::imported_vegetation::{ImportedPopulation, acquisition::acquire};
use std::{
    cell::{Cell, RefCell},
    error::Error,
    path::Path,
    task::Poll,
};

fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err("WORKSPACE AREA_ID required".into());
    }
    let mut host = AuthoringHost::new(Path::new(&args[0]))?;
    host.execute_workspace(WorkspaceHostRequest::Open)?;
    let state = host.execute(AuthoringRequest::OpenManaged {
        area_id: args[1].parse()?,
    })?;
    let head = state.zone_head.as_ref().ok_or("head")?;
    let hash = head.content_hash()?;
    let host = RefCell::new(host);
    let calls = Cell::new(0);
    let population = drive(acquire(
        head,
        |index, offset| {
            calls.set(calls.get() + 1);
            pending_once(
                host.borrow_mut()
                    .read_vegetation_source(hash, index, offset),
            )
        },
        || true,
    ))?;
    let store = SourceBlobStore::open_existing(&Path::new(&args[0]).join("workspace-v1"))?;
    let direct = ImportedPopulation::resolve(
        head.base.vegetation.as_ref().ok_or("vegetation")?,
        |pin, size| store.read(pin, size),
    )?;
    assert_eq!(population.instance_count(), direct.instance_count());
    for (actual, expected) in population.layers().iter().zip(direct.layers()) {
        assert_eq!(actual.layer_id, expected.layer_id);
        assert_eq!(actual.instances(), expected.instances());
    }
    for mode in 0..5 {
        let current = Cell::new(mode != 0);
        let requests = Cell::new(0);
        let result = drive(acquire(
            head,
            |index, offset| {
                requests.set(requests.get() + 1);
                let mut reply = host
                    .borrow_mut()
                    .read_vegetation_source(hash, index, offset);
                match mode {
                    1 => current.set(false),
                    2 => {
                        if let Ok(chunk) = &mut reply {
                            chunk.head_hash = bytes_sha256(b"stale");
                        }
                    }
                    3 => {
                        if let Ok(chunk) = &mut reply {
                            chunk.bytes[0] ^= 1;
                        }
                    }
                    4 => reply = Err("injected transport failure".into()),
                    _ => (),
                }
                pending_once(reply)
            },
            || current.get(),
        ));
        assert!(result.is_err());
        assert_eq!(requests.get(), usize::from(mode != 0));
    }
    assert_eq!(state, host.borrow_mut().execute(AuthoringRequest::Inspect)?);
    host.borrow_mut().execute(AuthoringRequest::Close)?;
    println!(
        "POPULATION_ACQUISITION_PASS instances={} chunks={}; async/direct transforms and stable IDs equal; cancelled before/after await, stale, corrupt and failed replies refused; state unchanged",
        population.instance_count(),
        calls.get()
    );
    Ok(())
}

async fn pending_once<T>(value: T) -> T {
    let mut pending = true;
    std::future::poll_fn(|cx| {
        if std::mem::take(&mut pending) {
            cx.waker().wake_by_ref();
            Poll::Pending
        } else {
            Poll::Ready(())
        }
    })
    .await;
    value
}
fn drive<F: std::future::Future>(future: F) -> F::Output {
    let mut future = std::pin::pin!(future);
    let mut cx = std::task::Context::from_waker(std::task::Waker::noop());
    for _ in 0..8192 {
        if let Poll::Ready(value) = future.as_mut().poll(&mut cx) {
            return value;
        }
    }
    panic!("bounded async acquisition exceeded poll budget");
}
