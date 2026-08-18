use std::sync::Mutex;

use crate::buffer::GMSliceWriter;
use crate::stream::{ArrayStream, DataStream};

/// Queues function Execute/Release packets for GML `invocation_handler` (C++ `DispatchQueue`).
pub struct DispatchQueue {
    pending: Mutex<Vec<DataStream>>,
    packed: Mutex<ArrayStream>,
}

impl DispatchQueue {
    pub const fn new() -> Self {
        Self {
            pending: Mutex::new(Vec::new()),
            packed: Mutex::new(ArrayStream::empty()),
        }
    }

    pub fn dispatch(&self, ev: DataStream) {
        if let Ok(mut pending) = self.pending.lock() {
            pending.push(ev);
        }
    }

    /// Pack pending packets into an outer Array and copy into `output`.
    /// Returns `0` (empty), bytes written, or `-needed` if `output` is too small.
    pub fn fetch(&self, output: &mut GMSliceWriter<'_>) -> f64 {
        let mut packed = match self.packed.lock() {
            Ok(g) => g,
            Err(_) => return -1.0,
        };

        let mut bytes_needed = packed.encoded_len();
        // Mirror C++: only steal pending when packed is empty (header+payload empty ⇒ count 0 & payload empty).
        if packed.is_empty() {
            let local = match self.pending.lock() {
                Ok(mut pending) => {
                    if pending.is_empty() {
                        return 0.0;
                    }
                    std::mem::take(&mut *pending)
                }
                Err(_) => return -1.0,
            };

            for ev in local {
                packed.push_packet(&ev);
            }
            bytes_needed = packed.encoded_len();
        }

        if bytes_needed > output.remaining() {
            return -(bytes_needed as f64);
        }

        if packed.write_to(output).is_none() {
            return -1.0;
        }
        packed.clear();
        bytes_needed as f64
    }
}

impl Default for DispatchQueue {
    fn default() -> Self {
        Self::new()
    }
}
