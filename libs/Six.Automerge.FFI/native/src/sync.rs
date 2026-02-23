use interoptopus::ffi;
use interoptopus::ffi_service;
use interoptopus::ffi_type;
use interoptopus::pattern::result::Result;

use crate::util::panic_safe_result_to_ffi as result_to_ffi;

use crate::error::AutomergeFfiError;
use crate::types::ByteArray;

#[ffi_type(opaque)]
pub struct AutomergeSyncState {
    pub(crate) inner: automerge::sync::State,
}

#[ffi_service]
impl AutomergeSyncState {
    pub fn new() -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            Ok(Self {
                inner: automerge::sync::State::new(),
            })
        })
    }

    pub fn reset(&mut self) {
        self.inner = automerge::sync::State::new();
    }

    pub fn encode(&self) -> ByteArray {
        ByteArray::from_vec(self.inner.encode())
    }

    pub fn decode(bytes: ffi::Slice<'_, u8>) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let state = automerge::sync::State::decode(bytes.as_slice())
                .map_err(AutomergeFfiError::from)?;
            Ok(Self { inner: state })
        })
    }

    pub fn shared_heads(&self) -> ffi::Vec<crate::types::ChangeHashBytes> {
        self.inner
            .shared_heads
            .iter()
            .copied()
            .map(crate::types::ChangeHashBytes::from)
            .collect::<Vec<_>>()
            .into()
    }
}
