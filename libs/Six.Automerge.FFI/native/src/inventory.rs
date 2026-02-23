use interoptopus::inventory::Inventory;
use interoptopus::{builtins_string, builtins_vec, ffi, function, pattern};

use crate::{doc, sync, util};

pub fn ffi_inventory() -> Inventory {
    Inventory::builder()
        .register(function!(util::automerge_version))
        .register(pattern!(doc::AutomergeDoc))
        .register(pattern!(sync::AutomergeSyncState))
        .register(builtins_string!())
        .register(builtins_vec!(u8))
        .register(builtins_vec!(ffi::String))
        .register(builtins_vec!(crate::types::ByteArray))
        .register(builtins_vec!(crate::types::ChangeBytes))
        .register(builtins_vec!(crate::types::AutomergeObjId))
        .register(builtins_vec!(crate::types::ChangeHashBytes))
        .register(builtins_vec!(crate::types::AutomergePatch))
        .register(builtins_vec!(crate::types::AutomergePatchPathElement))
        .register(builtins_vec!(crate::types::AutomergeInsertEntry))
        .register(builtins_vec!(crate::types::AutomergeMarkSpan))
        .register(builtins_vec!(crate::types::AutomergeMarkData))
        .register(builtins_vec!(crate::types::AutomergeConflictValue))
        .register(builtins_vec!(crate::types::AutomergeSpan))
        .register(builtins_vec!(crate::types::AutomergePathElement))
        .validate()
        .build()
}
