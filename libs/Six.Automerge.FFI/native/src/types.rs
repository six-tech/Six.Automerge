use std::convert::TryInto;

use automerge::marks::{ExpandMark, Mark};
use automerge::{
    ActorId, ChangeHash, ObjId, ObjType, Patch, PatchAction, Prop, ScalarValue, Value,
};
use interoptopus::ffi;
use interoptopus::ffi_type;

use crate::error::{AutomergeFfiInternalError, FfiResult};

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeObjId {
    pub bytes: ffi::Vec<u8>,
}

impl AutomergeObjId {
    pub fn root() -> Self {
        ObjId::Root.into()
    }

    pub fn as_obj_id(&self) -> FfiResult<ObjId> {
        let bytes: Vec<u8> = self.bytes.clone().into();
        ObjId::try_from(bytes.as_slice()).map_err(AutomergeFfiInternalError::from)
    }
}

impl From<ObjId> for AutomergeObjId {
    fn from(value: ObjId) -> Self {
        Self {
            bytes: value.to_bytes().into(),
        }
    }
}

impl From<&ObjId> for AutomergeObjId {
    fn from(value: &ObjId) -> Self {
        value.clone().into()
    }
}

impl TryFrom<&AutomergeObjId> for ObjId {
    type Error = AutomergeFfiInternalError;

    fn try_from(value: &AutomergeObjId) -> Result<Self, Self::Error> {
        value.as_obj_id()
    }
}

impl TryFrom<AutomergeObjId> for ObjId {
    type Error = AutomergeFfiInternalError;

    fn try_from(value: AutomergeObjId) -> Result<Self, Self::Error> {
        let bytes: Vec<u8> = value.bytes.into();
        ObjId::try_from(bytes.as_slice()).map_err(AutomergeFfiInternalError::from)
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct ByteArray {
    pub data: ffi::Vec<u8>,
}

impl ByteArray {
    pub fn from_vec(vec: Vec<u8>) -> Self {
        Self { data: vec.into() }
    }

    pub fn into_vec(self) -> Vec<u8> {
        self.data.into()
    }
}

impl From<Vec<u8>> for ByteArray {
    fn from(value: Vec<u8>) -> Self {
        Self::from_vec(value)
    }
}

impl From<ByteArray> for Vec<u8> {
    fn from(value: ByteArray) -> Vec<u8> {
        value.into_vec()
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct ChangeHashBytes {
    pub part1: u64,
    pub part2: u64,
    pub part3: u64,
    pub part4: u64,
}

impl ChangeHashBytes {
    pub fn to_change_hash(&self) -> ChangeHash {
        ChangeHash(self.to_bytes())
    }

    pub fn to_bytes(&self) -> [u8; 32] {
        let mut bytes = [0_u8; 32];
        bytes[0..8].copy_from_slice(&self.part1.to_le_bytes());
        bytes[8..16].copy_from_slice(&self.part2.to_le_bytes());
        bytes[16..24].copy_from_slice(&self.part3.to_le_bytes());
        bytes[24..32].copy_from_slice(&self.part4.to_le_bytes());
        bytes
    }

    fn from_bytes(bytes: [u8; 32]) -> Self {
        let part1 = u64::from_le_bytes(bytes[0..8].try_into().expect("slice of len 8"));
        let part2 = u64::from_le_bytes(bytes[8..16].try_into().expect("slice of len 8"));
        let part3 = u64::from_le_bytes(bytes[16..24].try_into().expect("slice of len 8"));
        let part4 = u64::from_le_bytes(bytes[24..32].try_into().expect("slice of len 8"));
        Self {
            part1,
            part2,
            part3,
            part4,
        }
    }
}

impl From<ChangeHash> for ChangeHashBytes {
    fn from(value: ChangeHash) -> Self {
        Self::from_bytes(value.0)
    }
}

impl From<&ChangeHash> for ChangeHashBytes {
    fn from(value: &ChangeHash) -> Self {
        Self::from_bytes(value.0)
    }
}

impl From<ChangeHashBytes> for ChangeHash {
    fn from(value: ChangeHashBytes) -> Self {
        ChangeHash(value.to_bytes())
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct ChangeBytes {
    pub bytes: ffi::Vec<u8>,
}

impl ChangeBytes {
    pub fn from_vec(vec: Vec<u8>) -> Self {
        Self { bytes: vec.into() }
    }

    pub fn into_vec(self) -> Vec<u8> {
        self.bytes.into()
    }
}

impl From<Vec<u8>> for ChangeBytes {
    fn from(value: Vec<u8>) -> Self {
        Self::from_vec(value)
    }
}

impl From<ChangeBytes> for Vec<u8> {
    fn from(value: ChangeBytes) -> Self {
        value.into_vec()
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeObjectType {
    Map,
    List,
    Text,
}

impl From<ObjType> for AutomergeObjectType {
    fn from(value: ObjType) -> Self {
        match value {
            ObjType::Map | ObjType::Table => Self::Map,
            ObjType::List => Self::List,
            ObjType::Text => Self::Text,
        }
    }
}

impl From<AutomergeObjectType> for ObjType {
    fn from(value: AutomergeObjectType) -> Self {
        match value {
            AutomergeObjectType::Map => ObjType::Map,
            AutomergeObjectType::List => ObjType::List,
            AutomergeObjectType::Text => ObjType::Text,
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeExpandMark {
    Before,
    After,
    Both,
    None,
}

impl From<AutomergeExpandMark> for ExpandMark {
    fn from(value: AutomergeExpandMark) -> Self {
        match value {
            AutomergeExpandMark::Before => ExpandMark::Before,
            AutomergeExpandMark::After => ExpandMark::After,
            AutomergeExpandMark::Both => ExpandMark::Both,
            AutomergeExpandMark::None => ExpandMark::None,
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeCommitOptions {
    pub message: ffi::Option<ffi::String>,
    pub timestamp_millis: ffi::Option<i64>,
}

impl AutomergeCommitOptions {
    pub fn to_commit_options(&self) -> automerge::transaction::CommitOptions {
        let mut options = automerge::transaction::CommitOptions::default();
        if let ffi::Option::Some(message) = &self.message {
            options.set_message(message.as_str().to_owned());
        }
        if let ffi::Option::Some(ts) = self.timestamp_millis {
            options.set_time(ts);
        }
        options
    }
}

impl Default for AutomergeCommitOptions {
    fn default() -> Self {
        Self {
            message: ffi::Option::None,
            timestamp_millis: ffi::Option::None,
        }
    }
}

pub fn actor_id_from_option(option: ffi::Option<ffi::String>) -> FfiResult<Option<ActorId>> {
    match option.into_option() {
        None => Ok(None),
        Some(value) => {
            let owned: std::string::String = value.into();
            let actor = ActorId::try_from(owned.as_str())
                .map_err(|_| AutomergeFfiInternalError::InvalidActorId(owned))?;
            Ok(Some(actor))
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergePropKind {
    Map,
    Seq,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeProp {
    pub kind: AutomergePropKind,
    pub key: ffi::Option<ffi::String>,
    pub index: u64,
}

impl AutomergeProp {
    fn map(key: String) -> Self {
        Self {
            kind: AutomergePropKind::Map,
            key: ffi::Option::Some(key.into()),
            index: 0,
        }
    }

    fn seq(index: usize) -> Self {
        Self {
            kind: AutomergePropKind::Seq,
            key: ffi::Option::None,
            index: index as u64,
        }
    }
}

impl From<Prop> for AutomergeProp {
    fn from(value: Prop) -> Self {
        match value {
            Prop::Map(key) => Self::map(key.into()),
            Prop::Seq(index) => Self::seq(index),
        }
    }
}

impl From<&Prop> for AutomergeProp {
    fn from(value: &Prop) -> Self {
        match value {
            Prop::Map(key) => Self::map(key.to_string()),
            Prop::Seq(index) => Self::seq(*index),
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeScalarType {
    Bytes,
    String,
    Int,
    Uint,
    F64,
    Counter,
    Timestamp,
    Boolean,
    Null,
    Unknown,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeScalarValue {
    pub value_type: AutomergeScalarType,
    pub string_value: ffi::Option<ffi::String>,
    pub bool_value: bool,
    pub int_value: i64,
    pub uint_value: u64,
    pub f64_value: f64,
    pub bytes_value: ffi::Vec<u8>,
    pub counter_value: i64,
    pub timestamp_value: i64,
    pub unknown_type_code: u8,
    pub unknown_bytes: ffi::Vec<u8>,
}

impl AutomergeScalarValue {
    fn new(value_type: AutomergeScalarType) -> Self {
        Self {
            value_type,
            string_value: ffi::Option::None,
            bool_value: false,
            int_value: 0,
            uint_value: 0,
            f64_value: 0.0,
            bytes_value: ffi::Vec::from_vec(Vec::new()),
            counter_value: 0,
            timestamp_value: 0,
            unknown_type_code: 0,
            unknown_bytes: ffi::Vec::from_vec(Vec::new()),
        }
    }

    pub fn to_scalar_value(&self) -> ScalarValue {
        match self.value_type {
            AutomergeScalarType::String => {
                if let ffi::Option::Some(ref s) = self.string_value {
                    ScalarValue::Str(s.as_str().into())
                } else {
                    ScalarValue::Null
                }
            }
            AutomergeScalarType::Boolean => ScalarValue::Boolean(self.bool_value),
            AutomergeScalarType::Int => ScalarValue::Int(self.int_value),
            AutomergeScalarType::Uint => ScalarValue::Uint(self.uint_value),
            AutomergeScalarType::F64 => ScalarValue::F64(self.f64_value),
            AutomergeScalarType::Counter => ScalarValue::counter(self.counter_value),
            AutomergeScalarType::Timestamp => ScalarValue::Timestamp(self.timestamp_value),
            AutomergeScalarType::Bytes => {
                let bytes: Vec<u8> = self.bytes_value.clone().into();
                ScalarValue::Bytes(bytes)
            }
            AutomergeScalarType::Null => ScalarValue::Null,
            AutomergeScalarType::Unknown => {
                let bytes: Vec<u8> = self.unknown_bytes.clone().into();
                ScalarValue::Unknown {
                    type_code: self.unknown_type_code,
                    bytes,
                }
            }
        }
    }
}

impl From<&ScalarValue> for AutomergeScalarValue {
    fn from(value: &ScalarValue) -> Self {
        match value {
            ScalarValue::Bytes(bytes) => {
                let mut scalar = Self::new(AutomergeScalarType::Bytes);
                scalar.bytes_value = bytes.clone().into();
                scalar
            }
            ScalarValue::Str(s) => {
                let mut scalar = Self::new(AutomergeScalarType::String);
                scalar.string_value = ffi::Option::Some(s.to_string().into());
                scalar
            }
            ScalarValue::Int(i) => {
                let mut scalar = Self::new(AutomergeScalarType::Int);
                scalar.int_value = *i;
                scalar
            }
            ScalarValue::Uint(u) => {
                let mut scalar = Self::new(AutomergeScalarType::Uint);
                scalar.uint_value = *u;
                scalar
            }
            ScalarValue::F64(f) => {
                let mut scalar = Self::new(AutomergeScalarType::F64);
                scalar.f64_value = *f;
                scalar
            }
            ScalarValue::Counter(counter) => {
                let mut scalar = Self::new(AutomergeScalarType::Counter);
                scalar.counter_value = i64::from(counter);
                scalar
            }
            ScalarValue::Timestamp(ts) => {
                let mut scalar = Self::new(AutomergeScalarType::Timestamp);
                scalar.timestamp_value = *ts;
                scalar
            }
            ScalarValue::Boolean(b) => {
                let mut scalar = Self::new(AutomergeScalarType::Boolean);
                scalar.bool_value = *b;
                scalar
            }
            ScalarValue::Null => Self::new(AutomergeScalarType::Null),
            ScalarValue::Unknown { type_code, bytes } => {
                let mut scalar = Self::new(AutomergeScalarType::Unknown);
                scalar.unknown_type_code = *type_code;
                scalar.unknown_bytes = bytes.clone().into();
                scalar
            }
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeValueType {
    Scalar,
    Object,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeValue {
    pub value_type: AutomergeValueType,
    pub scalar: ffi::Option<AutomergeScalarValue>,
    pub object_type: ffi::Option<AutomergeObjectType>,
}

impl AutomergeValue {
    fn scalar(value: AutomergeScalarValue) -> Self {
        Self {
            value_type: AutomergeValueType::Scalar,
            scalar: ffi::Option::Some(value),
            object_type: ffi::Option::None,
        }
    }

    fn object(obj: ObjType) -> Self {
        Self {
            value_type: AutomergeValueType::Object,
            scalar: ffi::Option::None,
            object_type: ffi::Option::Some(obj.into()),
        }
    }
}

impl From<Value<'_>> for AutomergeValue {
    fn from(value: Value<'_>) -> Self {
        match value {
            Value::Object(obj_type) => Self::object(obj_type),
            Value::Scalar(scalar) => Self::scalar(AutomergeScalarValue::from(scalar.as_ref())),
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeConflictValue {
    pub is_object: bool,
    pub scalar: AutomergeScalarValue,
    pub obj_id: AutomergeObjId,
    pub obj_type: AutomergeObjectType,
    pub op_id: ffi::String,
}

impl AutomergeConflictValue {
    pub fn from_value_and_id(val: &Value<'_>, id: &ObjId) -> Self {
        match val {
            Value::Scalar(scalar) => Self {
                is_object: false,
                scalar: AutomergeScalarValue::from(scalar.as_ref()),
                obj_id: AutomergeObjId::root(),
                obj_type: AutomergeObjectType::Map,
                op_id: id.to_string().into(),
            },
            Value::Object(obj_type) => Self {
                is_object: true,
                scalar: AutomergeScalarValue::new(AutomergeScalarType::Null),
                obj_id: AutomergeObjId::from(id),
                obj_type: (*obj_type).into(),
                op_id: id.to_string().into(),
            },
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeMarkData {
    pub name: ffi::String,
    pub value: AutomergeScalarValue,
}

impl AutomergeMarkData {
    pub fn from_mark_entry(name: &str, value: &ScalarValue) -> Self {
        Self {
            name: name.to_string().into(),
            value: AutomergeScalarValue::from(value),
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeMarkSpan {
    pub start: u64,
    pub end: u64,
    pub name: ffi::String,
    pub value: AutomergeScalarValue,
}

impl From<&Mark> for AutomergeMarkSpan {
    fn from(mark: &Mark) -> Self {
        Self {
            start: mark.start as u64,
            end: mark.end as u64,
            name: mark.name.to_string().into(),
            value: AutomergeScalarValue::from(&mark.value),
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeSpanKind {
    Text,
    Block,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeSpan {
    pub kind: AutomergeSpanKind,
    // Text variant fields (valid when kind == Text):
    pub text: ffi::String,
    pub marks: ffi::Vec<AutomergeMarkData>,
    // Block variant fields (valid when kind == Block):
    pub block_obj_id: AutomergeObjId,
    pub block_type: ffi::String,
    pub block_parents: ffi::Vec<ffi::String>,
    pub block_attrs_json: ffi::String,
}

impl AutomergeSpan {
    pub fn from_text(text: String, marks_data: Vec<AutomergeMarkData>) -> Self {
        Self {
            kind: AutomergeSpanKind::Text,
            text: text.into(),
            marks: marks_data.into(),
            block_obj_id: AutomergeObjId::root(),
            block_type: String::new().into(),
            block_parents: ffi::Vec::from_vec(Vec::new()),
            block_attrs_json: String::new().into(),
        }
    }

    pub fn from_block(block_map: &automerge::hydrate::Map) -> Self {
        let mut block_type = String::new();
        let mut parents: Vec<ffi::String> = Vec::new();
        let mut attrs = serde_json::Map::new();

        for (key, map_value) in block_map.iter() {
            match key.as_str() {
                "type" => {
                    if let automerge::hydrate::Value::Scalar(ScalarValue::Str(s)) = &map_value.value
                    {
                        block_type = s.to_string();
                    }
                }
                "parents" => {
                    if let automerge::hydrate::Value::List(list) = &map_value.value {
                        for item in list.iter() {
                            if let automerge::hydrate::Value::Scalar(ScalarValue::Str(s)) =
                                &item.value
                            {
                                parents.push(s.to_string().into());
                            }
                        }
                    }
                }
                other => {
                    let json_val = hydrate_value_to_json(&map_value.value);
                    attrs.insert(other.to_string(), json_val);
                }
            }
        }

        let attrs_json = serde_json::to_string(&serde_json::Value::Object(attrs))
            .unwrap_or_else(|_| "{}".to_string());

        Self {
            kind: AutomergeSpanKind::Block,
            text: String::new().into(),
            marks: ffi::Vec::from_vec(Vec::new()),
            block_obj_id: AutomergeObjId::root(),
            block_type: block_type.into(),
            block_parents: parents.into(),
            block_attrs_json: attrs_json.into(),
        }
    }
}

fn hydrate_value_to_json(value: &automerge::hydrate::Value) -> serde_json::Value {
    use automerge::hydrate::Value as HydrateValue;
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
            ScalarValue::Bytes(v) => {
                use base64::engine::general_purpose::STANDARD as BASE64_STANDARD;
                use base64::Engine;
                serde_json::Value::String(BASE64_STANDARD.encode(v))
            }
            ScalarValue::Counter(v) => serde_json::json!({ "type": "counter", "value": *v }),
            ScalarValue::Timestamp(v) => serde_json::json!({ "type": "timestamp", "value": *v }),
            ScalarValue::Unknown { bytes, .. } => {
                use base64::engine::general_purpose::STANDARD as BASE64_STANDARD;
                use base64::Engine;
                serde_json::json!({ "type": "unknown", "value": BASE64_STANDARD.encode(bytes) })
            }
        },
        HydrateValue::Map(map) => {
            let mut obj = serde_json::Map::new();
            for (key, map_value) in map.iter() {
                obj.insert(key.clone(), hydrate_value_to_json(&map_value.value));
            }
            serde_json::Value::Object(obj)
        }
        HydrateValue::List(list) => {
            let array = list
                .iter()
                .map(|item| hydrate_value_to_json(&item.value))
                .collect::<Vec<_>>();
            serde_json::Value::Array(array)
        }
        HydrateValue::Text(text) => {
            let s: String = text.into();
            serde_json::Value::String(s)
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergeInsertEntry {
    pub value: AutomergeValue,
    pub object_id: AutomergeObjId,
    pub conflict: bool,
}

impl AutomergeInsertEntry {
    fn from_tuple(entry: &(Value<'static>, ObjId, bool)) -> Self {
        Self {
            value: AutomergeValue::from(entry.0.clone()),
            object_id: AutomergeObjId::from(entry.1.clone()),
            conflict: entry.2,
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergePatchActionKind {
    PutMap,
    PutSeq,
    Insert,
    SpliceText,
    Increment,
    Conflict,
    DeleteMap,
    DeleteSeq,
    Mark,
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergePatchAction {
    pub kind: AutomergePatchActionKind,
    pub key: ffi::Option<ffi::String>,
    pub index: u64,
    pub value: ffi::Option<AutomergeValue>,
    pub value_object_id: ffi::Option<AutomergeObjId>,
    pub conflict: bool,
    pub inserted: ffi::Vec<AutomergeInsertEntry>,
    pub text: ffi::Option<ffi::String>,
    pub active_marks: ffi::Vec<AutomergeMarkData>,
    pub marks: ffi::Vec<AutomergeMarkSpan>,
    pub prop: ffi::Option<AutomergeProp>,
    pub length: u64,
    pub increment_value: i64,
}

impl AutomergePatchAction {
    fn from_patch_action(action: PatchAction) -> Self {
        match action {
            PatchAction::PutMap {
                key,
                value,
                conflict,
            } => {
                let (value_variant, object_id) = value;
                let value_object_id = if matches!(&value_variant, Value::Object(_)) {
                    ffi::Option::Some(AutomergeObjId::from(object_id.clone()))
                } else {
                    ffi::Option::None
                };
                Self {
                    kind: AutomergePatchActionKind::PutMap,
                    key: ffi::Option::Some(key.into()),
                    index: 0,
                    value: ffi::Option::Some(AutomergeValue::from(value_variant.clone())),
                    value_object_id,
                    conflict,
                    inserted: ffi::Vec::from_vec(Vec::new()),
                    text: ffi::Option::None,
                    active_marks: ffi::Vec::from_vec(Vec::new()),
                    marks: ffi::Vec::from_vec(Vec::new()),
                    prop: ffi::Option::None,
                    length: 0,
                    increment_value: 0,
                }
            }
            PatchAction::PutSeq {
                index,
                value,
                conflict,
            } => {
                let (value_variant, object_id) = value;
                let value_object_id = if matches!(&value_variant, Value::Object(_)) {
                    ffi::Option::Some(AutomergeObjId::from(object_id.clone()))
                } else {
                    ffi::Option::None
                };
                Self {
                    kind: AutomergePatchActionKind::PutSeq,
                    key: ffi::Option::None,
                    index: index as u64,
                    value: ffi::Option::Some(AutomergeValue::from(value_variant.clone())),
                    value_object_id,
                    conflict,
                    inserted: ffi::Vec::from_vec(Vec::new()),
                    text: ffi::Option::None,
                    active_marks: ffi::Vec::from_vec(Vec::new()),
                    marks: ffi::Vec::from_vec(Vec::new()),
                    prop: ffi::Option::None,
                    length: 0,
                    increment_value: 0,
                }
            }
            PatchAction::Insert { index, values } => {
                let entries: Vec<AutomergeInsertEntry> = values
                    .iter()
                    .map(AutomergeInsertEntry::from_tuple)
                    .collect();
                Self {
                    kind: AutomergePatchActionKind::Insert,
                    key: ffi::Option::None,
                    index: index as u64,
                    value: ffi::Option::None,
                    conflict: false,
                    value_object_id: ffi::Option::None,
                    inserted: entries.into(),
                    text: ffi::Option::None,
                    active_marks: ffi::Vec::from_vec(Vec::new()),
                    marks: ffi::Vec::from_vec(Vec::new()),
                    prop: ffi::Option::None,
                    length: 0,
                    increment_value: 0,
                }
            }
            PatchAction::SpliceText {
                index,
                value,
                marks,
            } => {
                let text_value = ffi::Option::Some(value.make_string().into());
                let active_marks_vec: Vec<AutomergeMarkData> = match marks {
                    Some(mark_set) => mark_set
                        .iter()
                        .map(|(name, value)| AutomergeMarkData::from_mark_entry(name, value))
                        .collect(),
                    None => Vec::new(),
                };

                Self {
                    kind: AutomergePatchActionKind::SpliceText,
                    key: ffi::Option::None,
                    index: index as u64,
                    value: ffi::Option::None,
                    conflict: false,
                    value_object_id: ffi::Option::None,
                    inserted: ffi::Vec::from_vec(Vec::new()),
                    text: text_value,
                    active_marks: active_marks_vec.into(),
                    marks: ffi::Vec::from_vec(Vec::new()),
                    prop: ffi::Option::None,
                    length: 0,
                    increment_value: 0,
                }
            }
            PatchAction::Increment { prop, value } => Self {
                kind: AutomergePatchActionKind::Increment,
                key: ffi::Option::None,
                index: 0,
                value: ffi::Option::None,
                value_object_id: ffi::Option::None,
                conflict: false,
                inserted: ffi::Vec::from_vec(Vec::new()),
                text: ffi::Option::None,
                active_marks: ffi::Vec::from_vec(Vec::new()),
                marks: ffi::Vec::from_vec(Vec::new()),
                prop: ffi::Option::Some(AutomergeProp::from(prop)),
                length: 0,
                increment_value: value,
            },
            PatchAction::Conflict { prop } => Self {
                kind: AutomergePatchActionKind::Conflict,
                key: ffi::Option::None,
                index: 0,
                value: ffi::Option::None,
                value_object_id: ffi::Option::None,
                conflict: false,
                inserted: ffi::Vec::from_vec(Vec::new()),
                text: ffi::Option::None,
                active_marks: ffi::Vec::from_vec(Vec::new()),
                marks: ffi::Vec::from_vec(Vec::new()),
                prop: ffi::Option::Some(AutomergeProp::from(prop)),
                length: 0,
                increment_value: 0,
            },
            PatchAction::DeleteMap { key } => Self {
                kind: AutomergePatchActionKind::DeleteMap,
                key: ffi::Option::Some(key.into()),
                index: 0,
                value: ffi::Option::None,
                value_object_id: ffi::Option::None,
                conflict: false,
                inserted: ffi::Vec::from_vec(Vec::new()),
                text: ffi::Option::None,
                active_marks: ffi::Vec::from_vec(Vec::new()),
                marks: ffi::Vec::from_vec(Vec::new()),
                prop: ffi::Option::None,
                length: 0,
                increment_value: 0,
            },
            PatchAction::DeleteSeq { index, length } => Self {
                kind: AutomergePatchActionKind::DeleteSeq,
                key: ffi::Option::None,
                index: index as u64,
                value: ffi::Option::None,
                value_object_id: ffi::Option::None,
                conflict: false,
                inserted: ffi::Vec::from_vec(Vec::new()),
                text: ffi::Option::None,
                active_marks: ffi::Vec::from_vec(Vec::new()),
                marks: ffi::Vec::from_vec(Vec::new()),
                prop: ffi::Option::None,
                length: length as u64,
                increment_value: 0,
            },
            PatchAction::Mark { marks } => {
                let converted: Vec<AutomergeMarkSpan> =
                    marks.iter().map(AutomergeMarkSpan::from).collect();
                Self {
                    kind: AutomergePatchActionKind::Mark,
                    key: ffi::Option::None,
                    index: 0,
                    value: ffi::Option::None,
                    value_object_id: ffi::Option::None,
                    conflict: false,
                    inserted: ffi::Vec::from_vec(Vec::new()),
                    text: ffi::Option::None,
                    active_marks: ffi::Vec::from_vec(Vec::new()),
                    marks: converted.into(),
                    prop: ffi::Option::None,
                    length: 0,
                    increment_value: 0,
                }
            }
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergePatchPathElement {
    pub object_id: AutomergeObjId,
    pub prop: AutomergeProp,
}

impl From<(ObjId, Prop)> for AutomergePatchPathElement {
    fn from(value: (ObjId, Prop)) -> Self {
        Self {
            object_id: value.0.into(),
            prop: AutomergeProp::from(value.1),
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergePatch {
    pub object_id: AutomergeObjId,
    pub path: ffi::Vec<AutomergePatchPathElement>,
    pub action: AutomergePatchAction,
}

impl From<Patch> for AutomergePatch {
    fn from(value: Patch) -> Self {
        let path: Vec<AutomergePatchPathElement> = value
            .path
            .into_iter()
            .map(AutomergePatchPathElement::from)
            .collect();
        Self {
            object_id: value.obj.into(),
            path: path.into(),
            action: AutomergePatchAction::from_patch_action(value.action),
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct DecodedChange {
    pub hash: ChangeHashBytes,
    pub actor: ffi::String,
    pub seq: u64,
    pub timestamp: i64,
    pub message: ffi::Option<ffi::String>,
    pub deps: ffi::Vec<ChangeHashBytes>,
}

impl DecodedChange {
    pub fn from_change_metadata(metadata: automerge::ChangeMetadata<'_>) -> Self {
        let hash = ChangeHashBytes::from(&metadata.hash);
        let actor = metadata.actor.to_hex_string().into();
        let seq = metadata.seq;
        let timestamp = metadata.timestamp;
        let message = match &metadata.message {
            Some(msg) => ffi::Option::Some(msg.to_string().into()),
            None => ffi::Option::None,
        };
        let deps: Vec<ChangeHashBytes> = metadata
            .deps
            .iter()
            .map(|h| ChangeHashBytes::from(h))
            .collect();

        Self {
            hash,
            actor,
            seq,
            timestamp,
            message,
            deps: deps.into(),
        }
    }
}

#[ffi_type]
#[derive(Clone, Debug)]
pub struct AutomergePathElement {
    pub obj_id: AutomergeObjId,
    pub prop: AutomergeProp,
}

impl AutomergePathElement {
    pub fn from_exid_prop(obj_id: ObjId, prop: &Prop) -> Self {
        Self {
            obj_id: AutomergeObjId::from(obj_id),
            prop: AutomergeProp::from(prop),
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug)]
pub struct AutomergeSaveOptions {
    pub deflate: bool,
    pub retain_orphans: bool,
}

impl AutomergeSaveOptions {
    pub fn to_save_options(&self) -> automerge::SaveOptions {
        automerge::SaveOptions {
            deflate: self.deflate,
            retain_orphans: self.retain_orphans,
        }
    }
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeVerificationMode {
    Check,
    NoCheck,
}

#[ffi_type]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AutomergeOnPartialLoad {
    Error,
    Ignore,
}

#[ffi_type]
#[derive(Clone, Copy, Debug)]
pub struct AutomergeLoadOptions {
    pub verification_mode: AutomergeVerificationMode,
    pub on_partial_load: AutomergeOnPartialLoad,
}
