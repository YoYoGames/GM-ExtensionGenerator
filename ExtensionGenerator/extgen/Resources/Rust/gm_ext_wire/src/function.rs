use std::sync::Arc;

use crate::dispatch::DispatchQueue;
use crate::stream::{ArrayStream, DataStream};

/// GML-side function callback handle (Execute / Release via DispatchQueue).
#[derive(Clone)]
pub struct GMFunction {
    handle: Option<Arc<FunctionHandle>>,
}

struct FunctionHandle {
    id: u64,
    queue: &'static DispatchQueue,
}

#[repr(u8)]
#[derive(Debug, Clone, Copy)]
enum GMFunctionAction {
    Execute = 1,
    Release = 2,
}

impl FunctionHandle {
    fn release(&self) {
        if self.id == 0 {
            return;
        }
        let mut ds = DataStream::new();
        ds.write_raw_u64(self.id);
        ds.write_raw_u8(GMFunctionAction::Release as u8);
        self.queue.dispatch(ds);
    }
}

impl Drop for FunctionHandle {
    fn drop(&mut self) {
        self.release();
    }
}

impl GMFunction {
    pub fn none() -> Self {
        Self { handle: None }
    }

    pub fn from_u64(id: u64, queue: &'static DispatchQueue) -> Self {
        if id == 0 {
            return Self::none();
        }
        Self {
            handle: Some(Arc::new(FunctionHandle { id, queue })),
        }
    }

    pub fn id(&self) -> u64 {
        self.handle.as_ref().map(|h| h.id).unwrap_or(0)
    }

    pub fn is_some(&self) -> bool {
        self.handle.is_some()
    }

    pub fn call_with_args(&self, args: &ArrayStream) {
        let Some(handle) = self.handle.as_ref() else {
            return;
        };
        if handle.id == 0 {
            return;
        }
        let mut ds = DataStream::new();
        ds.write_raw_u64(handle.id);
        ds.write_raw_u8(GMFunctionAction::Execute as u8);
        ds.push_array(args);
        handle.queue.dispatch(ds);
    }

    pub fn call_f64(&self, args: &[f64]) {
        let mut as_ = ArrayStream::new();
        for v in args {
            as_.push_f64(*v);
        }
        self.call_with_args(&as_);
    }
}

impl Default for GMFunction {
    fn default() -> Self {
        Self::none()
    }
}

impl std::fmt::Debug for GMFunction {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("GMFunction")
            .field("id", &self.id())
            .finish()
    }
}
