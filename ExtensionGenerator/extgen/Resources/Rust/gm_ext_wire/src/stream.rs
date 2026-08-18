use crate::buffer::{GMSliceWriter, GMType};

/// Builder for tagged GMValue trees returned as `any` (mirrors C++ `DataStream`).
#[derive(Debug, Clone, Default)]
pub struct DataStream {
    buffer: Vec<u8>,
}

impl DataStream {
    pub fn new() -> Self {
        Self {
            buffer: Vec::with_capacity(512),
        }
    }

    pub fn with_capacity(cap: usize) -> Self {
        Self {
            buffer: Vec::with_capacity(cap),
        }
    }

    pub fn clear(&mut self) {
        self.buffer.clear();
    }

    pub fn len(&self) -> usize {
        self.buffer.len()
    }

    pub fn is_empty(&self) -> bool {
        self.buffer.is_empty()
    }

    pub fn as_bytes(&self) -> &[u8] {
        &self.buffer
    }

    pub fn write_to(&self, output: &mut GMSliceWriter<'_>) -> Option<()> {
        output.write_bytes(self.as_bytes())
    }

    /// Append raw IDL bytes (used for function Execute/Release packets).
    pub fn write_raw_u8(&mut self, v: u8) {
        self.buffer.push(v);
    }

    pub fn write_raw_u64(&mut self, v: u64) {
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn append_bytes(&mut self, bytes: &[u8]) {
        self.buffer.extend_from_slice(bytes);
    }

    pub fn append_stream(&mut self, other: &DataStream) {
        self.buffer.extend_from_slice(other.as_bytes());
    }

    fn write_tag(&mut self, t: GMType) {
        self.buffer.push(t as u8);
    }

    fn write_idl_string_payload(&mut self, s: &str) {
        self.buffer
            .extend_from_slice(&(s.len() as u32).to_le_bytes());
        self.buffer.extend_from_slice(s.as_bytes());
        self.buffer.push(0);
    }

    pub fn push_bool(&mut self, v: bool) {
        self.write_tag(GMType::Bool);
        self.buffer.push(if v { 1 } else { 0 });
    }

    pub fn push_f64(&mut self, v: f64) {
        self.write_tag(GMType::F64);
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn push_f32(&mut self, v: f32) {
        self.write_tag(GMType::F32);
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn push_i32(&mut self, v: i32) {
        self.write_tag(GMType::I32);
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn push_u32(&mut self, v: u32) {
        self.write_tag(GMType::U32);
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn push_u64(&mut self, v: u64) {
        self.write_tag(GMType::U64);
        self.buffer.extend_from_slice(&v.to_le_bytes());
    }

    pub fn push_string(&mut self, s: &str) {
        self.write_tag(GMType::String);
        self.write_idl_string_payload(s);
    }

    pub fn push_undefined(&mut self) {
        self.write_tag(GMType::Undefined);
    }

    pub fn push_array(&mut self, arr: &ArrayStream) {
        arr.serialize_into(&mut self.buffer);
    }

    pub fn push_struct(&mut self, obj: &StructStream) {
        obj.serialize_into(&mut self.buffer);
    }
}

/// Tagged array builder / packed dispatch outer array (mirrors C++ `ArrayStream`).
#[derive(Debug, Clone, Default)]
pub struct ArrayStream {
    count: u16,
    payload: Vec<u8>,
}

impl ArrayStream {
    pub const fn empty() -> Self {
        Self {
            count: 0,
            payload: Vec::new(),
        }
    }

    pub fn new() -> Self {
        Self {
            count: 0,
            payload: Vec::with_capacity(512),
        }
    }

    pub fn clear(&mut self) {
        self.count = 0;
        self.payload.clear();
    }

    pub fn len(&self) -> u16 {
        self.count
    }

    pub fn is_empty(&self) -> bool {
        self.count == 0
    }

    /// Encoded size including `[254][u16 count]` header.
    pub fn encoded_len(&self) -> usize {
        1 + 2 + self.payload.len()
    }

    pub fn write_to(&self, output: &mut GMSliceWriter<'_>) -> Option<()> {
        output.write_u8(GMType::Array as u8)?;
        output.write_u16(self.count)?;
        output.write_bytes(&self.payload)
    }

    pub(crate) fn serialize_into(&self, out: &mut Vec<u8>) {
        out.push(GMType::Array as u8);
        out.extend_from_slice(&self.count.to_le_bytes());
        out.extend_from_slice(&self.payload);
    }

    /// Opaque packet append (DispatchQueue packing): increments count, no element tag.
    pub fn push_packet(&mut self, packet: &DataStream) {
        self.count = self.count.saturating_add(1);
        self.payload.extend_from_slice(packet.as_bytes());
    }

    fn push_tagged_raw(&mut self, tag: GMType, bytes: &[u8]) {
        self.count = self.count.saturating_add(1);
        self.payload.push(tag as u8);
        self.payload.extend_from_slice(bytes);
    }

    pub fn push_f64(&mut self, v: f64) {
        self.push_tagged_raw(GMType::F64, &v.to_le_bytes());
    }

    pub fn push_bool(&mut self, v: bool) {
        self.push_tagged_raw(GMType::Bool, &[if v { 1 } else { 0 }]);
    }

    pub fn push_i32(&mut self, v: i32) {
        self.push_tagged_raw(GMType::I32, &v.to_le_bytes());
    }

    pub fn push_u64(&mut self, v: u64) {
        self.push_tagged_raw(GMType::U64, &v.to_le_bytes());
    }

    pub fn push_string(&mut self, s: &str) {
        self.count = self.count.saturating_add(1);
        self.payload.push(GMType::String as u8);
        self.payload
            .extend_from_slice(&(s.len() as u32).to_le_bytes());
        self.payload.extend_from_slice(s.as_bytes());
        self.payload.push(0);
    }

    pub fn push_undefined(&mut self) {
        self.push_tagged_raw(GMType::Undefined, &[]);
    }

    pub fn push_array(&mut self, arr: &ArrayStream) {
        self.count = self.count.saturating_add(1);
        arr.serialize_into(&mut self.payload);
    }

    pub fn push_struct(&mut self, obj: &StructStream) {
        self.count = self.count.saturating_add(1);
        obj.serialize_into(&mut self.payload);
    }
}

/// Tagged struct builder (mirrors C++ `StructStream`).
#[derive(Debug, Clone, Default)]
pub struct StructStream {
    count: u16,
    payload: Vec<u8>,
}

impl StructStream {
    pub fn new() -> Self {
        Self {
            count: 0,
            payload: Vec::with_capacity(512),
        }
    }

    pub fn clear(&mut self) {
        self.count = 0;
        self.payload.clear();
    }

    pub fn len(&self) -> u16 {
        self.count
    }

    pub fn is_empty(&self) -> bool {
        self.count == 0
    }

    pub fn encoded_len(&self) -> usize {
        1 + 2 + self.payload.len()
    }

    pub fn write_to(&self, output: &mut GMSliceWriter<'_>) -> Option<()> {
        output.write_u8(GMType::Struct as u8)?;
        output.write_u16(self.count)?;
        output.write_bytes(&self.payload)
    }

    pub(crate) fn serialize_into(&self, out: &mut Vec<u8>) {
        out.push(GMType::Struct as u8);
        out.extend_from_slice(&self.count.to_le_bytes());
        out.extend_from_slice(&self.payload);
    }

    fn write_key(&mut self, key: &str) {
        self.payload.push(GMType::String as u8);
        self.payload
            .extend_from_slice(&(key.len() as u32).to_le_bytes());
        self.payload.extend_from_slice(key.as_bytes());
        self.payload.push(0);
    }

    pub fn add_f64(&mut self, key: &str, v: f64) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.push(GMType::F64 as u8);
        self.payload.extend_from_slice(&v.to_le_bytes());
    }

    pub fn add_bool(&mut self, key: &str, v: bool) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.push(GMType::Bool as u8);
        self.payload.push(if v { 1 } else { 0 });
    }

    pub fn add_string(&mut self, key: &str, v: &str) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.push(GMType::String as u8);
        self.payload
            .extend_from_slice(&(v.len() as u32).to_le_bytes());
        self.payload.extend_from_slice(v.as_bytes());
        self.payload.push(0);
    }

    pub fn add_i32(&mut self, key: &str, v: i32) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.push(GMType::I32 as u8);
        self.payload.extend_from_slice(&v.to_le_bytes());
    }

    pub fn add_undefined(&mut self, key: &str) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.push(GMType::Undefined as u8);
    }

    pub fn add_array(&mut self, key: &str, arr: &ArrayStream) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        arr.serialize_into(&mut self.payload);
    }

    pub fn add_struct(&mut self, key: &str, obj: &StructStream) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        obj.serialize_into(&mut self.payload);
    }
}
