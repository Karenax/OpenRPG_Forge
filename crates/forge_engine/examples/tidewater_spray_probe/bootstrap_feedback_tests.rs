use super::*;

fn feedback() -> BudgetFeedback {
    BudgetFeedback {
        controller: SprayBudget::new(32768).expect("valid pool"),
        latest: None,
        completed: BTreeMap::new(),
        encoded_step: 1,
        encoded_time: 1.0 / 60.0,
        requested_step: 1,
        rows: vec![],
    }
}
fn sample(head: u32, time: f64) -> HeadSample {
    HeadSample { head, time }
}

#[test]
fn accepts_the_first_encoded_sample() {
    let mut state = feedback();
    assert_eq!(
        state.accept_bootstrap_sample(1, sample(57, 1.0 / 60.0)),
        Ok(true)
    );
    assert_eq!(state.latest.expect("accepted").0, 1);
    assert_eq!(state.completed.len(), 1);
}

#[test]
fn duplicate_completion_does_not_overwrite_the_first_head() {
    let mut state = feedback();
    assert_eq!(
        state.accept_bootstrap_sample(1, sample(57, 1.0 / 60.0)),
        Ok(true)
    );
    assert_eq!(state.accept_bootstrap_sample(1, sample(99, 9.0)), Ok(false));
    let (_, latest) = state.latest.expect("accepted");
    assert_eq!(latest.head, 57);
    assert_eq!(latest.time.to_bits(), (1.0_f64 / 60.0).to_bits());
    assert_eq!(state.completed.len(), 1);
}

#[test]
fn delayed_duplicate_does_not_regress_a_newer_gpu_step() {
    let mut state = feedback();
    assert_eq!(
        state.accept_bootstrap_sample(1, sample(57, 1.0 / 60.0)),
        Ok(true)
    );
    state.encoded_step = 2;
    assert_eq!(
        state.accept_bootstrap_sample(2, sample(81, 2.0 / 60.0)),
        Ok(true)
    );
    assert_eq!(state.accept_bootstrap_sample(1, sample(99, 9.0)), Ok(false));
    let (step, latest) = state.latest.expect("accepted");
    assert_eq!(step, 2);
    assert_eq!(latest.head, 81);
    assert_eq!(state.completed.len(), 2);
}

#[test]
fn an_uncompleted_future_step_is_still_rejected() {
    let mut state = feedback();
    assert!(
        state
            .accept_bootstrap_sample(2, sample(81, 2.0 / 60.0))
            .is_err()
    );
    assert!(state.latest.is_none());
    assert!(state.completed.is_empty());
}

#[test]
fn an_inconsistent_order_is_still_rejected() {
    let mut state = feedback();
    state.latest = Some((1, sample(57, 1.0 / 60.0)));
    assert!(state.accept_bootstrap_sample(1, sample(99, 9.0)).is_err());
    assert_eq!(state.latest.expect("retained").1.head, 57);
    assert!(state.completed.is_empty());
}
