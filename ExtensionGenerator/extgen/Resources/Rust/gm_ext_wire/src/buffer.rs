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
    /// TypedStruct (codec id follows) — not fully implemented in Rust v1.
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

pub struct GMBufferReader<'a> {
    data: &'a [u8],
    pub cursor: usize,
}

impl<'a> GMBufferReader<'a> {
    pub fn new(data: &'a [u8]) -> Self {
        Self { data, cursor: 0 }
    }

    fn read_bytes(&mut self, n: usize) -> Option<&'a [u8]> {
        if self.cursor + n > self.data.len() {
            return None;
        }
        let slice = &self.data[self.cursor..self.cursor + n];
        self.cursor += n;
        Some(slice)
    }

    pub fn read_type(&mut self) -> Option<GMType> {
        let type_byte = self.read_bytes(1)?[0];
        GMType::try_from(type_byte).ok()
    }

    pub fn read_string(&mut self) -> Option<&'a str> {
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
            GMType::U8 => Some(GMValue::U8(self.read_bytes(1)?[0])),
            GMType::I8 => Some(GMValue::I8(self.read_bytes(1)?[0] as i8)),
            GMType::U16 => Some(GMValue::U16(u16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?))),
            GMType::I16 => Some(GMValue::I16(i16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?))),
            GMType::U32 => Some(GMValue::U32(u32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))),
            GMType::I32 => Some(GMValue::I32(i32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))),
            GMType::U64 => Some(GMValue::U64(u64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))),
            GMType::F32 => Some(GMValue::F32(f32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))),
            GMType::F64 => Some(GMValue::F64(f64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))),
            GMType::Bool => Some(GMValue::Bool(self.read_bytes(1)?[0] != 0)),
            GMType::String => Some(GMValue::String(self.read_string()?)),
            GMType::Pointer => Some(GMValue::Pointer(u64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))),
            GMType::Buffer => {
                let length = u32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?);
                let address = u64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?);
                Some(GMValue::Buffer { length, address })
            }
            GMType::Array => {
                let len = u16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?) as usize;
                let mut arr = Vec::with_capacity(len);
                for _ in 0..len {
                    arr.push(self.unpack_value()?);
                }
                Some(GMValue::Array(arr))
            }
            GMType::Struct => {
                let len = u16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?) as usize;
                let mut map = HashMap::with_capacity(len);
                for _ in 0..len {
                    let key = self.read_string()?;
                    let value = self.unpack_value()?;
                    map.insert(key, value);
                }
                Some(GMValue::Struct(map))
            }
            GMType::Undefined => Some(GMValue::Undefined),
            GMType::TypedStruct => None, // unsupported in v1
            GMType::TypedArray => {
                let len = u16::from_le_bytes(self.read_bytes(2)?.try_into().ok()?) as usize;
                let elem = self.read_bytes(1)?[0];
                let mut values = Vec::with_capacity(len);
                for _ in 0..len {
                    match elem {
                        9 => values.push(GMValue::F64(f64::from_le_bytes(self.read_bytes(8)?.try_into().ok()?))),
                        6 => values.push(GMValue::I32(i32::from_le_bytes(self.read_bytes(4)?.try_into().ok()?))),
                        _ => return None,
                    }
                }
                Some(GMValue::Array(values))
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
        self.write_raw_string(val);
    }

    fn write_raw_string(&mut self, val: &str) {
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

    pub fn len(&self) -> usize {
        self.data.len()
    }

    pub fn is_empty(&self) -> bool {
        self.data.is_empty()
    }
}
