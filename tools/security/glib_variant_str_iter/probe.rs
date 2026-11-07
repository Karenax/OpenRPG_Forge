//! Safe callers of the five methods listed in RUSTSEC-2024-0429.
//! Run each method in its own process, with core dumps disabled by the harness.
#[cfg(any(
    all(feature = "baseline", feature = "patched"),
    not(any(feature = "baseline", feature = "patched"))
))]
compile_error!("Select exactly one of baseline or patched");

#[cfg(feature = "baseline")]
use glib_baseline as glib;
#[cfg(feature = "patched")]
use glib_patched as glib;

use glib::variant::ToVariant;

fn main() {
    let method = std::env::args().nth(1).expect("Iterator method required");
    for _ in 0..128 {
        let expected = std::hint::black_box(["alpha", "", "épreuve", "龍", "omega"]);
        let variant = expected.to_variant();
        let mut iter = variant.array_iter_str().unwrap();
        assert_eq!(iter.len(), expected.len());
        match method.as_str() {
            "next" => {
                for value in expected {
                    assert_eq!(iter.next(), Some(value));
                }
            }
            "next_back" => {
                for value in expected.into_iter().rev() {
                    assert_eq!(iter.next_back(), Some(value));
                }
            }
            "last" => {
                assert_eq!(iter.last(), Some("omega"));
                iter = variant.array_iter_str().unwrap();
                assert_eq!(iter.nth(usize::MAX), None);
            }
            "nth" => {
                assert_eq!(iter.nth(2), Some("épreuve"));
                assert_eq!(iter.len(), 2);
                assert_eq!(iter.collect::<Vec<_>>(), ["龍", "omega"]);
                iter = variant.array_iter_str().unwrap();
                assert_eq!(iter.nth(usize::MAX), None);
            }
            "nth_back" => {
                assert_eq!(iter.nth_back(2), Some("épreuve"));
                assert_eq!(iter.len(), 2);
                assert_eq!(iter.collect::<Vec<_>>(), ["alpha", ""]);
                iter = variant.array_iter_str().unwrap();
                assert_eq!(iter.nth_back(usize::MAX), None);
            }
            _ => panic!("Unknown iterator method"),
        }
        assert_eq!(iter.len(), 0);
        assert_eq!(iter.next(), None);
        assert_eq!(iter.next_back(), None);
        assert_eq!(iter.next(), None);
        let empty = Vec::<String>::new().to_variant();
        assert_eq!(empty.array_iter_str().unwrap().last(), None);
        assert!(42_u32.to_variant().array_iter_str().is_err());
    }
    println!("PASS: {method}, 128 iterations with UTF-8, empty strings and exhausted iterators");
}
