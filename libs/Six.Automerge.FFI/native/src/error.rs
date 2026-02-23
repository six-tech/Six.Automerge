use automerge::sync::DecodeStateError;
use automerge::{AutomergeError, LoadChangeError, ObjIdFromBytesError};
use interoptopus::ffi;
use interoptopus::ffi_type;
use thiserror::Error;

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeFfiErrorCode {
    AutomergeError,
    InvalidActorId,
    InvalidObjectId,
    InvalidChange,
    InvalidUtf8,
    JsonError,
    SyncDecodeError,
    NullPointer,
    Unsupported,
    Unknown,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeFfiError {
    pub code: AutomergeFfiErrorCode,
    pub message: ffi::String,
}

impl AutomergeFfiError {
    pub fn new(code: AutomergeFfiErrorCode, message: impl Into<std::string::String>) -> Self {
        Self {
            code,
            message: message.into().into(),
        }
    }

    pub fn automerge(err: AutomergeError) -> Self {
        Self::new(AutomergeFfiErrorCode::AutomergeError, err.to_string())
    }

    pub fn invalid_actor_id(message: impl Into<std::string::String>) -> Self {
        Self::new(AutomergeFfiErrorCode::InvalidActorId, message)
    }

    pub fn invalid_object_id(err: ObjIdFromBytesError) -> Self {
        Self::new(AutomergeFfiErrorCode::InvalidObjectId, err.to_string())
    }

    pub fn invalid_utf8(err: std::str::Utf8Error) -> Self {
        Self::new(AutomergeFfiErrorCode::InvalidUtf8, err.to_string())
    }

    pub fn invalid_change(message: impl Into<std::string::String>) -> Self {
        Self::new(AutomergeFfiErrorCode::InvalidChange, message)
    }

    pub fn json_error(err: serde_json::Error) -> Self {
        Self::new(AutomergeFfiErrorCode::JsonError, err.to_string())
    }

    pub fn sync_decode(err: DecodeStateError) -> Self {
        Self::new(AutomergeFfiErrorCode::SyncDecodeError, err.to_string())
    }

    pub fn null_pointer() -> Self {
        Self::new(AutomergeFfiErrorCode::NullPointer, "pointer was null")
    }
}

impl std::fmt::Display for AutomergeFfiError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{:?}: {}", self.code, self.message.as_str())
    }
}

impl std::error::Error for AutomergeFfiError {}

impl From<AutomergeError> for AutomergeFfiError {
    fn from(value: AutomergeError) -> Self {
        Self::automerge(value)
    }
}

impl From<ObjIdFromBytesError> for AutomergeFfiError {
    fn from(value: ObjIdFromBytesError) -> Self {
        Self::invalid_object_id(value)
    }
}

impl From<LoadChangeError> for AutomergeFfiError {
    fn from(value: LoadChangeError) -> Self {
        Self::invalid_change(value.to_string())
    }
}

impl From<std::str::Utf8Error> for AutomergeFfiError {
    fn from(value: std::str::Utf8Error) -> Self {
        Self::invalid_utf8(value)
    }
}

impl From<serde_json::Error> for AutomergeFfiError {
    fn from(value: serde_json::Error) -> Self {
        Self::json_error(value)
    }
}

impl From<DecodeStateError> for AutomergeFfiError {
    fn from(value: DecodeStateError) -> Self {
        Self::sync_decode(value)
    }
}

#[derive(Debug, Error)]
pub enum AutomergeFfiInternalError {
    #[error("automerge error: {0}")]
    Automerge(#[from] AutomergeError),
    #[error("invalid actor id: {0}")]
    InvalidActorId(String),
    #[error("invalid object id: {0}")]
    InvalidObjectId(#[from] ObjIdFromBytesError),
    #[error("invalid change: {0}")]
    InvalidChange(String),
    #[error("invalid utf8: {0}")]
    InvalidUtf8(#[from] std::str::Utf8Error),
    #[error("json error: {0}")]
    JsonError(#[from] serde_json::Error),
    #[error("sync decode error: {0}")]
    SyncDecodeError(#[from] DecodeStateError),
    #[error("null pointer provided")]
    NullPointer,
    #[error("{0}")]
    Other(String),
}

impl From<AutomergeFfiInternalError> for AutomergeFfiError {
    fn from(value: AutomergeFfiInternalError) -> Self {
        match value {
            AutomergeFfiInternalError::Automerge(err) => Self::automerge(err),
            AutomergeFfiInternalError::InvalidActorId(msg) => Self::invalid_actor_id(msg),
            AutomergeFfiInternalError::InvalidObjectId(err) => Self::invalid_object_id(err),
            AutomergeFfiInternalError::InvalidChange(msg) => Self::invalid_change(msg),
            AutomergeFfiInternalError::InvalidUtf8(err) => Self::invalid_utf8(err),
            AutomergeFfiInternalError::JsonError(err) => Self::json_error(err),
            AutomergeFfiInternalError::SyncDecodeError(err) => Self::sync_decode(err),
            AutomergeFfiInternalError::NullPointer => Self::null_pointer(),
            AutomergeFfiInternalError::Other(msg) => Self::new(AutomergeFfiErrorCode::Unknown, msg),
        }
    }
}

pub type FfiResult<T> = std::result::Result<T, AutomergeFfiInternalError>;

impl From<LoadChangeError> for AutomergeFfiInternalError {
    fn from(value: LoadChangeError) -> Self {
        AutomergeFfiInternalError::InvalidChange(value.to_string())
    }
}
