use crate::buffer::{GrowableWireWriter, WireByteWriter, GMType};

/// IDL struct that can be pushed into Any streams.
pub trait GmStruct {
    const CODEC_ID: u32;
    fn encode_fields<W: WireByteWriter>(&self, w: &mut W) -> Option<()>;
}

/// Builder for tagged GMValue trees (`any`).
///
/// TypedStruct: `[249][u32 codec_id][IDL fields]`.
/// TypedArray of structs: `[250][u16 count][249][u32 codec_id once][payload × count]`.
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

    pub fn write_to<W: WireByteWriter + ?Sized>(&self, output: &mut W) -> Option<()> {
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

    /// Append a TypedStruct: `[249][codec_id][IDL payload]`.
    pub fn push_typed_struct<F>(&mut self, codec_id: u32, encode: F) -> Option<()>
    where
        F: FnOnce(&mut GrowableWireWriter<'_>) -> Option<()>,
    {
        {
            let mut w = GrowableWireWriter::new(&mut self.buffer);
            w.write_typed_struct_header(codec_id)?;
            encode(&mut w)?;
        }
        Some(())
    }

    /// TypedArray of structs: `[250][u16 count][249][u32 codec_id once][payload × count]`.
    pub fn push_typed_struct_array<F>(&mut self, codec_id: u32, count: u16, mut encode_elem: F) -> Option<()>
    where
        F: FnMut(usize, &mut GrowableWireWriter<'_>) -> Option<()>,
    {
        self.buffer.push(GMType::TypedArray as u8);
        self.buffer.extend_from_slice(&count.to_le_bytes());
        self.buffer.push(GMType::TypedStruct as u8);
        self.buffer.extend_from_slice(&codec_id.to_le_bytes());
        for i in 0..(count as usize) {
            let mut w = GrowableWireWriter::new(&mut self.buffer);
            encode_elem(i, &mut w)?;
        }
        Some(())
    }

    pub fn push_gm_struct<T: GmStruct>(&mut self, obj: &T) -> Option<()> {
        self.push_typed_struct(T::CODEC_ID, |w| obj.encode_fields(w))
    }

    pub fn push_gm_struct_slice<T: GmStruct>(&mut self, items: &[T]) -> Option<()> {
        let count: u16 = items.len().try_into().ok()?;
        self.push_typed_struct_array(T::CODEC_ID, count, |i, w| items[i].encode_fields(w))
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

    pub fn write_to<W: WireByteWriter + ?Sized>(&self, output: &mut W) -> Option<()> {
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

    /// Nest a tagged TypedStruct / DataStream fragment.
    pub fn push_data_stream(&mut self, ds: &DataStream) {
        self.count = self.count.saturating_add(1);
        self.payload.extend_from_slice(ds.as_bytes());
    }

    pub fn push_typed_struct<F>(&mut self, codec_id: u32, encode: F) -> Option<()>
    where
        F: FnOnce(&mut GrowableWireWriter<'_>) -> Option<()>,
    {
        self.count = self.count.saturating_add(1);
        {
            let mut w = GrowableWireWriter::new(&mut self.payload);
            w.write_typed_struct_header(codec_id)?;
            encode(&mut w)?;
        }
        Some(())
    }

    pub fn push_gm_struct<T: GmStruct>(&mut self, obj: &T) -> Option<()> {
        self.push_typed_struct(T::CODEC_ID, |w| obj.encode_fields(w))
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

    pub fn write_to<W: WireByteWriter + ?Sized>(&self, output: &mut W) -> Option<()> {
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

    /// Nest a tagged TypedStruct / DataStream fragment under `key`.
    pub fn add_data_stream(&mut self, key: &str, ds: &DataStream) {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        self.payload.extend_from_slice(ds.as_bytes());
    }

    pub fn add_typed_struct<F>(&mut self, key: &str, codec_id: u32, encode: F) -> Option<()>
    where
        F: FnOnce(&mut GrowableWireWriter<'_>) -> Option<()>,
    {
        self.count = self.count.saturating_add(1);
        self.write_key(key);
        {
            let mut w = GrowableWireWriter::new(&mut self.payload);
            w.write_typed_struct_header(codec_id)?;
            encode(&mut w)?;
        }
        Some(())
    }

    pub fn add_gm_struct<T: GmStruct>(&mut self, key: &str, obj: &T) -> Option<()> {
        self.add_typed_struct(key, T::CODEC_ID, |w| obj.encode_fields(w))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::buffer::{GMBufferReader, GMType, WireByteWriter};

    fn encode_point<W: WireByteWriter>(w: &mut W, x: i32, y: i32) -> Option<()> {
        w.write_i32(x)?;
        w.write_i32(y)?;
        Some(())
    }

    struct Point {
        x: i32,
        y: i32,
    }

    impl GmStruct for Point {
        const CODEC_ID: u32 = 0;
        fn encode_fields<W: WireByteWriter>(&self, w: &mut W) -> Option<()> {
            encode_point(w, self.x, self.y)
        }
    }

    #[test]
    fn typed_struct_layout_matches_cpp() {
        let mut ds = DataStream::new();
        ds.push_typed_struct(0, |w| encode_point(w, 10, -20))
            .unwrap();
        let b = ds.as_bytes();
        assert_eq!(b[0], GMType::TypedStruct as u8);
        assert_eq!(u32::from_le_bytes(b[1..5].try_into().unwrap()), 0);
        assert_eq!(i32::from_le_bytes(b[5..9].try_into().unwrap()), 10);
        assert_eq!(i32::from_le_bytes(b[9..13].try_into().unwrap()), -20);
        assert_eq!(b.len(), 13);
    }

    #[test]
    fn push_gm_struct_matches_typed_struct() {
        let pt = Point { x: 10, y: -20 };
        let mut a = DataStream::new();
        a.push_gm_struct(&pt).unwrap();
        let mut b = DataStream::new();
        b.push_typed_struct(0, |w| encode_point(w, 10, -20)).unwrap();
        assert_eq!(a.as_bytes(), b.as_bytes());
    }

    #[test]
    fn typed_struct_array_layout() {
        let mut ds = DataStream::new();
        let pts = [(1i32, 2i32), (3, 4)];
        ds.push_typed_struct_array(7, 2, |i, w| encode_point(w, pts[i].0, pts[i].1))
            .unwrap();
        let b = ds.as_bytes();
        assert_eq!(b[0], GMType::TypedArray as u8);
        assert_eq!(u16::from_le_bytes(b[1..3].try_into().unwrap()), 2);
        assert_eq!(b[3], GMType::TypedStruct as u8);
        assert_eq!(u32::from_le_bytes(b[4..8].try_into().unwrap()), 7);
        // two payloads of 8 bytes
        assert_eq!(b.len(), 8 + 16);
    }

    #[test]
    fn empty_typed_struct_array_still_writes_headers() {
        let mut ds = DataStream::new();
        ds.push_typed_struct_array(3, 0, |_, _| Some(())).unwrap();
        let b = ds.as_bytes();
        assert_eq!(
            b,
            &[
                GMType::TypedArray as u8,
                0,
                0,
                GMType::TypedStruct as u8,
                3,
                0,
                0,
                0
            ]
        );
    }

    #[test]
    fn nested_typed_struct_in_open_array() {
        let mut inner = DataStream::new();
        inner
            .push_typed_struct(1, |w| encode_point(w, 5, 6))
            .unwrap();
        let mut arr = ArrayStream::new();
        arr.push_data_stream(&inner);
        let mut outer = DataStream::new();
        outer.push_array(&arr);
        let b = outer.as_bytes();
        assert_eq!(b[0], GMType::Array as u8);
        assert_eq!(u16::from_le_bytes(b[1..3].try_into().unwrap()), 1);
        assert_eq!(b[3], GMType::TypedStruct as u8);
    }

    #[test]
    fn inbound_typed_struct_rejected_without_decoder() {
        crate::error::clear_last_error();
        let bytes = {
            let mut ds = DataStream::new();
            ds.push_typed_struct(0, |w| encode_point(w, 1, 2)).unwrap();
            ds.as_bytes().to_vec()
        };
        let mut r = GMBufferReader::new(&bytes);
        assert!(r.unpack_value().is_none());
        let err = unsafe { std::ffi::CStr::from_ptr(crate::error::get_last_error_ptr()) };
        assert!(err.to_str().unwrap().contains("typed kinds"));
    }

    #[test]
    fn push_undefined_tag() {
        let mut ds = DataStream::new();
        ds.push_undefined();
        assert_eq!(ds.as_bytes(), &[GMType::Undefined as u8]);
    }

    #[test]
    fn wire_byte_writer_slice_and_growable() {
        let mut vec = Vec::new();
        {
            let mut g = GrowableWireWriter::new(&mut vec);
            g.write_typed_struct_header(2).unwrap();
            g.write_u32(99).unwrap();
        }
        let mut slice_buf = [0u8; 16];
        let written = {
            use crate::buffer::GMSliceWriter;
            let mut s = GMSliceWriter::new(&mut slice_buf);
            s.write_typed_struct_header(2).unwrap();
            s.write_u32(99).unwrap();
            s.cursor
        };
        assert_eq!(&slice_buf[..written], &vec[..]);
    }
}
