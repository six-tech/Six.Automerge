use std::error::Error;
use std::fs;
use std::path::PathBuf;

use interoptopus::lang::NamespaceMappings;
use interoptopus_backend_csharp::Interop;

fn main() -> Result<(), Box<dyn Error>> {
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    let out_dir = manifest_dir.join("..").join("generated");
    fs::create_dir_all(&out_dir)?;

    let inventory = six_automerge::ffi_inventory();
    let namespace = NamespaceMappings::new("Six.Automerge.FFI.Native");

    Interop::builder()
        .inventory(inventory)
        .dll_name("libsix_automerge")
        .namespace_mappings(namespace)
        .build()?
        .write_file(out_dir.join("Interop.g.cs"))?;

    Ok(())
}
