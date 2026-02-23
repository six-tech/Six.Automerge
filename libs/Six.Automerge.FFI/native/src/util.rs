use interoptopus::ffi;
use interoptopus::ffi_function;
use interoptopus::pattern::result::{result_to_ffi, Result};

use crate::error::AutomergeFfiError;

#[ffi_function]
pub fn automerge_version() -> ffi::String {
    format!("Six.Automerge FFI v{}", env!("CARGO_PKG_VERSION")).into()
}

/// Wraps `result_to_ffi` with `std::panic::catch_unwind` so that Rust panics
/// (e.g. from upstream automerge `unwrap()` calls) are converted to FFI errors
/// instead of aborting the process across the FFI boundary.
///
/// Uses `AssertUnwindSafe` because callers capture `&mut self` which is not
/// `UnwindSafe`. This is acceptable: after a panic the FFI returns an error,
/// and the C# caller should not reuse a corrupted document handle.
pub fn panic_safe_result_to_ffi<T: interoptopus::lang::TypeInfo>(
    f: impl FnOnce() -> std::result::Result<T, AutomergeFfiError>,
) -> Result<T, AutomergeFfiError> {
    result_to_ffi(|| match std::panic::catch_unwind(std::panic::AssertUnwindSafe(f)) {
        Ok(result) => result,
        Err(panic_info) => {
            let msg = if let Some(s) = panic_info.downcast_ref::<&str>() {
                format!("Rust panic: {}", s)
            } else if let Some(s) = panic_info.downcast_ref::<String>() {
                format!("Rust panic: {}", s)
            } else {
                "Rust panic: unknown payload".to_string()
            };
            Err(AutomergeFfiError::new(
                crate::error::AutomergeFfiErrorCode::Unknown,
                msg,
            ))
        }
    })
}
