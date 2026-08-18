use std::collections::HashMap;

/// Wire type tags — keep in sync with ExtensionCore / GMExtWire.h
#[repr(u8)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GMType {
    U8 = 1,
    I8 = 2,
    U16 = 3,
    I16 = 4,
    U32 = 5,
    I32 = 6,
    F16 = 7,
    F32 = 8,
    F64 = 9,
    Bool = 10,
    String = 11,
    U64 = 12,
    /// TypedStruct: codec id (u32 LE) then IDL field payload.
    TypedStruct = 249,
    TypedArray = 250,
    Undefined = 251,
    Pointer = 252,
    Buffer = 253,
    Array = 254,
    Struct = 255,
}

impl TryFrom<u8> for GMType {
    type Error = ();
    fn try_from(v: u8) -> Result<Self, Self::Error> {
        match v {
            1 => Ok(GMType::U8),
            2 => Ok(GMType::I8),
            3 => Ok(GMType::U16),
            4 => Ok(GMType::I16),
            5 => Ok(GMType::U32),
            6 => Ok(GMType::I32),
            7 => Ok(GMType::F16),
            8 => Ok(GMType::F32),
            9 => Ok(GMType::F64),
            10 => Ok(GMType::Bool),
            11 => Ok(GMType::String),
            12 => Ok(GMType::U64),
            249 => Ok(GMType::TypedStruct),
            250 => Ok(GMType::TypedArray),
            251 => Ok(GMType::Undefined),
            252 => Ok(GMType::Pointer),
            253 => Ok(GMType::Buffer),
            254 => Ok(GMType::Array),
            255 => Ok(GMType::Struct),
            _ => Err(()),
        }
    }
}

/// Decoder for TypedStruct payloads encountered while unpacking tagged GMValue trees.
pub type TypedStructDecoder<'a> = dyn Fn(u32, &mut GMBufferReader<'a>) -> Option<GMValue<'a>> + 'a;

pub struct GMBufferReader<'a> {
    data: &'a [u8],
    pub cursor: usize,
    typed_struct_decoder: Option<&'a TypedStructDecoder<'a>>,
}

impl<'a> GMBufferReader<'a> {
    pub fn new(data: &'a [u8]) -> Self {
        Self {
            data,
            cursor: 0,
            typed_struct_decoder: None,
        }
    }

    /// Attach a codec-id → value decoder used when unpacking `GMType::TypedStruct`.
    pub fn with_typed_struct_decoder(mut self, decoder: &'a TypedStructDecoder<'a>) -> Self {
        self.typed_struct_decoder = Some(decoder);
        self
    }

    /// # Safety
    /// `ptr` must be valid for `len` bytes for the lifetime of the reader.
    pub unsafe fn from_raw_parts(ptr: *const u8, len: usize) -> Self {
        if ptr.is_null() || len == 0 {
            Self::new(&[])
        } else {
            Self::new(std::slice::from_raw_parts(ptr, len))
        }
    }

    pub fn remaining(&self) -> usize {
        self.data.len().saturating_sub(self.cursor)
    }

    pub fn is_empty(&self) -> bool {
        self.remaining() == 0
    }

    fn read_bytes(&mut self, n: usize) -> Option<&'a [u8]> {
        if self.cursor + n > self.data.len() {
            return None;
        }
        let slice = &self.data[self.cursor..self.cursor + n];
        self.cursor += n;
        Some(slice)
    }

    // ---- Raw IDL readers (no type tags; match GMExtWire codec::readValue) ----

    pub fn read_u8(&mut self) -> Option<u8> {
        Some(self.read_bytes(1)?[0])
    }

    pub fn read_i8(&mut self) -> Option<i8> {
        Some(self.read_u8()? as i8)
    }

    pub fn read_u16(&mut self) -> Option<u16> {
        Some(u16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?))
    }

    pub fn read_i16(&mut self) -> Option<i16> {
        Some(i16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?))
    }

    pub fn read_u32(&mut self) -> Option<u32> {
        Some(u32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))
    }

    pub fn read_i32(&mut self) -> Option<i32> {
        Some(i32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))
    }

    pub fn read_u64(&mut self) -> Option<u64> {
        Some(u64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))
    }

    pub fn read_i64(&mut self) -> Option<i64> {
        Some(i64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))
    }

    pub fn read_f32(&mut self) -> Option<f32> {
        Some(f32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))
    }

    pub fn read_f64(&mut self) -> Option<f64> {
        Some(f64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))
    }

    pub fn read_bool(&mut self) -> Option<bool> {
        Some(self.read_u8()? != 0)
    }

    /// IDL string: `u32 LE len` + UTF-8 bytes + `NUL`.
    pub fn read_idl_string(&mut self) -> Option<&'a str> {
        let len = self.read_u32()? as usize;
        let bytes = self.read_bytes(len)?;
        let nul = self.read_u8()?;
        if nul != 0 {
            return None;
        }
        std::str::from_utf8(bytes).ok()
    }

    pub fn read_type(&mut self) -> Option<GMType> {
        let type_byte = self.read_u8()?;
        GMType::try_from(type_byte).ok()
    }

    /// Tagged GMValue string payload: NUL-terminated (no length prefix).
    pub fn read_cstring(&mut self) -> Option<&'a str> {
        let remainder = &self.data[self.cursor..];
        let nul_pos = remainder.iter().position(|&b| b == 0)?;
        let bytes = &remainder[..nul_pos];
        let s = std::str::from_utf8(bytes).ok()?;
        self.cursor += nul_pos + 1;
        Some(s)
    }

    pub fn unpack_value(&mut self) -> Option<GMValue<'a>> {
        let gm_type = self.read_type()?;
        match gm_type {
            GMType::U8 => Some(GMValue::U8(self.read_u8()?)),
            GMType::I8 => Some(GMValue::I8(self.read_i8()?)),
            GMType::U16 => Some(GMValue::U16(self.read_u16()?)),
            GMType::I16 => Some(GMValue::I16(self.read_i16()?)),
            GMType::U32 => Some(GMValue::U32(self.read_u32()?)),
            GMType::I32 => Some(GMValue::I32(self.read_i32()?)),
            GMType::U64 => Some(GMValue::U64(self.read_u64()?)),
            GMType::F32 => Some(GMValue::F32(self.read_f32()?)),
            GMType::F64 => Some(GMValue::F64(self.read_f64()?)),
            GMType::Bool => Some(GMValue::Bool(self.read_bool()?)),
            GMType::String => Some(GMValue::String(self.read_idl_string()?)),
            GMType::Pointer => Some(GMValue::Pointer(self.read_u64()?)),
            GMType::Buffer => {
                let length = self.read_u32()?;
                let address = self.read_u64()?;
                Some(GMValue::Buffer { length, address })
            }
            GMType::Array => {
                let len = self.read_u16()? as usize;
                let mut arr = Vec::with_capacity(len);
                for _ in 0..len {
                    arr.push(self.unpack_value()?);
                }
                Some(GMValue::Array(arr))
            }
            GMType::Struct => {
                let len = self.read_u16()? as usize;
                let mut map = HashMap::with_capacity(len);
                for _ in 0..len {
                    // Keys are tagged strings: [11][u32 len][utf8][NUL]
                    let key_ty = self.read_type()?;
                    if key_ty != GMType::String {
                        return None;
                    }
                    let key = self.read_idl_string()?;
                    let value = self.unpack_value()?;
                    map.insert(key, value);
                }
                Some(GMValue::Struct(map))
            }
            GMType::Undefined => Some(GMValue::Undefined),
            GMType::TypedStruct => {
                let codec_id = self.read_u32()?;
                if let Some(decoder) = self.typed_struct_decoder {
                    decoder(codec_id, self)
                } else {
                    // Payload size is codec-specific; fail rather than desync the reader.
                    None
                }
            }
            GMType::TypedArray => {
                // Layout: [250][u16 count][elem_type]… — if elem is TypedStruct(249):
                // [250][count][249][u32 codec_id once][payload × count]
                let len = self.read_u16()? as usize;
                let elem = self.read_u8()?;
                if elem == GMType::TypedStruct as u8 {
                    let codec_id = self.read_u32()?;
                    let mut values = Vec::with_capacity(len);
                    let decoder = self.typed_struct_decoder?;
                    for _ in 0..len {
                        values.push(decoder(codec_id, self)?);
                    }
                    Some(GMValue::Array(values))
                } else {
                    let mut values = Vec::with_capacity(len);
                    for _ in 0..len {
                        match elem {
                            9 => values.push(GMValue::F64(self.read_f64()?)),
                            6 => values.push(GMValue::I32(self.read_i32()?)),
                            5 => values.push(GMValue::U32(self.read_u32()?)),
                            10 => values.push(GMValue::Bool(self.read_bool()?)),
                            1 => values.push(GMValue::U8(self.read_u8()?)),
                            2 => values.push(GMValue::I8(self.read_i8()?)),
                            8 => values.push(GMValue::F32(self.read_f32()?)),
                            12 => values.push(GMValue::U64(self.read_u64()?)),
                            _ => return None,
                        }
                    }
                    Some(GMValue::Array(values))
                }
            }
            GMType::F16 => None,
        }
    }
}

#[derive(Debug, Clone)]
pub enum GMValue<'a> {
    U8(u8),
    I8(i8),
    U16(u16),
    I16(i16),
    U32(u32),
    I32(i32),
    U64(u64),
    F32(f32),
    F64(f64),
    Bool(bool),
    String(&'a str),
    Pointer(u64),
    Buffer { length: u32, address: u64 },
    Array(Vec<GMValue<'a>>),
    Struct(HashMap<&'a str, GMValue<'a>>),
    Undefined,
}

/// Owned snapshot of a tagged GMValue (safe to keep after the FFI arg buffer is gone).
#[derive(Debug, Clone)]
pub enum GMValueOwned {
    U8(u8),
    I8(i8),
    U16(u16),
    I16(i16),
    U32(u32),
    I32(i32),
    U64(u64),
    F32(f32),
    F64(f64),
    Bool(bool),
    String(String),
    Pointer(u64),
    Buffer { length: u32, address: u64 },
    Array(Vec<GMValueOwned>),
    Struct(HashMap<String, GMValueOwned>),
    Undefined,
}

impl<'a> GMValue<'a> {
    pub fn into_owned(self) -> GMValueOwned {
        match self {
            GMValue::U8(v) => GMValueOwned::U8(v),
            GMValue::I8(v) => GMValueOwned::I8(v),
            GMValue::U16(v) => GMValueOwned::U16(v),
            GMValue::I16(v) => GMValueOwned::I16(v),
            GMValue::U32(v) => GMValueOwned::U32(v),
            GMValue::I32(v) => GMValueOwned::I32(v),
            GMValue::U64(v) => GMValueOwned::U64(v),
            GMValue::F32(v) => GMValueOwned::F32(v),
            GMValue::F64(v) => GMValueOwned::F64(v),
            GMValue::Bool(v) => GMValueOwned::Bool(v),
            GMValue::String(v) => GMValueOwned::String(v.to_string()),
            GMValue::Pointer(v) => GMValueOwned::Pointer(v),
            GMValue::Buffer { length, address } => GMValueOwned::Buffer { length, address },
            GMValue::Array(v) => {
                GMValueOwned::Array(v.into_iter().map(GMValue::into_owned).collect())
            }
            GMValue::Struct(v) => GMValueOwned::Struct(
                v.into_iter()
                    .map(|(k, val)| (k.to_string(), val.into_owned()))
                    .collect(),
            ),
            GMValue::Undefined => GMValueOwned::Undefined,
        }
    }
}

/// Growable tagged GMValue writer.
pub struct GMBufferWriter {
    pub data: Vec<u8>,
}

impl GMBufferWriter {
    pub fn new(capacity: usize) -> Self {
        Self {
            data: Vec::with_capacity(capacity),
        }
    }

    pub fn write_type(&mut self, t: GMType) {
        self.data.push(t as u8);
    }

    pub fn write_u8(&mut self, val: u8) {
        self.write_type(GMType::U8);
        self.data.push(val);
    }

    pub fn write_u32(&mut self, val: u32) {
        self.write_type(GMType::U32);
        self.data.extend_from_slice(&val.to_le_bytes());
    }

    pub fn write_f64(&mut self, val: f64) {
        self.write_type(GMType::F64);
        self.data.extend_from_slice(&val.to_le_bytes());
    }

    pub fn write_bool(&mut self, val: bool) {
        self.write_type(GMType::Bool);
        self.data.push(if val { 1 } else { 0 });
    }

    pub fn write_string(&mut self, val: &str) {
        self.write_type(GMType::String);
        self.data
            .extend_from_slice(&(val.len() as u32).to_le_bytes());
        self.data.extend_from_slice(val.as_bytes());
        self.data.push(0);
    }

    pub fn write_array<F>(&mut self, count: u16, builder: F)
    where
        F: FnOnce(&mut GMBufferWriter),
    {
        self.write_type(GMType::Array);
        self.data.extend_from_slice(&count.to_le_bytes());
        builder(self);
    }

    pub fn write_struct<F>(&mut self, count: u16, builder: F)
    where
        F: FnOnce(&mut GMBufferWriter),
    {
        self.write_type(GMType::Struct);
        self.data.extend_from_slice(&count.to_le_bytes());
        builder(self);
    }

    pub fn write_f64_typed_array(&mut self, values: &[f64]) {
        self.data.push(250);
        self.data.extend_from_slice(&(values.len() as u16).to_le_bytes());
        self.data.push(9);
        for val in values {
            self.data.extend_from_slice(&val.to_le_bytes());
        }
    }

    /// `[249][u32 codec_id]` then caller writes IDL payload bytes.
    pub fn write_typed_struct_header(&mut self, codec_id: u32) {
        self.data.push(GMType::TypedStruct as u8);
        self.data.extend_from_slice(&codec_id.to_le_bytes());
    }

    /// TypedArray of structs: `[250][u16 count][249][u32 codec_id]` then payloads.
    pub fn write_typed_struct_array_header(&mut self, count: u16, codec_id: u32) {
        self.data.push(GMType::TypedArray as u8);
        self.data.extend_from_slice(&count.to_le_bytes());
        self.data.push(GMType::TypedStruct as u8);
        self.data.extend_from_slice(&codec_id.to_le_bytes());
    }

    pub fn len(&self) -> usize {
        self.data.len()
    }

    pub fn is_empty(&self) -> bool {
        self.data.is_empty()
    }
}

/// Raw IDL writer over an external mutable slice (return / arg buffer protocol).
pub struct GMSliceWriter<'a> {
    data: &'a mut [u8],
    pub cursor: usize,
}

impl<'a> GMSliceWriter<'a> {
    pub fn new(data: &'a mut [u8]) -> Self {
        Self { data, cursor: 0 }
    }

    /// # Safety
    /// `ptr` must be valid for `len` writable bytes for the lifetime of the writer.
    pub unsafe fn from_raw_parts(ptr: *mut u8, len: usize) -> Self {
        if ptr.is_null() || len == 0 {
            Self {
                data: &mut [],
                cursor: 0,
            }
        } else {
            Self::new(std::slice::from_raw_parts_mut(ptr, len))
        }
    }

    pub fn remaining(&self) -> usize {
        self.data.len().saturating_sub(self.cursor)
    }

    pub fn write_bytes(&mut self, bytes: &[u8]) -> Option<()> {
        if self.cursor + bytes.len() > self.data.len() {
            return None;
        }
        self.data[self.cursor..self.cursor + bytes.len()].copy_from_slice(bytes);
        self.cursor += bytes.len();
        Some(())
    }

    pub fn write_u8(&mut self, val: u8) -> Option<()> {
        self.write_bytes(&[val])
    }

    pub fn write_i8(&mut self, val: i8) -> Option<()> {
        self.write_u8(val as u8)
    }

    pub fn write_u16(&mut self, val: u16) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_i16(&mut self, val: i16) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_u32(&mut self, val: u32) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_i32(&mut self, val: i32) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_u64(&mut self, val: u64) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_i64(&mut self, val: i64) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_f32(&mut self, val: f32) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_f64(&mut self, val: f64) -> Option<()> {
        self.write_bytes(&val.to_le_bytes())
    }

    pub fn write_bool(&mut self, val: bool) -> Option<()> {
        self.write_u8(if val { 1 } else { 0 })
    }

    /// IDL string: `u32 LE len` + UTF-8 + `NUL`.
    pub fn write_idl_string(&mut self, val: &str) -> Option<()> {
        self.write_u32(val.len() as u32)?;
        self.write_bytes(val.as_bytes())?;
        self.write_u8(0)
    }

    pub fn write_typed_struct_header(&mut self, codec_id: u32) -> Option<()> {
        self.write_u8(GMType::TypedStruct as u8)?;
        self.write_u32(codec_id)
    }
}
