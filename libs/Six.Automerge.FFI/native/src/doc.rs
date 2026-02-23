use std::sync::Arc;

use automerge::marks::{MarkSet, UpdateSpansConfig};
use automerge::sync::SyncDoc;
use automerge::transaction::Transactable;
use automerge::ReadDoc;
use automerge::{Change, ChangeHash, ObjId, ObjType, ScalarValue, Value};
use base64::engine::general_purpose::STANDARD as BASE64_STANDARD;
use base64::Engine;
use interoptopus::ffi;
use interoptopus::ffi_service;
use interoptopus::ffi_service_method;
use interoptopus::ffi_type;
use interoptopus::pattern::result::Result;

use crate::util::panic_safe_result_to_ffi as result_to_ffi;

use automerge::Cursor;

use crate::error::AutomergeFfiError;
use crate::types::{
    actor_id_from_option, AutomergeCommitOptions, AutomergeConflictValue, AutomergeExpandMark,
    AutomergeLoadOptions, AutomergeMarkData, AutomergeMarkSpan, AutomergeObjId,
    AutomergeObjectType, AutomergeOnPartialLoad, AutomergePatch, AutomergePathElement,
    AutomergeSaveOptions, AutomergeScalarValue, AutomergeSpan, AutomergeSpanKind,
    AutomergeVerificationMode, ByteArray, ChangeBytes, ChangeHashBytes, DecodedChange,
};

#[ffi_type(opaque)]
pub struct AutomergeDoc {
    inner: automerge::AutoCommit,
}

#[ffi_service]
impl AutomergeDoc {
    pub fn new(actor: ffi::Option<ffi::String>) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let maybe_actor = actor_id_from_option(actor).map_err(AutomergeFfiError::from)?;
            let inner = match maybe_actor {
                Some(actor_id) => automerge::AutoCommit::new().with_actor(actor_id),
                None => automerge::AutoCommit::new(),
            };
            Ok(Self { inner })
        })
    }

    pub fn load(bytes: ffi::Slice<'_, u8>) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let inner =
                automerge::AutoCommit::load(bytes.as_slice()).map_err(AutomergeFfiError::from)?;
            Ok(Self { inner })
        })
    }

    pub fn load_with_options(
        bytes: ffi::Slice<'_, u8>,
        options: AutomergeLoadOptions,
    ) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let verification = match options.verification_mode {
                AutomergeVerificationMode::Check => automerge::VerificationMode::Check,
                AutomergeVerificationMode::NoCheck => automerge::VerificationMode::DontCheck,
            };
            let on_partial = match options.on_partial_load {
                AutomergeOnPartialLoad::Error => automerge::OnPartialLoad::Error,
                AutomergeOnPartialLoad::Ignore => automerge::OnPartialLoad::Ignore,
            };
            let load_opts = automerge::LoadOptions::new()
                .verification_mode(verification)
                .on_partial_load(on_partial);
            let inner = automerge::AutoCommit::load_with_options(bytes.as_slice(), load_opts)
                .map_err(AutomergeFfiError::from)?;
            Ok(Self { inner })
        })
    }

    pub fn fork(doc: &mut AutomergeDoc) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let forked = doc.inner.fork();
            Ok(Self { inner: forked })
        })
    }

    pub fn fork_at_heads(
        doc: &mut AutomergeDoc,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<Self, AutomergeFfiError> {
        result_to_ffi(|| {
            let hashes = convert_heads(heads);
            let forked = doc
                .inner
                .fork_at(&hashes)
                .map_err(AutomergeFfiError::from)?;
            Ok(Self { inner: forked })
        })
    }

    pub fn save(&mut self) -> ByteArray {
        ByteArray::from_vec(self.inner.save())
    }

    pub fn save_incremental(&mut self) -> ByteArray {
        ByteArray::from_vec(self.inner.save_incremental())
    }

    pub fn save_after(
        &mut self,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> ByteArray {
        let change_hashes = convert_heads(heads);
        let bytes = self.inner.save_after(&change_hashes);
        ByteArray::from_vec(bytes)
    }

    pub fn save_nocompress(&mut self) -> ByteArray {
        ByteArray::from_vec(self.inner.save_nocompress())
    }

    pub fn save_with_options(&mut self, options: AutomergeSaveOptions) -> ByteArray {
        ByteArray::from_vec(self.inner.save_with_options(options.to_save_options()))
    }

    pub fn load_incremental(
        &mut self,
        bytes: ffi::Slice<'_, u8>,
    ) -> Result<u64, AutomergeFfiError> {
        result_to_ffi(|| {
            let loaded = self
                .inner
                .load_incremental(bytes.as_slice())
                .map_err(AutomergeFfiError::from)?;
            Ok(loaded as u64)
        })
    }

    pub fn get_actor_id(&self) -> ffi::String {
        self.inner.get_actor().to_hex_string().into()
    }

    pub fn set_actor_id(&mut self, actor_hex: ffi::String) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let owned: std::string::String = actor_hex.into();
            let actor = automerge::ActorId::try_from(owned.as_str())
                .map_err(|_| AutomergeFfiError::invalid_actor_id(owned.clone()))?;
            self.inner.set_actor(actor);
            Ok(())
        })
    }

    #[ffi_service_method(on_panic = "return_default")]
    pub fn pending_ops(&self) -> u64 {
        self.inner.pending_ops() as u64
    }

    pub fn commit(
        &mut self,
        options: AutomergeCommitOptions,
    ) -> Result<ffi::Option<ChangeHashBytes>, AutomergeFfiError> {
        result_to_ffi(|| {
            let hash = self.inner.commit_with(options.to_commit_options());
            Ok(match hash {
                Some(change_hash) => ffi::Option::Some(ChangeHashBytes::from(change_hash)),
                None => ffi::Option::None,
            })
        })
    }

    pub fn empty_change(
        &mut self,
        options: AutomergeCommitOptions,
    ) -> ChangeHashBytes {
        let hash = self.inner.empty_change(options.to_commit_options());
        ChangeHashBytes::from(hash)
    }

    pub fn base_heads(&mut self) -> ffi::Vec<ChangeHashBytes> {
        self.inner
            .base_heads()
            .into_iter()
            .map(ChangeHashBytes::from)
            .collect::<Vec<_>>()
            .into()
    }

    pub fn rollback(&mut self) -> u64 {
        self.inner.rollback() as u64
    }

    pub fn merge(
        &mut self,
        other: &mut AutomergeDoc,
    ) -> Result<ffi::Vec<ChangeHashBytes>, AutomergeFfiError> {
        result_to_ffi(|| {
            let changes = self
                .inner
                .merge(&mut other.inner)
                .map_err(AutomergeFfiError::from)?;
            let hashes: Vec<ChangeHashBytes> =
                changes.into_iter().map(ChangeHashBytes::from).collect();
            Ok(hashes.into())
        })
    }

    pub fn apply_change(&mut self, change: ChangeBytes) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let bytes: Vec<u8> = change.into();
            let change = Change::try_from(bytes.as_slice()).map_err(AutomergeFfiError::from)?;
            self.inner
                .apply_changes([change])
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn apply_changes(
        &mut self,
        changes: ffi::Slice<'_, ChangeBytes>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let parsed: Vec<Change> = changes
                .as_slice()
                .iter()
                .map(|c| {
                    let bytes: Vec<u8> = c.bytes.clone().into();
                    Change::try_from(bytes.as_slice()).map_err(AutomergeFfiError::from)
                })
                .collect::<std::result::Result<_, _>>()?;
            self.inner
                .apply_changes(parsed)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn get_last_local_change(&mut self) -> ffi::Option<ChangeBytes> {
        match self.inner.get_last_local_change() {
            None => ffi::Option::None,
            Some(change) => {
                let bytes = change.raw_bytes().to_vec();
                ffi::Option::Some(ChangeBytes::from(bytes))
            }
        }
    }

    pub fn get_heads(&mut self) -> ffi::Vec<ChangeHashBytes> {
        self.inner
            .get_heads()
            .into_iter()
            .map(ChangeHashBytes::from)
            .collect::<Vec<_>>()
            .into()
    }

    pub fn get_changes(
        &mut self,
        have: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<ChangeBytes>, AutomergeFfiError> {
        result_to_ffi(|| {
            let have_hashes: Vec<ChangeHash> = have
                .as_slice()
                .iter()
                .copied()
                .map(ChangeHash::from)
                .collect();
            let changes = self.inner.get_changes(&have_hashes);
            let result: Vec<ChangeBytes> = changes
                .into_iter()
                .map(|change| ChangeBytes::from(change.raw_bytes().to_vec()))
                .collect();
            Ok(result.into())
        })
    }

    pub fn diff(
        &mut self,
        before: ffi::Slice<'_, ChangeHashBytes>,
        after: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergePatch>, AutomergeFfiError> {
        result_to_ffi(|| {
            let before_hashes = convert_heads(before);
            let after_hashes = convert_heads(after);
            let patches = self.inner.diff(&before_hashes, &after_hashes);
            let converted: Vec<AutomergePatch> =
                patches.into_iter().map(AutomergePatch::from).collect();
            Ok(converted.into())
        })
    }

    pub fn topo_history_traversal(&mut self) -> ffi::Vec<ChangeHashBytes> {
        self.inner
            .get_changes(&[])
            .into_iter()
            .map(|change| ChangeHashBytes::from(change.hash()))
            .collect::<Vec<_>>()
            .into()
    }

    pub fn isolate(
        &mut self,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let hashes = convert_heads(heads);
            self.inner.isolate(&hashes);
            Ok(())
        })
    }

    pub fn integrate(&mut self) {
        self.inner.integrate();
    }

    pub fn actor_id_from_bytes(
        &mut self,
        bytes: ffi::Slice<'_, u8>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let actor = automerge::ActorId::from(bytes.as_slice());
            self.inner.set_actor(actor);
            Ok(())
        })
    }

    pub fn root(&self) -> AutomergeObjId {
        AutomergeObjId::root()
    }

    pub fn object_type(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<AutomergeObjectType, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let obj_type = self
                .inner
                .object_type(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            Ok(obj_type.into())
        })
    }

    pub fn map_put_string(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        string_value: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            let value: std::string::String = string_value.into();
            self.inner
                .put(&obj_id, key, value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_boolean(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        bool_value: bool,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, bool_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_int(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        int_value: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, int_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_uint(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        uint_value: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, uint_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_f64(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        float_value: f64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, float_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_bytes(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        bytes_value: ffi::Slice<'_, u8>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(
                    &obj_id,
                    key,
                    ScalarValue::Bytes(bytes_value.as_slice().to_vec()),
                )
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_null(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, ScalarValue::Null)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_counter(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        counter_value: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, ScalarValue::counter(counter_value))
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_timestamp(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        timestamp_value: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .put(&obj_id, key, ScalarValue::Timestamp(timestamp_value))
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_put_object(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        object_type: AutomergeObjectType,
    ) -> Result<AutomergeObjId, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            let new_id = self
                .inner
                .put_object(&obj_id, key, ObjType::from(object_type))
                .map_err(AutomergeFfiError::from)?;
            Ok(AutomergeObjId::from(new_id))
        })
    }

    pub fn map_get(
        &self,
        obj: &AutomergeObjId,
        key: ffi::String,
    ) -> Result<ffi::Option<AutomergeScalarValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();

            match self.inner.get(&obj_id, key.as_str()) {
                Ok(Some((Value::Scalar(scalar), _))) => {
                    Ok(ffi::Option::Some(AutomergeScalarValue::from(scalar.as_ref())))
                }
                Ok(Some(_)) => Ok(ffi::Option::None), // Non-scalar (object/list)
                Ok(None) => Ok(ffi::Option::None),    // Missing key
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn list_get(
        &self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<ffi::Option<AutomergeScalarValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;

            match self.inner.get(&obj_id, index as usize) {
                Ok(Some((Value::Scalar(scalar), _))) => {
                    Ok(ffi::Option::Some(AutomergeScalarValue::from(scalar.as_ref())))
                }
                Ok(Some(_)) => Ok(ffi::Option::None), // Non-scalar (object/list)
                Ok(None) => Ok(ffi::Option::None),     // Index out of range
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn list_get_object(
        &self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<ffi::Option<AutomergeObjId>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;

            match self.inner.get(&obj_id, index as usize) {
                Ok(Some((Value::Object(_), child_id))) => {
                    Ok(ffi::Option::Some(AutomergeObjId::from(child_id)))
                }
                Ok(Some(_)) => Ok(ffi::Option::None), // Scalar, not an object
                Ok(None) => Ok(ffi::Option::None),     // Index out of range
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn map_get_object(
        &self,
        obj: &AutomergeObjId,
        key: ffi::String,
    ) -> Result<ffi::Option<AutomergeObjId>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();

            match self.inner.get(&obj_id, key.as_str()) {
                Ok(Some((Value::Object(_), child_id))) => {
                    Ok(ffi::Option::Some(AutomergeObjId::from(child_id)))
                }
                Ok(Some(_)) => Ok(ffi::Option::None), // Scalar, not an object
                Ok(None) => Ok(ffi::Option::None),     // Missing key
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn map_get_at(
        &self,
        obj: &AutomergeObjId,
        key: ffi::String,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Option<AutomergeScalarValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            let hashes = convert_heads(heads);

            match self.inner.get_at(&obj_id, key.as_str(), &hashes) {
                Ok(Some((Value::Scalar(scalar), _))) => {
                    Ok(ffi::Option::Some(AutomergeScalarValue::from(scalar.as_ref())))
                }
                Ok(Some(_)) => Ok(ffi::Option::None),
                Ok(None) => Ok(ffi::Option::None),
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn list_get_at(
        &self,
        obj: &AutomergeObjId,
        index: u64,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Option<AutomergeScalarValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);

            match self.inner.get_at(&obj_id, index as usize, &hashes) {
                Ok(Some((Value::Scalar(scalar), _))) => {
                    Ok(ffi::Option::Some(AutomergeScalarValue::from(scalar.as_ref())))
                }
                Ok(Some(_)) => Ok(ffi::Option::None),
                Ok(None) => Ok(ffi::Option::None),
                Err(e) => Err(AutomergeFfiError::from(e)),
            }
        })
    }

    pub fn map_get_all(
        &self,
        obj: &AutomergeObjId,
        key: ffi::String,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            let values = self
                .inner
                .get_all(&obj_id, key.as_str())
                .map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = values
                .into_iter()
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn list_get_all(
        &self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let values = self
                .inner
                .get_all(&obj_id, index as usize)
                .map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = values
                .into_iter()
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn map_get_all_at(
        &self,
        obj: &AutomergeObjId,
        key: ffi::String,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            let hashes = convert_heads(heads);
            let values = self
                .inner
                .get_all_at(&obj_id, key.as_str(), &hashes)
                .map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = values
                .into_iter()
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn list_get_all_at(
        &self,
        obj: &AutomergeObjId,
        index: u64,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let values = self
                .inner
                .get_all_at(&obj_id, index as usize, &hashes)
                .map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = values
                .into_iter()
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn map_delete(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .delete(&obj_id, key)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_string(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        string_value: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let value: std::string::String = string_value.into();
            self.inner
                .insert(&obj_id, index as usize, value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_bool(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        bool_value: bool,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(&obj_id, index as usize, bool_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_int(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        int_value: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(&obj_id, index as usize, int_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_uint(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        uint_value: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(&obj_id, index as usize, uint_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_f64(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        float_value: f64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(&obj_id, index as usize, float_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_bytes(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        bytes_value: ffi::Slice<'_, u8>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(
                    &obj_id,
                    index as usize,
                    ScalarValue::Bytes(bytes_value.as_slice().to_vec()),
                )
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_null(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .insert(&obj_id, index as usize, ScalarValue::Null)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_insert_object(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        object_type: AutomergeObjectType,
    ) -> Result<AutomergeObjId, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let new_id = self
                .inner
                .insert_object(&obj_id, index as usize, ObjType::from(object_type))
                .map_err(AutomergeFfiError::from)?;
            Ok(AutomergeObjId::from(new_id))
        })
    }

    pub fn list_remove(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .delete(&obj_id, index as usize)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn map_increment(
        &mut self,
        obj: &AutomergeObjId,
        key: ffi::String,
        delta: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let key: std::string::String = key.into();
            self.inner
                .increment(&obj_id, key, delta)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_increment(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        delta: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .increment(&obj_id, index as usize, delta)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_splice_text(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        delete: i64,
        text_value: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let value: std::string::String = text_value.into();
            self.inner
                .splice_text(&obj_id, index as usize, delete as isize, value.as_str())
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn mark(
        &mut self,
        obj: &AutomergeObjId,
        start: u64,
        end: u64,
        name: ffi::String,
        mark_value: AutomergeScalarValue,
        expand: AutomergeExpandMark,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let name_str: std::string::String = name.into();
            let scalar = mark_value.to_scalar_value();
            let mark = automerge::marks::Mark {
                start: start as usize,
                end: end as usize,
                name: name_str.into(),
                value: scalar,
            };
            self.inner
                .mark(&obj_id, mark, expand.into())
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn unmark(
        &mut self,
        obj: &AutomergeObjId,
        name: ffi::String,
        start: u64,
        end: u64,
        expand: AutomergeExpandMark,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let name_str: std::string::String = name.into();
            self.inner
                .unmark(&obj_id, &name_str, start as usize, end as usize, expand.into())
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn marks(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergeMarkSpan>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let marks = self
                .inner
                .marks(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            let converted: Vec<AutomergeMarkSpan> =
                marks.iter().map(AutomergeMarkSpan::from).collect();
            Ok(converted.into())
        })
    }

    pub fn marks_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergeMarkSpan>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let marks = self
                .inner
                .marks_at(&obj_id, &hashes)
                .map_err(AutomergeFfiError::from)?;
            let converted: Vec<AutomergeMarkSpan> =
                marks.iter().map(AutomergeMarkSpan::from).collect();
            Ok(converted.into())
        })
    }

    pub fn spans(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergeSpan>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let spans = self
                .inner
                .spans(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            let converted: Vec<AutomergeSpan> = spans.map(|span| convert_span(&span)).collect();
            Ok(converted.into())
        })
    }

    pub fn spans_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergeSpan>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let spans = self
                .inner
                .spans_at(&obj_id, &hashes)
                .map_err(AutomergeFfiError::from)?;
            let converted: Vec<AutomergeSpan> = spans.map(|span| convert_span(&span)).collect();
            Ok(converted.into())
        })
    }

    pub fn split_block(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<AutomergeObjId, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let new_block_id = self
                .inner
                .split_block(&obj_id, index as usize)
                .map_err(AutomergeFfiError::from)?;
            Ok(AutomergeObjId::from(new_block_id))
        })
    }

    pub fn join_block(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .join_block(&obj_id, index as usize)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn update_spans(
        &mut self,
        obj: &AutomergeObjId,
        new_spans: ffi::Slice<'_, AutomergeSpan>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let span_vec: Vec<automerge::iter::Span> = new_spans
                .as_slice()
                .iter()
                .map(convert_ffi_span_to_automerge)
                .collect();
            let config = UpdateSpansConfig::default();
            self.inner
                .update_spans(&obj_id, config, span_vec)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_string(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        string_value: ffi::String,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let put_value: std::string::String = string_value.into();
            self.inner
                .put(&obj_id, index as usize, put_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_bool(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        bool_value: bool,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(&obj_id, index as usize, bool_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_int(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        int_value: i64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(&obj_id, index as usize, int_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_uint(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        uint_value: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(&obj_id, index as usize, uint_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_f64(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        float_value: f64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(&obj_id, index as usize, float_value)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_bytes(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        bytes_value: ffi::Slice<'_, u8>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(
                    &obj_id,
                    index as usize,
                    ScalarValue::Bytes(bytes_value.as_slice().to_vec()),
                )
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_null(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            self.inner
                .put(&obj_id, index as usize, ScalarValue::Null)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn list_put_object(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        object_type: AutomergeObjectType,
    ) -> Result<AutomergeObjId, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let new_id = self
                .inner
                .put_object(&obj_id, index as usize, ObjType::from(object_type))
                .map_err(AutomergeFfiError::from)?;
            Ok(AutomergeObjId::from(new_id))
        })
    }

    pub fn list_splice(
        &mut self,
        obj: &AutomergeObjId,
        index: u64,
        delete: i64,
        values: ffi::Slice<'_, AutomergeScalarValue>,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let scalar_values: Vec<ScalarValue> = values
                .as_slice()
                .iter()
                .map(|v| v.to_scalar_value())
                .collect();
            self.inner
                .splice(&obj_id, index as usize, delete as isize, scalar_values)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn get_cursor(
        &self,
        obj: &AutomergeObjId,
        index: u64,
    ) -> Result<ByteArray, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let cursor = self
                .inner
                .get_cursor(&obj_id, index as usize, None)
                .map_err(AutomergeFfiError::from)?;
            let cursor_bytes = cursor.to_bytes();
            Ok(ByteArray::from_vec(cursor_bytes))
        })
    }

    pub fn get_cursor_position(
        &self,
        obj: &AutomergeObjId,
        cursor_bytes: ByteArray,
    ) -> Result<u64, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let bytes: Vec<u8> = cursor_bytes.into();
            let cursor = Cursor::try_from(bytes.as_slice())
                .map_err(|e| AutomergeFfiError::new(crate::error::AutomergeFfiErrorCode::Unknown, e.to_string()))?;
            let position = self
                .inner
                .get_cursor_position(&obj_id, &cursor, None)
                .map_err(AutomergeFfiError::from)?;
            Ok(position as u64)
        })
    }

    pub fn list_length(&self, obj: &AutomergeObjId) -> Result<u64, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            Ok(self.inner.length(&obj_id) as u64)
        })
    }

    pub fn text(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::String, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let text = self
                .inner
                .text(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            Ok(text.into())
        })
    }

    pub fn text_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::String, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let text = self
                .inner
                .text_at(&obj_id, &hashes)
                .map_err(AutomergeFfiError::from)?;
            Ok(text.into())
        })
    }

    pub fn map_keys(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<ffi::String>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let keys = self
                .inner
                .keys(&obj_id)
                .collect::<Vec<_>>()
                .into_iter()
                .map(|k| ffi::String::from(k.to_string()))
                .collect::<Vec<_>>();
            Ok(keys.into())
        })
    }

    pub fn map_keys_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<ffi::String>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let keys = self
                .inner
                .keys_at(&obj_id, &hashes)
                .collect::<Vec<_>>()
                .into_iter()
                .map(|k| ffi::String::from(k.to_string()))
                .collect::<Vec<_>>();
            Ok(keys.into())
        })
    }

    pub fn length_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<u64, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            Ok(self.inner.length_at(&obj_id, &hashes) as u64)
        })
    }

    pub fn obj_values(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .values(&obj_id)
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_values_at(
        &self,
        obj: &AutomergeObjId,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let hashes = convert_heads(heads);
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .values_at(&obj_id, &hashes)
                .map(|(val, id)| AutomergeConflictValue::from_value_and_id(&val, &id))
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_map_range(
        &self,
        obj: &AutomergeObjId,
        start_key: ffi::String,
        end_key: ffi::String,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let start: std::string::String = start_key.into();
            let end: std::string::String = end_key.into();
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .map_range(&obj_id, start..end)
                .map(|item| {
                    let id = item.id();
                    let val = item.value.into_value();
                    AutomergeConflictValue::from_value_and_id(&val, &id)
                })
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_map_range_full(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .map_range(&obj_id, ..)
                .map(|item| {
                    let id = item.id();
                    let val = item.value.into_value();
                    AutomergeConflictValue::from_value_and_id(&val, &id)
                })
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_list_range(
        &self,
        obj: &AutomergeObjId,
        start_idx: u64,
        end_idx: u64,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .list_range(&obj_id, (start_idx as usize)..(end_idx as usize))
                .map(|item| {
                    let id = item.id();
                    let val = item.value.into_value();
                    AutomergeConflictValue::from_value_and_id(&val, &id)
                })
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_list_range_full(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergeConflictValue>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let result: Vec<AutomergeConflictValue> = self
                .inner
                .list_range(&obj_id, ..)
                .map(|item| {
                    let id = item.id();
                    let val = item.value.into_value();
                    AutomergeConflictValue::from_value_and_id(&val, &id)
                })
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_parents(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergePathElement>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let parents = self
                .inner
                .parents(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            let path = parents.path();
            let result: Vec<AutomergePathElement> = path
                .into_iter()
                .map(|(exid, prop)| AutomergePathElement::from_exid_prop(exid, &prop))
                .collect();
            Ok(result.into())
        })
    }

    pub fn obj_path_to_object(
        &self,
        obj: &AutomergeObjId,
    ) -> Result<ffi::Vec<AutomergePathElement>, AutomergeFfiError> {
        result_to_ffi(|| {
            let obj_id = ObjId::try_from(obj).map_err(AutomergeFfiError::from)?;
            let parents = self
                .inner
                .parents(&obj_id)
                .map_err(AutomergeFfiError::from)?;
            match parents.visible_path() {
                Some(path) => {
                    let result: Vec<AutomergePathElement> = path
                        .into_iter()
                        .map(|(exid, prop)| AutomergePathElement::from_exid_prop(exid, &prop))
                        .collect();
                    Ok(result.into())
                }
                None => Ok(ffi::Vec::from_vec(Vec::new())),
            }
        })
    }

    pub fn hydrate_json(&self) -> Result<ffi::String, AutomergeFfiError> {
        result_to_ffi(|| {
            let root = ObjId::Root;
            let hydrated = self
                .inner
                .hydrate(&root, None)
                .map_err(AutomergeFfiError::from)?;
            let json = value_to_json(&hydrated);
            let serialized = serde_json::to_string(&json).map_err(AutomergeFfiError::from)?;
            Ok(serialized.into())
        })
    }

    pub fn hydrate_json_at_heads(
        &self,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ffi::String, AutomergeFfiError> {
        result_to_ffi(|| {
            let root = ObjId::Root;
            let hashes = convert_heads(heads);
            let hydrated = self
                .inner
                .hydrate(&root, Some(&hashes))
                .map_err(AutomergeFfiError::from)?;
            let json = value_to_json(&hydrated);
            let serialized = serde_json::to_string(&json).map_err(AutomergeFfiError::from)?;
            Ok(serialized.into())
        })
    }

    pub fn sync_generate_message(
        &mut self,
        state: &mut crate::sync::AutomergeSyncState,
    ) -> Result<ffi::Option<ByteArray>, AutomergeFfiError> {
        result_to_ffi(|| {
            #[allow(unused_mut)]
            let mut sync_doc = self.inner.sync();
            let maybe_message = sync_doc.generate_sync_message(&mut state.inner);
            Ok(match maybe_message {
                Some(message) => {
                    let encoded = message.encode();
                    ffi::Option::Some(ByteArray::from_vec(encoded))
                }
                None => ffi::Option::None,
            })
        })
    }

    pub fn sync_receive_message(
        &mut self,
        state: &mut crate::sync::AutomergeSyncState,
        message: ByteArray,
    ) -> Result<(), AutomergeFfiError> {
        result_to_ffi(|| {
            let bytes: Vec<u8> = message.into();
            let message = automerge::sync::Message::decode(bytes.as_slice())
                .map_err(|err| AutomergeFfiError::invalid_change(err.to_string()))?;
            #[allow(unused_mut)]
            let mut sync_doc = self.inner.sync();
            sync_doc
                .receive_sync_message(&mut state.inner, message)
                .map_err(AutomergeFfiError::from)?;
            Ok(())
        })
    }

    pub fn get_change_metadata(
        &mut self,
        hash: ChangeHashBytes,
    ) -> Result<ffi::Option<DecodedChange>, AutomergeFfiError> {
        result_to_ffi(|| {
            let change_hash = ChangeHash::from(hash);
            match self.inner.get_change_meta_by_hash(&change_hash) {
                Some(metadata) => {
                    let decoded = DecodedChange::from_change_metadata(metadata);
                    Ok(ffi::Option::Some(decoded))
                }
                None => Ok(ffi::Option::None),
            }
        })
    }

    pub fn get_missing_deps(
        &self,
        heads: ffi::Slice<'_, ChangeHashBytes>,
    ) -> ffi::Vec<ChangeHashBytes> {
        let hashes = convert_heads(heads);
        let missing = self.inner.get_missing_deps(&hashes);
        let result: Vec<ChangeHashBytes> =
            missing.into_iter().map(ChangeHashBytes::from).collect();
        result.into()
    }

    pub fn has_our_changes(
        &mut self,
        state: &mut crate::sync::AutomergeSyncState,
    ) -> bool {
        self.inner.has_our_changes(&state.inner)
    }

    pub fn diff_incremental(&mut self) -> ffi::Vec<AutomergePatch> {
        let patches = self.inner.diff_incremental();
        let converted: Vec<AutomergePatch> =
            patches.into_iter().map(AutomergePatch::from).collect();
        converted.into()
    }

    pub fn get_changes_added(
        &mut self,
        other: &mut AutomergeDoc,
    ) -> ffi::Vec<ChangeBytes> {
        let changes = self.inner.get_changes_added(&mut other.inner);
        let result: Vec<ChangeBytes> = changes
            .into_iter()
            .map(|change| ChangeBytes::from(change.raw_bytes().to_vec()))
            .collect();
        result.into()
    }

    pub fn bundle(
        &mut self,
        hashes: ffi::Slice<'_, ChangeHashBytes>,
    ) -> Result<ByteArray, AutomergeFfiError> {
        result_to_ffi(|| {
            let change_hashes: Vec<ChangeHash> = hashes
                .as_slice()
                .iter()
                .copied()
                .map(ChangeHash::from)
                .collect();
            let bundle = self
                .inner
                .bundle(change_hashes)
                .map_err(AutomergeFfiError::from)?;
            Ok(ByteArray::from_vec(bundle.bytes().to_vec()))
        })
    }
}

fn convert_span(span: &automerge::iter::Span) -> AutomergeSpan {
    match span {
        automerge::iter::Span::Text { text, marks } => {
            let marks_data: Vec<AutomergeMarkData> = match marks {
                Some(mark_set) => mark_set
                    .iter()
                    .map(|(name, scalar_value)| AutomergeMarkData::from_mark_entry(name, scalar_value))
                    .collect(),
                None => Vec::new(),
            };
            AutomergeSpan::from_text(text.clone(), marks_data)
        }
        automerge::iter::Span::Block(block_map) => AutomergeSpan::from_block(block_map),
    }
}

fn convert_ffi_span_to_automerge(span: &AutomergeSpan) -> automerge::iter::Span {
    match span.kind {
        AutomergeSpanKind::Text => {
            let text = span.text.as_str().to_string();
            let marks_vec: Vec<AutomergeMarkData> = span.marks.clone().into();
            let marks = if marks_vec.is_empty() {
                None
            } else {
                let mark_set: MarkSet = marks_vec
                    .iter()
                    .map(|m| {
                        let name = m.name.as_str().to_string();
                        let scalar = m.value.to_scalar_value();
                        (name, scalar)
                    })
                    .collect();
                Some(Arc::new(mark_set))
            };
            automerge::iter::Span::Text { text, marks }
        }
        AutomergeSpanKind::Block => {
            let mut map = std::collections::HashMap::<std::string::String, automerge::hydrate::Value>::new();
            let block_type = span.block_type.as_str().to_string();
            if !block_type.is_empty() {
                map.insert(
                    "type".to_string(),
                    automerge::hydrate::Value::Scalar(ScalarValue::Str(block_type.into())),
                );
            }

            let parents_vec: Vec<ffi::String> = span.block_parents.clone().into();
            if !parents_vec.is_empty() {
                let parent_values: Vec<automerge::hydrate::Value> = parents_vec
                    .iter()
                    .map(|p| {
                        automerge::hydrate::Value::Scalar(ScalarValue::Str(p.as_str().into()))
                    })
                    .collect();
                map.insert(
                    "parents".to_string(),
                    automerge::hydrate::Value::List(parent_values.into()),
                );
            }

            let attrs_json = span.block_attrs_json.as_str().to_string();
            if !attrs_json.is_empty() && attrs_json != "{}" {
                if let Ok(serde_json::Value::Object(attrs_map)) = serde_json::from_str(&attrs_json) {
                    for (key, json_val) in attrs_map {
                        let hydrate_val = json_to_hydrate_value(&json_val);
                        map.insert(key, hydrate_val);
                    }
                }
            }

            let hydrate_map: automerge::hydrate::Map = map.into();
            automerge::iter::Span::Block(hydrate_map)
        }
    }
}

fn json_to_hydrate_value(json: &serde_json::Value) -> automerge::hydrate::Value {
    match json {
        serde_json::Value::Null => automerge::hydrate::Value::Scalar(ScalarValue::Null),
        serde_json::Value::Bool(b) => automerge::hydrate::Value::Scalar(ScalarValue::Boolean(*b)),
        serde_json::Value::Number(n) => {
            if let Some(i) = n.as_i64() {
                automerge::hydrate::Value::Scalar(ScalarValue::Int(i))
            } else if let Some(u) = n.as_u64() {
                automerge::hydrate::Value::Scalar(ScalarValue::Uint(u))
            } else if let Some(f) = n.as_f64() {
                automerge::hydrate::Value::Scalar(ScalarValue::F64(f))
            } else {
                automerge::hydrate::Value::Scalar(ScalarValue::Null)
            }
        }
        serde_json::Value::String(s) => {
            automerge::hydrate::Value::Scalar(ScalarValue::Str(s.clone().into()))
        }
        serde_json::Value::Array(arr) => {
            let list: Vec<automerge::hydrate::Value> = arr
                .iter()
                .map(|v| json_to_hydrate_value(v))
                .collect();
            automerge::hydrate::Value::List(list.into())
        }
        serde_json::Value::Object(obj) => {
            let map: std::collections::HashMap<std::string::String, automerge::hydrate::Value> = obj
                .iter()
                .map(|(k, v)| (k.clone(), json_to_hydrate_value(v)))
                .collect();
            automerge::hydrate::Value::Map(map.into())
        }
    }
}

fn convert_heads(heads: ffi::Slice<'_, ChangeHashBytes>) -> Vec<ChangeHash> {
    heads
        .as_slice()
        .iter()
        .copied()
        .map(ChangeHash::from)
        .collect()
}

fn value_to_json(value: &automerge::hydrate::Value) -> serde_json::Value {
    use automerge::hydrate::Value as HydrateValue;
    use automerge::ScalarValue;
    match value {
        HydrateValue::Scalar(scalar) => match scalar {
            ScalarValue::Null => serde_json::Value::Null,
            ScalarValue::Boolean(v) => serde_json::Value::Bool(*v),
            ScalarValue::Int(v) => serde_json::Value::Number((*v).into()),
            ScalarValue::Uint(v) => serde_json::Value::Number((*v).into()),
            ScalarValue::F64(v) => serde_json::Number::from_f64(*v)
                .map(serde_json::Value::Number)
                .unwrap_or(serde_json::Value::Null),
            ScalarValue::Str(v) => serde_json::Value::String(v.to_string()),
            ScalarValue::Bytes(v) => serde_json::Value::String(BASE64_STANDARD.encode(v)),
            ScalarValue::Counter(v) => serde_json::json!({ "type": "counter", "value": *v }),
            ScalarValue::Timestamp(v) => {
                serde_json::json!({ "type": "timestamp", "value": *v })
            }
            ScalarValue::Unknown { bytes, .. } => {
                serde_json::json!({ "type": "unknown", "value": BASE64_STANDARD.encode(bytes) })
            }
        },
        HydrateValue::Map(map) => {
            let mut obj = serde_json::Map::new();
            for (key, map_value) in map.iter() {
                obj.insert(key.clone(), value_to_json(&map_value.value));
            }
            serde_json::Value::Object(obj)
        }
        HydrateValue::List(list) => {
            let array = list
                .iter()
                .map(|item| value_to_json(&item.value))
                .collect::<Vec<_>>();
            serde_json::Value::Array(array)
        }
        HydrateValue::Text(text) => {
            let s: String = text.into();
            serde_json::Value::String(s)
        }
    }
}
