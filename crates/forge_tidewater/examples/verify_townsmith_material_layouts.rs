//! Verify the product material layouts using a product-owned harness.
#[path = "support/source_material_layouts.rs"]
mod harness;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    harness::main()
}
