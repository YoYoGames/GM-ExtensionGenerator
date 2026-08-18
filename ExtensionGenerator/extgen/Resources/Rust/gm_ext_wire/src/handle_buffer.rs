use std::collections::VecDeque;
use std::sync::Mutex;

/// Out-of-band buffer handle queued by GML via `queue_buffer` (not tagged `GMValue::Buffer`).
#[derive(Debug, Clone, Copy)]
pub struct GMBuffer {
    pub ptr: *mut u8,
    pub len: u64,
}

// Safety: GML owns the underlying memory for the duration of the native call that pops this handle.
unsafe impl Send for GMBuffer {}

impl GMBuffer {
    pub fn new(ptr: *mut u8, len: u64) -> Self {
        Self { ptr, len }
    }

    pub fn as_slice(&self) -> &[u8] {
        if self.ptr.is_null() || self.len == 0 {
            &[]
        } else {
            unsafe { std::slice::from_raw_parts(self.ptr, self.len as usize) }
        }
    }

    pub fn as_slice_mut(&mut self) -> &mut [u8] {
        if self.ptr.is_null() || self.len == 0 {
            &mut []
        } else {
            unsafe { std::slice::from_raw_parts_mut(self.ptr, self.len as usize) }
        }
    }
}

/// FIFO used by generated `__EXT_NATIVE__*_queue_buffer` + buffer-arg decode.
pub struct BufferQueue {
    inner: Mutex<VecDeque<GMBuffer>>,
}

impl BufferQueue {
    pub const fn new() -> Self {
        Self {
            inner: Mutex::new(VecDeque::new()),
        }
    }

    pub fn push(&self, buffer: GMBuffer) {
        if let Ok(mut q) = self.inner.lock() {
            q.push_back(buffer);
        }
    }

    pub fn pop_front(&self) -> Option<GMBuffer> {
        self.inner.lock().ok()?.pop_front()
    }
}

impl Default for BufferQueue {
    fn default() -> Self {
        Self::new()
    }
}
