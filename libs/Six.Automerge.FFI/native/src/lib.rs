pub mod doc;
pub mod error;
pub mod sync;
pub mod types;
pub mod util;

mod inventory;

pub use doc::AutomergeDoc;
pub use error::{AutomergeFfiError, AutomergeFfiErrorCode};
pub use inventory::ffi_inventory;
pub use sync::AutomergeSyncState;
